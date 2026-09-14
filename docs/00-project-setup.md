# Phase 0 — Thiết lập dự án (Project Setup)

**Mục tiêu:** tạo một khung dự án (skeleton) chạy được bằng **một lệnh duy nhất**, để mọi phase sau có môi trường kiểm thử ổn định.

**Đầu vào:** đề bài (đã OCR), công nghệ giả định ở `README.md`.

**Sản phẩm bàn giao:** repo Git, `docker-compose.yml`, 6 project backend + 1 frontend tối thiểu, cấu hình chung, health check.

---

## Bước 0.1 — Khởi tạo Git và cấu trúc thư mục

**Việc cần làm:**
1. `git init` tại thư mục gốc dự án.
2. Tạo `.gitignore` gồm:
   - Java/Maven: `target/`, `*.class`, `.mvn/wrapper/maven-wrapper.jar`
   - Node: `node_modules/`, `dist/`, `.vite/`
   - Môi trường: `.env`, `*.local`
   - IDE/OS: `.idea/`, `.vscode/`, `*.iml`, `.DS_Store`, `Thumbs.db`
3. Tạo cây thư mục (xem `README.md`) — đủ 6 thư mục service, `gateway/`, `web/`, `docs/`.
4. Tạo `README.md` gốc (tóm tắt cách chạy — sẽ hoàn thiện ở Phase 9).

## Bước 0.2 — Viết `docker-compose.yml` (hạ tầng)

**Việc cần làm:** định nghĩa các container hạ tầng (chưa cần container cho service — service chạy bằng Maven khi dev):

| Container | Image | Cổng (host:container) | Credential dev | Dùng cho |
|---|---|---|---|---|
| `postgres` | `postgres:16-alpine` | `5432:5432` | user `postgres` / pass `postgres` | Tạo 5 database: `user_db`, `tuition_db`, `payment_db`, `otp_db`, `notification_db` |
| `redis` | `redis:7-alpine` | `6379:6379` | — | OTP TTL, idempotency key, rate limit |
| `rabbitmq` | `rabbitmq:3-management` | `5672:5672`, `15672:15672` | user `guest` / pass `guest` (mặc định) | Hàng đợi email |
| `mailhog` | `mailhog/mailhog` | `1025:1025` (SMTP), `8025:8025` (UI) | — | Bắt email trong lúc phát triển |

**Mẫu** (chỉ là template — sẽ chỉnh ở Phase 4 khi có migration):
```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports: ["5432:5432"]
    volumes: [pgdata:/var/lib/postgresql/data]
  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
  rabbitmq:
    image: rabbitmq:3-management
    ports: ["5672:5672", "15672:15672"]
  mailhog:
    image: mailhog/mailhog
    ports: ["1025:1025", "8025:8025"]
volumes:
  pgdata:
```

**Lưu ý quan trọng:** database cho 5 service nên tạo bằng lệnh trong `init` script của Postgres (mount thư mục `./docker/initdb/*.sql`) hoặc để **Flyway của từng service tự tạo schema** — chọn 1 cách và ghi rõ vào README. Gợi ý: mỗi service kết nối tới database riêng của nó, Flyway chạy migration khi service khởi động.

**Kiểm tra:** `docker compose up -d` → cả 4 container ở trạng thái healthy:
```bash
docker compose ps
```

## Bước 0.3 — Khởi tạo các Spring Boot project

**Việc cần làm:** với mỗi service (trừ gateway), tạo project Spring Boot 3.x, Java 17, packaging `jar`, group `edu.tdtu`:

| Service | Artifact | Port | Dependencies bắt buộc |
|---|---|---|---|
| user-service | `user-service` | 8081 | Web, Security, Data JPA, Validation, PostgreSQL, Flyway, Actuator, springdoc, AMQP (nếu cần publish) |
| tuition-service | `tuition-service` | 8082 | Web, Data JPA, Validation, PostgreSQL, Flyway, Actuator, springdoc |
| payment-service | `payment-service` | 8083 | Web, Data JPA, Validation, PostgreSQL, Flyway, Actuator, springdoc, AMQP, WebClient (gọi service khác) |
| otp-service | `otp-service` | 8084 | Web, Data JPA, Validation, PostgreSQL, Flyway, Actuator, springdoc, AMQP, (Spring Data Redis — tuỳ chọn) |
| notification-service | `notification-service` | 8085 | Web, Data JPA, Validation, PostgreSQL, Flyway, Actuator, AMQP, Spring Mail |
| gateway | `gateway` | 8080 | Gateway, Security (Reactive), Actuator |

**Lưu ý:**
- `payment-service` gọi service khác qua `WebClient` (không dùng RestTemplate deprecated).
- `gateway` dùng **WebFlux/Reactive**, không dùng Spring MVC.
- Có thể tạo project bằng [start.spring.io](https://start.spring.io) hoặc Spring Initializr trong IDE.

## Bước 0.4 — Cấu hình chung từng service (`application.yml`)

**Việc cần làm:** mỗi service có `application.yml` với đúng:
1. **Port** riêng theo bảng trên.
2. **Datasource** trỏ đúng database của nó, ví dụ user-service:
```yaml
spring:
  datasource:
    url: jdbc:postgresql://localhost:5432/user_db
    username: postgres
    password: postgres
  jpa:
    hibernate.ddl-auto: validate   # schema do Flyway quản lý
    open-in-view: false
  flyway:
    enabled: true
    locations: classpath:db/migration
```
3. **JWT:** dùng chung một secret (dev) hoặc cặp RSA. Cấu hình: `app.jwt.secret`, `app.jwt.expiration-ms` (gợi ý 30 phút). `user-service` **phát** token; `gateway` **xác thực** token.
4. **RabbitMQ** (service nào cần): `spring.rabbitmq.host=localhost`, port 5672; khai báo queue/exchange trong code (xem Phase 2).
5. **springdoc:** bật `/swagger-ui` cho từng service dev.
6. **Logging pattern** kèm `traceId` (MDC) — thiết lập sau khi có gateway truyền header `X-Trace-Id`.

**Kiểm tra:** `mvn spring-boot:run` từng service (theo thứ tự bất kỳ, chưa cần nhau) → log "Started ... in x seconds", không lỗi kết nối DB.

## Bước 0.5 — Health check & endpoint `ping`

**Việc cần làm:** mỗi service trả về:
- `GET /actuator/health` → `{"status":"UP"}` (kèm health của DB, RabbitMQ nếu có).
- `GET /api/v1/ping` → `{"service":"user-service","status":"ok","time":"..."}` (dùng để demo & test gateway routing).

**Kiểm tra:**
```bash
curl http://localhost:8081/api/v1/ping
curl http://localhost:8080/actuator/health   # sau khi có gateway
```

## Bước 0.6 — Biến môi trường & khởi tạo frontend (tối thiểu)

**Việc cần làm:**
1. Tạo `.env.example` liệt kê: DB credentials, JWT secret, RabbitMQ, cổng.
2. Frontend (Phase 7 mới làm đầy đủ): tạo `web/` bằng Vite + React, cấu hình proxy `/api` → `http://localhost:8080` để tránh CORS khi dev.
3. Commit lần đầu với message chuẩn, ví dụ: `chore: scaffold monorepo with docker-compose and spring boot services`.

---

## Checklist hoàn thành Phase 0

- [ ] `git init` + `.gitignore` + cấu trúc thư mục đúng
- [ ] `docker compose up -d` → 4 container healthy
- [ ] 6 backend project khởi tạo đúng dependency/port
- [ ] Mỗi service chạy được `mvn spring-boot:run`, health OK
- [ ] `user_db`…`notification_db` tồn tại (hoặc cơ chế tạo tự động rõ ràng)
- [ ] JWT secret chung đã cấu hình
- [ ] `.env.example`, README gốc, commit đầu tiên

**Acceptance criteria (tiêu chí chấp nhận):** chạy đúng 1 lệnh `docker compose up -d`, sau đó khởi động từng service bằng Maven; tất cả trả `200` tại `/actuator/health`; không có service nào ném lỗi kết nối khi khởi động.
