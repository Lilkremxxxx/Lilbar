using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Lilbar
{
    class TrayIcon
    {
        public IntPtr HWnd;
        public uint UID, Callback, Version;
        public Guid Guid;
        public Bitmap Icon;
        public string Tip = "", Exe = "";
        public bool Hidden;
        public Rectangle Rect; // where the icon is drawn, in screen pixels; apps ask for it to place flyouts
        public DateTime LastL, LastR;
    }

    // Notification-area host. Apps find the tray with FindWindow("Shell_TrayWnd") and send their icons
    // as WM_COPYDATA. Our own Shell_TrayWnd, kept above explorer's in z-order, receives them, keeps a copy
    // and forwards every message to explorer, so explorer's state stays complete if Lilbar stops.
    // Protocol and structures follow ManagedShell (Apache-2.0, https://github.com/cairoshell/ManagedShell).
    static class TrayHost
    {
        public static IntPtr Hwnd;
        public static int Version;
        public static readonly List<TrayIcon> Icons = new List<TrayIcon>();
        static IntPtr notify, explorer, inst;
        static N.WndProcFn proc; // field keeps the delegate alive while the window class holds it
        static readonly Dictionary<string, bool> promoted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        static DateTime promotedAt = DateTime.MinValue;
        const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_STATE = 8, NIF_GUID = 0x20;
        const uint WM_COPYDATA = 0x4A, WM_USER = 0x400;

        public static void Start()
        {
            if (Hwnd != IntPtr.Zero) return;
            explorer = Taskbar.Primary();
            proc = WndProc;
            inst = N.GetModuleHandle(null);
            Register("Shell_TrayWnd");
            Register("TrayNotifyWnd");
            Hwnd = N.CreateWindowEx(0x8 | 0x80, "Shell_TrayWnd", "", 0x80000000 | 0x04000000 | 0x02000000, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero); // TOPMOST|TOOLWINDOW, POPUP|CLIP*
            notify = N.CreateWindowEx(0, "TrayNotifyWnd", null, 0x40000000 | 0x04000000 | 0x02000000, 0, 0, 0, 0, Hwnd, IntPtr.Zero, inst, IntPtr.Zero); // some apps look for this child
            KeepOnTop();
        }

        static void Register(string cls)
        {
            var wc = new N.WNDCLASS { style = 8, lpfnWndProc = proc, hInstance = inst, lpszClassName = cls };
            N.RegisterClass(ref wc);
        }

        public static void Stop()
        {
            if (Hwnd == IntPtr.Zero) return;
            N.DestroyWindow(notify);
            N.DestroyWindow(Hwnd);
            Hwnd = notify = IntPtr.Zero;
            N.UnregisterClass("TrayNotifyWnd", inst);
            N.UnregisterClass("Shell_TrayWnd", inst);
        }

        // Asks every running app to (re)register its tray icons with whichever tray FindWindow returns.
        public static void BroadcastTaskbarCreated()
        {
            N.SendNotifyMessage((IntPtr)0xFFFF, (uint)N.RegisterWindowMessage("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        }

        public static void SetRect(Rectangle r)
        {
            if (Hwnd != IntPtr.Zero) N.SetWindowPos(Hwnd, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, 0x10 | 0x4); // NOACTIVATE | NOZORDER
        }

        // Our window must stay first in z-order so Shell_NotifyIcon finds it, not explorer's.
        public static void KeepOnTop()
        {
            if (Hwnd != IntPtr.Zero && N.FindWindow("Shell_TrayWnd", null) != Hwnd)
                N.SetWindowPos(Hwnd, (IntPtr)(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10); // HWND_TOPMOST, NOSIZE|NOMOVE|NOACTIVATE
        }

        static IntPtr WndProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
        {
            try
            {
                if (msg == WM_COPYDATA && l != IntPtr.Zero)
                {
                    var cds = (N.COPYDATASTRUCT)Marshal.PtrToStructure(l, typeof(N.COPYDATASTRUCT));
                    long kind = cds.dwData.ToInt64();
                    if (kind == 1 && cds.lpData != IntPtr.Zero) // Shell_NotifyIcon
                    {
                        var td = Copy<N.SHELLTRAYDATA>(cds);
                        bool ok = Handle(td.dwMessage, td.nid);
                        IntPtr r = Forward(h, msg, w, l);
                        return ok ? (IntPtr)1 : r;
                    }
                    if (kind == 3 && cds.lpData != IntPtr.Zero) // Shell_NotifyIconGetRect
                    {
                        var id = Copy<N.WINNOTIFYICONIDENTIFIER>(cds);
                        TrayIcon t = Find((IntPtr)(long)id.hWnd, id.uID, id.guidItem);
                        if (t != null && !t.Rect.IsEmpty)
                            return id.dwMessage == 1 ? MakeLParam(t.Rect.Left, t.Rect.Top) : MakeLParam(t.Rect.Right, t.Rect.Bottom);
                    }
                    return Forward(h, msg, w, l); // appbar messages and anything we don't answer
                }
                if (msg == 0x47) // WM_WINDOWPOSCHANGED: this window must never become visible
                {
                    long style = N.GetWindowLongPtr(h, -16).ToInt64();
                    if ((style & 0x10000000) != 0) N.SetWindowLongPtr(h, -16, (IntPtr)(style & ~0x10000000L));
                }
                if (msg == 0x111 || msg == 0x1C || msg >= WM_USER) return Forward(h, msg, w, l); // COMMAND, ACTIVATEAPP, private messages
            }
            catch (Exception e) { Taskbar.Log(e); }
            return N.DefWindowProc(h, msg, w, l);
        }

        static IntPtr Forward(IntPtr h, uint msg, IntPtr w, IntPtr l)
        {
            if (explorer == IntPtr.Zero || !N.IsWindow(explorer)) explorer = Taskbar.Primary();
            if (explorer == IntPtr.Zero) return N.DefWindowProc(h, msg, w, l);
            if (msg == WM_USER + 372) { N.PostMessage(explorer, msg, w, l); return N.DefWindowProc(h, msg, w, l); } // ManagedShell posts this one
            IntPtr r;
            N.SendMessageTimeout(explorer, msg, w, l, 0x2, 3000, out r); // SMTO_ABORTIFHUNG
            return r;
        }

        static IntPtr MakeLParam(int lo, int hi) { return (IntPtr)(long)(((uint)(ushort)hi << 16) | (ushort)lo); }

        // Copies at most cbData bytes into a zeroed struct, so short legacy payloads never over-read.
        static T Copy<T>(N.COPYDATASTRUCT cds) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T)), n = Math.Min(size, Math.Max(0, cds.cbData));
            var bytes = new byte[size];
            Marshal.Copy(cds.lpData, bytes, 0, n);
            GCHandle gh = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { return (T)Marshal.PtrToStructure(gh.AddrOfPinnedObject(), typeof(T)); }
            finally { gh.Free(); }
        }

        static TrayIcon Find(IntPtr hwnd, uint uid, Guid g)
        {
            return Icons.Find(i => (hwnd != IntPtr.Zero && i.HWnd == hwnd && i.UID == uid) || (g != Guid.Empty && i.Guid == g));
        }

        static bool Handle(uint m, N.NOTIFYICONDATA d)
        {
            IntPtr hwnd = (IntPtr)(long)d.hWnd;
            Guid g = (d.uFlags & NIF_GUID) != 0 ? d.guidItem : Guid.Empty;
            if (hwnd == IntPtr.Zero && (g == Guid.Empty || m == 0)) return false;
            TrayIcon t = Find(hwnd, d.uID, g);
            bool ok = true;
            if (m == 0 || m == 1) // NIM_ADD, NIM_MODIFY
            {
                if (t == null)
                {
                    if (hwnd == IntPtr.Zero) return false;
                    t = new TrayIcon { HWnd = hwnd, UID = d.uID, Exe = Sys.ExeOfWindow(hwnd) };
                    Icons.Add(t);
                    if (m == 1) ok = false; // modify for an icon we never saw: report failure like explorer
                }
                if ((d.uFlags & NIF_STATE) != 0 && (d.dwStateMask & 1) != 0) t.Hidden = (d.dwState & 1) != 0;
                if ((d.uFlags & NIF_TIP) != 0 && !string.IsNullOrEmpty(d.szTip)) t.Tip = d.szTip;
                if ((d.uFlags & NIF_ICON) != 0) t.Icon = ToBitmap((IntPtr)(long)d.hIcon) ?? t.Icon;
                if (g != Guid.Empty) t.Guid = g;
                if (hwnd != IntPtr.Zero) { t.HWnd = hwnd; t.UID = d.uID; }
                if (d.uVersion > 0 && d.uVersion <= 4) t.Version = d.uVersion;
                if ((d.uFlags & NIF_MESSAGE) != 0) t.Callback = d.uCallbackMessage;
            }
            else if (m == 2) // NIM_DELETE
            {
                if (t == null) return false;
                Icons.Remove(t);
            }
            else if (m == 4) // NIM_SETVERSION
            {
                if (d.uVersion > 4) return false;
                if (t != null) t.Version = d.uVersion;
            }
            Version++;
            Manager.PostRefresh();
            return ok;
        }

        static Bitmap ToBitmap(IntPtr h)
        {
            if (h == IntPtr.Zero) return null;
            try { using (Icon ic = Icon.FromHandle(h)) return ic.ToBitmap(); } // copy now: the app owns the handle
            catch (Exception) { return null; }
        }

        public static List<TrayIcon> Visible() { return Icons.FindAll(i => !i.Hidden && i.Icon != null); }

        public static void Prune()
        {
            if (Icons.RemoveAll(i => !N.IsWindow(i.HWnd)) > 0) Version++;
        }

        public static bool IsPinned(TrayIcon t)
        {
            if (t.Exe.Length == 0) return false;
            List<string> cfg = Config.Current.TrayPinned;
            if (cfg != null) return cfg.Exists(p => string.Equals(p, t.Exe, StringComparison.OrdinalIgnoreCase));
            return WindowsPromoted(t.Exe);
        }

        // Windows 11 keeps "show in taskbar" per app under NotifyIconSettings; paths may start with a known-folder GUID.
        public static bool WindowsPromoted(string exe)
        {
            if ((DateTime.Now - promotedAt).TotalSeconds > 60) LoadPromoted();
            bool v;
            return promoted.TryGetValue(exe, out v) && v;
        }

        static void LoadPromoted()
        {
            promotedAt = DateTime.Now;
            promoted.Clear();
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
                {
                    if (root == null) return;
                    foreach (string name in root.GetSubKeyNames())
                        using (RegistryKey k = root.OpenSubKey(name))
                        {
                            string path = k == null ? null : k.GetValue("ExecutablePath") as string;
                            if (string.IsNullOrEmpty(path)) continue;
                            path = ExpandKnownFolder(path);
                            object p = k.GetValue("IsPromoted");
                            if (p is int && (int)p == 1) promoted[path] = true;
                            else if (!promoted.ContainsKey(path)) promoted[path] = false;
                        }
                }
            }
            catch (Exception e) { Taskbar.Log(e); }
        }

        public static string ExpandKnownFolder(string path)
        {
            Match m = Regex.Match(path, @"^\{([0-9A-Fa-f-]{36})\}(.*)$");
            if (!m.Success) return path;
            Guid id = new Guid(m.Groups[1].Value);
            IntPtr p;
            if (N.SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out p) != 0) return path;
            try { return Marshal.PtrToStringUni(p) + m.Groups[2].Value; }
            finally { Marshal.FreeCoTaskMem(p); }
        }

        // Click forwarding, packed the way explorer does for each NOTIFYICON_VERSION.
        static void Send(TrayIcon t, uint m)
        {
            if (t.Callback == 0) return;
            N.POINT p;
            N.GetCursorPos(out p);
            uint mouse = ((uint)(ushort)p.Y << 16) | (ushort)p.X;
            IntPtr w = t.Version > 3 ? (IntPtr)(long)mouse : (IntPtr)(long)t.UID;
            IntPtr l = (IntPtr)(long)(m | (t.Version > 3 ? t.UID << 16 : 0));
            N.SendNotifyMessage(t.HWnd, t.Callback, w, l);
        }

        public static void MouseDown(TrayIcon t, bool right)
        {
            uint pid;
            N.GetWindowThreadProcessId(t.HWnd, out pid);
            N.AllowSetForegroundWindow(pid); // lets the app show and dismiss its menu
            bool dbl = (DateTime.Now - (right ? t.LastR : t.LastL)).TotalMilliseconds <= N.GetDoubleClickTime();
            Send(t, right ? (dbl ? 0x206u : 0x204u) : (dbl ? 0x203u : 0x201u));
        }

        public static void MouseUp(TrayIcon t, bool right)
        {
            if (right) { Send(t, 0x205); if (t.Version >= 3) Send(t, 0x7B); t.LastR = DateTime.Now; } // RBUTTONUP, CONTEXTMENU
            else { Send(t, 0x202); if (t.Version >= 3) Send(t, 0x400); t.LastL = DateTime.Now; } // LBUTTONUP, NIN_SELECT
        }
    }
}
