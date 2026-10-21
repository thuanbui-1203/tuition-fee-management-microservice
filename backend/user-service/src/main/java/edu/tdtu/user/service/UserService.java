package edu.tdtu.user.service;

import edu.tdtu.user.dto.AccountResponse;
import edu.tdtu.user.dto.UserResponse;
import edu.tdtu.user.entity.Account;
import edu.tdtu.user.entity.User;
import edu.tdtu.user.exception.EntityNotFoundException;
import edu.tdtu.user.repository.AccountRepository;
import edu.tdtu.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
public class UserService {

    private final UserRepository userRepository;
    private final AccountRepository accountRepository;

    public UserResponse getUserProfile(String userId) {
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new EntityNotFoundException("User not found"));
        return UserResponse.fromEntity(user);
    }

    public AccountResponse getUserAccount(String userId) {
        Account account = accountRepository.findByUserId(userId)
                .orElseThrow(() -> new EntityNotFoundException("Account not found for this user"));
        return AccountResponse.fromEntity(account);
    }
}
