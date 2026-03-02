using System;

namespace BF_STT.Services.Audio
{
    /// <summary>
    /// Thuật toán RMS-based Auto Gain Control (AGC) để tự động điều chỉnh âm lượng
    /// tín hiệu mic yếu lên mức mục tiêu, đồng thời giới hạn tiếng ồn và tránh clipping.
    /// </summary>
    public class AutoGainControl
    {
        // Các cấu hình giới hạn Gain
        public float TargetLevel { get; set; } = 0.25f; // Mức RMS mục tiêu (normalized)
        public float MaxGain { get; set; } = 30.0f; // Khuếch đại tối đa (x30 lần)
        public float MinGain { get; set; } = 1.0f; // Không bao giờ giảm âm lượng thấp hơn gốc
        
        // Tốc độ phản hồi của hệ thống thay đổi Gain
        // Dùng cho frame-based processing. Giả sử 50ms buffers.
        public float AttackAlpha { get; set; } = 0.1f; // Thay đổi nhanh khi tiếng to đột ngột
        public float ReleaseAlpha { get; set; } = 0.02f; // Tăng chậm khi tiếng nhỏ

        // Trạng thái hiện tại
        private float _currentGain = 1.0f;

        public AutoGainControl()
        {
            // Reset to default
            _currentGain = 1.0f;
        }

        public void Reset()
        {
            _currentGain = 1.0f;
        }

        /// <summary>
        /// Xử lý một mảng mẫu âm thanh (float từ -1.0 đến 1.0).
        /// Tính toán RMS của frame và áp dụng Gain vào từng mẫu.
        /// Quá trình thay đổi Gain được vuốt mượt (smoothing) cho từng sample để tránh gây ra tiếng lách cách (click/pop artifacts).
        /// </summary>
        public void Process(Span<float> buffer)
        {
            if (buffer.Length == 0) return;

            // 1. Tính toán RMS của frame hiện tại
            double sumSquares = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                sumSquares += buffer[i] * buffer[i];
            }
            float currentRms = (float)Math.Sqrt(sumSquares / buffer.Length);

            // 2. Tính toán Target Gain
            float targetGain = 1.0f;
            
            // Chỉ tính Target Gain nều tín hiệu có vẻ là âm thanh (không phải silence tuyệt đối)
            // Ngưỡng 0.001f tránh chia cho 0 hoặc khuếch đại điện tĩnh cực nhỏ
            if (currentRms > 0.001f) 
            {
                targetGain = TargetLevel / currentRms;
            }

            // Kẹp Target Gain trong khoảng cho phép
            targetGain = Math.Clamp(targetGain, MinGain, MaxGain);

            // 3. Chọn tốc độ thay đổi Gain (Attack hay Release)
            // Nếu Target cần bé hơn Current (tín hiệu đang to) => Giảm Gain => Attack
            // Nếu Target đo được lớn hơn Current (tín hiệu nhỏ) => Tăng Gain => Release
            float alpha = (targetGain < _currentGain) ? AttackAlpha : ReleaseAlpha;

            // 4. Áp dụng Gain cho từng sample với smoothing
            for (int i = 0; i < buffer.Length; i++)
            {
                // Thay đổi Gain mượt mà theo từng sample thay vì giật cục từng frame
                // Alpha ở trên là theo frame. Chia nhỏ alpha ra cho sample để smooth hơn
                float sampleAlpha = alpha / buffer.Length;
                _currentGain = (_currentGain * (1.0f - sampleAlpha)) + (targetGain * sampleAlpha);
                
                // Áp dụng Gain
                buffer[i] = buffer[i] * _currentGain;
            }
        }
    }
}
