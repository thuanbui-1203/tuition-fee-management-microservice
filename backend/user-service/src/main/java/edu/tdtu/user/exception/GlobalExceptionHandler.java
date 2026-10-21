package edu.tdtu.user.exception;

import edu.tdtu.user.dto.ErrorResponse;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

import java.time.Instant;
import java.util.UUID;

@RestControllerAdvice
public class GlobalExceptionHandler {

    @ExceptionHandler(BusinessException.class)
    public ResponseEntity<ErrorResponse> handleBusinessException(BusinessException ex) {
        HttpStatus status = switch (ex.getCode()) {
            case "NOT_FOUND" -> HttpStatus.NOT_FOUND;
            case "INSUFFICIENT_BALANCE" -> HttpStatus.UNPROCESSABLE_ENTITY; // 422
            case "IDEMPOTENCY_CONFLICT" -> HttpStatus.CONFLICT; // 409
            default -> HttpStatus.BAD_REQUEST;
        };
        
        return new ResponseEntity<>(buildErrorResponse(ex.getCode(), ex.getMessage()), status);
    }

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ErrorResponse> handleValidationException(MethodArgumentNotValidException ex) {
        String message = ex.getBindingResult().getFieldErrors().stream()
                .map(error -> error.getField() + ": " + error.getDefaultMessage())
                .findFirst()
                .orElse("Invalid input");
        
        return new ResponseEntity<>(buildErrorResponse("INVALID_INPUT", message), HttpStatus.BAD_REQUEST);
    }

    @ExceptionHandler(Exception.class)
    public ResponseEntity<ErrorResponse> handleGenericException(Exception ex) {
        return new ResponseEntity<>(buildErrorResponse("INTERNAL_ERROR", "An unexpected error occurred"), HttpStatus.INTERNAL_SERVER_ERROR);
    }

    private ErrorResponse buildErrorResponse(String code, String message) {
        return ErrorResponse.builder()
                .code(code)
                .message(message)
                .timestamp(Instant.now())
                .traceId(UUID.randomUUID().toString()) // Basic trace ID, can be replaced with MDC
                .build();
    }
}
