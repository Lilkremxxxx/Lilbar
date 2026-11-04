# Lilbar

A lightweight clear-glass dock that replaces the Windows 11 taskbar on every monitor, in the same strip the taskbar used. The Windows key still opens Start, and the old taskbar stays hidden.

- Pinned apps and open windows.
- The notification area: apps you pinned sit next to an up-arrow, the rest open from it, clicks and right-click menus work as on the real taskbar.
- Today tile with weekday, date, time and current weather.
- A folder tile that opens a group of apps you choose.
- CPU/RAM rings, Claude Code usage limits, battery, clock.
- Now playing with controls, volume, network speed, board temperature, Wi-Fi and Bluetooth switches.
- World clocks, a flip clock, a timer, pomodoro rounds, a todo list, and GitHub notifications with your contribution grid (personal access token, stored encrypted for your Windows user).
- Right-click the dock for Settings: turn tiles on or off, reorder them, reorder or hide pinned apps, choose which tray icons are pinned, edit the folder.

One small exe, one process, about 60 MB of RAM. No install, no dependencies. Settings live in `%APPDATA%\Lilbar\config.json`.

The weather tile sends your IP address to ipinfo.io to find your city, then asks open-meteo.com for the forecast. Turn the tile off in Settings to stop both.

Other tiles talk to the internet only when you turn them on: stock quotes (Yahoo Finance), a crypto price (Binance), exchange rates (open.er-api.com), Bing images, LeetCode, and your iCal address. Tiles that need an account use the key you paste in Settings → Tài khoản; keys are encrypted for your Windows user and each one is sent only to its own service (Stripe, Polar, Dodo Payments, Shopify, Lemon Squeezy, Paddle, Gumroad, Plausible, Dub, Resend, Linear, Supabase, YouTube, X, Instagram, Gmail over IMAP, DeepSeek). TikTok follower counts are read from the public profile page. Screen time stays on this PC.

The widget set covers the same functions as [lumabar](https://lumabar.xyz)'s library with Lilbar's own design and code.

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

- `native/` — the dock (C#). The notification-area host in `TrayHost.cs` follows [ManagedShell](https://github.com/cairoshell/ManagedShell) (Apache-2.0).
- `main.js`, `win32.js`, `preload.js`, `index.html` — the earlier Electron prototype.
- `Project_proposal.md`, `task_board.md`, `Process_phases/` — project notes.
- `docs/lumabar-reference.md` — feature notes on [lumabar](https://lumabar.xyz), the inspiration. No lumabar code or assets are used.
