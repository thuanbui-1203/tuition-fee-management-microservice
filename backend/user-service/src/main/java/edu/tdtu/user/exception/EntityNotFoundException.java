package edu.tdtu.user.exception;

public class EntityNotFoundException extends BusinessException {
    public EntityNotFoundException(String message) {
        super("NOT_FOUND", message);
    }
}
