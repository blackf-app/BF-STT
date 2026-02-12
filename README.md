# BF-STT (Bright-Fast Speech To Text)

BF-STT là một ứng dụng Windows (WPF) nhẹ nhàng và mạnh mẽ, giúp chuyển đổi giọng nói thành văn bản ngay lập tức và nhập liệu trực tiếp vào các cửa sổ ứng dụng đang hoạt động. Ứng dụng sử dụng API Deepgram để đảm bảo độ chính xác cao và tốc độ xử lý vượt trội.

## 🚀 Tính năng nổi bật

- **Phím tắt toàn cầu (Global Hotkey):** Nhấn `F3` để bắt đầu/dừng ghi âm một cách nhanh chóng mà không cần chuyển cửa sổ.
- **Tự động nhập liệu (Auto-Typing):** Sau khi chuyển đổi, văn bản sẽ được tự động "gõ" vào cửa sổ ứng dụng bạn đã sử dụng trước đó.
- **Phản hồi âm thanh (Sound Feedback):** Phát âm báo nhẹ nhàng khi bắt đầu và kết thúc ghi âm để người dùng nhận biết trạng thái mà không cần nhìn màn hình.
- **Luôn hiển thị (Always on Top):** Giao diện nhỏ gọn luôn nằm trên cùng, giúp bạn dễ dàng theo dõi trạng thái ghi âm.
- **Đồng hồ ghi âm:** Hiển thị thời gian thực khi đang ghi âm.
- **Tự động dọn dẹp:** File âm thanh tạm thời được tự động xóa sau khi xử lý hoặc khi đóng ứng dụng để tiết kiệm dung lượng.
- **Xử lý âm thanh thông minh:** Tự động chuẩn hóa âm thanh về định dạng PCM 16kHz Mono để tối ưu hóa việc nhận diện.
- **Hỗ trợ đa ngôn ngữ:** Mặc định được cấu hình tối ưu cho Tiếng Việt (`vi`).

## 🛠 Công nghệ sử dụng

- **Framework:** .NET 8, WPF (Windows Presentation Foundation)
- **Audio Library:** [NAudio](https://github.com/naudio/NAudio) để ghi âm và xử lý luồng âm thanh.
- **STT Engine:** [Deepgram API](https://deepgram.com/) - Một trong những engine STT nhanh nhất hiện nay.
- **Pattern:** MVVM (Model-View-ViewModel) đảm bảo mã nguồn sạch và dễ bảo trì.

## 📦 Hướng dẫn cài đặt

### 1. Yêu cầu hệ thống
- Windows 10/11.
- .NET 8 Runtime hoặc SDK.

### 2. Cấu hình API Key
Trước khi chạy ứng dụng, bạn cần có API Key từ Deepgram.
1. Đăng ký tài khoản tại [Deepgram Console](https://console.deepgram.com/).
2. Tạo một API Key mới.
3. Ứng dụng sẽ tự động đọc cấu hình từ file `appsettings.json` nằm cùng thư mục với file chạy hoặc file cấu hình nhúng. Để ghi đè cấu hình, tạo file `appsettings.json` trong cùng thư mục với nội dung:

```json
{
  "Deepgram": {
    "ApiKey": "YOUR_DEEPGRAM_API_KEY_HERE",
    "BaseUrl": "https://api.deepgram.com/v1/listen",
    "DefaultLanguage": "vi",
    "Model": "nova-3"
  }
}
```

### 3. Build và Chạy dự án
Bạn có thể sử dụng Visual Studio 2022 hoặc .NET CLI:
```bash
dotnet build
dotnet run
```

Hệ thống versioning tự động sẽ chạy script `scripts/increment_version.ps1` để tăng số version mỗi khi build (trừ khi chạy publish tự động).

### 4. Xuất bản thành file duy nhất (Single EXE)
Để tạo ra một file `.exe` duy nhất bao gồm tất cả thư viện và file cấu hình, hãy chạy lệnh sau trong PowerShell:
```powershell
dotnet publish -c Release -o ./publish
```
Sau khi chạy xong, file `BF-STT.exe` sẽ nằm trong thư mục `publish`. Bạn có thể mang file này đi sử dụng ở bất kỳ máy Windows x64 nào mà không cần cài đặt thêm.

## ⌨️ Cách sử dụng

1. **Mở ứng dụng:** Ứng dụng sẽ xuất hiện ở phía trên cùng của màn hình.
2. **Bắt đầu ghi âm:** Nhấn phím `F3` (hoặc click nút Start). Bạn sẽ nghe thấy tiếng "bíp" và trạng thái chuyển sang "Recording...".
3. **Nói:** Thực hiện đoạn nói của bạn. Đồng hồ sẽ đếm thời gian.
4. **Dừng và Chuyển đổi:** Nhấn phím `F3` một lần nữa. Bạn sẽ nghe thấy tiếng "bíp" kết thúc. Ứng dụng sẽ tự động gửi dữ liệu đến Deepgram.
5. **Nhận kết quả:** Văn bản đã chuyển đổi sẽ xuất hiện trong giao diện và được tự động nhập vào cửa sổ ứng dụng bạn đang làm việc (ví dụ: Word, Notepad, Trình duyệt...).

## 📂 Cấu trúc thư mục

- `Services/`: Chứa các logic xử lý về Audio, Hotkey, Deepgram API, Sound và Input Injection.
- `ViewModels/`: Chứa logic điều khiển giao diện (MVVM).
- `Models/`: Các định dạng dữ liệu.
- `MainWindow.xaml`: Giao diện chính của ứng dụng.
- `scripts/`: Chứa các script hỗ trợ development (ví dụ: versioning).

## 🤖 Workflows

Dự án này hỗ trợ các workflow tự động thông qua agent:

- **publish**: Thực thi lệnh `dotnet publish -c Release -o ./publish` để đóng gói ứng dụng. Để chạy, hãy nhập `/publish` trong khung chat với agent.

## 📄 Giấy phép

Dự án này được phát triển cho mục đích cá nhân và cộng đồng. Bạn có thể tự do chỉnh sửa và sử dụng.

---
*Phát triển bởi Antigravity AI.*
