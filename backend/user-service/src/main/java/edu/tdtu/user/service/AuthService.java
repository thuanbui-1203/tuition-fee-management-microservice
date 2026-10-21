package edu.tdtu.user.service;

import edu.tdtu.user.config.JwtTokenProvider;
import edu.tdtu.user.dto.AuthResponse;
import edu.tdtu.user.dto.LoginRequest;
import edu.tdtu.user.dto.UserResponse;
import edu.tdtu.user.entity.User;
import edu.tdtu.user.exception.BusinessException;
import edu.tdtu.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;

import java.time.Instant;

@Service
@RequiredArgsConstructor
public class AuthService {

    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtTokenProvider jwtTokenProvider;

    public AuthResponse login(LoginRequest request) {
        User user = userRepository.findByUsername(request.getUsername())
                .orElseThrow(() -> new BusinessException("UNAUTHENTICATED", "Tên đăng nhập hoặc mật khẩu không đúng"));

        if (!passwordEncoder.matches(request.getPassword(), user.getPasswordHash())) {
            throw new BusinessException("UNAUTHENTICATED", "Tên đăng nhập hoặc mật khẩu không đúng");
        }

        String token = jwtTokenProvider.generateToken(user.getUsername(), user.getId());

        // We can get expiration from tokenProvider if exposed, here we simplify assuming 30 mins
        Instant expiresAt = Instant.now().plusSeconds(1800); 

        return AuthResponse.builder()
                .token(token)
                .expiresAt(expiresAt)
                .user(UserResponse.fromEntity(user))
                .build();
    }
}
