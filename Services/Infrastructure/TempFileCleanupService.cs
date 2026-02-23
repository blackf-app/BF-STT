using System.Diagnostics;
using System.IO;

namespace BF_STT.Services.Infrastructure
{
    /// <summary>
    /// Cleans up old temporary WAV files created by AudioRecordingService.
    /// </summary>
    public static class TempFileCleanupService
    {
        private const string TempFilePattern = "bf_stt_*.wav";

        /// <summary>
        /// Deletes all bf_stt_*.wav files from the system temp directory.
        /// Should be called once at application startup.
        /// </summary>
        public static void CleanupOldTempFiles()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var files = Directory.GetFiles(tempPath, TempFilePattern);

                int deleted = 0;
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deleted++;
                    }
                    catch
                    {
                        // File may be in use by another instance — skip
                    }
                }

                if (deleted > 0)
                {
                    Debug.WriteLine($"[TempCleanup] Deleted {deleted} old temp file(s).");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TempCleanup] Error scanning temp directory: {ex.Message}");
            }
        }
    }
}
