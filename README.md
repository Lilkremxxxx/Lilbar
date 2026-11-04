# Lilbar

A lightweight glass dock that replaces the Windows 11 taskbar: pinned apps, open windows, CPU/RAM rings, battery and clock on an acrylic bar at the bottom of the screen.

One 27 KB exe, one process, about 40 MB of RAM. No install, no dependencies.

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
