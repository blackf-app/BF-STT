# BF-STT (Bright-Fast Speech To Text)

**BF-STT** là một ứng dụng trợ lý giọng nói tối ưu cho Windows (WPF), cho phép bạn chuyển đổi lời nói thành văn bản và nhập liệu trực tiếp vào bất kỳ phần mềm nào (Word, Browser, Game, Discord...) với độ trễ cực thấp. Ứng dụng tích hợp sức mạnh từ những "ông lớn" trong ngành Speech-to-Text như **Deepgram**, **Speechmatics**, **Soniox**, **OpenAI Whisper**, **ElevenLabs**, **Google Cloud**, **AssemblyAI**, và **Microsoft Azure**.

---

### 🚀 Tính năng vượt trội

#### 🔹 1. Chế độ Hybrid Thông minh (Mặc định Hotkey: `F3`)
Cơ chế nhận diện hành vi nhấn phím cực kỳ linh hoạt:
- **Nhấn nhanh (Short Press):** Chế độ **Batch**. Ghi âm hoàn bộ câu thoại, xử lý và trả kết quả sau khi kết thúc. Phù hợp cho ghi chú dài, cần độ chính xác cao nhất và tự động ngắt câu/dấu chấm.
- **Nhấn giữ (Long Press):** Chế độ **Streaming**. Văn bản xuất hiện ngay lập tức khi bạn đang nói (Real-time). Phù hợp cho nhắn tin nhanh hoặc nhập lệnh điều khiển.

#### 🔹 2. Tính năng "Stop & Send" (Mặc định Hotkey: `F4`)
- Dừng ngay lập tức phiên ghi âm hiện tại, lấy kết quả cuối cùng và tự động giả lập phím **Enter**. Cực kỳ hữu dụng khi bạn muốn chat nhanh trong Game hoặc gửi tin nhắn mà không cần chạm vào bàn phím.

#### 🔹 3. Chế độ Thử nghiệm & So sánh (Test Mode)
- Cho phép chạy song song **tất cả 8 API STT** cùng lúc trên một giao diện để so sánh độ chính xác và tốc độ trong thời gian thực.
- Giúp bạn chọn ra dịch vụ tốt nhất cho từng ngữ cảnh (tiếng Việt, tiếng Anh, môi trường ồn...).

#### 🔹 4. Quản lý Âm thanh & Xử lý Nâng cao
- **Phản hồi âm thanh (Audio Feedback):** Hệ thống tiếng "Beep" thông minh thông báo trạng thái Bắt đầu/Kết thúc ghi âm.
- **Lọc nhiễu thông minh (RNNoise Suppression):** Tích hợp công nghệ lọc nhiễu dựa trên AI. Hệ thống tự động ghi âm ở 48kHz để tối ưu cho RNNoise trước khi xử lý, giúp loại bỏ tiếng quạt, bàn phím và tiếng ồn môi trường.
- **Tự động điều chỉnh âm lượng (Auto Gain Control - AGC):** Tự động khuếch đại tín hiệu micro yếu (lên đến x30) và chống vỡ tiếng (soft-clipping) khi nói quá to, giúp STT hoạt động ổn định trên mọi loại microphone.
- **Phát hiện giọng nói cải tiến (VAD):** Sử dụng năng lượng trung bình (RMS) thay vì peak level để nhận diện chính xác giọng nói, giảm thiểu kích hoạt nhầm bởi tiếng ồn đột ngột hoặc tiếng gõ phím.
- **Gửi lại âm thanh (Resend Audio):** Thử lại đoạn âm thanh vừa thu với một API khác chỉ với 1-click (Hotkey: `Ctrl + Resend Icon`).

#### 🔹 5. Giao diện Hiện đại & Tiện ích
- **Lọc API thông minh:** Dropdown chọn API chỉ hiển thị những dịch vụ bạn đã cấu hình API Key, giúp giao diện luôn gọn gàng.
- **Quản lý Lịch sử (History):** Lưu lại các đoạn hội thoại gần nhất, cho phép copy lại hoặc gửi lại vào cửa sổ đang active.
- **Auto-Typing & Clipboard Protection:** Nhập liệu siêu tốc và tự động khôi phục dữ liệu Clipboard sau khi gõ xong.

---

### 🛠 Công nghệ & Kiến trúc

- **Framework:** .NET 8 (C#) với WPF hiện đại, hỗ trợ hiệu ứng hiển thị mượt mà.
- **Quản lý trạng thái:** Sử dụng **State Pattern** (Idle, Pending, Batch, Streaming, Processing, Failed) để đảm bảo luồng xử lý ổn định.
- **Audio Engine:** NAudio xử lý luồng âm thanh PCM. Tự động xử lý Downsampling, lọc High-Pass (HPF), và Auto Gain Control (AGC).
- **Bảo mật:** Lưu trữ cấu hình an toàn trong Registry và file cấu hình cục bộ.
- **Single Instance:** Ngăn chặn việc chạy nhiều bản ghi đè lên nhau.

---

### 📦 Hướng dẫn cài đặt & Sử dụng

#### 1. Yêu cầu hệ thống
- Windows 10/11 (x64).
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

#### 2. Cấu hình nhanh
1. Mở **Settings** (biểu tượng bánh răng).
2. Nhập **API Key** cho các dịch vụ bạn muốn dùng.
3. Cấu hình **Hotkey** và chọn **Microphone**.
4. Bật **Noise Suppression** nếu làm việc trong môi trường ồn.

#### 3. Thao tác nhanh
- **F3 (Tap):** Bắt đầu/Kết thúc ghi âm (Batch).
- **F3 (Hold):** Streaming (nói đến đâu gõ đến đó).
- **F4:** Dừng và nhấn Enter.
- **Click vào item History:** Gửi lại văn bản cũ vào ứng dụng đang dùng.

---

### 📂 Cấu trúc dự án

- `Services/STT/Providers/`: Chứa logic của 8 nhà cung cấp (Deepgram, Speechmatics, Soniox, OpenAI, ElevenLabs, Google, AssemblyAI, Azure).
- `Services/Workflow/`: State Machine điều phối toàn bộ vòng đời ghi âm.
- `Services/Audio/`: Xử lý âm thanh, VAD và RNNoise.
- `Services/Infrastructure/`: Quản lý Settings, History, Sound và DI Container.
- `ViewModels/` & `Views/`: Triển khai giao diện người dùng theo mô hình MVVM.

---

### 🔌 Hướng dẫn thêm STT API mới

1.  **Tạo Service:** Kế thừa `BaseBatchSttService` trong thư mục `Providers`.
2.  **Đăng ký Service:** Thêm vào `ServiceRegistration.cs` (phân loại Batch/Streaming).
3.  **Cập nhật UI/Settings:** Thêm API Key vào `AppSettings` và `SettingsWindow.xaml`.

---

### 💻 Thông tin Nhà phát triển

- **Tác giả:** Black Face
- **Hỗ trợ phát triển bởi:** Antigravity AI
- **Cập nhật mới nhất:** 02/03/2026
- **Phiên bản:** v1.2.1 (AGC & RMS VAD Update)

---
*Copyright © 2026 Black Face. All rights reserved.*

