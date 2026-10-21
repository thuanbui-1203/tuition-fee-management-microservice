package edu.tdtu.user.service;


import edu.tdtu.user.dto.DebitRequest;
import edu.tdtu.user.dto.DebitResponse;
import edu.tdtu.user.entity.Account;
import edu.tdtu.user.entity.Transaction;
import edu.tdtu.user.exception.BusinessException;
import edu.tdtu.user.exception.EntityNotFoundException;
import edu.tdtu.user.exception.InsufficientBalanceException;
import edu.tdtu.user.repository.AccountRepository;
import edu.tdtu.user.repository.TransactionRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.Optional;

@Service
@RequiredArgsConstructor
public class AccountService {

    private final AccountRepository accountRepository;
    private final TransactionRepository transactionRepository;

    @Transactional
    public DebitResponse debit(DebitRequest request, String idempotencyKey) {
        // 1. Check idempotency
        Optional<Transaction> existingTx = transactionRepository.findByIdempotencyKey(idempotencyKey);
        if (existingTx.isPresent()) {
            Transaction tx = existingTx.get();
            if (tx.getStatus().equals("SUCCESS")) {
                // If it was already successful, return the current balance
                Account account = accountRepository.findById(request.getAccountId())
                        .orElseThrow(() -> new EntityNotFoundException("Account not found"));
                return DebitResponse.builder().success(true).newBalance(account.getBalance()).build();
            } else {
                throw new BusinessException("IDEMPOTENCY_CONFLICT", "Transaction was already processed but not successful");
            }
        }

        // 2. Perform atomic debit (balance >= amount)
        int updatedRows = accountRepository.debitBalance(request.getAccountId(), request.getAmount(), Instant.now());

        if (updatedRows == 0) {
            // It could be either not found or insufficient balance. Let's check which one.
            boolean accountExists = accountRepository.existsById(request.getAccountId());
            if (!accountExists) {
                throw new EntityNotFoundException("Account not found");
            } else {
                throw new InsufficientBalanceException("Số dư không đủ");
            }
        }

        // 3. Get updated account for response
        Account updatedAccount = accountRepository.findById(request.getAccountId())
                .orElseThrow(() -> new EntityNotFoundException("Account not found after update"));

        // 4. Save transaction log to ensure idempotency for future calls
        Transaction newTx = Transaction.builder()
                .idempotencyKey(idempotencyKey)
                .amount(request.getAmount())
                .payerUserId(updatedAccount.getUserId())
                .status("SUCCESS")
                .createdAt(Instant.now())
                .completedAt(Instant.now())
                .build();
        transactionRepository.save(newTx);

        return DebitResponse.builder()
                .success(true)
                .newBalance(updatedAccount.getBalance())
                .build();
    }
}
