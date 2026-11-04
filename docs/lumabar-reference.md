# lumabar — bản đồ tham chiếu (từ REA + website)

_Ngày: 2026-11-03. Nguồn: lumabar-1.2.11-64-bit.msi (SHA256 exe 8a9426aa...638cc), lumabar.xyz, lumabar.xyz/widgets, lumabar.xyz/docs._
_Mục đích: SPEC để tự viết lại. KHÔNG sao chép mã/asset của lumabar (phần mềm thương mại, đóng nguồn)._

## Kiến trúc (bằng chứng từ file cài)
- MSI chứa đúng 2 file: `lumabar.exe` (24,9 MB) + `WebView2Loader.dll`.
- **Tauri 2** (Rust + WebView2): chuỗi `__TAURI` x55, `wry`, `muda`, plugin `tauri-plugin-updater`, `tauri-plugin-notification`, `tauri-plugin-log`. Frontend HTML/JS nhúng trong exe, nén brotli.
- Updater đọc `github.com/Acadbek/lumabar/releases/latest/download/latest.json` (repo công khai chỉ là kênh phát hành).
- Không có `.dep-v0`, đường dẫn crate bị strip nên không liệt kê được version crate.

## Win32 / WinRT lumabar dùng (REA `inspect-pe-imports` + quét chuỗi)
| Việc | API |
|---|---|
| Ẩn taskbar / neo dock vào cạnh màn hình | `SHAppBarMessage`, `SetWindowPos`, `FindWindow`, `SystemParametersInfo` |
| Danh sách cửa sổ đang mở, theo dõi focus | `EnumWindows`, `SetWinEventHook`, `RegisterShellHookWindow`, `SetWindowsHookEx` |
| Hiệu ứng kính / trong suốt | `DwmSetWindowAttribute`, `DwmEnableBlurBehindWindow`, `UpdateLayeredWindow`, `DwmRegisterThumbnail`, `BitBlt`/`CreateDIBSection` (chụp nền để "bẻ cong" live) |
| Tray icon, hotkey | `Shell_NotifyIcon`, `RegisterHotKey`, `RegisterRawInputDevices` |
| Now playing | WinRT `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager` |
| Pin, Wi-Fi/BT/hotspot | `GetSystemPowerStatus`, `Windows.System.Power.PowerManager`, `Windows.Devices.Radios.Radio`, `NetworkOperatorTetheringManager` |
| CPU/RAM/mạng | `GetSystemTimes`, `GlobalMemoryStatusEx`, `PdhCollectQueryData`, `GetIfTable` |
| Recycle bin, màn hình, bàn phím | `SHQueryRecycleBin`, `SHEmptyRecycleBin`, `EnumDisplayMonitors`, `GetKeyboardLayout` |
| Thông báo, chạy khi khởi động | `Windows.UI.Notifications.ToastNotification`, `Windows.ApplicationModel.StartupTask` |
| OCR (chụp màn hình) | `Windows.Media.Ocr.OcrEngine` |

## API bên ngoài (từ chuỗi URL trong exe)
open-meteo (thời tiết + geocoding), ipinfo.io (vị trí), frankfurter.dev / open.er-api (tỷ giá), finnhub + yahoo finance (chứng khoán), unsplash, github (REST + GraphQL + device OAuth), gmail/youtube/searchconsole (Google OAuth), x.com OAuth, instagram/tiktok (scrape), leetcode GraphQL, stripe/polar/dodo/shopify/lemonsqueezy/paddle/gumroad, plausible/datafast/dub, resend/linear/supabase, api.anthropic.com/api/oauth/usage (Claude), chatgpt.com backend usage, cursor.com usage, grok billing, deepseek balance, z.ai (GLM), cloudcode-pa.googleapis.com (Gemini).

## Giao diện (website)
- Dock ở cạnh dưới (mặc định) hoặc trên; kéo bằng vùng trống để chuyển cạnh; dạng "pill" hoặc full-width.
- 5 style: Black, White, Liquid Glass, iOS Glass, Midnight. 3 cỡ: S/M/L. Đồng hồ 12/24h.
- Chuột phải dock -> "Widgets..." (chọn theo nhóm / tìm kiếm, click để thêm, badge trừ để bỏ). Kéo widget để sắp xếp.
- Ẩn khi game/video fullscreen; ẩn taskbar Windows trên mọi màn hình có dock; thoát app thì taskbar trở lại.
- Settings: Appearance, Dock size, Clock, Launch at startup, Update, Feedback. Không có hotkey công khai.

## 103 widget / 13 nhóm (tên gốc)
- **AI (10):** Claude Usage, Claude Plan Usage, ChatGPT Usage, Gemini Usage, Grok Usage, DeepSeek Balance, GLM Usage, OpenCode Usage, OpenCode Plan Usage, Cursor Usage.
- **Productivity (9):** Day Timeline, Screen Time, Pomodoro, Timer, Stopwatch, Todos, Notes, Calendar, Currency.
- **Finance (15):** Total Revenue; Stripe/Polar/Dodo/Shopify/Lemon Squeezy/Paddle/Gumroad, mỗi cái có bản số và bản Chart 14 ngày.
- **System (14):** System Stats (2 ring CPU/RAM), Tray, Language, Temperature, CPU, RAM, Network, Battery, Quick Toggles, Brightness, Theme Toggle, Month/Day/Year Progress.
- **Media & Audio (5):** Now Playing, Audio Output, Now Playing (compact), Now Playing Mini, Now Playing Bar.
- **Analytics (3):** DataFast Chart, Plausible, Dub.
- **Developer (3):** Resend, Linear, Supabase.
- **Online & Mail (7):** Metric, Wallpaper Gallery, Gmail, GitHub Activity, GitHub, GitHub Overview, LeetCode Stats.
- **Social (4):** X Profile, TikTok, YouTube, Instagram.
- **Apps & Files (9):** Apps (Start + pinned + cửa sổ mở), Recently Opened, Recycle Bin, Search, Commands, Storage, Recent Screenshots, Downloads, Screenshot Capture.
- **Weather (7):** Weather, Weather Details, Forecast, Today & Forecast, Weather Status, Hourly, Sunrise & Sunset.
- **Stocks (3):** Stock, Stock Chart, Stock Watchlist.
- **Clocks (14):** Clock, Flip Clock, World Clocks, Analog Classic, Analog Classic Dark, 7 Days, World Clock, World Time, Today, Date, Day Number, Analog Minimal, Segment Clock, Clock & Weather.

## Website lumabar.xyz (kiểm tra 2026-11-03)
Site tĩnh (Vite + Tailwind, `styles-BDHK4TNW.css`, `index-CLn_IvYJ.js`), 207 ảnh webp chụp từng widget ở light/dark, không có dock chạy bằng HTML. Không có gì để REA ngoài ảnh. Thiết kế lấy từ ảnh: pill tối `rgba(18,18,20,.9)`, viền hairline, bo 18px; tile cao 44px, bo 12px, nền trắng 7%; chữ Segoe UI; ring CPU/RAM xanh `#4a9bff`; ô pin xanh `#2f8bff`.

## Cách lấy thêm bằng chứng (nếu cần, user tự chạy)
Chạy lumabar với `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` rồi dùng REA `list-browser-targets` / `inspect-web-page` để xem DOM + CSS thật. Chỉ để học bố cục, không copy.
