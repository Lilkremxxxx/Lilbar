# Lilbar

Clear-glass widgets that live on the Windows 11 taskbar, next to the notification area (or at its left edge), on every monitor. The Windows taskbar keeps doing its own job (apps, tray, Start); Lilbar hides with it when it auto-hides and when a game or video goes fullscreen. An experimental mode in Settings replaces the taskbar entirely instead.

- Pinned apps and open windows.
- The notification area: apps you pinned sit next to an up-arrow, the rest open from it, clicks and right-click menus work as on the real taskbar.
- Today tile with weekday, date, time and current weather.
- A folder tile that opens a group of apps you choose.
- CPU/RAM rings, Claude Code usage limits, battery, clock.
- Now playing with controls, volume, network speed, board temperature, Wi-Fi and Bluetooth switches.
- World clocks, a flip clock, a timer, pomodoro rounds, a todo list, and GitHub notifications with your contribution grid (personal access token, stored encrypted for your Windows user).
- One clear box the height of the taskbar, with hairline tiles inside. Drag tiles or pinned apps on the dock (or rows in Settings) to reorder them.
- Hover an app for live previews of its windows; click one to switch, ✕ to close it.
- Quick toggles: Wi-Fi, Bluetooth, mobile hotspot, airplane mode and the battery on laptops.
- Start with Windows from the right-click menu or Settings.
- Right-click the dock for Settings: turn tiles on or off, reorder them, reorder or hide pinned apps, choose which tray icons are pinned, edit the folder.

One small exe, one process, about 25 MB of working set (40 MB private) once it settles. No install, no dependencies. Settings live in `%APPDATA%\Lilbar\config.json`.

The weather tile sends your IP address to ipinfo.io to find your city, then asks open-meteo.com for the forecast. Turn the tile off in Settings to stop both.

Other tiles talk to the internet only when you turn them on: stock quotes (Yahoo Finance), a crypto price (Binance), exchange rates (open.er-api.com), Bing images, LeetCode, and your iCal address. Tiles that need an account use the key you paste in Settings → Tài khoản; keys are encrypted for your Windows user and each one is sent only to its own service (Stripe, Polar, Dodo Payments, Shopify, Lemon Squeezy, Paddle, Gumroad, Plausible, Dub, Resend, Linear, Supabase, YouTube, X, Instagram, Gmail over IMAP, DeepSeek). TikTok follower counts are read from the public profile page. Screen time stays on this PC.

The widget set covers the same functions as [lumabar](https://lumabar.xyz)'s library with Lilbar's own design and code.

The Claude Code widget reads the sign-in Claude Code already stores in `~/.claude/.credentials.json` and asks `api.anthropic.com` for your 5-hour and weekly limits, at most every 5 minutes. The token never goes anywhere else. If Claude Code isn't installed, the widget doesn't appear.

## Download and install

1. Download [`release/Lilbar-1.0.0.zip`](release/Lilbar-1.0.0.zip) and unzip it.
2. Double-click `install.cmd`. It copies Lilbar to `%LOCALAPPDATA%\Programs\Lilbar`, adds a Start menu shortcut, turns on start with Windows and starts it. No admin rights needed.
3. Lilbar isn't code-signed, so SmartScreen may say "Windows protected your PC": choose More info → Run anyway.

`uninstall.cmd` removes it again (your settings in `%APPDATA%\Lilbar` stay). Windows 10 or 11, 64-bit; it uses the .NET Framework 4.8 that ships with Windows. `HUONG-DAN.txt` in the zip is a Vietnamese guide.

`releaseelease.cmd` rebuilds the zip.

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

- `native/` — the dock (C#). The notification-area host in `TrayHost.cs` follows [ManagedShell](https://github.com/cairoshell/ManagedShell) (Apache-2.0). Line icons in `Icons.cs` are from [coolicons](https://github.com/krystonschwarze/coolicons) by Kryston Schwarze (MIT).
- `main.js`, `win32.js`, `preload.js`, `index.html` — the earlier Electron prototype.
- `Project_proposal.md`, `task_board.md`, `Process_phases/` — project notes.
- `docs/lumabar-reference.md` — feature notes on [lumabar](https://lumabar.xyz), the inspiration. No lumabar code or assets are used.
