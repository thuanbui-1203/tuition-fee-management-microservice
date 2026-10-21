package edu.tdtu.user.config.migration;

import edu.tdtu.user.entity.Account;
import edu.tdtu.user.entity.User;
import edu.tdtu.user.repository.AccountRepository;
import edu.tdtu.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.boot.CommandLineRunner;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.time.Instant;

@Component
@RequiredArgsConstructor
public class DatabaseSeeder implements CommandLineRunner {

    private final UserRepository userRepository;
    private final AccountRepository accountRepository;
    private final PasswordEncoder passwordEncoder;

    @Override
    @Transactional
    public void run(String... args) throws Exception {
        if (userRepository.count() == 0) {
            // 1. Insert User A
            User userA = User.builder()
                    .username("nguyenvana")
                    .passwordHash(passwordEncoder.encode("secret123"))
                    .fullName("Nguyễn Văn A")
                    .email("nguyenvana@example.com")
                    .phone("0901234567")
                    .createdAt(Instant.now())
                    .build();
            userA = userRepository.save(userA);

            // 2. Insert Account A
            Account accountA = Account.builder()
                    .userId(userA.getId())
                    .balance(new BigDecimal("10000000.00")) // 10 million VND
                    .version(0L)
                    .updatedAt(Instant.now())
                    .build();
            accountRepository.save(accountA);
            
            // 3. Insert User B (Low balance)
            User userB = User.builder()
                    .username("tranthib")
                    .passwordHash(passwordEncoder.encode("secret123"))
                    .fullName("Trần Thị B")
                    .email("tranthib@example.com")
                    .phone("0907654321")
                    .createdAt(Instant.now())
                    .build();
            userB = userRepository.save(userB);

            // 4. Insert Account B
            Account accountB = Account.builder()
                    .userId(userB.getId())
                    .balance(new BigDecimal("500000.00")) // 500k VND
                    .version(0L)
                    .updatedAt(Instant.now())
                    .build();
            accountRepository.save(accountB);
        }
    }
}
