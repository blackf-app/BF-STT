# BF-STT (Bright-Fast Speech To Text)

**BF-STT** là một ứng dụng trợ lý giọng nói tối ưu cho Windows (WPF), cho phép bạn chuyển đổi lời nói thành văn bản và nhập liệu trực tiếp vào bất kỳ phần mềm nào (Word, Browser, Game, Discord...) với độ trễ cực thấp. Ứng dụng tích hợp sức mạnh từ những "ông lớn" trong ngành Speech-to-Text như **Deepgram**, **Speechmatics**, **Soniox**, và **OpenAI Whisper**.

---

### 🚀 Tính năng vượt trội

#### 🔹 1. Chế độ Hybrid Thông minh (Mặc định Hotkey: `F3`)
Cảm nhận sự linh hoạt tối đa với cơ chế nhận diện hành vi nhấn phím:
- **Nhấn nhanh (Short Press):** Chế độ **Batch**. Ghi âm toàn bộ câu thoại, xử lý offline và trả kết quả sau khi bạn kết thúc. Phù hợp cho ghi chú dài, cần độ chính xác cao nhất và tự động ngắt câu/dấu chấm.
- **Nhấn giữ (Long Press):** Chế độ **Streaming**. Văn bản xuất hiện ngay lập tức khi bạn đang nói (Real-time). Phù hợp cho nhắn tin nhanh hoặc nhập lệnh điều khiển.

#### 🔹 2. Tính năng "Stop & Send" (Mặc định Hotkey: `F4`)
- Dừng ngay lập tức phiên ghi âm hiện tại, lấy kết quả cuối cùng và tự động giả lập phím **Enter**. Cực kỳ hữu dụng khi bạn muốn chat nhanh trong Game hoặc gửi tin nhắn mà không cần chạm vào bàn phím.

#### 🔹 3. Chế độ Thử nghiệm & So sánh (Test Mode)
- Cho phép chạy song song nhiều API STT cùng lúc trên một giao diện.
- So sánh trực tiếp độ chính xác và tốc độ giữa **Deepgram**, **Speechmatics**, **Soniox** và **OpenAI** để chọn ra dịch vụ tốt nhất cho nhu cầu của bạn.

#### 🔹 4. Quản lý Âm thanh & Xử lý Nâng cao
- **Phản hồi âm thanh (Audio Feedback):** Hệ thống tiếng "Beep" thông minh thông báo trạng thái Bắt đầu/Kết thúc ghi âm giúp bạn sử dụng ứng dụng mà không cần nhìn màn hình.
- **Bộ lọc ảo giác (Anti-Hallucination):** Tự động loại bỏ các câu thừa do AI tự suy diễn khi gặp môi trường yên tĩnh hoặc nhiễu.
- **Tự động phát hiện khoảng lặng (VAD):** Dừng ghi âm thông minh khi bạn ngừng nói.
- **Gửi lại âm thanh (Resend Audio):** Bạn không hài lòng với kết quả từ API này? Chỉ cần 1-click để gửi lại đoạn âm thanh vừa thu cho API khác mà không cần nói lại lần hai.

#### 🔹 5. Nhập liệu & Bảo mật Clipboard
- **Auto-Typing:** Giả lập bàn phím siêu tốc để nhập văn bản vào ứng dụng đích.
- **Clipboard Protection:** Tự động sao lưu dữ liệu Clipboard cũ của bạn và khôi phục lại sau khi gõ xong, đảm bảo bạn không bị mất dữ liệu quan trọng đang lưu trong bộ nhớ tạm.

---

### 🛠 Công nghệ & Kiến trúc

- **Framework:** .NET 8 (C#) với WPF hiện đại.
- **Quản lý trạng thái:** Sử dụng **State Pattern** (Idle, Pending, Batch, Streaming, Processing) để đảm bảo luồng xử lý chặt chẽ và ổn định.
- **Audio Engine:** NAudio xử lý luồng âm thanh PCM 16kHz, Mono.
- **Dọn dẹp tự động:** Hệ thống tự động xóa các file ghi âm tạm thời sau mỗi phiên làm việc để giải phóng dung lượng ổ cứng.
- **Single Instance:** Đảm bảo chỉ có một bản chạy duy nhất thông qua cơ chế Mutex hệ thống.

---

### 📦 Hướng dẫn cài đặt & Sử dụng

#### 1. Yêu cầu hệ thống
- Hệ điều hành Windows 10 hoặc 11 (x64).
- Cài đặt [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
- Micro chất lượng ổn định.

#### 2. Cấu hình nhanh
1. Mở **Settings** (biểu tượng bánh răng) hoặc chuột phải vào icon khay hệ thống.
2. Nhập các **API Key** cần thiết (Deepgram được khuyến nghị cho tốc độ nhanh nhất).
3. Thiết lập **Hotkeys** và **Microphone** đầu vào.
4. Bật **Start with Windows** nếu muốn ứng dụng luôn sẵn sàng.

#### 3. Thao tác nhanh
- **F3 (Tap):** Bắt đầu/Kết thúc ghi âm (Batch).
- **F3 (Hold):** Nói đến đâu gõ đến đó (Streaming). Thả phím để gửi.
- **F4 (Tap):** Dừng ghi âm và nhấn Enter tự động.
- **Ctrl + Click (vào icon Resend):** Thử lại với API khác.

---

### 📂 Cấu trúc dự án

- `Services/`: Chứa toàn bộ logic xử lý STT, Audio, Hotkey và Registry.
- `Services/States/`: Triển khai State Machine cho quy trình ghi âm.
- `ViewModels/`: Logic giao diện theo mô hình MVVM.
- `Models/`: Các cấu trúc dữ liệu cho API Response và Cấu hình.
- `Scripts/`: Các script Powershell hỗ trợ tăng phiên bản và build tự động.

---

### 💻 Thông tin Nhà phát triển

**BF-STT** được thiết kế để mang lại trải nghiệm nhập liệu tự nhiên và mạnh mẽ nhất cho người dùng Windows.

- **Phiên bản hiện tại:** Tự động cập nhật qua Build Workflow.
- **Cập nhật mới nhất:** 22/02/2026
- **Phát triển bởi:** Antigravity AI

---
*Copyright © 2026. All rights reserved.*

