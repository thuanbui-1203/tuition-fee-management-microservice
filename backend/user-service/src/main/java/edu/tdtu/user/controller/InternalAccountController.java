package edu.tdtu.user.controller;

import edu.tdtu.user.dto.DebitRequest;
import edu.tdtu.user.dto.DebitResponse;
import edu.tdtu.user.service.AccountService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/internal/accounts")
@RequiredArgsConstructor
public class InternalAccountController {

    private final AccountService accountService;

    @PostMapping("/debit")
    public ResponseEntity<DebitResponse> debit(
            @RequestHeader("Idempotency-Key") String idempotencyKey,
            @Valid @RequestBody DebitRequest request) {
        
        return ResponseEntity.ok(accountService.debit(request, idempotencyKey));
    }
}
