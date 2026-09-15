# Sơ đồ thiết kế — Phân hệ đóng học phí iBanking

Thư mục chứa **file nguồn PlantUML (`.puml`)** và ảnh xuất **`.png`** dùng cho báo cáo (Yêu cầu 1, 2 và phần giải thích Yêu cầu 6).

| File | Loại sơ đồ | Phục vụ yêu cầu | Nội dung |
|---|---|---|---|
| `use-case-diagram.puml` | Use Case Diagram | YC 1 | 2 actor + 6 UC chính + `«include»/«extend»` + BR1–BR5 |
| `erd.puml` | Entity Relationship Diagram | YC 1 | 8 thực thể, quan hệ + cardinality, nhóm theo service sở hữu |
| `architecture.puml` | Microservices Architecture Diagram | YC 2 | gateway + 5 service + DB riêng + Redis + RabbitMQ; REST (liền) vs event (đứt) |
| `sequence-payment.puml` | Sequence Diagram | YC 2, 6 | Luồng UC-04: tạo giao dịch → OTP → confirm → saga + compensation |
| `activity-payment.puml` | Activity Diagram | YC 5, 6 | Luồng nghiệp vụ thanh toán với các quyết định/ràng buộc |
| `state-transaction.puml` | State Diagram | YC 6 | Vòng đời giao dịch `INITIATED → SUCCESS/FAILED` |

## Cách xuất PNG (chọn 1)

1. **VS Code + extension "PlantUML"**: mở file `.puml` → `Alt+D` (preview) → chuột phải → *Export Current Diagram* → PNG.
2. **PlantUML online**: dán nội dung vào https://www.plantuml.com/plantuml/uml → tải PNG.
3. **Kroki (không cần cài đặt)**:
   ```bash
   curl -X POST -H "Content-Type: text/plain" \
     --data-binary @use-case-diagram.puml \
     https://kroki.io/plantuml/png -o use-case-diagram.png
   ```
4. **plantuml.jar** (cần Java): `java -jar plantuml.jar *.puml`.

## Quy ước

- Giữ file `.puml` trong repo để chỉnh sửa được; commit cả `.png` để báo cáo/tài liệu dùng trực tiếp.
- Mũi tên **liền** = REST đồng bộ / DB / SMTP; mũi tên **đứt** = event bất đồng bộ qua RabbitMQ.
- Đặt tên thống nhất theo `docs/03-rest-api-design.md`: `payment-service`, `/internal/**`, `tuition_fees`, `otp_codes`.
