# BF-STT (Bright-Fast Speech To Text)

BF-STT là một ứng dụng Windows (WPF) mạnh mẽ và linh hoạt, giúp chuyển đổi giọng nói thành văn bản ngay lập tức và nhập liệu trực tiếp vào bất kỳ ứng dụng nào đang hoạt động. Ứng dụng tích hợp công nghệ Deepgram mới nhất (Nova-3) để đảm bảo độ chính xác cực cao và độ trễ gần như bằng không.

## 🚀 Tính năng nổi bật

- **Chế độ Hybrid thông minh (F3):**
  - **Nhấn nhanh (Short Press):** Chế độ **Batch**. Ghi âm và gửi toàn bộ đoạn hội thoại sau khi kết thúc. Phù hợp cho các câu thoại dài, cần độ chính xác cao nhất và tự động thêm dấu chấm câu.
  - **Nhấn giữ (Long Press):** Chế độ **Streaming**. Văn bản xuất hiện và được "gõ" trực tiếp ngay khi bạn đang nói. Phù hợp cho việc nhắn tin hoặc nhập liệu thời gian thực.
- **Tự động nhập liệu (Auto-Typing):** Hỗ trợ nhập liệu thông minh vào cửa sổ ứng dụng đang hoạt động trước đó (Word, Notepad, Browser, Game...).
- **Xử lý dấu câu tự động:** Trong chế độ Batch, ứng dụng tự động thêm dấu chấm và khoảng trắng vào cuối đoạn văn.
- **Giao diện cấu hình (Settings Window):** Dễ dàng thay đổi API Key, Model, và tùy chọn khởi động cùng Windows ngay trong ứng dụng.
- **Phản hồi âm thanh & Trình quan sát âm lượng:** Âm báo "bíp" đặc trưng khi bắt đầu/kết thúc và thanh hiển thị mức độ âm thanh (Audio Level) trực quan.
- **Tiết kiệm tài nguyên:** Tự động xóa file tạm, phát hiện đoạn thu âm im lặng để bỏ qua yêu cầu API, và chỉ sử dụng một instance duy nhất.
- **Duy trì Clipboard:** Tự động sao lưu và khôi phục nội dung Clipboard của người dùng sau khi nhập liệu (đối với chế độ Batch).

## 🛠 Công nghệ sử dụng

- **Framework:** .NET 8, WPF (Windows Presentation Foundation)
- **Audio:** [NAudio](https://github.com/naudio/NAudio) xử lý luồng âm thanh PCM 16kHz Mono chuẩn hóa.
- **STT Engine:** [Deepgram API](https://deepgram.com/) (Nova-3) qua REST API (Batch) và WebSocket (Streaming).
- **Kiến trúc:** MVVM (Model-View-ViewModel) sạch sẽ và dễ mở rộng.

## 📦 Hướng dẫn cài đặt

### 1. Yêu cầu hệ thống
- Windows 10/11 x64.
- .NET 8 Runtime.

### 2. Cấu hình ban đầu
Khi khởi chạy lần đầu, ứng dụng sẽ yêu cầu bạn nhập **Deepgram API Key**.
- Lấy Key tại: [Deepgram Console](https://console.deepgram.com/).
- Cấu hình sẽ được lưu an toàn tại `%AppData%/BF-STT/settings.json`.

### 3. Build từ mã nguồn
Nếu bạn là nhà phát triển, có thể build dự án bằng Visual Studio 2022 hoặc CLI:
```bash
dotnet build
dotnet run
```
Hệ thống versioning tự động sẽ tự tăng số phiên bản sau mỗi lần build thành công.

### 4. Đóng gói (Single EXE)
Để tạo file chạy duy nhất không cần cài đặt:
```powershell
dotnet publish -c Release -o ./publish
```

## ⌨️ Cách sử dụng

1. **Khởi động:** Ứng dụng sẽ nằm ở phía trên cùng màn hình.
2. **Ghi âm (F3):**
   - **Click F3 một lần:** Bắt đầu ghi âm Batch (nhấn F3 lần nữa để dừng). Status sẽ hiển thị "Recording (Batch)...".
   - **Nhấn và giữ F3:** Bắt đầu Streaming. Status hiển thị "Streaming...". Thả phím F3 để dừng.
3. **Kết quả:** Văn bản sẽ tự động được nhập vào vị trí con trỏ chuột của bạn trong ứng dụng đích.

## 📂 Giao diện & Cấu trúc

- `MainWindow.xaml`: Giao diện chính nhỏ gọn, hiển thị trạng thái và thời gian.
- `SettingsWindow.xaml`: Nơi quản lý API Key và các tùy chọn hệ thống.
- `Services/`:
  - `DeepgramStreamingService`: Xử lý luồng WebSocket thời gian thực.
  - `InputInjector`: Xử lý logic gõ phím, xử lý delta text cho streaming và bảo vệ clipboard.
  - `HotkeyService`: Đăng ký phím nóng F3 hệ thống.

## 📄 Giấy phép

Dự án phát triển bởi Antigravity AI, phục vụ cộng đồng. Tự do sử dụng và đóng góp ý kiến.

---
*Last update: February 2026*
