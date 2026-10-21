package edu.tdtu.user.dto;

import lombok.Builder;
import lombok.Data;

import java.time.Instant;

@Data
@Builder
public class AuthResponse {
    private String token;
    private Instant expiresAt;
    private UserResponse user;
}
