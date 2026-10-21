package edu.tdtu.user.repository;

import edu.tdtu.user.entity.Account;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.Optional;

@Repository
public interface AccountRepository extends JpaRepository<Account, String> {
    Optional<Account> findByUserId(String userId);

    @Modifying
    @Query("UPDATE Account a SET a.balance = a.balance - :amount, a.updatedAt = :now WHERE a.id = :id AND a.balance >= :amount")
    int debitBalance(@Param("id") String id, @Param("amount") BigDecimal amount, @Param("now") Instant now);
}
