# 02 — dock_mvp

**Status:** in-progress | **Started:** 2026-11-03 | **Ended:** -

## Goal
Dock chạy trên máy user: ẩn taskbar Windows, pinned apps, CPU/RAM, pin, đồng hồ; thoát sạch.

## Scope
- **In:** Electron, không native dep; Win32 qua PowerShell; 4 widget; tray thoát.
- **Out:** cửa sổ đang mở, Now Playing, theme, widget picker, autostart, đa màn hình.

## Tasks
T-004, T-005, T-006

## Work log
- 2026-11-03 [claude-code] `npm i -D -E electron@45.2.1` (postinstall bị npm chặn, chạy tay `node node_modules/electron/install.js`). Viết main.js, preload.js, index.html, scripts/taskbar.ps1; why: T-004/T-005.
- 2026-11-03 [claude-code] Test lần 1: dock không ở đúng y (Chromium kẹp bounds vào work area) và vùng trong suốt 980px nuốt click; icon .lnk ra icon chung. Sửa: đặt lại vị trí sau khi ẩn taskbar, cửa sổ co đúng cỡ pill qua ResizeObserver + IPC `resize`, icon lấy từ `shell.readShortcutLink().target`. Test lần 2: rect 764,1008-1157,1072, ảnh chụp thấy Explorer + Chrome, CPU 28/RAM 93, pin 100, 21:33 THỨ 3 3 THG 11. Thoát: `taskbar.ps1 show` trả taskbar về (IsWindowVisible=True).
- 2026-11-03 [claude-code] Sửa `[hidden]{display:none!important}` (tile flex đè UA stylesheet nên ô pin 100% đang sạc vẫn hiện). Test thoát mềm (`taskkill` không /F, đi qua `before-quit`): taskbar hiện lại, file work area bị xoá, không còn tiến trình electron; why: xác nhận đường thoát thật của tray, không chỉ script dự phòng.

- 2026-11-04 [claude-code] User: "tiếp tục". `npm i -E koffi@3.3.6`, viết `win32.js` (EnumWindows lọc như taskbar: visible, root owner, không TOOLWINDOW, không cloaked, bỏ Progman/CoreWindow; activate = ALT trick + SetForegroundWindow, toggle minimize; taskbar auto-hide qua ABM_SETSTATE + SW_HIDE; appbar ABM_NEW/QUERYPOS/SETPOS 80px). main.js bỏ PowerShell, dùng win32.js; UI gộp pinned + cửa sổ mở, chấm xanh khi chạy, highlight khi active, chỉ render lại khi danh sách đổi. Test: work area 1000px khi chạy, 1032px sau thoát mềm, taskbar hiện, file trạng thái bị xoá. Lúc bắt đầu test taskbar đang ẩn sẵn (user kill cứng phiên trước), lần chạy này trả lại. `restore-taskbar` giờ gọi win32.js; why: T-006, T-009.
- 2026-11-04 [claude-code] Test kill cứng: ABM_GETSTATE 0 -> 1 khi chạy -> 1 sau kill -> `npm run restore-taskbar` về 0, work area 1032, taskbar hiện, file trạng thái xoá. Test `activate()` từ tiến trình không focus: chuyển sang VS Code thành công, click lần 2 thu nhỏ; why: hai đường mã chưa từng chạy.
- 2026-11-04 [claude-code] Xoá `scripts/taskbar.ps1` (và thư mục `scripts/` rỗng), user xác nhận "Có xoá đi" ngày 2026-11-04; không file nào còn gọi nó. Đo bản Electron chạy 20 s: 4 tiến trình, working set 329 MB, private 211 MB, CPU 0,16 s / 20 s, đĩa 357 MB. Tìm thấy `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` để build exe native không cần cài gì; why: user hỏi bản exe có nhẹ hơn không.
- 2026-11-04 [claude-code] User: "Ok viết lại bằng c# đi". Viết `native/Lilbar.cs` (C# 5 vì csc có sẵn chỉ hỗ trợ tới đó) + `build.cmd`. Kính: ACCENT_ENABLE_ACRYLICBLURBEHIND, tint 0x55181414, DWM bo góc + dark mode; vẽ GDI+ vào bitmap 32bppPArgb, đưa lên bằng SetDIBitsToDevice để giữ alpha. Self-check 8/8 ok. Lỗi gặp: icon .lnk có mũi tên (đổi sang icon của exe đích); thoát mềm không khôi phục taskbar vì vòng lặp kết thúc không qua FormClosing (thêm `finally { Taskbar.Restore(); }` quanh Application.Run, Restore idempotent). Test: WM_CLOSE, taskkill mềm, kill cứng + `--restore` đều trả state 0, work area 1032, taskbar hiện; click ô app thứ 4 chuyển foreground đúng. Đo 20 s: 1 tiến trình, 41,9 MB working set, 26 MB private, CPU 0; why: T-012.
- 2026-11-04 [claude-code] User: "từ giờ hãy update repo lên đây nhé". `git init` trên nền commit "Initial commit" của remote (giữ LICENSE Apache 2.0), gộp `.gitignore` (bỏ qua node_modules, native/*.exe, lumabar/), bỏ tên user và tên app cá nhân khỏi tài liệu vì repo công khai, viết README, push lên main; why: T-014.
- 2026-11-04 [claude-code] User: phím Windows bật thanh cũ, muốn thay hẳn; cao bằng taskbar gốc; hiệu ứng clear; widget usage Claude Code, dùng REA. REA `open_binary` lumabar.exe (sha256 8a9426aa…) được, `search_strings` không chạy vì thiếu Ghidra (không cài thêm); đọc byte quanh chuỗi: lumabar đọc `.claude/.credentials.json`, gọi `/api/oauth/usage` với `anthropic-beta: oauth-2025-04-20`, tôn trọng `retry-after`. Gọi thử bằng token của user (không in token) xác nhận dạng `five_hour`/`seven_day`. Bỏ auto-hide (nguyên nhân thanh cũ hiện khi nhấn Win), thay bằng SW_HIDE + SetWinEventHook EVENT_OBJECT_SHOW trên explorer + TaskbarCreated. Test: Win mở Start, `IsWindowVisible(Shell_TrayWnd)` vẫn false; work area 1032 không đổi; thoát mềm và kill cứng + `--restore` đều trả taskbar. Self-check 11/11. RAM 51,6 MB working set, 31,6 MB private, CPU 0; why: T-015, T-016, T-017.
- 2026-11-04 [claude-code] User: màn hình 2, mũi tên khay, ô ngày/thời tiết, ô thư mục, menu chuột phải + cài đặt; sau đó hỏi thêm làm 103 widget giống hệt lumabar. Hỏi advisor: làm 6 tính năng, không làm phần "giống hệt" (bản quyền, repo công khai), hỏi user chọn widget. Đọc ManagedShell (Apache-2.0) để port tray host. Xem 4 ảnh tile lumabar trong scratchpad để tham khảo bố cục ô Hôm nay (không đưa vào repo). Tách code thành 6 file. Lỗi: thứ tự khởi tạo static trong Config (sửa). Self-check 14/14. Test: FindWindow(Shell_TrayWnd) trả cửa sổ của Lilbar; icon khay (TranslucentTB, loa, UniKey, Discord, Tailscale) hiện cạnh mũi tên; popup mũi tên hiện 5 icon ẩn; ô thư mục rỗng mở Cài đặt tab Thư mục; tab Thanh và Khay đúng; phím Win không hiện thanh cũ; thoát: explorer lại sở hữu Shell_TrayWnd, icon UniKey/Discord/Tailscale về taskbar gốc, work area 1032. RAM 61,9 MB working set, 37,7 MB private, CPU 0,03 s/15 s. Màn hình 2 chưa test (chỉ có 1). Menu chuột phải chưa chụp được vì mở theo vị trí chuột thật; why: T-019..T-022.

## Decisions
| Decision | Why | Alternatives rejected |
|----------|-----|-----------------------|
| Win32 qua PowerShell `Add-Type` (v1) | không cần compiler/koffi | koffi (thêm dep), node-gyp (cần MSVC) |
| Chuyển sang koffi (2026-11-04) | EnumWindows cần callback trong tiến trình; PowerShell không làm được | Tauri (cài Rust vài GB) |
| Taskbar auto-hide thay vì SPI_SETWORKAREA | explorer giữ chỗ appbar 48px nên SETWORKAREA bị ghi đè | ABM_REMOVE taskbar của explorer (không được phép) |
| Cửa sổ co đúng cỡ pill | vùng trong suốt của Electron vẫn chặn click | `setIgnoreMouseEvents` + theo dõi chuột (phức tạp hơn) |
| Tray icon = icon electron.exe | chưa có asset | vẽ PNG bằng tay |

## Outputs
`main.js`, `preload.js`, `index.html`, `scripts/taskbar.ps1`, `package.json` (`npm start`, `npm run restore-taskbar`)

## Issues & risks
- Dock bị kill cứng thì taskbar vẫn ẩn: chạy `npm run restore-taskbar` hoặc restart explorer.exe.
- (đã xử lý 2026-11-04) Work area giờ là 1000px nhờ appbar thật.
- Dock bật auto-hide taskbar của Windows: kill cứng sẽ để lại setting đó cho đến khi `npm run restore-taskbar`.
- Trong lúc test, user đang dùng máy: mỗi lần test phải restore ngay.

## Exit criteria / Next phase
- Done when: T-006 (cửa sổ đang mở) xong và user dùng dock hằng ngày không phải restore tay.
- Next: 03_widgets_and_themes
