using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.DataObject;
using WpfIDataObject = System.Windows.IDataObject;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Handles clipboard backup and restore operations.
    /// Preserves the user's clipboard content during text injection.
    /// </summary>
    internal static class ClipboardHelper
    {
        /// <summary>
        /// Backs up the current clipboard content, preserving all formats.
        /// Returns null if clipboard is empty or contains unsupported formats.
        /// </summary>
        public static WpfIDataObject? Backup()
        {
            if (!WpfClipboard.ContainsText() &&
                !WpfClipboard.ContainsImage() &&
                !WpfClipboard.ContainsFileDropList() &&
                !WpfClipboard.ContainsAudio())
            {
                return null;
            }

            var backup = new WpfDataObject();

            if (WpfClipboard.ContainsText())
            {
                backup.SetText(WpfClipboard.GetText());
            }
            if (WpfClipboard.ContainsImage())
            {
                var image = WpfClipboard.GetImage();
                if (image != null) backup.SetImage(image);
            }
            if (WpfClipboard.ContainsFileDropList())
            {
                var files = WpfClipboard.GetFileDropList();
                if (files != null) backup.SetFileDropList(files);
            }
            if (WpfClipboard.ContainsAudio())
            {
                var audio = WpfClipboard.GetAudioStream();
                if (audio != null) backup.SetAudio(audio);
            }

            return backup;
        }

        /// <summary>
        /// Restores previously backed-up clipboard content.
        /// If backup is null (original clipboard was empty), clears the clipboard.
        /// </summary>
        public static void Restore(WpfIDataObject? backup)
        {
            if (backup == null)
            {
                WpfClipboard.Clear();
                return;
            }

            WpfClipboard.SetDataObject(backup, true); // true = persist after app exits
        }
    }
}
