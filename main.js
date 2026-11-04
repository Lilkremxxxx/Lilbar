const { app, BrowserWindow, Tray, Menu, screen, shell, ipcMain } = require('electron');
const os = require('os');
const fs = require('fs');
const path = require('path');
const win32 = require('./win32');

const DOCK_W = 980, DOCK_H = 64, MARGIN = 8, STRIP = DOCK_H + MARGIN * 2;
const PINNED = path.join(process.env.APPDATA, 'Microsoft/Internet Explorer/Quick Launch/User Pinned/TaskBar');

let prevCpu = os.cpus();
function cpuPercent() {
  const cur = os.cpus();
  let idle = 0, total = 0;
  cur.forEach((c, i) => {
    const p = prevCpu[i].times;
    for (const k in c.times) total += c.times[k] - p[k];
    idle += c.times.idle - p.idle;
  });
  prevCpu = cur;
  return total ? Math.round(100 * (1 - idle / total)) : 0;
}

const icons = new Map(); // exe path -> data URL promise
function iconFor(file) {
  if (!file) return '';
  if (!icons.has(file)) icons.set(file, app.getFileIcon(file, { size: 'large' }).then((i) => i.toDataURL(), () => ''));
  return icons.get(file);
}

async function pinnedApps() {
  let files = [];
  try { files = fs.readdirSync(PINNED).filter((f) => f.toLowerCase().endsWith('.lnk')); } catch (e) { console.error(e); }
  return Promise.all(files.map(async (f) => {
    const p = path.join(PINNED, f);
    let exe = p; // the .lnk itself only yields a generic shortcut icon
    try { exe = shell.readShortcutLink(p).target || p; } catch (e) { console.error(f, e.message); }
    return { name: f.slice(0, -4), path: p, exe, icon: await iconFor(exe) };
  }));
}

const openWindows = () => Promise.all(win32.listWindows(process.pid).map(async (w) => ({ ...w, icon: await iconFor(w.exe) })));

ipcMain.handle('stats', () => ({ cpu: cpuPercent(), ram: Math.round(100 * (1 - os.freemem() / os.totalmem())) }));
ipcMain.handle('apps', pinnedApps);
ipcMain.handle('windows', openWindows);
ipcMain.on('launch', (_e, p) => { if (typeof p === 'string' && p.startsWith(PINNED)) shell.openPath(p); });
ipcMain.on('activate', (_e, h) => { if (Number.isInteger(h)) win32.activate(h); });
ipcMain.on('resize', (_e, w) => { if (Number.isFinite(w) && w > 0) place(Math.ceil(w)); });

let win, tray, hwnd = 0;

// Window is exactly the pill's size: transparent margins would still swallow clicks meant for windows below.
// Chromium clamps initial bounds to the work area, so re-place after the bottom strip is reserved.
function place(width) {
  const { bounds } = screen.getPrimaryDisplay();
  win.setBounds({ width, height: DOCK_H, x: Math.round(bounds.x + (bounds.width - width) / 2), y: bounds.y + bounds.height - DOCK_H - MARGIN });
}

function createDock() {
  win = new BrowserWindow({
    width: DOCK_W, height: DOCK_H, show: false,
    frame: false, transparent: true, hasShadow: false, resizable: false, movable: false,
    alwaysOnTop: true, skipTaskbar: true, focusable: false,
    webPreferences: { preload: path.join(__dirname, 'preload.js') },
  });
  win.setAlwaysOnTop(true, 'screen-saver');
  win.webContents.once('did-finish-load', () => {
    hwnd = Number(win.getNativeWindowHandle().readBigInt64LE());
    const { bounds } = screen.getPrimaryDisplay(); // shortcut: DIP == px at 100% scale; multiply by scaleFactor for HiDPI
    win32.hideTaskbar();
    win32.reserveBottom(hwnd, { left: bounds.x, top: bounds.y + bounds.height - STRIP, right: bounds.x + bounds.width, bottom: bounds.y + bounds.height });
    place(win.getBounds().width);
    win.show();
  });
  win.loadFile('index.html');
}

app.whenReady().then(async () => {
  // shortcut: tray icon is Electron's own exe icon, swap for a real asset when one exists
  tray = new Tray(await app.getFileIcon(process.execPath));
  tray.setToolTip('Lilbar');
  tray.setContextMenu(Menu.buildFromTemplate([{ label: 'Thoát Lilbar', click: () => app.quit() }]));
  createDock();
});

app.on('before-quit', () => {
  if (hwnd) win32.unreserve(hwnd);
  win32.restoreTaskbar();
});

app.on('window-all-closed', () => app.quit());
