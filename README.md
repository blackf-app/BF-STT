# BF-STT (Bright-Fast Speech To Text)

**BF-STT** là một ứng dụng Windows (WPF) mạnh mẽ và linh hoạt, giúp chuyển đổi giọng nói thành văn bản ngay lập tức và nhập liệu trực tiếp vào bất kỳ ứng dụng nào đang hoạt động. Ứng dụng tích hợp công nghệ từ nhiều nhà cung cấp Speech-to-Text hàng đầu như **Deepgram**, **Speechmatics**, **Soniox**, và **OpenAI Whisper** để đảm bảo độ chính xác cực cao và độ trễ gần như bằng không.

---

### 🚀 Tính năng nổi bật

- **Chế độ Hybrid thông minh (Mặc định F3):**
  - **Nhấn nhanh (Short Press):** Chế độ **Batch**. Ghi âm và gửi toàn bộ đoạn hội thoại sau khi kết thúc. Phù hợp cho các câu thoại dài, cần độ chính xác cao và tự động thêm dấu chấm câu.
  - **Nhấn giữ (Long Press):** Chế độ **Streaming**. Văn bản xuất hiện và được gõ trực tiếp ngay khi bạn đang nói. Phù hợp cho việc nhắn tin hoặc nhập liệu thời gian thực.
- **Chế độ "Stop & Send" (Mặc định F4):**
  - Giúp dừng nhanh cuộc hội thoại đang streaming hoặc batch, nhận kết quả cuối cùng và tự động gửi (nhấn Enter) vào ứng dụng đích. Cực kỳ tiện lợi khi chat hoặc ra lệnh nhanh.
- **Hỗ trợ Đa Nền tảng STT:**
  - Hỗ trợ linh hoạt chuyển đổi giữa **Deepgram (Nova-3)**, **Speechmatics**, **Soniox**, và **OpenAI Whisper**.
  - Cho phép cấu hình nhà cung cấp khác nhau cho chế độ Batch và Streaming.
  - **Chế độ Kiểm thử (Test Mode):** Chạy đồng thời và so sánh trực tiếp kết quả từ nhiều API trên cùng một giao diện.
- **Quản lý âm thanh & Lọc nhiễu:**
  - **VAD & Silence Detection:** Tự động phát hiện khoảng lặng để dừng ghi âm hoặc lọc bỏ các đoạn không có tiếng người.
  - **Resend Audio:** Gửi lại đoạn âm thanh vừa thu cho API khác để đối chiếu kết quả mà không cần nói lại.
  - **Anti-Hallucination:** Bộ lọc thông minh loại bỏ các câu "ảo giác" do AI tự suy diễn (ví dụ: "Cảm ơn đã xem", "Subscribe",...).
- **Tối ưu trải nghiệm người dùng:**
  - **Auto-Typing:** Nhập văn bản trực tiếp vào Word, Notepad, Trình duyệt, Game... với khả năng bảo vệ Clipboard (sao lưu và khôi phục nội dung cũ).
  - **Quản lý lịch sử:** Lưu lại lịch sử các đoạn hội thoại với giới hạn số lượng mục có thể cấu hình (Max History Items).
  - **Giao diện Compact:** Thiết kế nhỏ gọn, hiện đại, có thể thu nhỏ xuống Taskbar và luôn sẵn sàng hoạt động.

---

### 🛠 Công nghệ sử dụng

- **Framework:** .NET 8, WPF (Windows Presentation Foundation).
- **Audio Engine:** [NAudio](https://github.com/naudio/NAudio) xử lý luồng âm thanh PCM 16kHz Mono.
- **Giao tiếp API:** REST API (Batch) và WebSocket (Streaming).
- **Kiến trúc:** Clean MVVM với Dependency Injection và Service-oriented architecture.

---

### 📦 Hướng dẫn cài đặt & Sử dụng

#### 1. Yêu cầu hệ thống
- Windows 10/11 x64.
- .NET 8 Desktop Runtime.
- Microphone hoạt động tốt.

#### 2. Cấu hình
- Mở bảng **Settings** (biểu tượng bánh răng) để thiết lập:
  - **API Keys:** Nhập key cho các dịch vụ muốn dùng.
  - **Hotkeys:** Thay đổi phím tắt F3 (Ghi âm) và F4 (Dừng & Gửi).
  - **History Limit:** Giới hạn số lượng hội thoại lưu lại để tối ưu bộ nhớ.
  - **Microphone:** Chọn thiết bị đầu vào mong muốn.

#### 3. Cách dùng nhanh
- **F3 (Nhấn nhả):** Bắt đầu/Kết thúc ghi âm (Batch).
- **F3 (Giữ phím):** Nói đến đâu gõ đến đó (Streaming). Thả phím để kết thúc.
- **F4:** Dừng ghi âm ngay lập tức và nhấn Enter tự động.

---

### 📂 Cấu trúc mã nguồn chính

- `Services/`:
  - `AudioRecordingService.cs`: Quản lý thu âm và VAD.
  - `InputInjector.cs`: Xử lý việc mô phỏng bàn phím và quản lý Clipboard.
  - `HotkeyService.cs`: Đăng ký và quản lý phím tắt hệ thống (Low-level hook).
  - `HistoryService.cs`: Lưu trữ và quản lý lịch sử hội thoại.
- `ViewModels/`: Liên kết logic xử lý với giao diện người dùng.
- `MainWindow.xaml`: Giao diện chính với bộ hiển thị sóng âm (Visualizer) và lịch sử.

---

### 💻 Dành cho nhà phát triển

**Build dự án:**
```bash
dotnet build
```

**Tạo bản phát hành (Single File EXE):**
Sử dụng workflow `/publish` hoặc chạy thủ công:
```powershell
dotnet publish -c Release -p:PublishSingleFile=true -p:PublishReadyToRun=true --self-contained false -o ./publish
```

---
*Phát triển bởi **Antigravity AI**. Cập nhật mới nhất: Tháng 2, 2026*
