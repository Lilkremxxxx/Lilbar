# Project Proposal — Lilbar

_Last updated: 2026-11-04 by claude-code_

## Overview
- **Topic:** Thanh taskbar/dock thay thế cho Windows 11, có widget, lấy cảm hứng từ lumabar (lumabar.xyz).
- **Goal:** Một dock riêng (Lilbar) neo cạnh màn hình, ẩn taskbar Windows, có vài widget sống; tự viết, không sao chép lumabar.
- **One-liner:** "lumabar tự làm": dock + widget cho Windows.

## Scope
- **In scope (v1):** 1 cửa sổ dock không viền, always-on-top, neo cạnh dưới; ẩn/khôi phục taskbar Windows (appbar); 3 widget: Clock, CPU/RAM, Now Playing; tray icon để thoát.
- **Out of scope (v1):** 103 widget, 5 theme, Liquid Glass refraction, license/updater, bản 32-bit, widget cần OAuth/API key. Backlog lấy từ `docs/lumabar-reference.md`.

## Data
| Source | Location / path | Format / schema | Size | Sensitivity |
|--------|-----------------|-----------------|------|-------------|
| lumabar MSI 1.2.11 (đã trích) | scratchpad Claude (tạm, sẽ mất) | PE + WebView2Loader.dll | 25 MB | public, thương mại: chỉ đọc để làm spec |
| Bản đồ tính năng lumabar | `docs/lumabar-reference.md` | Markdown | - | public |
| Repo release của lumabar | `lumabar/` (clone, chỉ README + ảnh) | git | - | public, không phải của ta |

## Pipeline / Architecture
**Bản chính (từ 2026-11-04): `native/*.cs`**, C# 5, build bằng `native/build.cmd` (csc.exe có sẵn trong Windows) ra `native/Lilbar.exe`, 1 tiến trình.
- `Lilbar.cs`: P/Invoke, `Taskbar`, `Claude`, `Sys`, `Dock` (mỗi màn hình một dock, ô theo `Config.Items`), `Manager` (vòng đời, timer, dựng lại dock khi đổi màn hình), `Program`.
- `TrayHost.cs`: khay hệ thống. Cửa sổ `Shell_TrayWnd` riêng giữ trên explorer trong z-order để nhận `Shell_NotifyIcon` (WM_COPYDATA), chuyển tiếp mọi thứ cho explorer, gửi click theo NOTIFYICON_VERSION. Ghim cạnh mũi tên mặc định theo `HKCU\Control Panel\NotifyIconSettings\*\IsPromoted`. Port từ ManagedShell (Apache-2.0).
- `Services.cs`: `Config` (`%APPDATA%\Lilbar\config.json`), `Weather` (ipinfo.io + open-meteo, 15 phút), `Model` (dữ liệu chung, chữ ký để chỉ vẽ lại khi đổi).
- `Widgets.cs`: dữ liệu widget. WinRT (đang phát `GlobalSystemMediaTransportControls`, radio Wi-Fi/Bluetooth) gọi bằng cách chờ `IAsyncOperation.Status` vì csc có sẵn không có cầu nối await; luồng nền `Bg` mỗi giây. Âm lượng qua Core Audio COM, mạng theo card có default gateway, nhiệt độ từ bộ đếm "Thermal Zone Information" (cảm biến ACPI, không phải lõi CPU), hẹn giờ, pomodoro, GitHub (token cá nhân mã hoá DPAPI), danh sách thành phố.
- `DockWidgets.cs`: phần vẽ các widget trên (Dock là partial class) và `TodoPopup` (cửa sổ đặc để ô nhập chữ hoạt động).
- `Popup.cs`: popup kính cho mũi tên khay, thư mục, menu chuột phải. `Settings.cs`: cửa sổ cài đặt (Thanh, Ứng dụng, Khay hệ thống, Thư mục). `Glass.cs`: hàm vẽ chung.
- Phần mô tả lớp bên dưới là của lần viết trước, vẫn đúng cho các lớp còn giữ.
- Lớp `N`: P/Invoke user32/kernel32/dwmapi/shell32/gdi32.
- `Taskbar`: ẩn taskbar Windows (không bật auto-hide), `SetWinEventHook` trên tiến trình explorer ẩn lại ngay khi nó hiện (phím Windows, đổi màn hình), xử lý `TaskbarCreated` khi explorer khởi động lại. Dock nằm đúng dải 48px explorer vẫn giữ chỗ; chỉ đăng ký appbar riêng nếu taskbar gốc của user là auto-hide. Vẫn đọc `%TEMP%\lilbar-taskbar-state.json` của bản cũ để dọn.
- `Claude`: widget usage Claude Code. Đọc `~/.claude/.credentials.json` (hoặc `CLAUDE_CONFIG_DIR`) lấy `claudeAiOauth.accessToken`, gọi `GET https://api.anthropic.com/api/oauth/usage` (header `anthropic-beta: oauth-2025-04-20`) tối đa 5 phút/lần trên thread pool, tôn trọng `retry-after` khi 429. Hiện `five_hour` và `seven_day` (`utilization`, `resets_at`). Cách gọi lấy từ phân tích lumabar.exe bằng REA + quét byte.
- `Sys`: EnumWindows, chuyển cửa sổ, app ghim (WScript.Shell đọc .lnk), CPU (GetSystemTimes), RAM (GlobalMemoryStatusEx), icon (SHGetFileInfo, có cache).
- `Dock` (WinForms Form): cao đúng bằng taskbar gốc, nền trong suốt (accent TRANSPARENTGRADIENT, không blur), ô kính tối có viền sáng, vẽ GDI+ vào bitmap ARGB rồi `SetDIBitsToDevice` để giữ alpha; timer 1 s, chỉ vẽ lại khi dữ liệu đổi.
- `Lilbar.exe --restore` trả taskbar sau kill cứng; `--selftest` kiểm tra Win32.

**Bản Electron cũ** (giữ tới khi user xác nhận bỏ):
1. `main.js` — Electron main: tạo cửa sổ dock (frameless, transparent, always-on-top, không nhận focus), tray icon, IPC `stats`/`apps`/`windows`/`launch`/`activate`/`resize`.
2. `win32.js` — koffi gọi user32/shell32/dwmapi: `EnumWindows` (danh sách cửa sổ như taskbar), `SetForegroundWindow` (chuyển/thu nhỏ), `SHAppBarMessage` (đặt taskbar Windows auto-hide + ẩn, đăng ký appbar 80px cạnh dưới). Lưu trạng thái taskbar gốc ở `%TEMP%\lilbar-taskbar-state.json` dạng `{"state": <int>}`.
3. `preload.js` — contextBridge `window.lilbar`.
4. `index.html` — UI pill + widget (Apps = pinned + cửa sổ mở, CPU/RAM, Battery, Clock), CSS theo style "Black" của lumabar.
```
os.cpus/freemem, .lnk pinned, getFileIcon, win32.listWindows -> main.js -> IPC -> index.html (pill)   |   main.js -> win32.js -> Win32
```

## Tech stack
Bản chính: C# 5 + WinForms trên .NET Framework 4.8 có sẵn trong Windows, không dependency. Bản cũ: Electron 45.2.1 + koffi 3.3.6, Node 24 hệ thống. **Giả định:** chọn Electron vì máy chưa có Rust/MSVC; UI (index.html) giữ nguyên nếu sau này chuyển sang Tauri, chỉ thay main.js + taskbar.ps1 bằng Rust.
Công cụ phân tích: REA 6.8.0 (`rea` CLI + MCP server, Node 24.22.2 qua nvm).

## How to run
```
D:\Lilbar\native\build.cmd           # build (vài giây)
D:\Lilbar\native\Lilbar.exe          # chạy; thoát bằng chuột phải icon tray -> "Thoát Lilbar"
D:\Lilbar\native\Lilbar.exe --restore   # nếu dock chết mà taskbar chưa về
D:\Lilbar\native\Lilbar.exe --selftest  # in kết quả kiểm tra, exit code = số lỗi

# Bản Electron cũ:
cd D:\Lilbar
npm install          # lần đầu (tải Electron ~250 MB)
# npm 11 chặn postinstall: nếu thiếu node_modules\electron\dist thì chạy  node node_modules/electron/install.js
npm start            # dock hiện ở cạnh dưới, taskbar Windows ẩn
# Thoát: chuột phải icon tray -> "Thoát Lilbar". Nếu dock chết mà taskbar chưa về:
npm run restore-taskbar
PATH="$LOCALAPPDATA/nvm/v24.22.2:$PATH" rea doctor --client claude_code   # kiểm tra REA
```

## Status & current phase
Current phase: 02_dock_mvp — see `Process_phases/README.md`.

## Open questions / Challenges
- **Stack:** đã chọn Electron (giả định, xem Tech stack). Muốn Tauri thì cài Rust + VS Build Tools C++ rồi port backend.
- **Taskbar auto-hide:** dock bật "tự động ẩn taskbar" của Windows để bỏ chỗ giữ của explorer, thoát thì trả lại trạng thái cũ. Nếu dock bị kill cứng, `npm run restore-taskbar` đọc file trạng thái; mất file thì mặc định tắt auto-hide.
- **Widget bị ẩn tạm thời:** Now Playing cần WinRT (koffi không gọi WinRT trực tiếp; cần tìm đường khác).
- **Hiệu năng (đo 2026-11-04):** bản Electron chiếm 4 tiến trình, ~330 MB RAM (211 MB private), 357 MB đĩa; CPU gần 0. User ưu tiên ít tài nguyên nhất + glassmorphism. Đã viết lại bằng C# (user đồng ý 2026-11-04): 1 tiến trình, ~42 MB working set, 26 MB private, CPU 0 trong 20 s, exe 27 KB.
- **103 widget "giống hệt" lumabar (user hỏi 2026-11-04):** không trích frontend hay sao chép thiết kế của sản phẩm thương mại vào repo công khai; khoảng 60 widget cần tài khoản/API key (Stripe, Shopify, X…). Đề xuất làm cùng chức năng, thiết kế riêng, theo nhóm user thực sự dùng. Chờ user chọn.
- **Màn hình thứ hai:** đã viết (mỗi màn hình một dock, ẩn `Shell_SecondaryTrayWnd`, dựng lại khi đổi màn hình) nhưng máy chỉ có 1 màn hình nên chưa kiểm chứng.
- **Bản Electron:** còn giữ (`main.js`, `win32.js`, `preload.js`, `index.html`, `node_modules/`). Bỏ khi user xác nhận.
- **Ponytail ultra pushback:** "giống lumabar" = 103 widget + 5 theme + glass live là hàng tháng công; v1 chỉ nên 1 dock + 3 widget, thêm widget theo nhu cầu thật.
- **Pháp lý:** lumabar đóng nguồn, có license. Chỉ dùng bằng chứng REA làm spec; không trích JS/CSS/ảnh của họ, không đụng license/updater.
- Clone `lumabar/` trong thư mục dự án chỉ để tham khảo; có cần giữ không?

## Changelog
| Date | Agent | Area (topic/scope/data/pipeline) | Change | Why |
|------|-------|----------------------------------|--------|-----|
| 2026-11-03 | claude-code | all | Initial proposal từ yêu cầu user + phân tích REA | bang-skill bootstrap |
| 2026-11-03 | claude-code | pipeline | Chọn Electron, dựng dock v1 (Apps, CPU/RAM, Battery, Clock) chạy được trên máy | user: "làm taskbar ngay trên máy tôi"; Rust chưa cài |
| 2026-11-04 | claude-code | pipeline | Thêm koffi + `win32.js`: cửa sổ đang mở, chuyển cửa sổ, appbar thật; `taskbar.ps1` không còn dùng | user: "tiếp tục"; T-006/T-009 |
| 2026-11-04 | claude-code | pipeline | Viết lại thành `native/Lilbar.exe` C# + kính acrylic; Electron thành bản cũ | user: "Ok viết lại bằng c# đi"; ưu tiên ít tài nguyên + glassmorphism |
| 2026-11-04 | claude-code | data | Mã nguồn công khai tại https://github.com/Lilkremxxxx/Lilbar; exe và clone lumabar không đưa lên | user yêu cầu cập nhật repo từ giờ |
| 2026-11-04 | claude-code | pipeline + data | Thay hẳn taskbar (hook giữ ẩn, bỏ auto-hide), cao 48px, nền trong suốt, widget Claude Code đọc token Claude Code và gọi API usage của Anthropic | user: phím Windows bật thanh cũ; muốn fit chiều cao, hiệu ứng clear, widget usage Claude |
| 2026-11-04 | claude-code | pipeline + data | Thêm 11 widget và tab Tiện ích; build tham chiếu WinMetadata của Windows; dữ liệu mới trong config: Cities, TimerMinutes, FocusMin/BreakMin/LongBreakMin, Todos, GitHubToken (DPAPI); GitHub gửi token tới api.github.com | user: "làm những thứ bạn đề xuất" |
| 2026-11-04 | claude-code | pipeline + data | Tách thành nhiều file; thêm khay hệ thống (tray host), ô Hôm nay & thời tiết (gửi IP tới ipinfo.io), ô thư mục, menu chuột phải, cửa sổ cài đặt, file cấu hình, dock cho mọi màn hình | user yêu cầu 6 tính năng; 103 widget kiểu lumabar để chờ user chọn |
