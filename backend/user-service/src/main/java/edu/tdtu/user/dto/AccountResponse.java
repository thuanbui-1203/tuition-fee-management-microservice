package edu.tdtu.user.dto;

import edu.tdtu.user.entity.Account;
import lombok.Builder;
import lombok.Data;

import java.math.BigDecimal;

@Data
@Builder
public class AccountResponse {
    private BigDecimal balance;
    private String currency;

    public static AccountResponse fromEntity(Account account) {
        return AccountResponse.builder()
                .balance(account.getBalance())
                .currency("VND") // Hardcoded based on project requirements
                .build();
    }
}
