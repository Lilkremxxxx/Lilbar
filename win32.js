// Win32 through koffi (prebuilt FFI, no compiler). Window handles are plain numbers.
const fs = require('fs');
const path = require('path');
const koffi = require('koffi');

const user32 = koffi.load('user32.dll');
const kernel32 = koffi.load('kernel32.dll');
const shell32 = koffi.load('shell32.dll');
const dwmapi = koffi.load('dwmapi.dll');

const EnumProc = koffi.proto('bool __stdcall EnumProc(intptr_t hwnd, intptr_t lp)');
const RECT = koffi.struct('RECT', { left: 'long', top: 'long', right: 'long', bottom: 'long' });
const APPBARDATA = koffi.struct('APPBARDATA', {
  cbSize: 'uint32_t', hWnd: 'intptr_t', uCallbackMessage: 'uint32_t', uEdge: 'uint32_t', rc: RECT, lParam: 'intptr_t',
});

const f = {
  EnumWindows: user32.func('bool __stdcall EnumWindows(EnumProc *cb, intptr_t lp)'),
  IsWindowVisible: user32.func('bool __stdcall IsWindowVisible(intptr_t h)'),
  IsIconic: user32.func('bool __stdcall IsIconic(intptr_t h)'),
  GetWindowTextW: user32.func('int __stdcall GetWindowTextW(intptr_t h, _Out_ char16_t *buf, int n)'),
  GetClassNameW: user32.func('int __stdcall GetClassNameW(intptr_t h, _Out_ char16_t *buf, int n)'),
  GetWindowLongPtrW: user32.func('intptr_t __stdcall GetWindowLongPtrW(intptr_t h, int i)'),
  GetAncestor: user32.func('intptr_t __stdcall GetAncestor(intptr_t h, uint32_t flags)'),
  GetWindowThreadProcessId: user32.func('uint32_t __stdcall GetWindowThreadProcessId(intptr_t h, _Out_ uint32_t *pid)'),
  GetForegroundWindow: user32.func('intptr_t __stdcall GetForegroundWindow()'),
  SetForegroundWindow: user32.func('bool __stdcall SetForegroundWindow(intptr_t h)'),
  ShowWindow: user32.func('bool __stdcall ShowWindow(intptr_t h, int cmd)'),
  FindWindowW: user32.func('intptr_t __stdcall FindWindowW(const char16_t *cls, const char16_t *title)'),
  keybd_event: user32.func('void __stdcall keybd_event(uint8_t vk, uint8_t scan, uint32_t flags, uintptr_t extra)'),
  OpenProcess: kernel32.func('intptr_t __stdcall OpenProcess(uint32_t access, bool inherit, uint32_t pid)'),
  QueryFullProcessImageNameW: kernel32.func('bool __stdcall QueryFullProcessImageNameW(intptr_t h, uint32_t flags, _Out_ char16_t *buf, _Inout_ uint32_t *size)'),
  CloseHandle: kernel32.func('bool __stdcall CloseHandle(intptr_t h)'),
  DwmGetWindowAttribute: dwmapi.func('int __stdcall DwmGetWindowAttribute(intptr_t h, uint32_t attr, _Out_ int *out, uint32_t size)'),
  SHAppBarMessage: shell32.func('uintptr_t __stdcall SHAppBarMessage(uint32_t msg, _Inout_ APPBARDATA *data)'),
};

const wstr = (fn, h) => { const b = Buffer.alloc(1024); const n = fn(h, b, 512); return b.toString('utf16le', 0, n * 2); };

const exeByPid = new Map(); // shortcut: pids are assumed not to be reused while the dock runs
function exePath(pid) {
  if (exeByPid.has(pid)) return exeByPid.get(pid);
  let exe = '';
  const h = f.OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
  if (h) {
    const b = Buffer.alloc(2048), size = [1024];
    if (f.QueryFullProcessImageNameW(h, 0, b, size)) exe = b.toString('utf16le', 0, size[0] * 2);
    f.CloseHandle(h);
  }
  exeByPid.set(pid, exe);
  return exe;
}

// Top-level, visible, uncloaked, titled windows: what the Windows taskbar shows.
function listWindows(selfPid) {
  const out = [], fg = f.GetForegroundWindow();
  f.EnumWindows((h) => {
    if (!f.IsWindowVisible(h) || f.GetAncestor(h, 3) !== h) return true; // GA_ROOTOWNER
    if (f.GetWindowLongPtrW(h, -20) & 0x80) return true; // WS_EX_TOOLWINDOW
    const cloaked = [0];
    f.DwmGetWindowAttribute(h, 14, cloaked, 4); // DWMWA_CLOAKED: hidden UWP hosts
    if (cloaked[0]) return true;
    const title = wstr(f.GetWindowTextW, h);
    if (!title) return true;
    const cls = wstr(f.GetClassNameW, h);
    if (cls === 'Progman' || cls === 'Windows.UI.Core.CoreWindow') return true;
    const pid = [0];
    f.GetWindowThreadProcessId(h, pid);
    if (pid[0] === selfPid) return true;
    out.push({ hwnd: h, title, exe: exePath(pid[0]), active: h === fg });
    return true;
  }, 0);
  return out;
}

function activate(hwnd) {
  if (f.GetForegroundWindow() === hwnd) { f.ShowWindow(hwnd, 6); return; } // SW_MINIMIZE
  if (f.IsIconic(hwnd)) f.ShowWindow(hwnd, 9); // SW_RESTORE
  f.keybd_event(0x12, 0, 0, 0); // ALT press lifts the foreground lock for a non-focused process
  f.SetForegroundWindow(hwnd);
  f.keybd_event(0x12, 0, 2, 0);
}

const STATE_FILE = path.join(process.env.TEMP, 'lilbar-taskbar-state.json');
const abd = (hWnd, extra = {}) => ({ cbSize: koffi.sizeof(APPBARDATA), hWnd, uCallbackMessage: 0, uEdge: 3, rc: { left: 0, top: 0, right: 0, bottom: 0 }, lParam: 0, ...extra });

// Auto-hide drops the taskbar's work-area reservation; SW_HIDE then removes it from view.
function hideTaskbar() {
  const tray = f.FindWindowW('Shell_TrayWnd', null);
  if (!fs.existsSync(STATE_FILE)) fs.writeFileSync(STATE_FILE, JSON.stringify({ state: Number(f.SHAppBarMessage(4, abd(tray))) })); // ABM_GETSTATE
  f.SHAppBarMessage(10, abd(tray, { lParam: 1 })); // ABM_SETSTATE ABS_AUTOHIDE
  f.ShowWindow(tray, 0);
}

function restoreTaskbar() {
  const tray = f.FindWindowW('Shell_TrayWnd', null);
  let state = 0;
  try { state = JSON.parse(fs.readFileSync(STATE_FILE, 'utf8')).state; fs.unlinkSync(STATE_FILE); } catch {}
  f.SHAppBarMessage(10, abd(tray, { lParam: state }));
  f.ShowWindow(tray, 5); // SW_SHOW
}

// Reserve a strip at the bottom edge so maximized windows stop above the dock.
function reserveBottom(hwnd, rect) {
  const d = abd(hwnd, { rc: rect });
  f.SHAppBarMessage(0, d); // ABM_NEW
  f.SHAppBarMessage(2, d); // ABM_QUERYPOS
  d.rc.top = d.rc.bottom - (rect.bottom - rect.top);
  f.SHAppBarMessage(3, d); // ABM_SETPOS
  return d.rc;
}
const unreserve = (hwnd) => f.SHAppBarMessage(1, abd(hwnd)); // ABM_REMOVE

module.exports = { listWindows, activate, hideTaskbar, restoreTaskbar, reserveBottom, unreserve };

if (require.main === module) { // self-check: node win32.js
  const w = listWindows(process.pid);
  console.assert(w.length > 0 && w.every((x) => x.hwnd && x.title), 'listWindows returns titled windows');
  console.assert(w.filter((x) => x.active).length <= 1, 'at most one window is foreground');
  console.log(w.length, 'windows;', w.filter((x) => x.exe).length, 'with exe path');
}
