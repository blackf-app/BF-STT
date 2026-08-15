using System.Collections.Generic;

namespace BFSTT.Droid
{
    /// <summary>
    /// Tiny in-app localization table. UI strings are keyed and stored per language, in the
    /// order given by <see cref="Langs"/>. Missing translations fall back to English, then to
    /// the first entry, then to the key itself. Set <see cref="Lang"/> from AppSettings.UiLang
    /// on startup; changing it and recreating the activity re-renders the UI in that language.
    ///
    /// Main-screen / bubble / notification strings are provided in all 7 languages. The long
    /// Guide-screen strings are provided in Vietnamese + English and fall back to English for
    /// the other display languages.
    /// </summary>
    public static class L
    {
        /// <summary>Display-language codes, in the column order used by every entry below.</summary>
        public static readonly string[] Langs = { "vi", "en", "de", "fr", "th", "zh", "ko" };

        /// <summary>Native names for the display-language picker (same order as Langs).</summary>
        public static readonly string[] LangNames = { "Tiếng Việt", "English", "Deutsch", "Français", "ไทย", "中文", "한국어" };

        public static string Lang = "vi";

        public static string T(string key)
        {
            if (!_t.TryGetValue(key, out var arr)) return key;
            int i = System.Array.IndexOf(Langs, Lang);
            if (i >= 0 && i < arr.Length && !string.IsNullOrEmpty(arr[i])) return arr[i];
            if (arr.Length > 1 && !string.IsNullOrEmpty(arr[1])) return arr[1]; // English fallback
            return arr.Length > 0 ? arr[0] : key;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        private static readonly Dictionary<string, string[]> _t = new()
        {
            // ---------- main screen ----------
            ["app_subtitle"] = new[] { "Nhập liệu bằng giọng nói", "Voice typing", "Spracheingabe", "Saisie vocale", "พิมพ์ด้วยเสียง", "语音输入", "음성 입력" },
            ["guide_btn"] = new[] { "Hướng dẫn", "Guide", "Anleitung", "Guide", "คู่มือ", "使用指南", "도움말" },

            ["rec_lang_title"] = new[] { "Ngôn ngữ nhận dạng", "Recognition language", "Erkennungssprache", "Langue de reconnaissance", "ภาษาที่จะถอดเสียง", "识别语言", "인식 언어" },
            ["rec_lang_desc"] = new[] { "Chọn ngôn ngữ giọng nói, hoặc để Tự động nhận diện.", "Pick the spoken language, or leave it on Auto detect.", "Wähle die gesprochene Sprache oder lass die automatische Erkennung an.", "Choisissez la langue parlée, ou laissez la détection automatique.", "เลือกภาษาที่พูด หรือปล่อยให้ตรวจจับอัตโนมัติ", "选择说话的语言，或使用自动检测。", "말하는 언어를 선택하거나 자동 감지를 사용하세요." },
            ["auto_detect"] = new[] { "Tự động nhận diện", "Auto detect", "Automatisch erkennen", "Détection auto", "ตรวจจับอัตโนมัติ", "自动检测", "자동 감지" },

            ["ui_lang_title"] = new[] { "Ngôn ngữ hiển thị", "Display language", "Anzeigesprache", "Langue d'affichage", "ภาษาที่แสดงผล", "显示语言", "표시 언어" },
            ["ui_lang_desc"] = new[] { "Ngôn ngữ giao diện của ứng dụng.", "The app's interface language.", "Die Sprache der App-Oberfläche.", "La langue de l'interface de l'application.", "ภาษาของหน้าจอแอป", "应用界面的语言。", "앱 인터페이스 언어입니다." },

            ["provider_title"] = new[] { "Nhà cung cấp & API key", "Provider & API key", "Anbieter & API-Schlüssel", "Fournisseur & clé API", "ผู้ให้บริการและคีย์ API", "服务商与 API 密钥", "제공업체 및 API 키" },
            ["provider_desc"] = new[] { "Chọn nhà cung cấp nhận dạng và dán API key tương ứng.", "Choose a recognition provider and paste its API key.", "Wähle einen Anbieter und füge dessen API-Schlüssel ein.", "Choisissez un fournisseur et collez sa clé API.", "เลือกผู้ให้บริการถอดเสียงและวางคีย์ API", "选择识别服务商并粘贴对应的 API 密钥。", "인식 제공업체를 선택하고 API 키를 붙여넣으세요." },
            ["apikey_hint"] = new[] { "Dán API key vào đây", "Paste API key here", "API-Schlüssel hier einfügen", "Collez la clé API ici", "วางคีย์ API ที่นี่", "在此粘贴 API 密钥", "여기에 API 키 붙여넣기" },
            ["save_btn"] = new[] { "Lưu cài đặt", "Save settings", "Einstellungen speichern", "Enregistrer", "บันทึกการตั้งค่า", "保存设置", "설정 저장" },
            ["saved_toast"] = new[] { "Đã lưu cài đặt.", "Settings saved.", "Einstellungen gespeichert.", "Réglages enregistrés.", "บันทึกการตั้งค่าแล้ว", "设置已保存。", "설정이 저장되었습니다." },

            ["autopaste_title"] = new[] { "Dán tự động", "Auto-paste", "Automatisch einfügen", "Collage auto", "วางอัตโนมัติ", "自动粘贴", "자동 붙여넣기" },
            ["autopaste_desc"] = new[] { "Bật Trợ năng để BF-STT tự dán văn bản vừa nhận dạng vào ô đang chọn. Nếu tắt, văn bản chỉ được sao chép vào bộ nhớ tạm.", "Enable Accessibility so BF-STT pastes the recognized text into the focused field. If off, text is only copied to the clipboard.", "Aktiviere die Bedienungshilfe, damit BF-STT den erkannten Text in das ausgewählte Feld einfügt. Sonst wird er nur in die Zwischenablage kopiert.", "Activez l'accessibilité pour que BF-STT colle le texte reconnu dans le champ sélectionné. Sinon, il est seulement copié dans le presse-papiers.", "เปิดการช่วยเหลือการเข้าถึงเพื่อให้ BF-STT วางข้อความที่ถอดได้ลงในช่องที่เลือก หากปิด ข้อความจะถูกคัดลอกไปยังคลิปบอร์ดเท่านั้น", "开启无障碍服务，BF-STT 会把识别出的文字直接粘贴到当前输入框。关闭时仅复制到剪贴板。", "접근성을 켜면 BF-STT가 인식된 텍스트를 선택된 입력창에 붙여넣습니다. 끄면 클립보드에만 복사됩니다." },
            ["autopaste_switch"] = new[] { "Tự động dán vào ô đang chọn", "Auto-paste into the focused field", "Automatisch in das Feld einfügen", "Coller dans le champ sélectionné", "วางลงในช่องที่เลือกอัตโนมัติ", "自动粘贴到当前输入框", "선택된 입력창에 자동 붙여넣기" },
            ["a11y_status_label"] = new[] { "Trạng thái Trợ năng:", "Accessibility status:", "Bedienungshilfe-Status:", "État de l'accessibilité :", "สถานะการช่วยเหลือการเข้าถึง:", "无障碍状态：", "접근성 상태:" },
            ["a11y_on"] = new[] { "●  Đang bật", "●  On", "●  An", "●  Activé", "●  เปิด", "●  已开启", "●  켜짐" },
            ["a11y_off"] = new[] { "○  Chưa bật", "○  Off", "○  Aus", "○  Désactivé", "○  ปิด", "○  未开启", "○  꺼짐" },
            ["a11y_open_btn"] = new[] { "Mở cài đặt hệ thống cho BF-STT Paste", "Open system settings for BF-STT Paste", "Systemeinstellungen für BF-STT Paste öffnen", "Ouvrir les réglages système pour BF-STT Paste", "เปิดการตั้งค่าระบบสำหรับ BF-STT Paste", "打开 BF-STT Paste 的系统设置", "BF-STT Paste 시스템 설정 열기" },

            ["bubble_title"] = new[] { "Icon nổi", "Floating bubble", "Schwebendes Symbol", "Bulle flottante", "ไอคอนลอย", "悬浮图标", "플로팅 아이콘" },
            ["bubble_desc"] = new[] { "Bật icon nổi để thu âm từ mọi ứng dụng. Chạm icon để thu, chạm lần nữa để dừng và dán. Giữ icon để mở cài đặt.", "Turn on the floating bubble to record from any app. Tap to record, tap again to stop and paste. Hold to open settings.", "Aktiviere das schwebende Symbol, um aus jeder App aufzunehmen. Tippen zum Aufnehmen, erneut tippen zum Stoppen und Einfügen. Halten öffnet die Einstellungen.", "Activez la bulle flottante pour enregistrer depuis n'importe quelle app. Touchez pour enregistrer, à nouveau pour arrêter et coller. Maintenez pour les réglages.", "เปิดไอคอนลอยเพื่อบันทึกเสียงจากทุกแอป แตะเพื่อบันทึก แตะอีกครั้งเพื่อหยุดและวาง กดค้างเพื่อเปิดการตั้งค่า", "开启悬浮图标即可在任意应用中录音。点按录音，再次点按停止并粘贴。长按打开设置。", "플로팅 아이콘을 켜면 어떤 앱에서도 녹음할 수 있습니다. 탭하여 녹음, 다시 탭하여 중지·붙여넣기, 길게 눌러 설정 열기." },
            ["bubble_on_btn"] = new[] { "Bật icon nổi", "Turn on bubble", "Symbol einschalten", "Activer la bulle", "เปิดไอคอนลอย", "开启悬浮图标", "아이콘 켜기" },
            ["bubble_off_btn"] = new[] { "Tắt icon nổi", "Turn off bubble", "Symbol ausschalten", "Désactiver la bulle", "ปิดไอคอนลอย", "关闭悬浮图标", "아이콘 끄기" },
            ["keepalive_desc"] = new[] { "Để icon nổi KHÔNG bị hệ thống tắt (nhất là Xiaomi/HyperOS, Oppo, Vivo, Huawei), hãy bật cả hai quyền sau rồi khoá app trong Recents:", "To keep the bubble from being killed by the system (especially Xiaomi/HyperOS, Oppo, Vivo, Huawei), enable both of these and lock the app in Recents:", "Damit das Symbol nicht vom System beendet wird (besonders Xiaomi/HyperOS, Oppo, Vivo, Huawei), aktiviere beide Optionen und sperre die App in den letzten Apps:", "Pour éviter que la bulle soit fermée par le système (surtout Xiaomi/HyperOS, Oppo, Vivo, Huawei), activez ces deux options et verrouillez l'app dans Récents :", "เพื่อไม่ให้ระบบปิดไอคอนลอย (โดยเฉพาะ Xiaomi/HyperOS, Oppo, Vivo, Huawei) ให้เปิดทั้งสองอย่างนี้แล้วล็อกแอปใน Recents:", "为防止系统关闭悬浮图标（尤其是 Xiaomi/HyperOS、Oppo、Vivo、Huawei），请开启以下两项并在最近任务中锁定应用：", "시스템이 아이콘을 종료하지 않도록(특히 Xiaomi/HyperOS, Oppo, Vivo, Huawei) 아래 두 가지를 켜고 최근 앱에서 앱을 잠그세요:" },
            ["battery_btn"] = new[] { "1) Tắt tối ưu pin cho app", "1) Disable battery optimization", "1) Akku-Optimierung deaktivieren", "1) Désactiver l'optimisation batterie", "1) ปิดการประหยัดแบตสำหรับแอป", "1) 关闭该应用的电池优化", "1) 배터리 최적화 끄기" },
            ["autostart_btn"] = new[] { "2) Mở Tự khởi động (Autostart)", "2) Open Autostart", "2) Autostart öffnen", "2) Ouvrir le démarrage auto", "2) เปิดการเริ่มอัตโนมัติ (Autostart)", "2) 打开自启动 (Autostart)", "2) 자동 실행(Autostart) 열기" },

            ["test_title"] = new[] { "Thử nghiệm ngay trong app", "Try it in the app", "In der App testen", "Tester dans l'app", "ทดลองในแอป", "在应用内试用", "앱에서 테스트" },
            ["test_desc"] = new[] { "Ghi âm thử và xem kết quả nhận dạng.", "Record a test and see the recognized text.", "Nimm einen Test auf und sieh das Ergebnis.", "Enregistrez un test et voyez le résultat.", "อัดเสียงทดสอบและดูผลลัพธ์", "录一段测试并查看识别结果。", "테스트 녹음을 하고 인식 결과를 확인하세요." },
            ["record_btn"] = new[] { "●  Thu âm thử", "●  Record test", "●  Test aufnehmen", "●  Enregistrer un test", "●  อัดทดสอบ", "●  录音测试", "●  테스트 녹음" },
            ["record_stop_btn"] = new[] { "■  Dừng & nhận dạng", "■  Stop & transcribe", "■  Stopp & umwandeln", "■  Arrêter & transcrire", "■  หยุดและถอดเสียง", "■  停止并识别", "■  중지 및 변환" },
            ["status_ready"] = new[] { "Sẵn sàng.", "Ready.", "Bereit.", "Prêt.", "พร้อม", "就绪。", "준비됨." },
            ["status_recording"] = new[] { "Đang thu âm…", "Recording…", "Aufnahme…", "Enregistrement…", "กำลังอัด…", "正在录音…", "녹음 중…" },
            ["status_transcribing"] = new[] { "Đang nhận dạng…", "Transcribing…", "Wird umgewandelt…", "Transcription…", "กำลังถอดเสียง…", "识别中…", "변환 중…" },
            ["status_done"] = new[] { "Xong.", "Done.", "Fertig.", "Terminé.", "เสร็จแล้ว", "完成。", "완료." },
            ["status_no_text"] = new[] { "Không nhận được văn bản.", "No text received.", "Kein Text empfangen.", "Aucun texte reçu.", "ไม่ได้รับข้อความ", "未收到文字。", "텍스트를 받지 못했습니다." },
            ["status_no_key"] = new[] { "Chưa nhập API key cho {0}", "No API key for {0}", "Kein API-Schlüssel für {0}", "Aucune clé API pour {0}", "ยังไม่ได้ใส่คีย์ API สำหรับ {0}", "尚未填写 {0} 的 API 密钥", "{0}의 API 키가 없습니다" },
            ["status_stt_error"] = new[] { "STT lỗi: {0}", "STT error: {0}", "STT-Fehler: {0}", "Erreur STT : {0}", "STT ผิดพลาด: {0}", "STT 错误：{0}", "STT 오류: {0}" },
            ["status_mic_error"] = new[] { "Lỗi micro: {0}", "Mic error: {0}", "Mikrofonfehler: {0}", "Erreur micro : {0}", "ไมค์ผิดพลาด: {0}", "麦克风错误：{0}", "마이크 오류: {0}" },
            ["status_stop_error"] = new[] { "Lỗi dừng thu: {0}", "Stop error: {0}", "Stopp-Fehler: {0}", "Erreur d'arrêt : {0}", "หยุดผิดพลาด: {0}", "停止录音出错：{0}", "중지 오류: {0}" },
            ["result_hint"] = new[] { "Văn bản nhận dạng sẽ hiện ở đây", "Recognized text will appear here", "Erkannter Text erscheint hier", "Le texte reconnu apparaîtra ici", "ข้อความที่ถอดได้จะแสดงที่นี่", "识别的文字将显示在此", "인식된 텍스트가 여기에 표시됩니다" },
            ["copy_btn"] = new[] { "Sao chép kết quả", "Copy result", "Ergebnis kopieren", "Copier le résultat", "คัดลอกผลลัพธ์", "复制结果", "결과 복사" },
            ["copy_empty"] = new[] { "Chưa có văn bản để sao chép.", "No text to copy.", "Kein Text zum Kopieren.", "Aucun texte à copier.", "ไม่มีข้อความให้คัดลอก", "没有可复制的文字。", "복사할 텍스트가 없습니다." },
            ["copied_toast"] = new[] { "Đã sao chép.", "Copied.", "Kopiert.", "Copié.", "คัดลอกแล้ว", "已复制。", "복사되었습니다." },

            ["overlay_needed"] = new[] { "Hãy cấp quyền hiển thị trên các ứng dụng khác.", "Please grant the 'Display over other apps' permission.", "Bitte erlaube 'Über anderen Apps anzeigen'.", "Veuillez accorder « Afficher par-dessus les autres apps ».", "โปรดอนุญาต \"แสดงบนแอปอื่น\"", "请授予“显示在其他应用上层”权限。", "'다른 앱 위에 표시' 권한을 허용해 주세요." },
            ["bubble_enabled_toast"] = new[] { "Icon nổi đã bật. Bạn có thể thu nhỏ app.", "Bubble is on. You can minimize the app.", "Symbol ist an. Du kannst die App minimieren.", "La bulle est activée. Vous pouvez réduire l'app.", "เปิดไอคอนลอยแล้ว คุณย่อแอปได้", "悬浮图标已开启，可最小化应用。", "아이콘이 켜졌습니다. 앱을 최소화해도 됩니다." },
            ["bubble_disabled_toast"] = new[] { "Đã tắt icon nổi.", "Bubble turned off.", "Symbol ausgeschaltet.", "Bulle désactivée.", "ปิดไอคอนลอยแล้ว", "已关闭悬浮图标。", "아이콘을 껐습니다." },
            ["a11y_find_toast"] = new[] { "Tìm \"BF-STT Paste\" và gạt để bật.", "Find \"BF-STT Paste\" and toggle it on.", "Finde \"BF-STT Paste\" und schalte es ein.", "Trouvez « BF-STT Paste » et activez-le.", "หา \"BF-STT Paste\" แล้วเปิดใช้งาน", "找到 “BF-STT Paste” 并开启。", "\"BF-STT Paste\"를 찾아 켜세요." },
            ["a11y_find_list_toast"] = new[] { "Tìm \"BF-STT Paste\" trong danh sách và gạt để bật.", "Find \"BF-STT Paste\" in the list and toggle it on.", "Finde \"BF-STT Paste\" in der Liste und schalte es ein.", "Trouvez « BF-STT Paste » dans la liste et activez-le.", "หา \"BF-STT Paste\" ในรายการแล้วเปิดใช้งาน", "在列表中找到 “BF-STT Paste” 并开启。", "목록에서 \"BF-STT Paste\"를 찾아 켜세요." },
            ["a11y_open_fail"] = new[] { "Không mở được cài đặt Trợ năng.", "Couldn't open Accessibility settings.", "Bedienungshilfe-Einstellungen konnten nicht geöffnet werden.", "Impossible d'ouvrir les réglages d'accessibilité.", "เปิดการตั้งค่าการช่วยเหลือการเข้าถึงไม่ได้", "无法打开无障碍设置。", "접근성 설정을 열 수 없습니다." },
            ["battery_open_fail"] = new[] { "Không mở được cài đặt pin.", "Couldn't open battery settings.", "Akku-Einstellungen konnten nicht geöffnet werden.", "Impossible d'ouvrir les réglages batterie.", "เปิดการตั้งค่าแบตไม่ได้", "无法打开电池设置。", "배터리 설정을 열 수 없습니다." },
            ["autostart_fail"] = new[] { "Không tìm thấy màn hình Autostart. Hãy bật thủ công trong Cài đặt.", "Autostart screen not found. Enable it manually in Settings.", "Autostart-Bildschirm nicht gefunden. Aktiviere es manuell in den Einstellungen.", "Écran Autostart introuvable. Activez-le manuellement dans les réglages.", "ไม่พบหน้า Autostart โปรดเปิดเองในการตั้งค่า", "未找到自启动页面。请在设置中手动开启。", "자동 실행 화면을 찾을 수 없습니다. 설정에서 직접 켜세요." },
            ["picker_ui_lang_title"] = new[] { "Chọn ngôn ngữ hiển thị", "Choose display language", "Anzeigesprache wählen", "Choisir la langue d'affichage", "เลือกภาษาที่แสดงผล", "选择显示语言", "표시 언어 선택" },
            ["picker_rec_lang_title"] = new[] { "Chọn ngôn ngữ nhận dạng", "Choose recognition language", "Erkennungssprache wählen", "Choisir la langue de reconnaissance", "เลือกภาษาที่จะถอดเสียง", "选择识别语言", "인식 언어 선택" },

            // ---------- bubble service + notification ----------
            ["bubble_cant_record"] = new[] { "Không thể thu âm: {0}", "Can't record: {0}", "Aufnahme nicht möglich: {0}", "Enregistrement impossible : {0}", "อัดเสียงไม่ได้: {0}", "无法录音：{0}", "녹음할 수 없습니다: {0}" },
            ["bubble_no_key"] = new[] { "Chưa có API key. Giữ icon để mở cài đặt.", "No API key. Hold the icon to open settings.", "Kein API-Schlüssel. Halte das Symbol für die Einstellungen.", "Aucune clé API. Maintenez l'icône pour les réglages.", "ยังไม่มีคีย์ API กดค้างที่ไอคอนเพื่อเปิดการตั้งค่า", "没有 API 密钥。长按图标打开设置。", "API 키가 없습니다. 아이콘을 길게 눌러 설정을 여세요." },
            ["bubble_pasted"] = new[] { "Đã dán: {0}", "Pasted: {0}", "Eingefügt: {0}", "Collé : {0}", "วางแล้ว: {0}", "已粘贴：{0}", "붙여넣음: {0}" },
            ["bubble_copied_a11y"] = new[] { "Đã copy (bật Trợ năng để tự dán): {0}", "Copied (enable Accessibility to auto-paste): {0}", "Kopiert (Bedienungshilfe für Auto-Einfügen aktivieren): {0}", "Copié (activez l'accessibilité pour le collage auto) : {0}", "คัดลอกแล้ว (เปิดการช่วยเหลือการเข้าถึงเพื่อวางอัตโนมัติ): {0}", "已复制（开启无障碍以自动粘贴）：{0}", "복사됨(자동 붙여넣기는 접근성을 켜세요): {0}" },
            ["bubble_copied"] = new[] { "Đã copy: {0}", "Copied: {0}", "Kopiert: {0}", "Copié : {0}", "คัดลอกแล้ว: {0}", "已复制：{0}", "복사됨: {0}" },
            ["notif_title"] = new[] { "BF-STT đang chạy", "BF-STT is running", "BF-STT läuft", "BF-STT est actif", "BF-STT กำลังทำงาน", "BF-STT 正在运行", "BF-STT 실행 중" },
            ["notif_text"] = new[] { "Chạm icon để thu âm • Giữ để mở cài đặt", "Tap the icon to record • Hold to open settings", "Symbol tippen zum Aufnehmen • Halten für Einstellungen", "Touchez l'icône pour enregistrer • Maintenez pour les réglages", "แตะไอคอนเพื่ออัด • กดค้างเพื่อเปิดการตั้งค่า", "点按图标录音 • 长按打开设置", "아이콘을 탭하여 녹음 • 길게 눌러 설정 열기" },
            ["notif_channel_desc"] = new[] { "Icon nổi nhận dạng giọng nói", "Floating speech-to-text bubble", "Schwebendes Sprache-zu-Text-Symbol", "Bulle flottante de reconnaissance vocale", "ไอคอนลอยสำหรับถอดเสียงเป็นข้อความ", "语音转文字悬浮图标", "음성-텍스트 플로팅 아이콘" },

            // ---------- guide screen (vi + en; other display languages fall back to English) ----------
            ["guide_back"] = new[] { "‹  Quay lại", "‹  Back" },
            ["guide_title"] = new[] { "Hướng dẫn sử dụng", "User guide" },
            ["guide_subtitle"] = new[] { "BF-STT — nhập liệu bằng giọng nói trên Android", "BF-STT — voice typing on Android" },
            ["g_device_title"] = new[] { "Thiết bị của bạn", "Your device" },
            ["g_device_line"] = new[] { "Máy của bạn: Android {0} (API {1}).", "Your device: Android {0} (API {1})." },
            ["g_device_steps_intro"] = new[] { "Các bước cần làm cho phiên bản này:", "Steps needed for this version:" },
            ["g_step_mic"] = new[] { "Cấp quyền Micro để thu âm.", "Grant the Microphone permission." },
            ["g_step_overlay"] = new[] { "Bật quyền \"Hiển thị trên ứng dụng khác\" để dùng icon nổi.", "Enable \"Display over other apps\" for the floating bubble." },
            ["g_step_notif"] = new[] { "Cho phép quyền Thông báo khi app hỏi.", "Allow the Notifications permission when asked." },
            ["g_step_a11y_restricted"] = new[] { "⚠ Để bật Trợ năng \"BF-STT Paste\": vào Cài đặt ứng dụng → ⋮ → \"Cho phép cài đặt bị hạn chế\" trước, rồi mới gạt bật được.", "⚠ To enable \"BF-STT Paste\": open App info → ⋮ → \"Allow restricted settings\" first, then you can toggle it on." },
            ["g_step_a11y_direct"] = new[] { "Bật Trợ năng \"BF-STT Paste\" trong Cài đặt → Trợ năng (nếu muốn tự dán).", "Enable \"BF-STT Paste\" in Settings → Accessibility (if you want auto-paste)." },
            ["g_step_battery"] = new[] { "Bật \"Tắt tối ưu pin\" và \"Tự khởi động\" để icon nổi không bị tắt.", "Enable \"Disable battery optimization\" and \"Autostart\" so the bubble isn't killed." },
            ["g_quick_title"] = new[] { "Bắt đầu nhanh", "Quick start" },
            ["g_quick_1"] = new[] { "Chọn nhà cung cấp (Deepgram / OpenAI / ElevenLabs) và dán API key tương ứng, rồi bấm \"Lưu cài đặt\".", "Choose a provider (Deepgram / OpenAI / ElevenLabs), paste its API key, then tap \"Save settings\"." },
            ["g_quick_2"] = new[] { "Chọn ngôn ngữ nhận dạng (hoặc Tự động nhận diện).", "Choose the recognition language (or Auto detect)." },
            ["g_quick_3"] = new[] { "Bấm \"Bật icon nổi\" và cấp quyền \"Hiển thị trên ứng dụng khác\". Một icon micro sẽ nổi trên màn hình.", "Tap \"Turn on bubble\" and grant \"Display over other apps\". A floating mic appears." },
            ["g_quick_4"] = new[] { "Mở ứng dụng bất kỳ có ô nhập chữ, chạm icon nổi để thu âm, chạm lần nữa để dừng và điền/sao chép.", "Open any app with a text field, tap the bubble to record, tap again to stop and paste/copy." },
            ["g_quick_5"] = new[] { "(Tuỳ chọn) Bật \"Dán tự động\" + Trợ năng \"BF-STT Paste\" để chữ tự điền vào ô đang chọn.", "(Optional) Enable \"Auto-paste\" + the \"BF-STT Paste\" service to type straight into the focused field." },
            ["g_gestures_title"] = new[] { "Thao tác với icon nổi", "Bubble gestures" },
            ["g_gesture_1"] = new[] { "Chạm 1 lần: bắt đầu thu âm.", "Single tap: start recording." },
            ["g_gesture_2"] = new[] { "Chạm lần nữa: dừng, nhận dạng, rồi dán hoặc sao chép văn bản.", "Tap again: stop, transcribe, then paste or copy." },
            ["g_gesture_3"] = new[] { "Giữ (nhấn lâu): mở màn hình cài đặt.", "Long-press: open the settings screen." },
            ["g_gesture_4"] = new[] { "Kéo: di chuyển icon tới vị trí khác.", "Drag: move the icon around." },
            ["g_gesture_5"] = new[] { "Màu icon — Xanh: đang rảnh · Đỏ: đang thu · Cam: đang xử lý.", "Icon color — Blue: idle · Red: recording · Orange: processing." },
            ["g_perms_title"] = new[] { "Các quyền cần cấp", "Permissions to grant" },
            ["g_perm_1"] = new[] { "Micro: bắt buộc, để thu âm.", "Microphone: required, to record." },
            ["g_perm_2"] = new[] { "Hiển thị trên ứng dụng khác: để icon nổi hiện trên mọi app.", "Display over other apps: for the floating bubble." },
            ["g_perm_3"] = new[] { "Thông báo: Android 13+ sẽ hỏi khi mở app (để hiện thông báo dịch vụ đang chạy).", "Notifications: Android 13+ asks on first launch (for the running-service notification)." },
            ["g_perm_4"] = new[] { "Trợ năng \"BF-STT Paste\": tuỳ chọn, để tự dán chữ vào ô đang chọn.", "\"BF-STT Paste\" Accessibility: optional, to auto-paste into the focused field." },
            ["g_perm_5"] = new[] { "Tắt tối ưu pin + Tự khởi động (Autostart): để icon nổi không bị hệ thống tắt.", "Disable battery optimization + Autostart: so the bubble isn't killed." },
            ["g_perm_btn_overlay"] = new[] { "Mở quyền Hiển thị trên ứng dụng khác", "Open 'Display over other apps'" },
            ["g_perm_btn_a11y"] = new[] { "Mở cài đặt Trợ năng (BF-STT Paste)", "Open Accessibility (BF-STT Paste)" },
            ["g_perm_btn_appdetails"] = new[] { "Mở cài đặt ứng dụng (pin / Autostart)", "Open App info (battery / Autostart)" },
            ["g_appdetails_toast"] = new[] { "Tìm mục Pin và Tự khởi động (Autostart) trong đây.", "Find Battery and Autostart in here." },
            ["g_overlay_fail"] = new[] { "Không mở được cài đặt.", "Couldn't open settings." },
            ["g_versions_title"] = new[] { "Hướng dẫn theo phiên bản Android", "Guide by Android version" },
            ["g_v89_title"] = new[] { "Android 8 – 9 (API 26–28)", "Android 8 – 9 (API 26–28)" },
            ["g_v89_1"] = new[] { "Cấp quyền Micro và \"Hiển thị trên ứng dụng khác\".", "Grant Microphone and \"Display over other apps\"." },
            ["g_v89_2"] = new[] { "Bật Trợ năng trực tiếp trong Cài đặt → Trợ năng → BF-STT Paste.", "Enable Accessibility directly in Settings → Accessibility → BF-STT Paste." },
            ["g_v89_3"] = new[] { "Icon nổi chạy nền khá ổn định trên bản Android gốc.", "The bubble runs reliably in the background on stock Android." },
            ["g_v1012_title"] = new[] { "Android 10 – 12 (API 29–31)", "Android 10 – 12 (API 29–31)" },
            ["g_v1012_1"] = new[] { "Có thêm hạn chế chạy nền: nên bật \"Tắt tối ưu pin\" và \"Tự khởi động\" để icon nổi không bị tắt.", "More background limits: enable \"Disable battery optimization\" and \"Autostart\" so the bubble isn't killed." },
            ["g_v1012_2"] = new[] { "Khi thu âm, hệ thống hiện chấm/biểu tượng micro ở thanh trạng thái — đó là bình thường.", "While recording, the system shows a mic dot/indicator — that's normal." },
            ["g_v13_title"] = new[] { "Android 13 (API 33)", "Android 13 (API 33)" },
            ["g_v13_1"] = new[] { "App sẽ hỏi quyền Thông báo khi mở lần đầu — hãy Cho phép.", "The app asks for Notifications on first launch — allow it." },
            ["g_v13_2"] = new[] { "⚠ App cài ngoài (sideload) muốn bật Trợ năng phải vào Cài đặt ứng dụng → ⋮ → \"Cho phép cài đặt bị hạn chế\" TRƯỚC, rồi mới gạt bật được BF-STT Paste.", "⚠ Sideloaded apps must first open App info → ⋮ → \"Allow restricted settings\" BEFORE the BF-STT Paste toggle can be turned on." },
            ["g_v14_title"] = new[] { "Android 14 (API 34)", "Android 14 (API 34)" },
            ["g_v14_1"] = new[] { "Dịch vụ nền dùng loại \"specialUse\" + \"microphone\"; icon nổi chạy như một trợ lý hệ thống.", "The service uses \"specialUse\" + \"microphone\" types; the bubble runs like a system helper." },
            ["g_v14_2"] = new[] { "⚠ Vẫn cần bước \"Cho phép cài đặt bị hạn chế\" trước khi bật Trợ năng (như Android 13).", "⚠ Still needs \"Allow restricted settings\" before enabling Accessibility (like Android 13)." },
            ["g_v15_title"] = new[] { "Android 15 trở lên (API 35+)", "Android 15+ (API 35+)" },
            ["g_v15_1"] = new[] { "Tương tự Android 14.", "Same as Android 14." },
            ["g_v15_2"] = new[] { "Nếu icon nổi bị tắt sau một thời gian, kiểm tra lại \"Tắt tối ưu pin\" và \"Tự khởi động\".", "If the bubble stops after a while, re-check \"Disable battery optimization\" and \"Autostart\"." },
            ["g_oem_title"] = new[] { "Xiaomi / HyperOS · Oppo · Vivo · Huawei", "Xiaomi / HyperOS · Oppo · Vivo · Huawei" },
            ["g_oem_1"] = new[] { "Bắt buộc bật \"Tự khởi động (Autostart)\" và khoá app trong màn hình Recents (đa nhiệm).", "Must enable \"Autostart\" and lock the app in Recents." },
            ["g_oem_2"] = new[] { "Đặt chế độ pin cho app là \"Không giới hạn\" để dịch vụ không bị kill.", "Set the app's battery mode to \"Unrestricted\" so the service isn't killed." },
            ["g_oem_3"] = new[] { "Các hãng này cũng yêu cầu \"Cho phép cài đặt bị hạn chế\" cho Trợ năng.", "These brands also require \"Allow restricted settings\" for Accessibility." },
            ["g_current_marker"] = new[] { "   ← máy của bạn", "   ← your device" },
            ["g_trouble_title"] = new[] { "Xử lý sự cố", "Troubleshooting" },
            ["g_trouble_1"] = new[] { "Không thu được âm: kiểm tra quyền Micro đã bật chưa.", "No audio: check the Microphone permission." },
            ["g_trouble_2"] = new[] { "Không ra chữ: kiểm tra API key đúng nhà cung cấp đang chọn, và thiết bị có mạng.", "No text: check the API key matches the selected provider, and that you have internet." },
            ["g_trouble_3"] = new[] { "Không tự dán: bật Trợ năng \"BF-STT Paste\" (và \"Cho phép cài đặt bị hạn chế\" ở Android 13+). Nếu không bật, chữ vẫn được sao chép — chỉ cần dán tay.", "No auto-paste: enable \"BF-STT Paste\" (and \"Allow restricted settings\" on Android 13+). Otherwise text is still copied — just paste manually." },
            ["g_trouble_4"] = new[] { "Icon nổi biến mất sau khi khoá máy / vuốt app khỏi Recents: bật \"Tắt tối ưu pin\" + \"Tự khởi động\", rồi khoá app trong Recents.", "Bubble disappears after locking the screen / swiping the app away: enable \"Disable battery optimization\" + \"Autostart\", then lock the app in Recents." },
            ["g_trouble_5"] = new[] { "Chú ý thông báo của icon nổi: \"Đã dán\" = đã điền vào ô; \"Đã copy (bật Trợ năng…)\" = chưa bật Trợ năng; \"Đã copy\" = đã tắt dán tự động hoặc chưa chọn ô nhập.", "Watch the bubble's toast: \"Pasted\" = inserted; \"Copied (enable Accessibility…)\" = service off; \"Copied\" = auto-paste off or no field focused." },
        };
    }
}
