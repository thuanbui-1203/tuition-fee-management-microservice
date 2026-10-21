package edu.tdtu.user.dto;

import lombok.Builder;
import lombok.Data;

import java.math.BigDecimal;

@Data
@Builder
public class DebitResponse {
    private boolean success;
    private BigDecimal newBalance;
}
