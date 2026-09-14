# Phase 7 — Giao diện Web Application (Frontend)

**Mục tiêu:** lập trình giao diện tương tác người dùng, **ưu tiên Web Application**, và tích hợp với các API đã xây dựng (yêu cầu 7 của đề).

**Đầu vào:** Phase 3 (API), Phase 5 (backend chạy được).

**Sản phẩm bàn giao:** web app hoàn chỉnh (đăng nhập → tra cứu → thanh toán → OTP → kết quả → lịch sử), gọi API qua gateway.

---

## 7.1. Lựa chọn công nghệ & cấu trúc

- **React + Vite** (hoặc HTML/JS thuần nếu muốn tối giản — nhưng React giúp quản lý trạng thái OTP countdown dễ hơn).
- Thư viện: `axios` (gọi API), `react-router-dom` (điều hướng), UI đơn giản bằng CSS thuần hoặc Tailwind.
- Cấu trúc:
```
web/
├── src/
│   ├── api/client.js        # axios instance: baseURL /api/v1, gắn JWT, xử lý 401
│   ├── api/auth.js, payments.js, tuition.js, users.js
│   ├── pages/Login.jsx, Dashboard.jsx, Payment.jsx, OtpDialog.jsx, Result.jsx, History.jsx
│   ├── components/…          # nút, bảng, thông báo lỗi
│   └── App.jsx, main.jsx
├── vite.config.js            # proxy /api → http://localhost:8080
└── package.json
```

**Quy tắc tích hợp:**
- **Mọi** request đi qua gateway `http://localhost:8080/api/v1/...` (không gọi thẳng service) — proxy dev `/api` → `8080`.
- Lưu JWT trong `localStorage` (hoặc cookie httpOnly nếu muốn chặt hơn); axios interceptor tự gắn `Authorization: Bearer <token>`.
- Interceptor response: gặp `401` → xoá token → chuyển về trang Login.
- Mỗi request ghi (ghi) gửi kèm `Idempotency-Key` sinh bằng `crypto.randomUUID()` — **giữ nguyên key khi bấm lại/retry** (lưu trong state của form, không sinh lại khi user bấm lại lần 2).

## 7.2. Từng màn hình — hành vi & API

### 7.2.1. Login (UC-01)
- Form: username, password → `POST /auth/login`.
- Thành công: lưu token + thông tin user → sang Dashboard.
- Lỗi: hiện message từ response (`401` → "Tên đăng nhập hoặc mật khẩu không đúng"; `400` → "Vui lòng nhập đầy đủ").

### 7.2.2. Dashboard / Xem thông tin tài khoản (UC-02, UC-06)
- Gọi song song: `GET /users/me`, `GET /users/me/accounts`, `GET /payments/me`.
- Hiển thị 3 vùng:
  1. **Hồ sơ:** họ tên, SĐT, email.
  2. **Số dư khả dụng:** nổi bật, định dạng tiền VND.
  3. **Lịch sử giao dịch:** bảng (thời gian, MSSV, tên SV, số tiền, trạng thái SUCCESS/FAILED…).
- Nút "Thanh toán học phí" → Payment.

### 7.2.3. Payment — màn hình thanh toán (UC-03, UC-04) — THEO ĐÚNG 3 NHÓM CỦA ĐỀ
Bố cục phải thể hiện đúng mô tả §2 (giáo viên sẽ đối chiếu):

1. **Nhóm "Người nộp tiền"** — hiển thị họ tên, SĐT, email lấy từ `GET /users/me`. **Chỉ đọc — mọi ô input bị `disabled`/`readOnly`** (đề: "không được chỉnh sửa").
2. **Nhóm "Thông tin học phí":**
   - Ô nhập **MSSV** + nút "Tra cứu" → `GET /tuitions?mssv=`.
   - Hiển thị: họ tên sinh viên, học kỳ, **số tiền học phí cần thanh toán**.
   - Xử lý lỗi: `404` → "Không tìm thấy MSSV"; `409` → "Học phí đã được thanh toán" (chặn không cho thanh toán).
3. **Nhóm "Thông tin thanh toán":**
   - Số dư khả dụng (từ `/users/me/accounts`), số tiền cần thanh toán.
   - Hiển thị cảnh báo nếu số dư < số tiền ("Số dư không đủ") và **vô hiệu hoá nút Xác nhận**.
   - Checkbox đồng ý điều khoản (tuỳ chọn làm cho đẹp, không bắt buộc).
- **Nút "Xác nhận giao dịch":** chỉ `enabled` khi **đủ 3 điều kiện**: (1) đã tra cứu MSSV thành công, (2) fee UNPAID, (3) số dư ≥ số tiền — đúng luật đề §2 "nút chỉ khả dụng khi thông tin đầy đủ và hợp lệ".
- Bấm Xác nhận → `POST /payments` (kèm Idempotency-Key) → thành công 201 → mở **OTP dialog** với `paymentId`.

### 7.2.4. OTP dialog (UC-05)
- Hiển thị thông báo "Mã OTP đã gửi đến email …" + **đồng hồ đếm ngược 5:00** (300 giây) — khi về 0, vô hiệu hoá nút Xác nhận OTP và hiện "Mã OTP đã hết hạn".
- Ô nhập 6 chữ số → nút "Xác nhận" → `POST /payments/{paymentId}/confirm` (kèm Idempotency-Key).
- Xử lý lỗi (ánh xạ chính xác — bảng dùng chung cho báo cáo):

| Status/Code | Thông báo UI |
|---|---|
| 400 OTP_INCORRECT | "Mã OTP không đúng. Còn X lần thử." |
| 409 OTP_ALREADY_USED | "Mã OTP đã được sử dụng. Vui lòng gửi lại." |
| 410 OTP_EXPIRED | "Mã OTP đã hết hạn." → bật nút "Gửi lại mã" |
| 422 INSUFFICIENT_BALANCE | "Số dư không đủ. Vui lòng kiểm tra lại." |
| 409 FEE_ALREADY_PAID | "Học phí đã được thanh toán bởi giao dịch khác." |
| 503 | "Hệ thống đang bận, vui lòng thử lại sau." |

- Nút **"Gửi lại mã"** (khi hết hạn/sai quá lần): `POST /payments/{paymentId}/otp/resend` → reset countdown 5:00; bị `429` → hiện "Vui lòng chờ 60 giây".
- Thành công → chuyển Result.

### 7.2.5. Result (kết quả — §4 "hiển thị kết quả")
- Hiển thị: trạng thái **THÀNH CÔNG**, số tiền đã thanh toán, **số dư mới**, trạng thái học phí, thời gian (`response.confirm`).
- Nút: "Xem lịch sử" / "Thanh toán tiếp".

### 7.2.6. History (UC-06)
- Bảng lịch sử có phân trang (dùng `page`/`size` của `GET /payments/me`); làm mới sau khi thanh toán.

## 7.3. Xử lý lỗi chung & trạng thái tải

- Component `<ErrorBanner>` hiển thị `message` từ response lỗi chuẩn (Phase 3.1).
- Mọi nút gửi đều có trạng thái `loading` (disabled + spinner) để chống bấm đúp; **Idempotency-Key giữ nguyên** khi bấm lại sau lỗi mạng → không lo trừ tiền 2 lần.
- Khi token hết hạn giữa chừng (401) → tự đăng xuất về Login kèm thông báo.

## 7.4. Kịch bản kiểm thử thủ công UI (chạy đủ trước Phase 8/9)

1. Đăng nhập sai → báo lỗi; đăng nhập đúng → Dashboard đúng thông tin.
2. Nhập MSSV không tồn tại → "Không tìm thấy MSSV".
3. Nhập MSSV đã PAID (`521H0003`) → "Học phí đã được thanh toán", nút Xác nhận bị khoá.
4. MSSV hợp lệ, số dư đủ → nút Xác nhận sáng → bấm → OTP dialog hiện, email có OTP trong MailHog.
5. Nhập OTP sai → báo lỗi; nhập OTP đúng → Result THÀNH CÔNG; số dư & lịch sử cập nhật.
6. Dùng tài khoản `tranthib` (số dư 500.000) thanh toán fee 8.400.000 → cảnh báo thiếu tiền, nút khoá.
7. (Demo concurrency) 2 trình duyệt/2 tài khoản cùng thanh toán 1 MSSV → 1 thành công, 1 nhận "đã được thanh toán".
8. (Demo OTP hết hạn) để OTP quá 5 phút → bấm xác nhận → báo hết hạn → Gửi lại mã → thành công.

## 7.5. Checklist hoàn thành Phase 7

- [ ] Màn hình thanh toán có đúng **3 nhóm thông tin**, nhóm người nộp tiền **chỉ đọc**
- [ ] Nút Xác nhận tuân thủ đúng điều kiện khả dụng của đề
- [ ] OTP dialog có countdown 5:00 và xử lý resend/429
- [ ] Mọi mã lỗi API → thông báo tiếng Việt rõ ràng
- [ ] Idempotency-Key giữ nguyên khi retry
- [ ] Chạy được 8 kịch bản mục 7.4

**Acceptance criteria:** một người dùng (không phải lập trình viên) tự chạy được luồng đăng nhập → thanh toán → nhập OTP → thấy kết quả thành công; các trường hợp lỗi hiển thị thông báo dễ hiểu; giao diện thể hiện đúng mô tả §2.
