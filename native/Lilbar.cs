// Lilbar: a clear-glass dock that replaces the Windows taskbar on every monitor.
// Build with build.cmd (csc.exe that ships with Windows, so C# 5 only: no $"", no ?., no => members).
// Lilbar.exe --restore   puts the Windows taskbar back after a hard kill.
// Lilbar.exe --selftest  checks the Win32 plumbing and returns the number of failures.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lilbar
{
    static class N
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        public delegate IntPtr WndProcFn(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint pid);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern bool SendNotifyMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern ushort RegisterClass(ref WNDCLASS wc);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool UnregisterClass(string cls, IntPtr inst);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr DefWindowProc(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr h, ref WCAD data);
        [DllImport("gdi32.dll")] public static extern int SetDIBitsToDevice(IntPtr hdc, int x, int y, uint w, uint h, int sx, int sy, uint start, uint lines, IntPtr bits, ref BITMAPINFOHEADER bmi, uint usage);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder sb, ref uint size);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMSTATUS m);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
        [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);
        [DllImport("shell32.dll")] public static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public uint cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
        [StructLayout(LayoutKind.Sequential)] public struct MEMSTATUS { public uint dwLength, dwMemoryLoad; public ulong total, avail, totalPage, availPage, totalVirtual, availVirtual, availExtended; }
        [StructLayout(LayoutKind.Sequential)] public struct ACCENT { public int AccentState, AccentFlags; public uint GradientColor; public int AnimationId; }
        [StructLayout(LayoutKind.Sequential)] public struct WCAD { public int Attribute; public IntPtr Data; public int SizeOfData; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFOHEADER { public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASS { public uint style; public WndProcFn lpfnWndProc; public int cbClsExtra, cbWndExtra; public IntPtr hInstance, hIcon, hCursor, hbrBackground; public string lpszMenuName, lpszClassName; }
        [StructLayout(LayoutKind.Sequential)] public struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }
        // Tray payloads use 32-bit handle fields on every architecture (ManagedShell's layout).
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize; public uint hWnd, uID, uFlags, uCallbackMessage, hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState, dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags; public Guid guidItem; public uint hBalloonIcon;
        }
        [StructLayout(LayoutKind.Sequential)] public struct SHELLTRAYDATA { public int dwUnknown; public uint dwMessage; public NOTIFYICONDATA nid; }
        [StructLayout(LayoutKind.Sequential)] public struct WINNOTIFYICONIDENTIFIER { public int dwMagic, dwMessage, cbSize, dwPadding; public uint hWnd, uID; public Guid guidItem; }
    }

    // Hides explorer's taskbars (every monitor) and keeps them hidden: explorer re-shows them on some events
    // (Start menu, display changes), so a WinEvent hook on explorer's process hides them again at once.
    // Explorer keeps reserving their strips while hidden, and each dock sits exactly in its strip.
    static class Taskbar
    {
        // Written only by the older auto-hide build; still read so its leftovers get undone.
        static readonly string StateFile = Path.Combine(Path.GetTempPath(), "lilbar-taskbar-state.json");
        static readonly StringBuilder cls = new StringBuilder(64);
        static N.WinEventProc onShow; // field keeps the delegate alive while native code holds it
        static IntPtr hook;
        public static bool WasAutoHide;

        static N.APPBARDATA Abd(IntPtr h)
        {
            var d = new N.APPBARDATA();
            d.cbSize = (uint)Marshal.SizeOf(typeof(N.APPBARDATA));
            d.hWnd = h;
            d.uEdge = 3; // ABE_BOTTOM
            return d;
        }

        // Explorer's main taskbar, skipping Lilbar's own tray host window of the same class.
        public static IntPtr Primary()
        {
            IntPtr h = IntPtr.Zero;
            while ((h = N.FindWindowEx(IntPtr.Zero, h, "Shell_TrayWnd", null)) != IntPtr.Zero)
                if (h != TrayHost.Hwnd) return h;
            return IntPtr.Zero;
        }

        public static List<IntPtr> All()
        {
            var list = new List<IntPtr>();
            foreach (string c in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            {
                IntPtr h = IntPtr.Zero;
                while ((h = N.FindWindowEx(IntPtr.Zero, h, c, null)) != IntPtr.Zero)
                    if (h != TrayHost.Hwnd) list.Add(h);
            }
            return list;
        }

        public static void CaptureAutoHide()
        {
            if (File.Exists(StateFile)) Restore();
            var d = Abd(Primary());
            WasAutoHide = ((ulong)N.SHAppBarMessage(4, ref d) & 1) != 0; // ABM_GETSTATE & ABS_AUTOHIDE
        }

        // Height of the taskbar on the monitor with these bounds; 0 when that monitor has none.
        public static int HeightOn(Rectangle bounds)
        {
            foreach (IntPtr t in All())
            {
                N.RECT r;
                N.GetWindowRect(t, out r);
                if (bounds.Contains(new Point((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2))) return r.Bottom - r.Top;
            }
            return 0;
        }

        public static void Hide()
        {
            foreach (IntPtr t in All()) N.ShowWindow(t, 0);
            Watch();
        }

        static bool IsTaskbar(IntPtr h)
        {
            cls.Clear();
            N.GetClassName(h, cls, cls.Capacity);
            string c = cls.ToString();
            return c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd";
        }

        static void Watch()
        {
            Unwatch();
            uint pid;
            N.GetWindowThreadProcessId(Primary(), out pid);
            if (pid == 0) return;
            onShow = delegate(IntPtr hk, uint ev, IntPtr h, int obj, int child, uint thread, uint time)
            {
                if (obj == 0 && h != TrayHost.Hwnd && IsTaskbar(h)) N.ShowWindow(h, 0);
            };
            hook = N.SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, onShow, pid, 0, 0); // EVENT_OBJECT_SHOW, out of context
        }

        static void Unwatch()
        {
            if (hook != IntPtr.Zero) N.UnhookWinEvent(hook);
            hook = IntPtr.Zero;
        }

        // Idempotent; also undoes the auto-hide flag the older build set.
        public static void Restore()
        {
            Unwatch();
            if (File.Exists(StateFile))
            {
                int state = 0;
                try
                {
                    Match m = Regex.Match(File.ReadAllText(StateFile), @"\d+");
                    if (m.Success) state = int.Parse(m.Value);
                    File.Delete(StateFile);
                }
                catch (Exception e) { Log(e); }
                var d = Abd(Primary());
                d.lParam = (IntPtr)state;
                N.SHAppBarMessage(10, ref d); // ABM_SETSTATE
            }
            foreach (IntPtr t in All()) N.ShowWindow(t, 5);
        }

        // Only needed when the user's taskbar is auto-hide, so explorer reserves nothing.
        public static void Reserve(IntPtr h, Rectangle strip)
        {
            var d = Abd(h);
            d.rc.Left = strip.Left; d.rc.Top = strip.Top; d.rc.Right = strip.Right; d.rc.Bottom = strip.Bottom;
            N.SHAppBarMessage(0, ref d); // ABM_NEW
            N.SHAppBarMessage(2, ref d); // ABM_QUERYPOS
            d.rc.Top = d.rc.Bottom - strip.Height;
            N.SHAppBarMessage(3, ref d); // ABM_SETPOS
        }

        public static void Unreserve(IntPtr h) { var d = Abd(h); N.SHAppBarMessage(1, ref d); } // ABM_REMOVE

        public static void Log(Exception e)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "lilbar.log"), DateTime.Now.ToString("s") + " " + e + Environment.NewLine); }
            catch { }
        }
    }

    // Claude Code plan limits, read the way Claude Code's /usage does: the OAuth token Claude Code
    // keeps in .credentials.json, sent only to api.anthropic.com. Response shape observed 2026-11-04.
    static class Claude
    {
        public class Limit { public double Pct; public DateTime Reset; }
        public static volatile Limit Session, Week;
        public static volatile string Error; // null when the last read worked
        public static volatile bool Available;
        static DateTime next = DateTime.MinValue;
        static int busy;

        static string CredPath()
        {
            string dir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return Path.Combine(dir, ".credentials.json");
        }

        public static void RefreshNow() { next = DateTime.MinValue; }

        // Called from the UI timer; the request runs on the thread pool, at most every 5 minutes.
        public static void MaybeFetch(Action done)
        {
            if (DateTime.Now < next || Interlocked.Exchange(ref busy, 1) == 1) return;
            next = DateTime.Now.AddMinutes(5);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Fetch(); }
                catch (Exception e) { Error = "Không đọc được"; Taskbar.Log(e); }
                finally { Interlocked.Exchange(ref busy, 0); done(); }
            });
        }

        static void Fetch()
        {
            string path = CredPath();
            Available = File.Exists(path);
            if (!Available) return;
            var cred = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            object o;
            var oauth = cred != null && cred.TryGetValue("claudeAiOauth", out o) ? o as Dictionary<string, object> : null;
            string token = oauth != null && oauth.ContainsKey("accessToken") ? oauth["accessToken"] as string : null;
            if (string.IsNullOrEmpty(token)) { Error = "Chưa đăng nhập Claude Code"; return; }
            long exp = oauth.ContainsKey("expiresAt") ? Convert.ToInt64(oauth["expiresAt"], CultureInfo.InvariantCulture) : 0;
            if (exp > 0 && DateTimeOffset.FromUnixTimeMilliseconds(exp) < DateTimeOffset.UtcNow)
            {
                Error = "Mở Claude Code để đăng nhập lại"; // Claude Code refreshes the token itself
                next = DateTime.Now.AddMinutes(1);
                return;
            }

            var req = (HttpWebRequest)WebRequest.Create("https://api.anthropic.com/api/oauth/usage");
            req.Headers["Authorization"] = "Bearer " + token;
            req.Headers["anthropic-beta"] = "oauth-2025-04-20";
            req.UserAgent = "claude-code/2.0.0";
            req.Timeout = 15000;
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    Parse(r.ReadToEnd());
                Error = null;
            }
            catch (WebException e)
            {
                var resp = e.Response as HttpWebResponse;
                if (resp == null) { Error = "Không có mạng"; next = DateTime.Now.AddMinutes(1); return; }
                using (resp)
                {
                    int code = (int)resp.StatusCode, wait;
                    if (code == 429) next = DateTime.Now.AddSeconds(int.TryParse(resp.Headers["retry-after"], out wait) ? Math.Max(wait, 60) : 300);
                    else if (code == 401 || code == 403) { Error = "Mở Claude Code để đăng nhập lại"; next = DateTime.Now.AddMinutes(1); }
                    else { Error = "Anthropic trả lỗi " + code; next = DateTime.Now.AddMinutes(2); }
                }
            }
        }

        public static void Parse(string json)
        {
            var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            Session = Read(d, "five_hour");
            Week = Read(d, "seven_day");
        }

        static Limit Read(Dictionary<string, object> d, string key)
        {
            object o;
            var m = d != null && d.TryGetValue(key, out o) ? o as Dictionary<string, object> : null;
            if (m == null) return null;
            var l = new Limit();
            if (m.TryGetValue("utilization", out o) && o != null) l.Pct = Convert.ToDouble(o, CultureInfo.InvariantCulture);
            if (m.TryGetValue("resets_at", out o) && o is string) l.Reset = DateTimeOffset.Parse((string)o, CultureInfo.InvariantCulture).LocalDateTime;
            return l;
        }
    }

    class Win { public IntPtr Hwnd; public string Title; public string Exe; public bool Active; }
    class Pin { public string Name; public string Lnk; public string Exe; }

    static class Sys
    {
        static readonly uint SelfPid = (uint)Process.GetCurrentProcess().Id;
        static readonly Dictionary<uint, string> exeByPid = new Dictionary<uint, string>(); // shortcut: pids assumed not reused while running
        static readonly Dictionary<string, Bitmap> icons = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        static readonly StringBuilder sb = new StringBuilder(512);
        static long lastIdle, lastTotal;

        static string ExePath(uint pid)
        {
            string exe;
            if (exeByPid.TryGetValue(pid, out exe)) return exe;
            exe = "";
            IntPtr h = N.OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (h != IntPtr.Zero)
            {
                var b = new StringBuilder(1024);
                uint size = 1024;
                if (N.QueryFullProcessImageName(h, 0, b, ref size)) exe = b.ToString();
                N.CloseHandle(h);
            }
            exeByPid[pid] = exe;
            return exe;
        }

        public static string ExeOfWindow(IntPtr h)
        {
            uint pid;
            N.GetWindowThreadProcessId(h, out pid);
            return pid == 0 ? "" : ExePath(pid);
        }

        // Top-level, visible, uncloaked, titled windows: what the Windows taskbar shows.
        public static List<Win> Windows()
        {
            var list = new List<Win>();
            IntPtr fg = N.GetForegroundWindow();
            N.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!N.IsWindowVisible(h) || N.GetAncestor(h, 3) != h) return true; // GA_ROOTOWNER
                if (((long)N.GetWindowLongPtr(h, -20) & 0x80) != 0) return true; // WS_EX_TOOLWINDOW
                int cloaked;
                if (N.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true; // DWMWA_CLOAKED
                sb.Clear();
                N.GetWindowText(h, sb, sb.Capacity);
                string title = sb.ToString();
                if (title.Length == 0) return true;
                sb.Clear();
                N.GetClassName(h, sb, sb.Capacity);
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "Windows.UI.Core.CoreWindow") return true;
                uint pid;
                N.GetWindowThreadProcessId(h, out pid);
                if (pid == SelfPid) return true;
                list.Add(new Win { Hwnd = h, Title = title, Exe = ExePath(pid), Active = h == fg });
                return true;
            }, IntPtr.Zero);
            return list;
        }

        // We usually own the last input (a click on the dock), which lets SetForegroundWindow work;
        // the ALT tap is the fallback when Windows' foreground lock still refuses.
        public static void Foreground(IntPtr h)
        {
            if (N.SetForegroundWindow(h) && N.GetForegroundWindow() == h) return;
            N.keybd_event(0x12, 0, 0, UIntPtr.Zero);
            N.SetForegroundWindow(h);
            N.keybd_event(0x12, 0, 2, UIntPtr.Zero);
        }

        public static void Activate(IntPtr h)
        {
            if (N.GetForegroundWindow() == h) { N.ShowWindow(h, 6); return; } // SW_MINIMIZE
            if (N.IsIconic(h)) N.ShowWindow(h, 9); // SW_RESTORE
            Foreground(h);
        }

        // Win + key, e.g. N for the notification center, like clicking the clock on the real taskbar.
        public static void WinKey(byte vk)
        {
            N.keybd_event(0x5B, 0, 0, UIntPtr.Zero);
            N.keybd_event(vk, 0, 0, UIntPtr.Zero);
            N.keybd_event(vk, 0, 2, UIntPtr.Zero);
            N.keybd_event(0x5B, 0, 2, UIntPtr.Zero);
        }

        public static void Launch(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception e) { Taskbar.Log(e); }
        }

        // The shell draws a shortcut arrow on .lnk icons; the target's own icon has none.
        static string LinkTarget(string lnk)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
                string target = (string)sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) ?? "";
                Marshal.ReleaseComObject(sc);
                Marshal.ReleaseComObject(sh);
                return target;
            }
            catch (Exception) { return ""; }
        }

        public static List<Pin> Pinned()
        {
            var list = new List<Pin>();
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
            if (!Directory.Exists(dir)) return list;
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = t == null ? null : Activator.CreateInstance(t);
            foreach (string f in Directory.GetFiles(dir, "*.lnk"))
            {
                string exe = "";
                try
                {
                    object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { f });
                    exe = (string)sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) ?? "";
                    Marshal.ReleaseComObject(sc);
                }
                catch (Exception e) { Taskbar.Log(e); }
                list.Add(new Pin { Name = Path.GetFileNameWithoutExtension(f), Lnk = f, Exe = exe });
            }
            if (sh != null) Marshal.ReleaseComObject(sh);
            return list;
        }

        public static int Cpu()
        {
            long idle, kernel, user;
            N.GetSystemTimes(out idle, out kernel, out user);
            long total = kernel + user; // kernel time includes idle time
            long di = idle - lastIdle, dt = total - lastTotal;
            lastIdle = idle; lastTotal = total;
            return dt > 0 ? (int)Math.Round(100.0 * (dt - di) / dt) : 0;
        }

        public static int Ram()
        {
            var m = new N.MEMSTATUS();
            m.dwLength = (uint)Marshal.SizeOf(typeof(N.MEMSTATUS));
            N.GlobalMemoryStatusEx(ref m);
            return (int)m.dwMemoryLoad;
        }

        // Shell icon without the shortcut arrow; works for .lnk, .exe and shell targets.
        public static Bitmap IconOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Bitmap b;
            if (icons.TryGetValue(path, out b)) return b;
            string target = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? LinkTarget(path) : "";
            var fi = new N.SHFILEINFO();
            N.SHGetFileInfo(File.Exists(target) ? target : path, 0, ref fi, (uint)Marshal.SizeOf(typeof(N.SHFILEINFO)), 0x100); // SHGFI_ICON | SHGFI_LARGEICON
            if (fi.hIcon != IntPtr.Zero)
            {
                using (Icon ic = Icon.FromHandle(fi.hIcon)) b = ic.ToBitmap();
                N.DestroyIcon(fi.hIcon);
            }
            icons[path] = b;
            return b;
        }
    }

    // One dock per monitor, in the strip that monitor's taskbar used. Tiles come from Config.Items, in order.
    partial class Dock : Form
    {
        class Hit { public Rectangle R; public Action<MouseButtons> Down, Up; public Action<int> Wheel; public string Tip; public bool Right; }
        class Seg { public int W; public Action<Graphics, Rectangle> Draw; }
        class AppItem { public string Title; public Bitmap Icon; public bool Running, Active; public Action Click; }

        static readonly int TaskbarCreatedMsg = N.RegisterWindowMessage("TaskbarCreated");
        readonly Screen screen;
        readonly bool primary;
        readonly ToolTip tip = new ToolTip();
        readonly List<Hit> hits = new List<Hit>();
        Bitmap buf;
        int H, hover = -1;
        bool reserved;
        Form popup;
        string popupKey, justClosed;

        static int S(float v) { return Glass.S(v); }

        public Dock(Screen s)
        {
            screen = s;
            primary = s.Primary;
            H = Taskbar.HeightOn(s.Bounds);
            if (H < S(24) || H > S(120)) H = S(48);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "Lilbar";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            tip.ShowAlways = true;
            Bounds = new Rectangle(s.Bounds.X, s.Bounds.Bottom - H, S(48), H);
        }

        public bool IsPrimary { get { return primary; } }
        public Rectangle Strip { get { Rectangle b = screen.Bounds; return new Rectangle(b.X, b.Bottom - H, b.Width, H); } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x8; // TOOLWINDOW | NOACTIVATE | TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Glass.MakeClear(Handle); }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (Taskbar.WasAutoHide) { Taskbar.Reserve(Handle, Strip); reserved = true; }
            Redraw();
        }

        // Explorer restarted (or our own broadcast): hide its taskbars again.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TaskbarCreatedMsg && primary) Manager.OnTaskbarCreated();
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ClosePopup();
            if (reserved) Taskbar.Unreserve(Handle);
            reserved = false;
            if (!Manager.Rebuilding) Manager.Quit(this); // closed from outside (taskkill, logoff): quit cleanly
            base.OnFormClosing(e);
        }

        bool AddHit(Rectangle r, Action<MouseButtons> down, Action<MouseButtons> up, string tipText, bool right, Action<int> wheel = null)
        {
            hits.Add(new Hit { R = r, Down = down, Up = up, Tip = tipText, Right = right, Wheel = wheel });
            return hits.Count - 1 == hover;
        }

        Rectangle ToScreen(Rectangle r) { return new Rectangle(Left + r.X, Top + r.Y, r.Width, r.Height); }

        public void Redraw()
        {
            int th = H - S(8), slot = th - S(6);
            var segs = new List<Seg>();
            foreach (ItemCfg it in Config.Current.Items)
            {
                if (!it.On) continue;
                Seg sg = Build(it.Id, th, slot);
                if (sg != null) segs.Add(sg);
            }
            // shortcut: tiles that don't fit the screen width are dropped from the right; a second row would be the upgrade
            int room = screen.Bounds.Width - S(8), used = S(8) - S(6);
            for (int i = 0; i < segs.Count; i++)
            {
                used += segs[i].W + S(6);
                if (used > room) { segs.RemoveRange(i, segs.Count - i); break; }
            }
            if (segs.Count == 0) // keep something to right-click
                segs.Add(new Seg { W = slot + S(8), Draw = (g, r) => { Glass.Tile(g, r); Glass.Center(g, "☰", Glass.Glyph, Color.White, r); AddHit(r, null, b => ShowMenu(), "Lilbar", false); } });

            int pad = S(4), gap = S(6), W = pad * 2 - gap;
            foreach (Seg sg in segs) W += sg.W + gap;
            if (buf == null || buf.Width != W || buf.Height != H)
            {
                if (buf != null) buf.Dispose();
                buf = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            }
            Rectangle sb = screen.Bounds;
            int x0 = Config.Current.Align == "left" ? sb.X + S(4) : sb.X + (sb.Width - W) / 2;
            if (Left != x0 || Top != sb.Bottom - H || Width != W || Height != H) SetBounds(x0, sb.Bottom - H, W, H);

            hits.Clear();
            using (Graphics g = Glass.Begin(buf))
            {
                int x = pad;
                foreach (Seg sg in segs) { sg.Draw(g, new Rectangle(x, (H - th) / 2, sg.W, th)); x += sg.W + gap; }
            }
            Invalidate();
        }

        List<AppItem> Items()
        {
            var used = new HashSet<IntPtr>();
            var list = new List<AppItem>();
            foreach (Pin p in Model.OrderedPins(false))
            {
                string exe = p.Exe;
                List<Win> mine = exe.Length == 0 ? new List<Win>() : Model.Wins.FindAll(w => string.Equals(w.Exe, exe, StringComparison.OrdinalIgnoreCase));
                foreach (Win w in mine) used.Add(w.Hwnd);
                string lnk = p.Lnk;
                list.Add(new AppItem
                {
                    Title = p.Name, Icon = Sys.IconOf(exe.Length > 0 ? exe : lnk), Running = mine.Count > 0, Active = mine.Exists(w => w.Active),
                    Click = mine.Count > 0 ? (Action)(() => Sys.Activate(mine[0].Hwnd)) : () => Sys.Launch(lnk)
                });
            }
            foreach (Win w in Model.Wins)
            {
                if (used.Contains(w.Hwnd)) continue;
                IntPtr h = w.Hwnd;
                list.Add(new AppItem { Title = w.Title, Icon = Sys.IconOf(w.Exe), Running = true, Active = w.Active, Click = () => Sys.Activate(h) });
            }
            return list;
        }

        Seg AppsSeg(int slot)
        {
            List<AppItem> items = Items();
            if (items.Count == 0) return null;
            return new Seg
            {
                W = S(8) + items.Count * (slot + S(4)) - S(4),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    int ax = r.X + S(4), sy = r.Y + (r.Height - slot) / 2, icon = slot - S(10);
                    foreach (AppItem it in items)
                    {
                        var c = new Rectangle(ax, sy, slot, slot);
                        AppItem item = it;
                        bool hov = AddHit(c, null, b => item.Click(), item.Title, false);
                        if (item.Active || hov) Glass.Fill(g, c, S(8), Color.FromArgb(item.Active ? 56 : 36, 255, 255, 255));
                        if (item.Icon != null) g.DrawImage(item.Icon, new Rectangle(c.X + (slot - icon) / 2, c.Y + (slot - icon) / 2 - S(1), icon, icon));
                        else Glass.Center(g, item.Title.Substring(0, 1), Glass.Value, Color.White, c);
                        if (item.Running)
                            using (var dot = new SolidBrush(Glass.Blue)) g.FillEllipse(dot, c.X + c.Width / 2 - S(2), c.Bottom - S(4), S(4), S(4));
                        ax += slot + S(4);
                    }
                }
            };
        }

        Seg FolderSeg(int slot)
        {
            List<string> apps = Config.Current.FolderApps;
            string name = Config.Current.FolderName;
            return new Seg
            {
                W = slot + S(8),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    bool hov = AddHit(r, null, b =>
                    {
                        if (apps.Count == 0) { Manager.OpenSettings(3); return; }
                        var items = new List<PopItem>();
                        foreach (string p in apps)
                        {
                            string path = p;
                            items.Add(new PopItem { Icon = Sys.IconOf(path), Tip = Path.GetFileNameWithoutExtension(path), Up = bb => { ClosePopup(); Sys.Launch(path); } });
                        }
                        TogglePopup("folder", name, items, true, ToScreen(r));
                    }, apps.Count == 0 ? name + ": bấm để chọn app" : name, false);
                    if (hov || popupKey == "folder") Glass.Fill(g, r, S(10), Color.FromArgb(30, 255, 255, 255));
                    if (apps.Count == 0) { Glass.Center(g, "+", Glass.Big, Glass.Dim, r); return; }
                    int m = S(14), gp = S(3), ox = r.X + (r.Width - (2 * m + gp)) / 2, oy = r.Y + (r.Height - (2 * m + gp)) / 2;
                    for (int i = 0; i < Math.Min(4, apps.Count); i++)
                    {
                        Bitmap ic = Sys.IconOf(apps[i]);
                        if (ic != null) g.DrawImage(ic, new Rectangle(ox + (i % 2) * (m + gp), oy + (i / 2) * (m + gp), m, m));
                    }
                }
            };
        }

        Seg SystemSeg(int slot)
        {
            return new Seg
            {
                W = slot * 2 + S(24),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.Launch("taskmgr.exe"), "CPU " + Model.Cpu + "%, RAM " + Model.Ram + "%\nBấm để mở Task Manager", false);
                    int sy = r.Y + (r.Height - slot) / 2;
                    Glass.Ring(g, new Rectangle(r.X + S(8), sy, slot, slot), Model.Cpu, "CPU");
                    Glass.Ring(g, new Rectangle(r.X + S(16) + slot, sy, slot, slot), Model.Ram, "RAM");
                }
            };
        }

        static string Until(DateTime reset)
        {
            TimeSpan t = reset - DateTime.Now;
            if (t.TotalMinutes < 1) return "now";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + "m";
            if (t.TotalHours < 24) return (int)t.TotalHours + "h" + t.Minutes.ToString("00");
            return reset.Day + "/" + reset.Month;
        }

        static string ClaudeTip()
        {
            var s = new StringBuilder("Claude Code");
            if (Claude.Session != null) s.Append("\nPhiên 5 giờ: ").Append(Math.Round(Claude.Session.Pct)).Append("%, đặt lại lúc ").Append(Claude.Session.Reset.ToString("HH:mm"));
            if (Claude.Week != null) s.Append("\nTuần: ").Append(Math.Round(Claude.Week.Pct)).Append("%, đặt lại ").Append(Claude.Week.Reset.ToString("HH:mm dd/MM"));
            if (Claude.Error != null) s.Append("\n").Append(Claude.Error);
            s.Append("\nBấm để cập nhật");
            return s.ToString();
        }

        Seg ClaudeSeg(int th)
        {
            if (!Claude.Available) return null;
            return new Seg
            {
                W = S(172),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => { Claude.RefreshNow(); Manager.Tick(); }, ClaudeTip(), false);
                    if (Claude.Session == null && Claude.Week == null)
                    {
                        Glass.Center(g, "Claude", Glass.Row, Color.White, new Rectangle(r.X, r.Y + S(3), r.Width, th / 2 - S(2)));
                        Glass.Center(g, Claude.Error ?? "Đang tải…", Glass.Date, Glass.Dim, new Rectangle(r.X + S(4), r.Y + th / 2, r.Width - S(8), th / 2 - S(3)));
                    }
                    else
                    {
                        UsageRow(g, r, r.Y + S(3), "5h", Claude.Session);
                        UsageRow(g, r, r.Y + th / 2 + S(1), "7d", Claude.Week);
                    }
                }
            };
        }

        // One "5h ████░░ 33%  2h59" row of the Claude tile.
        static void UsageRow(Graphics g, Rectangle tile, int y, string label, Claude.Limit l)
        {
            int rowH = S(15), x = tile.X + S(8);
            Glass.Text(g, label, Glass.Row, Glass.Dim, new Rectangle(x, y, S(20), rowH), StringAlignment.Near);
            var bar = new Rectangle(x + S(20), y + rowH / 2 - S(2), S(60), S(4));
            Glass.Fill(g, bar, S(2), Color.FromArgb(50, 255, 255, 255));
            if (l == null) return;
            double p = Math.Max(0, Math.Min(100, l.Pct));
            Color c = p >= 90 ? Color.FromArgb(255, 255, 69, 58) : p >= 70 ? Color.FromArgb(255, 255, 159, 10) : Color.FromArgb(255, 52, 199, 89);
            if (p > 0) Glass.Fill(g, new Rectangle(bar.X, bar.Y, Math.Max(S(4), (int)(bar.Width * p / 100)), bar.Height), S(2), c);
            Glass.Text(g, Math.Round(p) + "%", Glass.Row, Color.White, new Rectangle(bar.Right, y, S(36), rowH), StringAlignment.Far);
            if (l.Reset > DateTime.MinValue)
                Glass.Text(g, Until(l.Reset), Glass.Row, Glass.Dim, new Rectangle(bar.Right + S(36), y, tile.Right - bar.Right - S(36) - S(8), rowH), StringAlignment.Far);
        }

        Seg TraySeg()
        {
            List<TrayIcon> all = TrayHost.Visible();
            List<TrayIcon> pinned = all.FindAll(TrayHost.IsPinned);
            int cell = S(28), arrow = S(26);
            return new Seg
            {
                W = S(4) + pinned.Count * cell + arrow + S(4),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    int x = r.X + S(4), ic = S(18), cy = r.Y + r.Height / 2;
                    foreach (TrayIcon t in pinned)
                    {
                        var c = new Rectangle(x, r.Y + S(3), cell, r.Height - S(6));
                        TrayIcon ti = t;
                        if (primary) ti.Rect = ToScreen(c);
                        bool hov = AddHit(c, b => TrayHost.MouseDown(ti, b == MouseButtons.Right), b => TrayHost.MouseUp(ti, b == MouseButtons.Right), ti.Tip, true);
                        if (hov) Glass.Fill(g, c, S(8), Color.FromArgb(36, 255, 255, 255));
                        g.DrawImage(ti.Icon, new Rectangle(c.X + (cell - ic) / 2, cy - ic / 2, ic, ic));
                        x += cell;
                    }
                    var a = new Rectangle(x, r.Y + S(3), arrow, r.Height - S(6));
                    Rectangle aScreen = ToScreen(a);
                    if (primary) foreach (TrayIcon t in all) if (!pinned.Contains(t)) t.Rect = aScreen;
                    bool open = popupKey == "tray";
                    bool ah = AddHit(a, null, b =>
                    {
                        var items = new List<PopItem>();
                        foreach (TrayIcon t in TrayHost.Visible())
                        {
                            if (TrayHost.IsPinned(t)) continue;
                            TrayIcon ti = t;
                            items.Add(new PopItem { Icon = ti.Icon, Tip = ti.Tip, Right = true, Down = bb => TrayHost.MouseDown(ti, bb == MouseButtons.Right), Up = bb => TrayHost.MouseUp(ti, bb == MouseButtons.Right) });
                        }
                        TogglePopup("tray", null, items, true, aScreen);
                    }, "Hiện biểu tượng ẩn", false);
                    if (ah || open) Glass.Fill(g, a, S(8), Color.FromArgb(36, 255, 255, 255));
                    Glass.Chevron(g, a, open);
                }
            };
        }

        Seg BatterySeg()
        {
            PowerStatus ps = SystemInformation.PowerStatus;
            bool hasBattery = (ps.BatteryChargeStatus & (BatteryChargeStatus.NoSystemBattery | BatteryChargeStatus.Unknown)) == 0;
            int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
            bool charging = ps.PowerLineStatus == PowerLineStatus.Online;
            if (!hasBattery || (charging && pct >= 100)) return null;
            return new Seg
            {
                W = S(58),
                Draw = (g, r) =>
                {
                    Glass.Fill(g, r, S(10), pct <= 20 && !charging ? Color.FromArgb(220, 227, 67, 43) : Color.FromArgb(220, 47, 139, 255));
                    AddHit(r, null, b => Sys.WinKey(0x41), "Pin " + pct + "%" + (charging ? ", đang sạc" : ""), false); // Win+A: quick settings
                    if (charging)
                    {
                        Glass.Center(g, "⚡", Glass.Bolt, Color.White, new Rectangle(r.X + S(4), r.Y, S(18), r.Height));
                        Glass.Center(g, pct.ToString(), Glass.Batt, Color.White, new Rectangle(r.X + S(18), r.Y, r.Width - S(22), r.Height));
                    }
                    else Glass.Center(g, pct.ToString(), Glass.Batt, Color.White, r);
                }
            };
        }

        static string WeatherTip()
        {
            var s = new StringBuilder(Glass.DayName(DateTime.Now.DayOfWeek) + ", " + DateTime.Now.ToString("dd/MM/yyyy"));
            if (!double.IsNaN(Weather.Temp))
            {
                s.Append("\n").Append(Weather.City ?? "").Append(Weather.City != null ? ": " : "").Append(Weather.Describe(Weather.Code)).Append(", ").Append(Math.Round(Weather.Temp)).Append("°C");
                if (!double.IsNaN(Weather.Hi)) s.Append("\nCao ").Append(Math.Round(Weather.Hi)).Append("°, thấp ").Append(Math.Round(Weather.Lo)).Append("°");
            }
            if (Weather.Error != null) s.Append("\n").Append(Weather.Error);
            s.Append("\nBấm để mở thông báo và lịch");
            return s.ToString();
        }

        // Weekday and date on the left, time and current weather on the right.
        Seg TodaySeg()
        {
            DateTime now = DateTime.Now;
            string day = Glass.DayName(now.DayOfWeek).ToUpper(new CultureInfo("vi-VN"));
            string date = now.Day + " tháng " + now.Month, time = now.ToString("HH:mm");
            bool hasW = !double.IsNaN(Weather.Temp) && Weather.Code >= 0;
            string temp = hasW ? Math.Round(Weather.Temp) + "°" : "";
            string glyph = hasW ? Weather.Glyph(Weather.Code) : "";
            int lw = Math.Max(Glass.Measure(day, Glass.Day), Glass.Measure(date, Glass.DateBig)) + S(18);
            int gw = hasW ? Glass.Measure(glyph, Glass.Glyph) : 0, tw = hasW ? Glass.Measure(temp, Glass.Date) : 0;
            int rw = Math.Max(Glass.Measure(time, Glass.Time), gw + tw) + S(18);
            return new Seg
            {
                W = lw + rw,
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), WeatherTip(), false); // Win+N
                    Glass.Text(g, day, Glass.Day, Color.FromArgb(255, 255, 82, 70), new Rectangle(r.X + S(10), r.Y + S(3), lw, S(15)), StringAlignment.Near);
                    Glass.Text(g, date, Glass.DateBig, Color.White, new Rectangle(r.X + S(10), r.Y + S(17), lw, S(20)), StringAlignment.Near);
                    using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255))) g.DrawLine(pen, r.X + lw, r.Y + S(8), r.X + lw, r.Bottom - S(8));
                    int right = r.Right - S(10);
                    Glass.Text(g, time, Glass.Time, Color.White, new Rectangle(r.X + lw, r.Y + S(2), right - r.X - lw, S(19)), StringAlignment.Far);
                    if (hasW)
                    {
                        Glass.Text(g, temp, Glass.Date, Glass.Dim, new Rectangle(right - tw - S(2), r.Y + S(21), tw + S(2), S(15)), StringAlignment.Far);
                        Glass.Text(g, glyph, Glass.Glyph, Weather.GlyphColor(Weather.Code), new Rectangle(right - tw - gw - S(2), r.Y + S(19), gw, S(18)), StringAlignment.Far);
                    }
                }
            };
        }

        Seg ClockSeg()
        {
            DateTime now = DateTime.Now;
            string time = now.ToString("HH:mm");
            string date = (now.DayOfWeek == DayOfWeek.Sunday ? "CN" : "THỨ " + ((int)now.DayOfWeek + 1)) + ", " + now.Day + " THG " + now.Month;
            int w = Math.Max(S(74), Math.Max(Glass.Measure(time, Glass.Time), Glass.Measure(date, Glass.Date))) + S(18);
            return new Seg
            {
                W = w,
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), "Bấm để mở thông báo và lịch", false);
                    Glass.Text(g, time, Glass.Time, Color.White, new Rectangle(r.X, r.Y + S(2), r.Width - S(9), S(19)), StringAlignment.Far);
                    Glass.Text(g, date, Glass.Date, Glass.Dim, new Rectangle(r.X, r.Y + S(20), r.Width - S(9), S(15)), StringAlignment.Far);
                }
            };
        }

        void ShowMenu()
        {
            var items = new List<PopItem>
            {
                new PopItem { Glyph = "⚙", Text = "Cài đặt Lilbar", Up = b => { ClosePopup(); Manager.OpenSettings(0); } },
                new PopItem { Glyph = "✕", Text = "Thoát Lilbar", Up = b => { ClosePopup(); Manager.Quit(null); } },
            };
            N.POINT p;
            N.GetCursorPos(out p);
            TogglePopup("menu", null, items, false, new Rectangle(p.X, Top, 1, H));
        }

        void TogglePopup(string key, string title, List<PopItem> items, bool grid, Rectangle anchor)
        {
            OpenPopup(key, () => new Popup(title, items, grid, anchor));
        }

        void OpenPopup(string key, Func<Form> make)
        {
            if (justClosed == key) { justClosed = null; return; } // this click closed it; don't reopen
            ClosePopup();
            Form p = make();
            popup = p;
            popupKey = key;
            p.FormClosed += delegate
            {
                if (popup != p) return;
                popup = null;
                popupKey = null;
                if (!IsDisposed) Redraw();
            };
            p.Show();
            Redraw();
        }

        void ClosePopup()
        {
            if (popup == null) return;
            Form p = popup;
            popup = null;
            popupKey = null;
            p.Close();
        }

        protected override void OnPaint(PaintEventArgs e) { Glass.Blit(e, buf); }

        Hit HitAt(Point p) { return hits.Find(h => h.R.Contains(p)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = hits.FindIndex(x => x.R.Contains(e.Location));
            if (h == hover) return;
            hover = h;
            if (h >= 0 && !string.IsNullOrEmpty(hits[h].Tip)) tip.Show(hits[h].Tip, this, hits[h].R.X, -S(30) - S(16) * hits[h].Tip.Split('\n').Length, 4000);
            else tip.Hide(this);
            Redraw();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hover == -1) return;
            hover = -1;
            tip.Hide(this);
            Redraw();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // The dock never takes focus, so an open popup doesn't notice this click by itself.
            if (popup != null) { justClosed = popupKey; ClosePopup(); }
            Hit h = HitAt(e.Location);
            if (h != null && h.Down != null && (e.Button == MouseButtons.Left || h.Right)) h.Down(e.Button);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Hit h = HitAt(e.Location);
            if (h != null && h.Up != null && (e.Button == MouseButtons.Left || h.Right)) h.Up(e.Button);
            else if (e.Button == MouseButtons.Right) ShowMenu();
            justClosed = null;
            Manager.Refresh(); // show the result of the click now, not on the next tick
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Hit h = HitAt(e.Location);
            if (h != null && h.Wheel != null) { h.Wheel(e.Delta > 0 ? 1 : -1); Manager.Refresh(); }
        }
    }

    // Owns the docks, the tray host, the timers and the app's lifetime.
    static class Manager
    {
        public static bool Rebuilding;
        static readonly List<Dock> docks = new List<Dock>();
        static readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer(), hostTimer = new System.Windows.Forms.Timer();
        static SynchronizationContext ui;
        static NotifyIcon icon;
        static string lastSig;
        static bool quitting, shutDown;

        public static void Start()
        {
            ui = new WindowsFormsSynchronizationContext();
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Glass.Init(g.DpiX / 96f);
            Config.Load();
            Taskbar.CaptureAutoHide();
            TrayHost.Start();
            Taskbar.Hide();
            Sys.Cpu();
            Bg.Start();
            Model.Tick(PostRefresh);
            BuildDocks();
            icon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                Text = "Lilbar",
                ContextMenu = new ContextMenu(new[] { new MenuItem("Cài đặt Lilbar", delegate { OpenSettings(0); }), new MenuItem("Thoát Lilbar", delegate { Quit(null); }) }),
            };
            icon.Visible = true;
            Config.Changed += Refresh;
            SystemEvents.DisplaySettingsChanged += OnDisplays;
            timer.Interval = 1000;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            hostTimer.Interval = 250;
            hostTimer.Tick += delegate { TrayHost.KeepOnTop(); };
            hostTimer.Start();
            TrayHost.BroadcastTaskbarCreated(); // running apps re-register their tray icons with us
        }

        public static void Tick() { Model.Tick(PostRefresh); Refresh(); }

        static void OnDisplays(object s, EventArgs e) { ui.Post(delegate { if (!quitting) BuildDocks(); }, null); }

        public static void PostRefresh() { if (ui != null) ui.Post(delegate { Refresh(); }, null); }

        public static void Refresh()
        {
            if (quitting) return;
            string s = Model.Signature();
            if (s == lastSig) return; // nothing visible changed: no repaint
            lastSig = s;
            foreach (Dock d in docks) d.Redraw();
            SettingsForm.Ping();
        }

        static void BuildDocks()
        {
            Rebuilding = true;
            foreach (Dock d in docks) d.Close();
            docks.Clear();
            Rebuilding = false;
            Taskbar.Hide(); // a new monitor brings a new explorer taskbar
            foreach (Screen s in Screen.AllScreens)
            {
                var d = new Dock(s);
                docks.Add(d);
                d.Show();
                if (d.IsPrimary) TrayHost.SetRect(d.Strip);
            }
            lastSig = null;
            Refresh();
        }

        public static void OnTaskbarCreated() { if (!quitting) Taskbar.Hide(); }

        public static void OpenSettings(int tab) { SettingsForm.Open(tab); }

        // Timer and pomodoro alerts: a Windows notification from Lilbar's tray icon, plus a sound.
        public static void Notify(string title, string text)
        {
            if (icon != null) icon.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);
            System.Media.SystemSounds.Asterisk.Play();
        }

        public static void Quit(Dock closing)
        {
            if (quitting) return;
            quitting = true;
            timer.Stop();
            hostTimer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplays;
            SettingsForm.CloseIfOpen();
            Rebuilding = true;
            foreach (Dock d in docks.ToArray()) if (d != closing) d.Close();
            if (icon != null) icon.Visible = false;
            Shutdown();
            Application.ExitThread();
        }

        // Tray host first, so the broadcast makes apps re-register with explorer's tray.
        public static void Shutdown()
        {
            if (shutDown) return;
            shutDown = true;
            ScreenTime.Flush();
            TrayHost.Stop();
            Taskbar.Restore();
            TrayHost.BroadcastTaskbarCreated();
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            string arg = args.Length > 0 ? args[0] : "";
            if (arg == "--restore") { Taskbar.Restore(); TrayHost.BroadcastTaskbarCreated(); return 0; }
            if (arg == "--selftest") return SelfTest();

            bool fresh;
            using (var mutex = new Mutex(true, "Lilbar.Dock", out fresh))
            {
                if (!fresh) return 0; // already running
                N.SetProcessDPIAware();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // the 4.x runtime may default to older TLS
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Taskbar.Log(e.Exception); Manager.Quit(null); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Taskbar.Log(e.ExceptionObject as Exception); Manager.Shutdown(); };
                // The loop can end without the docks closing (taskkill posts WM_CLOSE to hidden helper windows too).
                try { Manager.Start(); Application.Run(); }
                finally { Manager.Shutdown(); }
            }
            return 0;
        }

        static int SelfTest()
        {
            int fails = 0;
            Action<bool, string> check = delegate(bool ok, string msg) { Console.WriteLine((ok ? "ok   " : "FAIL ") + msg); if (!ok) fails++; };
            check(Marshal.SizeOf(typeof(N.APPBARDATA)) == (IntPtr.Size == 8 ? 48 : 36), "APPBARDATA size " + Marshal.SizeOf(typeof(N.APPBARDATA)));
            check(Marshal.SizeOf(typeof(N.NOTIFYICONDATA)) == 956, "NOTIFYICONDATA size " + Marshal.SizeOf(typeof(N.NOTIFYICONDATA)));
            check(Marshal.SizeOf(typeof(N.SHELLTRAYDATA)) == 964, "SHELLTRAYDATA size " + Marshal.SizeOf(typeof(N.SHELLTRAYDATA)));
            check(Marshal.SizeOf(typeof(N.WINNOTIFYICONIDENTIFIER)) == 40, "WINNOTIFYICONIDENTIFIER size " + Marshal.SizeOf(typeof(N.WINNOTIFYICONIDENTIFIER)));
            N.RECT tr;
            check(N.GetWindowRect(Taskbar.Primary(), out tr) && tr.Bottom - tr.Top > 0, "taskbar height " + (tr.Bottom - tr.Top) + ", taskbars " + Taskbar.All().Count + ", screens " + Screen.AllScreens.Length);
            List<Win> w = Sys.Windows();
            check(w.Count > 0 && w.TrueForAll(x => x.Hwnd != IntPtr.Zero && x.Title.Length > 0), "windows " + w.Count);
            check(w.FindAll(x => x.Active).Count <= 1, "at most one active window");
            List<Pin> p = Sys.Pinned();
            check(p.TrueForAll(x => File.Exists(x.Lnk)), "pinned " + p.Count);
            Sys.Cpu();
            Thread.Sleep(300);
            int c = Sys.Cpu(), r = Sys.Ram();
            check(c >= 0 && c <= 100 && r > 0 && r <= 100, "cpu " + c + " ram " + r);
            check(Sys.IconOf(Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe")) != null, "icon of explorer.exe");
            Claude.Parse("{\"five_hour\":{\"utilization\":33.0,\"resets_at\":\"2026-11-04T05:30:00.230316+00:00\"},\"seven_day\":{\"utilization\":3.0,\"resets_at\":null}}");
            check(Claude.Session != null && Claude.Session.Pct == 33 && Claude.Session.Reset.ToUniversalTime().Hour == 5, "claude usage parse");
            Weather.Parse(new JavaScriptSerializer().Deserialize<Dictionary<string, object>>("{\"current\":{\"temperature_2m\":31.4,\"weather_code\":2},\"daily\":{\"temperature_2m_max\":[33.1],\"temperature_2m_min\":[25.0]}}"));
            check(Weather.Temp == 31.4 && Weather.Code == 2 && Weather.Hi == 33.1 && Weather.Lo == 25.0 && Weather.Glyph(2) == "⛅", "weather parse");
            Config cfg = Config.Normalize(new JavaScriptSerializer().Deserialize<Config>("{\"Items\":[{\"Id\":\"clock\",\"On\":true},{\"Id\":\"bogus\",\"On\":true},{\"Id\":\"clock\",\"On\":false}]}"));
            check(cfg.Items.Count == Catalog.All.Length && cfg.Items[0].Id == "clock" && cfg.Items[0].On && cfg.Align == "center", "config normalize keeps order, drops unknown and duplicates, adds missing");
            string sys = TrayHost.ExpandKnownFolder("{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\\x.exe");
            check(sys.EndsWith("\\System32\\x.exe", StringComparison.OrdinalIgnoreCase), "known folder path " + sys);
            GitHub.ParseCalendar("{\"data\":{\"viewer\":{\"login\":\"me\",\"contributionsCollection\":{\"contributionCalendar\":{\"totalContributions\":7,\"weeks\":[{\"contributionDays\":[{\"contributionCount\":0},{\"contributionCount\":2},{\"contributionCount\":3}]},{\"contributionDays\":[{\"contributionCount\":2},{\"contributionCount\":0}]}]}}}}}");
            check(GitHub.Login == "me" && GitHub.Total == 7 && GitHub.Days.Length == 5 && GitHub.Streak == 3, "github calendar parse, streak skips an empty today");
            check(Secret.Unprotect(Secret.Protect("ghp_test")) == "ghp_test" && Secret.Unprotect("garbage") == null, "token DPAPI round trip");
            var seq = new StringBuilder();
            for (int i = 0; i < 9; i++) { seq.Append(Pomodoro.Phase); Pomodoro.Skip(); }
            check(seq.ToString() == "010101020", "pomodoro cycle " + seq);
            Pomodoro.Reset();
            check(Net.Fmt(1500000) == "1.5 MB/s" && Net.Fmt(2400) == "2 KB/s" && Net.Fmt(12) == "12 B/s", "network speed format");
            check(Array.TrueForAll(Cities.All, z => z[0] == "" || TimeZoneInfo.FindSystemTimeZoneById(z[0]) != null), "world clock time zones exist");
            string ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART:" + DateTime.UtcNow.AddHours(2).ToString("yyyyMMdd'T'HHmmss'Z'") + "\r\nSUMMARY:Họp\\, nhóm\r\n dự án\r\nEND:VEVENT\r\n" +
                "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:" + DateTime.Today.AddDays(-14).ToString("yyyyMMdd") + "\r\nRRULE:FREQ=WEEKLY;COUNT=10\r\nSUMMARY:Hằng tuần\r\nEND:VEVENT\r\n" +
                "BEGIN:VEVENT\r\nDTSTART:20000101T090000\r\nSUMMARY:Cũ\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
            List<Calendar.Ev> evs = Calendar.Parse(ics, DateTime.Now, 7);
            // weekly event from 14 days ago lands today and in 7 days; the year-2000 one is dropped
            check(evs.Count == 3 && evs.Exists(e => e.Title == "Họp, nhómdự án" && !e.AllDay) && evs.FindAll(e => e.Title == "Hằng tuần" && e.AllDay).Count == 2 && evs.Exists(e => e.Start.Date == DateTime.Today), "ics parse: unfold, escapes, weekly repeat, past dropped (" + evs.Count + ")");
            Currency.Parse("USD", "{\"result\":\"success\",\"rates\":{\"USD\":1,\"VND\":25400.5}}");
            check(Currency.Rates["VND"] == 25400.5 && Currency.Base == "USD", "currency rates parse");
            Weather.Parse(new JavaScriptSerializer().Deserialize<Dictionary<string, object>>("{\"current\":{\"temperature_2m\":30,\"weather_code\":3,\"wind_speed_10m\":12.5,\"relative_humidity_2m\":70}," +
                "\"hourly\":{\"time\":[\"2026-11-05T14:00\",\"2026-11-05T15:00\"],\"temperature_2m\":[30,29],\"weather_code\":[3,61]}," +
                "\"daily\":{\"time\":[\"2026-11-05\",\"2026-11-06\"],\"temperature_2m_max\":[33,31],\"temperature_2m_min\":[24,23],\"weather_code\":[3,61],\"precipitation_probability_max\":[10,80],\"sunrise\":[\"2026-11-05T05:58\"],\"sunset\":[\"2026-11-05T17:16\"]}}"));
            check(Weather.Hours.Count == 2 && Weather.Hours[1].Code == 61 && Weather.Days.Count == 2 && Weather.Days[1].Rain == 80 && Weather.Sunset.Hour == 17 && Weather.Wind == 12.5, "weather hourly, daily, sun parse");
            check(ScreenTime.Kind(@"C:\x\chrome.exe") == 0 && ScreenTime.Kind("Code.exe") == 1 && ScreenTime.Kind("Discord.exe") == 2 && ScreenTime.Kind("notepad.exe") == 5, "app kinds");
            check(Files.Size(3L << 30) == "3 GB" && Files.Size(5L << 20) == "5 MB", "size format");
            check(Array.TrueForAll(Catalog.All, ci => Array.IndexOf(Catalog.Groups, ci.Group) >= 0) && new HashSet<string>(Array.ConvertAll(Catalog.All, ci => ci.Id)).Count == Catalog.All.Length, "catalog ids unique, groups known");
            Stocks.Quote sq = Stocks.Parse("AAPL", "{\"chart\":{\"result\":[{\"meta\":{\"currency\":\"USD\",\"regularMarketPrice\":210.5,\"chartPreviousClose\":200},\"indicators\":{\"quote\":[{\"close\":[201,null,205.5,210.5]}]}}]}}");
            check(sq.Price == 210.5 && sq.Line.Count == 3 && Math.Abs(Stocks.Change(sq) - 5.25) < 1e-9, "stock chart parse");
            Crypto.Parse("BTCUSDT", "{\"symbol\":\"BTCUSDT\",\"lastPrice\":\"101234.50\",\"priceChangePercent\":\"-1.25\"}");
            check(Crypto.Price == 101234.5 && Crypto.ChangePct == -1.25 && Crypto.Pair("BTCUSDT") == "BTC/USDT", "crypto ticker parse");
            LeetCode.Parse("me", "{\"data\":{\"matchedUser\":{\"submitStatsGlobal\":{\"acSubmissionNum\":[{\"difficulty\":\"All\",\"count\":120},{\"difficulty\":\"Easy\",\"count\":60},{\"difficulty\":\"Medium\",\"count\":50},{\"difficulty\":\"Hard\",\"count\":10}]},\"userCalendar\":{\"streak\":4}}}}");
            check(LeetCode.All == 120 && LeetCode.Hard == 10 && LeetCode.Streak == 4, "leetcode parse");
            List<Wallpapers.Pic> pics = Wallpapers.Parse("{\"images\":[{\"url\":\"/th?id=OHR.Test_1920x1080.jpg&rf=x\",\"copyright\":\"A (B)\"},{\"url\":\"https://evil.example/x.jpg\"}]}");
            check(pics.Count == 1 && pics[0].Url.StartsWith("https://www.bing.com/th?id=") && pics[0].Id == "OHR.Test_1920x1080.jpg", "bing images parse keeps only bing.com paths");
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool more; string last;
            List<Sale> st = Revenue.ParseStripe("{\"has_more\":true,\"data\":[{\"id\":\"txn_1\",\"type\":\"charge\",\"net\":4950,\"currency\":\"usd\",\"created\":" + nowUnix + "},{\"id\":\"txn_2\",\"type\":\"payout\",\"net\":-9000,\"currency\":\"usd\",\"created\":" + nowUnix + "},{\"id\":\"txn_3\",\"type\":\"charge\",\"net\":100000,\"currency\":\"vnd\",\"created\":" + nowUnix + "}]}", out more, out last);
            check(st.Count == 2 && st[0].Net == 49.5 && st[1].Net == 100000 && more && last == "txn_3", "stripe: payouts skipped, zero-decimal currency, paging");
            string iso = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            var src = new Revenue.Src { Id = "t", Name = "T" };
            Revenue.Aggregate(src, Revenue.ParseLemon("{\"meta\":{\"page\":{\"currentPage\":1,\"lastPage\":1}},\"data\":[{\"attributes\":{\"created_at\":\"" + iso + "\",\"total\":2000,\"tax\":200,\"refunded_amount\":300,\"currency\":\"USD\"}}]}", out more));
            check(src.Total30 == 15 && src.Days14[13] == 15 && src.Currency == "USD" && !more, "lemon squeezy: total minus tax and refunds, today's bucket");
            string next;
            List<Sale> pd = Revenue.ParsePaddle("{\"data\":[{\"created_at\":\"" + iso + "\",\"currency_code\":\"EUR\",\"details\":{\"totals\":{\"earnings\":\"1234\"}}}],\"meta\":{\"pagination\":{\"has_more\":true,\"next\":\"https://api.paddle.com/transactions?after=x\"}}}", out next);
            check(pd.Count == 1 && pd[0].Net == 12.34 && next.StartsWith("https://api.paddle.com/"), "paddle earnings and paging link");
            string gk;
            List<Sale> gr = Revenue.ParseGumroad("{\"sales\":[{\"created_at\":\"" + iso + "\",\"price\":1000,\"gumroad_fee\":100},{\"created_at\":\"" + iso + "\",\"price\":500,\"refunded\":true}],\"next_page_key\":\"k2\"}", out gk);
            check(gr.Count == 1 && gr[0].Net == 9 && gk == "k2", "gumroad: fee removed, refunds skipped");
            check(Revenue.ParseShopify("{\"orders\":[{\"created_at\":\"" + iso + "\",\"current_total_price\":\"110.00\",\"current_total_tax\":\"10.00\",\"currency\":\"USD\"},{\"created_at\":\"" + iso + "\",\"total_price\":\"50\",\"cancelled_at\":\"x\"}]}")[0].Net == 100, "shopify net of tax, cancelled skipped");
            var stat = new Stat();
            Svc.ParsePlausible("{\"results\":{\"visitors\":{\"value\":12345,\"change\":12}}}", stat);
            check(stat.Value == "12.3K" && stat.Sub.Contains("+12%"), "plausible aggregate");
            Svc.ParseLinear("{\"data\":{\"viewer\":{\"assignedIssues\":{\"nodes\":[{\"state\":{\"type\":\"started\"}},{\"state\":{\"type\":\"unstarted\"}}]}}}}", stat);
            check(stat.Value == "2" && stat.Sub.Contains("1 đang làm"), "linear assigned issues");
            Svc.ParseSupabase("[{\"name\":\"auth\",\"healthy\":true},{\"name\":\"db\",\"healthy\":false}]", stat);
            check(stat.Value == "1/2" && stat.Sub.Contains("db"), "supabase health");
            Svc.ParseDeepSeek("{\"is_available\":true,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"4.20\"}]}", stat);
            check(stat.Value == "$4.20", "deepseek balance");
            Svc.ParseTikTok("<script>{\"stats\":{\"followerCount\":2500000,\"x\":1}}</script>", "me", stat);
            check(stat.Value == "2.5M", "tiktok follower count");
            check(Imap.ParseStatus(new List<string> { "* STATUS \"INBOX\" (UNSEEN 7)", "a2 OK done" }) == 7, "imap STATUS unseen");
            Audio.Tick();
            check(Audio.Level >= 0 && Audio.Level <= 100, "volume " + Audio.Level + "% on " + Audio.Device);
            Thermal.Poll();
            check(Thermal.C > -50 && Thermal.C < 130, "thermal zone " + Thermal.C + "C");
            Net.Tick(); Thread.Sleep(500); Net.Tick();
            check(Net.Down >= 0 && Net.History.Count == 1, "network sample " + Net.Fmt(Net.Down));
            try { Media.Poll(); check(true, "media session: " + (Media.Has ? Media.Title : "none playing")); }
            catch (Exception e) { check(false, "media " + e.Message); }
            try { Radios.Poll(); check(true, "radios: " + string.Join(", ", Radios.List.ConvertAll(x => (x.WiFi ? "Wi-Fi " : "Bluetooth ") + (x.On ? "on" : "off")).ToArray())); }
            catch (Exception e) { check(false, "radios " + e.Message); }
            return fails;
        }
    }
}
