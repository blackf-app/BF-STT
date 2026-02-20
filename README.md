# BF-STT (Bright-Fast Speech To Text)

BF-STT là một ứng dụng Windows (WPF) mạnh mẽ và linh hoạt, giúp chuyển đổi giọng nói thành văn bản ngay lập tức và nhập liệu trực tiếp vào bất kỳ ứng dụng nào đang hoạt động. Ứng dụng tích hợp công nghệ từ nhiều nhà cung cấp Speech-to-Text hàng đầu như Deepgram, Speechmatics, Soniox, và OpenAI Whisper để đảm bảo độ chính xác cực cao, hỗ trợ nhiều ngôn ngữ (bao gồm tiếng Việt) và độ trễ gần như bằng không.

## 🚀 Tính năng nổi bật

- **Chế độ Hybrid thông minh (F3):**
  - **Nhấn nhanh (Short Press):** Chế độ **Batch**. Ghi âm và gửi toàn bộ đoạn hội thoại sau khi kết thúc. Phù hợp cho các câu thoại dài, cần độ chính xác cao nhất và tự động thêm dấu chấm câu.
  - **Nhấn giữ (Long Press):** Chế độ **Streaming**. Văn bản xuất hiện và được gõ trực tiếp ngay khi bạn đang nói. Phù hợp cho việc nhắn tin hoặc nhập liệu thời gian thực.
  
- **Hỗ trợ Đa Nền tảng STT (Multi-Provider Support):**
  - Hỗ trợ linh hoạt chuyển đổi giữa **Deepgram (Nova-3)**, **Speechmatics**, **Soniox**, và **OpenAI Whisper**.
  - **Test Mode (Chế độ Kiểm thử):** Cho phép chạy đồng thời và so sánh trực tiếp kết quả, tốc độ phản hồi từ nhiều API khác nhau (Deepgram, Speechmatics, Soniox, OpenAI) trên cùng một giao diện. Tự động vô hiệu hóa nhập liệu (Auto-Typing) khi ở chế độ này để tránh xung đột.
  
- **Resend Audio (Gửi lại âm thanh):** Dễ dàng gửi lại đoạn ghi âm vừa thu trong chế độ Batch cho API mà không cần phải đọc lại, cực kỳ hữu ích khi muốn đối chiếu chéo các API.
- **Tự động nhập liệu (Auto-Typing):** Nhập liệu thông minh trực tiếp vào cửa sổ ứng dụng đang hoạt động (Word, Notepad, Browser, Games...).
- **Xử lý dấu câu và định dạng:** Tự động tối ưu hóa, thêm dấu chấm câu (ví dụ: tự động thêm ". " vào cuối đoạn trong Batch mode) và nối chuỗi cho luồng Streaming kết quả cuối cùng.
- **Hỗ trợ Keyterm (Tùy chỉnh Từ vựng):** Tính năng thiết lập các từ khóa chuyên ngành, biến thể phương ngữ để tăng độ chính xác theo ngữ cảnh người dùng.
- **Voice Activity Detection (VAD) & Trình quan sát âm lượng:** Loại bỏ các khoảng lặng, tối ưu dung lượng/băng thông gửi API và có thanh hiển thị Audio Level trực quan kèm âm thanh thông báo.
- **Giao diện Cấu hình (Settings Window):** Quản lý chi tiết API Key, Model cho từng nhà cung cấp, Test Mode, và thiết lập khởi động cùng hệ thống.
- **Bảo vệ Clipboard:** Sao lưu và khôi phục an toàn nội dung Clipboard người dùng sau quá trình nhập liệu.

## 🛠 Công nghệ sử dụng

- **Framework:** .NET 8, WPF (Windows Presentation Foundation)
- **Audio:** [NAudio](https://github.com/naudio/NAudio) xử lý luồng âm thanh PCM 16kHz Mono chuẩn hóa kết hợp Voice Activity Detection.
- **Tích hợp API:** REST API (cho Batch) và WebSocket (cho Streaming) tương tác trực tiếp tới hệ thống backend của Deepgram, Speechmatics, Soniox và OpenAI.
- **Kiến trúc:** Clean MVVM (Model-View-ViewModel) với Interface riêng biệt (`IBatchSttService`, `IStreamingSttService`).

## 📦 Hướng dẫn cài đặt

### 1. Yêu cầu hệ thống
- Windows 10/11 x64.
- .NET 8 Desktop Runtime.

### 2. Cấu hình ban đầu
Khi khởi chạy lần đầu hoặc từ Context Menu hệ thống, truy cập bảng **Settings** để cấu hình API:
- Bạn có thể chuyển đổi linh hoạt thiết lập: **Deepgram**, **Speechmatics**, **Soniox**, **OpenAI**.
- Yêu cầu đăng ký API Key từ Dashboard của nhà cung cấp bạn định sử dụng (hoặc cấu hình tất cả cho Test Mode).
- Cấu hình được lưu trữ local (JSON) an toàn.

### 3. Build từ mã nguồn
Nếu bạn là nhà phát triển, có thể build dự án bằng Visual Studio 2022 hoặc qua CLI:
```bash
dotnet build
dotnet run
```
Dự án đã tích hợp sẵn kịch bản và script cho đóng gói tiện lợi.

### 4. Đóng gói (Single EXE)
Để tạo file chạy độc lập Publish (Ví dụ: thông qua slash command publish hay CLI tool):
```powershell
dotnet publish -c Release -o ./publish
```

## ⌨️ Cách sử dụng

1. **Khởi động:** Ứng dụng hiển thị trên cùng màn hình dạng thanh gọn nhẹ (có thể kéo thả) & chạy ngầm System Tray.
2. **Thao tác nhanh (Phím tắt F3):**
   - **Click F3 một lần:** Bắt đầu ghi âm Batch (nhấn F3 lần nữa để kết thúc ghi). Trạng thái (Status) hiển thị "Recording (Batch)...".
   - **Nhấn và giữ F3:** Bắt đầu truyền cảm trực tiếp (Streaming). Trạng thái hiển thị "Streaming...". Thả phím F3 để dừng ghi và chốt câu.
3. **Resend / Test Mode:**
   - Kích hoạt **Test Mode** trong Settings nếu muốn đánh giá độ phân tích của 4 API cùng lúc.
   - Nhấn **"Resend Batch"** để gửi lại tệp tin âm thanh lưu gần nhất phân tích lại mà không cần nói lại.

## 📂 Giao diện & Cấu trúc mã

- `MainWindow.xaml`: Giao diện chính nhỏ gọn (có thể hiển thị ở chế độ Test Mode chia làm 4 panels để so sánh kết quả các APIs).
- `SettingsWindow.xaml`: Trình quản lý nhà cung cấp APIs, Key, Models, Test Mode và UI Settings.
- `Services/`:
  - `*StreamingService.cs` / `*BatchService.cs`: Trình xử lý nghiệp vụ STT cho Deepgram, Speechmatics, Soniox, OpenAI.
  - `AudioRecordingService`: Lọc âm thanh, ghi dữ liệu, xử lý VAD (loại bỏ khoảng lặng) và cấp ngõ ra cho cả File lẫn Event Buffer.
  - `InputInjector`: Mô phỏng và gõ văn bản chính xác trên Target Window Handle.
  - `SettingsService`: Logic I/O cấu hình.
  - `HotkeyService`: Nhúng phím nóng (Global hook).
- `ViewModels/`: `MainViewModel.cs` dùng để điều phối toàn bộ trạng thái (Recording Timer, Hybrid Decision threshold, Mutiple providers tasks).

## 📄 Giấy phép

Dự án tối ưu và phát triển bởi **Antigravity AI**, phục vụ mục đích tiện ích cá nhân & cộng đồng. Tự do sử dụng.

---
*Last update: February 2026*
