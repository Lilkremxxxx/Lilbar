# Lilbar

A lightweight clear-glass dock that replaces the Windows 11 taskbar: pinned apps, open windows, CPU/RAM rings, Claude Code usage limits, battery and clock, in the same strip the taskbar used. The Windows key still opens Start, and the old taskbar stays hidden.

One small exe, one process, about 50 MB of RAM. No install, no dependencies.

The Claude Code widget reads the sign-in Claude Code already stores in `~/.claude/.credentials.json` and asks `api.anthropic.com` for your 5-hour and weekly limits, at most every 5 minutes. The token never goes anywhere else. If Claude Code isn't installed, the widget doesn't appear.

## Build and run

```
native\build.cmd
native\Lilbar.exe
```

`build.cmd` uses the C# compiler that ships with Windows (.NET Framework 4.8).

Quit from the tray icon's menu. If the dock is killed and the Windows taskbar stays hidden:

```
native\Lilbar.exe --restore
```

`Lilbar.exe --selftest` checks the Win32 plumbing and exits with the number of failures.

## Layout

- `native/` — the dock (C#).
- `main.js`, `win32.js`, `preload.js`, `index.html` — the earlier Electron prototype.
- `Project_proposal.md`, `task_board.md`, `Process_phases/` — project notes.
- `docs/lumabar-reference.md` — feature notes on [lumabar](https://lumabar.xyz), the inspiration. No lumabar code or assets are used.
