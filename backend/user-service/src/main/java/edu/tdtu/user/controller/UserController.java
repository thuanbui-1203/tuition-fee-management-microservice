package edu.tdtu.user.controller;

import edu.tdtu.user.dto.AccountResponse;
import edu.tdtu.user.dto.UserResponse;
import edu.tdtu.user.service.UserService;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/users")
@RequiredArgsConstructor
public class UserController {

    private final UserService userService;

    private String getCurrentUserId() {
        // According to our JwtAuthenticationFilter, username is principal, but let's assume 
        // we can extract it or we just use it directly. For now, since filter sets username as principal,
        // we need a way to get userId. Let's fix that conceptually. The filter could have set userId in details.
        // For simplicity, let's assume we fetch the user by username to get ID, or we improve the filter.
        // Assuming we improve the filter to set userId as principal instead of username for easier access.
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        return (String) authentication.getPrincipal(); 
    }

    @GetMapping("/me")
    public ResponseEntity<UserResponse> getCurrentUser() {
        return ResponseEntity.ok(userService.getUserProfile(getCurrentUserId()));
    }

    @GetMapping("/me/accounts")
    public ResponseEntity<AccountResponse> getCurrentUserAccount() {
        return ResponseEntity.ok(userService.getUserAccount(getCurrentUserId()));
    }
}
