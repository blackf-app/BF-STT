# BF-STT (Bright-Fast Speech To Text)

**BF-STT** là một ứng dụng Windows (WPF) mạnh mẽ và linh hoạt, giúp chuyển đổi giọng nói thành văn bản ngay lập tức và nhập liệu trực tiếp vào bất kỳ ứng dụng nào đang hoạt động. Ứng dụng tích hợp công nghệ từ nhiều nhà cung cấp Speech-to-Text hàng đầu như **Deepgram**, **Speechmatics**, **Soniox**, và **OpenAI Whisper** để đảm bảo độ chính xác cực cao và độ trễ gần như bằng không.

---

### 🚀 Tính năng nổi bật

- **Chế độ Hybrid thông minh (Phím tắt F3):**
  - **Nhấn nhanh (Short Press):** Chế độ **Batch**. Ghi âm và gửi toàn bộ đoạn hội thoại sau khi kết thúc. Phù hợp cho các câu thoại dài, cần độ chính xác cao và tự động thêm dấu chấm câu.
  - **Nhấn giữ (Long Press):** Chế độ **Streaming**. Văn bản xuất hiện và được gõ trực tiếp ngay khi bạn đang nói. Phù hợp cho việc nhắn tin hoặc nhập liệu thời gian thực.
- **Hỗ trợ Đa Nền tảng STT:**
  - Hỗ trợ linh hoạt chuyển đổi giữa **Deepgram (Nova-3)**, **Speechmatics**, **Soniox**, và **OpenAI Whisper**.
  - **Chế độ Kiểm thử (Test Mode):** Cho phép chạy đồng thời và so sánh trực tiếp kết quả, tốc độ phản hồi từ nhiều API khác nhau trên cùng một giao diện.
- **Quản lý âm thanh nâng cao:**
  - **Resend Audio (Gửi lại âm thanh):** Gửi lại đoạn ghi âm vừa thu cho API khác mà không cần đọc lại, cực kỳ hữu ích khi muốn đối chiếu chéo kết quả các API.
  - **VAD & Lọc im lặng:** Tự động loại bỏ các đoạn không có tiếng người để tối ưu dung lượng và tránh các kết quả rác.
  - **Chống Hallucination:** Bộ lọc thông minh loại bỏ các câu "ảo giác" do AI tự suy diễn (ví dụ: "Cảm ơn đã xem", "Subscribe",...).
- **Tích hợp hệ thống thông minh:**
  - **Tự động nhập liệu (Auto-Typing):** Nhập văn bản trực tiếp vào cửa sổ ứng dụng đang hoạt động (Word, Notepad, Browser, Games...).
  - **Bảo vệ Clipboard:** Tự động sao lưu và khôi phục nội dung Clipboard của người dùng sau khi nhập liệu.
- **Giao diện & Cấu hình:** Quản lý chi tiết API Key, Model, cấu hình phím nóng và thiết lập khởi động cùng hệ thống.

---

### 🛠 Công nghệ sử dụng

- **Framework:** .NET 8, WPF (Windows Presentation Foundation).
- **Audio Engine:** [NAudio](https://github.com/naudio/NAudio) xử lý luồng âm thanh PCM 16kHz Mono.
- **Giao tiếp API:** REST API (cho Batch) và WebSocket (cho Streaming) tương tác trực tiếp tới hệ thống backend của các nhà cung cấp.
- **Kiến trúc:** Clean MVVM (Model-View-ViewModel) với các Interface dịch vụ riêng biệt.

---

### 📦 Hướng dẫn cài đặt & Sử dụng

#### 1. Yêu cầu hệ thống
- Windows 10/11 x64.
- .NET 8 Desktop Runtime.

#### 2. Cấu hình ban đầu
- Mở bảng **Settings** từ menu chuột phải ở System Tray hoặc giao diện chính.
- Nhập API Key cho các nhà cung cấp bạn muốn sử dụng.
- Thiết lập ngôn ngữ mặc định (mặc định là `vi`).

#### 3. Cách sử dụng chính
- **F3 (Nhấn nhả):** Bắt đầu/Kết thúc ghi âm Batch.
- **F3 (Nhấn giữ):** Ghi âm Streaming (thả phím để kết thúc).
- **Nút Resend:** Gửi lại đoạn âm thanh vừa thu để thử nghiệm với API khác.

#### 4. Build từ mã nguồn
```bash
dotnet build
dotnet run
```

#### 5. Đóng gói (Single EXE)
```powershell
dotnet publish -c Release -o ./publish
```

---

### 📂 Cấu trúc mã nguồn

- `MainWindow.xaml`: Giao diện chính, tích hợp bảng điều khiển và Visualizer âm lượng.
- `SettingsWindow.xaml`: Quản lý cấu hình API, phím tắt và tùy chỉnh UI.
- `Services/`:
  - `*StreamingService.cs` & `*BatchService.cs`: Logic xử lý STT cho từng nhà cung cấp.
  - `AudioRecordingService`: Quản lý thu âm, VAD và lọc nhiễu.
  - `InputInjector`: Xử lý việc mô phỏng bàn phím để nhập dữ liệu.
  - `HallucinationFilter`: Bộ lọc hậu xử lý văn bản AI.
- `ViewModels/`: Điều phối trạng thái và logic ứng dụng.

---
*Phát triển bởi **Antigravity AI**. Cập nhật lần cuối: Tháng 2, 2026*


