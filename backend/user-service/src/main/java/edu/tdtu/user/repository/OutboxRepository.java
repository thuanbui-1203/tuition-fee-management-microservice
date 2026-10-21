package edu.tdtu.user.repository;

import edu.tdtu.user.entity.Outbox;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface OutboxRepository extends JpaRepository<Outbox, String> {
    List<Outbox> findByStatus(String status);
}
