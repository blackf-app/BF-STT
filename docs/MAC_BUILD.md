# Build BF-STT trên macOS

## Yêu cầu

- macOS 11.0 hoặc mới hơn (Apple Silicon hoặc Intel)
- .NET SDK 8.0+: tải từ <https://dotnet.microsoft.com/download> hoặc cài qua Homebrew:
  ```bash
  brew install --cask dotnet-sdk
  ```

## Build nhanh

Từ thư mục dự án:

```bash
# Apple Silicon (M1/M2/M3/M4)
./scripts/build-mac.sh arm64 Release

# Intel
./scripts/build-mac.sh x64 Release
```

Output: `./publish/mac/BF-STT.app`

Chạy:

```bash
open ./publish/mac/BF-STT.app
```

## Quyền cần cấp khi chạy lần đầu

macOS sẽ hỏi cấp quyền cho 2 nhóm. App **bắt buộc** phải có cả hai mới chạy đúng:

1. **Microphone** (System Settings → Privacy & Security → Microphone)
   - Để ghi âm giọng nói.

2. **Accessibility** (System Settings → Privacy & Security → Accessibility)
   - Để:
     - Bắt phím tắt toàn cục (F3, F4, v.v.) khi app chạy ngầm.
     - Tự động dán văn bản đã chuyển đổi vào cửa sổ đang focus.
   - Nếu không cấp, hotkey và auto-paste sẽ không hoạt động — bạn vẫn có thể dùng UI thủ công.

## Build thủ công (không qua script)

```bash
dotnet publish BF-STT.csproj \
    -f net8.0 \
    -r osx-arm64 \
    -c Release \
    --self-contained false \
    -p:PublishSingleFile=false \
    -p:IsAutoPublishing=true \
    -o ./publish/mac-raw
```

Sau đó copy binaries vào `.app/Contents/MacOS/` và viết `Info.plist` thủ công.

## Vấn đề đã biết trên macOS

- **Noise Suppression (RNNoise)** tự động bị tắt trên macOS — package RNNoise
  không có native library cho Mac. App vẫn hoạt động bình thường, chỉ là tuỳ
  chọn này không có hiệu lực.

- **App chạy lần đầu sẽ bị Gatekeeper chặn** (vì chưa code-sign). Bypass:
  - Cách 1: `xattr -d com.apple.quarantine /path/to/BF-STT.app`
  - Cách 2: Right-click → Open → chọn Open trong hộp thoại cảnh báo.

- **Tray icon (Notify Icon)** chưa được port. App vẫn chạy nhưng không có biểu tượng menubar. Bạn có thể tự đóng/mở qua Cmd+Tab.

- **Auto-update** chỉ tải file `.dmg` về và mở Finder, không tự động thay thế app như Windows.

- **Single-file publish** (PublishSingleFile=true) hiện bị tắt cho macOS vì single-file + multi-target gây xung đột. Output sẽ là nhiều file `.dll` trong `Contents/MacOS/`.

## Build cho Windows (vẫn hoạt động)

```bash
dotnet publish BF-STT.csproj \
    -f net8.0-windows \
    -r win-x64 \
    -c Release \
    -p:IsAutoPublishing=true
```
