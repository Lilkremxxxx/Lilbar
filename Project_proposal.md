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
**Bản chính (từ 2026-11-04): `native/Lilbar.cs`**, một file C# 5, build bằng `native/build.cmd` (csc.exe có sẵn trong Windows) ra `native/Lilbar.exe` 27 KB, 1 tiến trình.
- Lớp `N`: P/Invoke user32/kernel32/dwmapi/shell32/gdi32.
- `Taskbar`: auto-hide + ẩn taskbar Windows, appbar 72px cạnh dưới, khôi phục; dùng chung `%TEMP%\lilbar-taskbar-state.json` với bản Electron.
- `Sys`: EnumWindows, chuyển cửa sổ, app ghim (WScript.Shell đọc .lnk), CPU (GetSystemTimes), RAM (GlobalMemoryStatusEx), icon (SHGetFileInfo, có cache).
- `Dock` (WinForms Form): kính acrylic qua `SetWindowCompositionAttribute`, bo góc qua DWM, vẽ GDI+ vào bitmap ARGB rồi `SetDIBitsToDevice` để giữ alpha; timer 1 s, chỉ vẽ lại khi dữ liệu đổi.
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
