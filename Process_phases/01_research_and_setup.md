# 01 — research_and_setup

**Status:** done | **Started:** 2026-11-03 | **Ended:** 2026-11-03

## Goal
Cài REA, hiểu lumabar đủ để viết spec v1, chốt stack.

## Scope
- **In:** cài REA cho Claude Code; phân tích MSI lumabar + website; proposal + task board; quyết định stack.
- **Out:** viết mã dock.

## Tasks
T-001, T-002, T-003

## Work log
- 2026-11-03 [claude-code] Cài rea-agents 6.8.0: Node hệ thống 24.12.0 dưới mức REA yêu cầu (>=24.22.1) nên `nvm install 24.22.2` (không `nvm use`), `npm i -g rea-agents` trong thư mục nvm đó, đăng ký MCP `rea` scope user với đường dẫn tuyệt đối node.exe + rea.mjs; `claude mcp list` báo Connected. Setup ghi đè skill v55 bằng v49 (bản đi kèm 6.8.0, "aligned"); why: REA từ chối chạy trên Node 24.12.0.
- 2026-11-03 [claude-code] User sửa yêu cầu: muốn REA trang web để làm taskbar chạy ngay. Kiểm tra lumabar.xyz: site Tailwind tĩnh, 207 ảnh webp chụp widget, không có dock chạy thật; lấy thiết kế từ ảnh. Chốt Electron (Rust chưa cài), đóng phase; why: tiêu chí thoát (stack chốt) đạt.
- 2026-11-03 [claude-code] Tải lumabar-1.2.11-64-bit.msi, trích bằng `msiexec /a` (qua PowerShell; Bash sandbox trả mã 103). REA `inspect-desktop-application`, `inspect-rust-binary`, `inspect-pe-imports` + quét chuỗi: Tauri 2 + WebView2, danh sách Win32/WinRT/API. Website: 103 widget/13 nhóm. Kết quả `docs/lumabar-reference.md`. Không chạy lumabar.exe, không giải nén asset brotli; why: phần mềm thương mại, chỉ cần spec.

## Decisions
| Decision | Why | Alternatives rejected |
|----------|-----|-----------------------|
| REA chạy bằng Node 24.22.2 riêng (nvm), không đổi Node hệ thống | REA cần >=24.22.1; `nvm use` sẽ thay node toàn máy | nâng Node hệ thống (ảnh hưởng dự án khác) |
| Không trích frontend/asset của lumabar, chỉ lấy spec | phần mềm thương mại đóng nguồn | giải nén brotli, chạy app với CDP |
| v1 = dock + 3 widget | ponytail ultra: 103 widget không có nhu cầu thật | clone toàn bộ |

## Outputs
`docs/lumabar-reference.md`, `Project_proposal.md`, `task_board.md`

## Issues & risks
- `npx rea-agents` / `rea` trong shell thường của user vẫn lỗi vì Node 24.12.0; cần prefix PATH tới `%LOCALAPPDATA%\nvm\v24.22.2`.
- `~/.claude/settings.json` có marketplace `rea` (morluto/rea); cài thêm plugin sẽ tạo server `rea` thứ hai.
- Máy chưa có Rust/cargo/MSVC nên Tauri cần cài thêm.

## Exit criteria / Next phase
- Done when: user chốt stack và toolchain chạy được (`cargo --version` hoặc Electron scaffold).
- Next: 02_dock_mvp
