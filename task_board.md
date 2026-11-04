# Task Board

_Last updated: 2026-11-04 by claude-code_

## Legend
- Status: `todo` | `doing` | `blocked: <reason>` | `review` | `done` | `cancelled: <reason>`
- Priority: `P0` (urgent) | `P1` | `P2`
- Owner: agent name (`claude-code`, `codex`, `cursor`, ...) or `human`

## Active
| ID | Task | Status | Priority | Owner | Phase | Requested by | Done when | Updated |
|----|------|--------|----------|-------|-------|--------------|-----------|---------|
| T-007 | Theme (White/Glass acrylic), kéo lên cạnh trên, widget picker | todo | P2 | claude-code | backlog | user | - | 2026-11-03 |
| T-008 | Now Playing (WinRT GlobalSystemMediaTransportControls) | blocked: không gọi WinRT từ Node thuần | P2 | claude-code | backlog | user | hiện bài đang phát + play/pause | 2026-11-03 |
| T-013 | Bỏ bản Electron (main.js, win32.js, preload.js, index.html, node_modules, package*.json) | blocked: chờ user xác nhận xoá | P2 | human | 02_dock_mvp | claude-code | user đồng ý, đã xoá | 2026-11-04 |
| T-010 | Ẩn dock khi game/video fullscreen; tự chạy khi khởi động | todo | P2 | claude-code | backlog | claude-code | dock biến mất khi app fullscreen | 2026-11-04 |
| T-024 | Widget độ sáng màn hình | blocked: màn hình không hỗ trợ DDC/CI, WMI không có (máy bàn) | P2 | human | backlog | claude-code | bật DDC/CI trong menu màn hình rồi làm | 2026-11-04 |
| T-025 | Làm lại toàn bộ chức năng widget lumabar, thiết kế riêng: đợt 2 (internet công khai) | doing | P1 | claude-code | 02_dock_mvp | user | chứng khoán, crypto, LeetCode, hình nền, GitHub tổng quan | 2026-11-04 |
| T-026 | Đợt 3: widget cần key (doanh thu, phân tích, lập trình, mạng xã hội, Gmail, DeepSeek, GLM) | todo | P2 | claude-code | 02_dock_mvp | user | có parser + self-check, ghi rõ chưa kiểm chứng | 2026-11-04 |
| T-027 | Widget AI đọc đăng nhập app khác (ChatGPT/Codex, Gemini CLI, Grok, Cursor, OpenCode) | blocked: hệ thống chặn việc đọc file đăng nhập của app khác | P2 | human | backlog | user | user cấp quyền hoặc bỏ | 2026-11-04 |
| T-023 | Kiểm chứng màn hình thứ hai | blocked: máy chỉ có 1 màn hình | P1 | human | 02_dock_mvp | user | cắm màn hình 2, dock hiện và thanh cũ ẩn | 2026-11-04 |
| T-011 | Nhiều cửa sổ cùng app: click pinned đang chạy nên cho chọn cửa sổ (hiện tại lấy cửa sổ đầu) | todo | P2 | claude-code | backlog | claude-code | - | 2026-11-04 |

## Done
| ID | Task | Finished | Result / link |
|----|------|----------|---------------|
| T-000 | Bootstrap project docs (bang-skill) | 2026-11-03 | Project_proposal.md, task_board.md, Process_phases/ |
| T-001 | Cài skill REA (github.com/morluto/rea) cho Claude Code | 2026-11-03 | rea-agents 6.8.0 + MCP `rea` + skill v49 trong ~/.claude/skills; chạy bằng Node 24.22.2 (nvm) |
| T-002 | Phân tích lumabar bằng REA + website | 2026-11-03 | `docs/lumabar-reference.md` |
| T-003 | Chốt stack | 2026-11-03 | Electron (giả định: Rust chưa cài, user muốn chạy ngay) |
| T-004 | Dock v1: neo cạnh dưới, always-on-top, ẩn/khôi phục taskbar, tray thoát | 2026-11-03 | `main.js`, `scripts/taskbar.ps1`; chụp màn hình xác nhận dock hiện và taskbar về sau khi thoát |
| T-005 | Widget Apps (pinned), CPU/RAM, Battery, Clock | 2026-11-03 | `index.html` (Now Playing tách sang T-008) |
| T-006 | Widget Apps: cửa sổ đang mở, click chuyển/thu nhỏ, chấm xanh app đang chạy | 2026-11-04 | `win32.js` (koffi EnumWindows), `index.html` apps() |
| T-009 | Appbar thật (SHAppBarMessage) + taskbar auto-hide thay cho SPI_SETWORKAREA | 2026-11-04 | `win32.js`; work area 1000px khi chạy, 1032px sau khi thoát |
| T-012 | Viết lại dock bằng C# native + glassmorphism (acrylic DWM) | 2026-11-04 | `native/Lilbar.cs`, `native/build.cmd`; 42 MB RAM vs 329 MB Electron |
| T-014 | Đưa dự án lên GitHub, cập nhật repo sau mỗi thay đổi | 2026-11-04 | https://github.com/Lilkremxxxx/Lilbar (main) |
| T-015 | Thay hẳn taskbar: phím Windows không bật thanh cũ | 2026-11-04 | `Taskbar.Watch` (SetWinEventHook) + TaskbarCreated; test nhấn Win: Start mở, thanh cũ ẩn |
| T-016 | Dock cao bằng taskbar gốc, hiệu ứng clear | 2026-11-04 | 48px sát mép dưới, accent TRANSPARENTGRADIENT |
| T-017 | Widget usage Claude Code (phiên 5h, tuần) | 2026-11-04 | `Claude` class; dữ liệu thật 34% / 3% |
| T-019 | Khay hệ thống: mũi tên + app ghim cạnh nó (UniKey…) | 2026-11-04 | `TrayHost.cs`; test: icon hiện, popup ẩn hiện, thoát trả icon về explorer |
| T-020 | Ô Hôm nay & thời tiết | 2026-11-04 | `TodaySeg`, `Weather`; 34° lúc test |
| T-021 | Ô thư mục app + popup | 2026-11-04 | `FolderSeg`, `Popup.cs` |
| T-022 | Menu chuột phải + cửa sổ cài đặt (widget, thứ tự, app ghim, khay, thư mục, căn lề) | 2026-11-04 | `Settings.cs`, `Config` |
| T-028 | Đợt 1 widget kiểu lumabar (thiết kế riêng): 10 đồng hồ, 7 thời tiết, 9 hệ thống, 7 tệp, 8 năng suất, 3 media; danh mục + thư viện widget + tab Tài khoản | 2026-11-04 | `Catalog.cs`, `Widgets2.cs`, `Tiles2.cs`, `Popups2.cs`; self-check 31/31; chụp 4 bố cục |
| T-018 | Bộ widget đề xuất: đồng hồ thế giới, đồng hồ lật, mạng, nhiệt độ, Wi-Fi/Bluetooth, đang phát, âm lượng, hẹn giờ, pomodoro, việc cần làm, GitHub + tab Tiện ích | 2026-11-04 | `Widgets.cs`, `DockWidgets.cs`; self-check 24/24; chụp 2 bố cục thử |

## Rules for agents
- New task -> next free ID, new row in Active.
- Status change -> edit that row and its `Updated` date. Never delete rows.
- Done or cancelled -> move the row to Done with its result (file, PR, commit, report) or `cancelled: <reason>`.
- Update `_Last updated_` on every edit.
