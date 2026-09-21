Được. Nếu chỉ tập trung **các việc cần làm liên quan đến Ticket trước**, thì từ báo cáo có thể gom thành nhóm sau:

### Ticket — các việc cần làm trước

1. **Sửa quan hệ `Ticket → OrderItem`**

   * Hiện `Ticket` có `OrderItemId` nhưng migration chưa tạo Foreign Key tương ứng.
   * Cần đảm bảo mỗi Ticket bắt buộc thuộc một `OrderItem` hợp lệ.
   * Không cho phép tạo Ticket mồ côi.

2. **Thêm unique constraint cho `TicketCode`**

   * Mỗi vé phải có một mã vé duy nhất.
   * Database phải chặn trường hợp hai Ticket có cùng `TicketCode`.

3. **Thêm unique constraint cho `QrTokenHash`**

   * QR của mỗi Ticket phải xác định duy nhất một vé.
   * Không được để hai vé dùng cùng QR token/hash.

4. **Sửa kiểu dữ liệu của `TicketCode` và `QrTokenHash`**

   * Hiện đang là `nvarchar(max)`.
   * Cần chốt độ dài cụ thể rồi dùng kiểu có giới hạn phù hợp để có thể tạo index/unique constraint tốt hơn.

5. **Sửa concurrency cho Ticket**

   * `RowVersion` hiện chưa phải SQL Server `rowversion` đúng nghĩa.
   * Cần cấu hình concurrency token phù hợp để phát hiện các cập nhật đồng thời.

6. **Đảm bảo truy được ownership đầy đủ**

   * Sau khi sửa quan hệ, phải truy được chuỗi:

```text
Ticket
  ↓
OrderItem
  ↓
Order
  ↓
User
```

Đồng thời phải xác định được Ticket thuộc Event nào. Đây là nền tảng để làm “vé của tôi”, authorization và check-in. Báo cáo coi đây là ưu tiên P0.

1. **Xây logic phát hành Ticket sau thanh toán**

   * Ticket chỉ được phát hành sau khi payment được xác nhận hợp lệ.
   * Việc phát hành phải nằm trong transaction phù hợp.
   * Webhook gửi lặp không được tạo Ticket lặp. Phần này phụ thuộc vào `PAY-01` và việc sửa Ticket schema.

2. **Xây API “vé của tôi”**

   * Customer cần xem các Ticket thuộc về chính mình.
   * Query phải dựa trên ownership `Ticket → OrderItem → Order → User`.
   * Sau này có thể phân loại theo trạng thái vé.

3. **Thiết kế QR Ticket**

   * Chốt format/token của QR.
   * QR không nên chứa dữ liệu nhạy cảm không cần thiết.
   * Backend phải tra được QR về đúng một Ticket.
   * Report nói phần QR nên chốt sau khi sửa `Tickets`.

4. **Chuẩn bị Ticket cho Check-in**

* Ticket phải cung cấp đủ thông tin để kiểm tra:

  * vé có tồn tại không;
  * thuộc Event nào;
  * trạng thái vé có hợp lệ không;
  * đã check-in chưa.
* Sau đó mới xây check-in atomic ở `CHK-01`.

Nếu chuyển thành thứ tự triển khai thực tế thì mình sẽ xếp:

```text
1. Fix Ticket mapping
   ↓
2. FK OrderItemId
   ↓
3. Unique TicketCode
   ↓
4. Unique QrTokenHash
   ↓
5. Fix RowVersion
   ↓
6. Migration sửa schema
   ↓
7. Ticket issuance service
   ↓
8. My Tickets query/API
   ↓
9. QR generation/validation
   ↓
10. Kết nối với Check-in
```

Trong đó **5 việc đầu tiên nên làm trước tiên**, vì báo cáo đánh `TKT-01` là **P0**: phải sửa schema Ticket trước khi phát hành vé.

Nếu bạn muốn, mình có thể viết tiếp phần này thành dạng **task cho dev**, ví dụ `TKT-01.1`, `TKT-01.2`, `TKT-01.3` kèm **mô tả + việc cần sửa + tiêu chí nghiệm thu** để bạn đưa thẳng vào backlog.
