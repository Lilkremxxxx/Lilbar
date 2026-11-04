// Lilbar: a clear-glass dock that replaces the Windows taskbar.
// Build with build.cmd (csc.exe that ships with Windows, so C# 5 only: no $"", no ?., no => members).
// Lilbar.exe --restore   puts the Windows taskbar back after a hard kill.
// Lilbar.exe --selftest  checks the Win32 plumbing and returns the number of failures.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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

namespace Lilbar
{
    static class N
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr h, ref WCAD data);
        [DllImport("gdi32.dll")] public static extern int SetDIBitsToDevice(IntPtr hdc, int x, int y, uint w, uint h, int sx, int sy, uint start, uint lines, IntPtr bits, ref BITMAPINFOHEADER bmi, uint usage);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder sb, ref uint size);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMSTATUS m);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
        [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public uint cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
        [StructLayout(LayoutKind.Sequential)] public struct MEMSTATUS { public uint dwLength, dwMemoryLoad; public ulong total, avail, totalPage, availPage, totalVirtual, availVirtual, availExtended; }
        [StructLayout(LayoutKind.Sequential)] public struct ACCENT { public int AccentState, AccentFlags; public uint GradientColor; public int AnimationId; }
        [StructLayout(LayoutKind.Sequential)] public struct WCAD { public int Attribute; public IntPtr Data; public int SizeOfData; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFOHEADER { public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
    }

    // Hides the Windows taskbar and keeps it hidden: explorer re-shows it on some events (Start menu,
    // display changes), so a WinEvent hook on explorer's process hides it again at once.
    // Explorer keeps reserving its strip while hidden, and the dock sits exactly in that strip.
    static class Taskbar
    {
        // Written only by the older auto-hide build; still read so its leftovers get undone.
        static readonly string StateFile = Path.Combine(Path.GetTempPath(), "lilbar-taskbar-state.json");
        static N.WinEventProc onShow; // field keeps the delegate alive while native code holds it
        static IntPtr hook, tray;

        static N.APPBARDATA Abd(IntPtr h)
        {
            var d = new N.APPBARDATA();
            d.cbSize = (uint)Marshal.SizeOf(typeof(N.APPBARDATA));
            d.hWnd = h;
            d.uEdge = 3; // ABE_BOTTOM
            return d;
        }

        static IntPtr Tray() { return N.FindWindow("Shell_TrayWnd", null); }

        // Returns the taskbar's height in pixels; autoHide tells whether explorer reserves no strip.
        public static int Hide(out bool autoHide)
        {
            if (File.Exists(StateFile)) Restore();
            tray = Tray();
            var d = Abd(tray);
            autoHide = ((ulong)N.SHAppBarMessage(4, ref d) & 1) != 0; // ABM_GETSTATE & ABS_AUTOHIDE
            N.RECT r;
            N.GetWindowRect(tray, out r);
            N.ShowWindow(tray, 0);
            Watch();
            return r.Bottom - r.Top;
        }

        static void Watch()
        {
            Unwatch();
            uint pid;
            N.GetWindowThreadProcessId(tray, out pid);
            onShow = delegate(IntPtr hk, uint ev, IntPtr h, int obj, int child, uint thread, uint time)
            {
                if (h == tray && obj == 0) N.ShowWindow(h, 0);
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
            IntPtr t = Tray();
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
                var d = Abd(t);
                d.lParam = (IntPtr)state;
                N.SHAppBarMessage(10, ref d); // ABM_SETSTATE
            }
            N.ShowWindow(t, 5);
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

        public static void Activate(IntPtr h)
        {
            if (N.GetForegroundWindow() == h) { N.ShowWindow(h, 6); return; } // SW_MINIMIZE
            if (N.IsIconic(h)) N.ShowWindow(h, 9); // SW_RESTORE
            N.keybd_event(0x12, 0, 0, UIntPtr.Zero); // ALT press lifts the foreground lock for a non-focused process
            N.SetForegroundWindow(h);
            N.keybd_event(0x12, 0, 2, UIntPtr.Zero);
        }

        public static void Launch(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception e) { Taskbar.Log(e); }
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
            var fi = new N.SHFILEINFO();
            N.SHGetFileInfo(path, 0, ref fi, (uint)Marshal.SizeOf(typeof(N.SHFILEINFO)), 0x100); // SHGFI_ICON | SHGFI_LARGEICON
            if (fi.hIcon != IntPtr.Zero)
            {
                using (Icon ic = Icon.FromHandle(fi.hIcon)) b = ic.ToBitmap();
                N.DestroyIcon(fi.hIcon);
            }
            icons[path] = b;
            return b;
        }
    }

    class Dock : Form
    {
        class Hit { public Rectangle R; public Action Click; public string Tip; }
        class AppItem { public string Title; public Bitmap Icon; public bool Running, Active; public Action Click; }

        static readonly Color Blue = Color.FromArgb(255, 74, 155, 255);
        static readonly int TaskbarCreated = N.RegisterWindowMessage("TaskbarCreated");
        readonly float scale;
        readonly Font fTime, fDate, fValue, fLabel, fBatt, fBolt, fRow;
        readonly NotifyIcon tray;
        readonly ToolTip tip = new ToolTip();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly List<Hit> hits = new List<Hit>();
        readonly Bitmap measureBmp = new Bitmap(1, 1);
        List<Pin> pins = new List<Pin>();
        List<Win> wins = new List<Win>();
        Bitmap buf;
        int H, cpu, ram, tick, hover = -1;
        string sig = "";
        bool reserved, hidden;

        int S(float v) { return (int)Math.Round(v * scale); }

        public Dock()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            H = S(48);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "Lilbar";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);

            fTime = new Font("Segoe UI", S(14), FontStyle.Bold, GraphicsUnit.Pixel);
            fDate = new Font("Segoe UI", S(10), FontStyle.Regular, GraphicsUnit.Pixel);
            fValue = new Font("Segoe UI Semibold", S(10.5f), FontStyle.Regular, GraphicsUnit.Pixel);
            fLabel = new Font("Segoe UI", S(7), FontStyle.Regular, GraphicsUnit.Pixel);
            fBatt = new Font("Segoe UI", S(13), FontStyle.Bold, GraphicsUnit.Pixel);
            fBolt = new Font("Segoe UI Symbol", S(11), FontStyle.Regular, GraphicsUnit.Pixel);
            fRow = new Font("Segoe UI Semibold", S(9.5f), FontStyle.Regular, GraphicsUnit.Pixel);

            tray = new NotifyIcon();
            tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            tray.Text = "Lilbar";
            tray.ContextMenu = new ContextMenu(new[] { new MenuItem("Thoát Lilbar", delegate { Close(); }) });
            tray.Visible = true;

            tip.ShowAlways = true;
            timer.Interval = 1000;
            timer.Tick += delegate { Tick(); };
            pins = Sys.Pinned();
            Sys.Cpu();
        }

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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int none = unchecked((int)0xFFFFFFFE);
            N.DwmSetWindowAttribute(Handle, 34, ref none, 4); // DWMWA_BORDER_COLOR = none
            // Clear glass: per-pixel alpha with no blur; only the tiles are drawn.
            var accent = new N.ACCENT { AccentState = 2, AccentFlags = 2, GradientColor = 0 }; // TRANSPARENTGRADIENT
            int size = Marshal.SizeOf(typeof(N.ACCENT));
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, p, false);
                var d = new N.WCAD { Attribute = 19, Data = p, SizeOfData = size }; // WCA_ACCENT_POLICY
                N.SetWindowCompositionAttribute(Handle, ref d);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        void HideTaskbar()
        {
            bool autoHide;
            int h = Taskbar.Hide(out autoHide);
            if (h > S(24) && h < S(120)) H = h;
            hidden = true;
            if (autoHide && !reserved)
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                Taskbar.Reserve(Handle, new Rectangle(b.X, b.Bottom - H, b.Width, H));
                reserved = true;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            HideTaskbar();
            Tick();
            timer.Start();
        }

        // Explorer restarted: a new taskbar appeared, hide it again.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TaskbarCreated && hidden) HideTaskbar();
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            if (reserved) Taskbar.Unreserve(Handle);
            reserved = hidden = false;
            Taskbar.Restore();
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        static string DateText(DateTime d)
        {
            string day = d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "THỨ " + ((int)d.DayOfWeek + 1);
            return day + ", " + d.Day + " THG " + d.Month;
        }

        void Tick()
        {
            tick++;
            if (tick % 30 == 0) pins = Sys.Pinned();
            if (tick % 2 == 1) { cpu = Sys.Cpu(); ram = Sys.Ram(); }
            Claude.MaybeFetch(delegate { if (IsHandleCreated) BeginInvoke((Action)Tick); });
            wins = Sys.Windows();
            PowerStatus ps = SystemInformation.PowerStatus;
            var s = new StringBuilder();
            foreach (Win w in wins) s.Append(w.Hwnd).Append(w.Active ? '*' : '-').Append(w.Title).Append('|');
            s.Append(pins.Count).Append(cpu).Append('/').Append(ram).Append(DateTime.Now.ToString("HH:mm d"));
            s.Append(ps.BatteryLifePercent).Append(ps.PowerLineStatus);
            s.Append(Claude.Available).Append(Claude.Error).Append(LimitSig(Claude.Session)).Append(LimitSig(Claude.Week));
            if (s.ToString() == sig) return; // nothing visible changed: no repaint
            sig = s.ToString();
            Render();
        }

        static string LimitSig(Claude.Limit l) { return l == null ? "-" : l.Pct + "@" + l.Reset.ToString("HHmm"); }

        List<AppItem> Items()
        {
            var used = new HashSet<IntPtr>();
            var list = new List<AppItem>();
            foreach (Pin p in pins)
            {
                string exe = p.Exe;
                List<Win> mine = exe.Length == 0 ? new List<Win>() : wins.FindAll(w => string.Equals(w.Exe, exe, StringComparison.OrdinalIgnoreCase));
                foreach (Win w in mine) used.Add(w.Hwnd);
                string lnk = p.Lnk;
                list.Add(new AppItem
                {
                    Title = p.Name, Icon = Sys.IconOf(exe.Length > 0 ? exe : lnk), Running = mine.Count > 0, Active = mine.Exists(w => w.Active),
                    Click = mine.Count > 0 ? (Action)(() => Sys.Activate(mine[0].Hwnd)) : () => Sys.Launch(lnk)
                });
            }
            foreach (Win w in wins)
            {
                if (used.Contains(w.Hwnd)) continue;
                IntPtr h = w.Hwnd;
                list.Add(new AppItem { Title = w.Title, Icon = Sys.IconOf(w.Exe), Running = true, Active = w.Active, Click = () => Sys.Activate(h) });
            }
            return list;
        }

        static string Until(DateTime reset)
        {
            TimeSpan t = reset - DateTime.Now;
            if (t.TotalMinutes < 1) return "now";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + "m";
            if (t.TotalHours < 24) return (int)t.TotalHours + "h" + t.Minutes.ToString("00");
            return reset.Day + "/" + reset.Month;
        }

        string ClaudeTip()
        {
            var s = new StringBuilder("Claude Code");
            if (Claude.Session != null) s.Append("\nPhiên 5 giờ: ").Append(Math.Round(Claude.Session.Pct)).Append("%, đặt lại lúc ").Append(Claude.Session.Reset.ToString("HH:mm"));
            if (Claude.Week != null) s.Append("\nTuần: ").Append(Math.Round(Claude.Week.Pct)).Append("%, đặt lại ").Append(Claude.Week.Reset.ToString("HH:mm dd/MM"));
            if (Claude.Error != null) s.Append("\n").Append(Claude.Error);
            s.Append("\nBấm để cập nhật");
            return s.ToString();
        }

        void Render()
        {
            List<AppItem> items = Items();
            PowerStatus ps = SystemInformation.PowerStatus;
            bool hasBattery = (ps.BatteryChargeStatus & (BatteryChargeStatus.NoSystemBattery | BatteryChargeStatus.Unknown)) == 0;
            int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
            bool charging = ps.PowerLineStatus == PowerLineStatus.Online;
            bool showBatt = hasBattery && !(charging && pct >= 100);

            DateTime now = DateTime.Now;
            string time = now.ToString("HH:mm"), date = DateText(now);
            int clockText;
            using (Graphics m = Graphics.FromImage(measureBmp))
                clockText = (int)Math.Ceiling(Math.Max(m.MeasureString(time, fTime).Width, m.MeasureString(date, fDate).Width));

            int th = H - S(8), ty = (H - th) / 2, slot = th - S(6), pad = S(4), gap = S(6);
            int appW = items.Count == 0 ? 0 : S(8) + items.Count * (slot + S(4)) - S(4);
            int sysW = slot * 2 + S(24), claudeW = Claude.Available ? S(172) : 0;
            int batW = showBatt ? S(58) : 0, clockW = Math.Max(S(74), clockText) + S(18);
            int W = pad + (appW > 0 ? appW + gap : 0) + sysW + gap + (claudeW > 0 ? claudeW + gap : 0) + (showBatt ? batW + gap : 0) + clockW + pad;

            if (buf == null || buf.Width != W || buf.Height != H)
            {
                if (buf != null) buf.Dispose();
                buf = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            }
            if (Width != W || Height != H)
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                SetBounds(b.X + (b.Width - W) / 2, b.Bottom - H, W, H); // exactly the strip the taskbar occupied
            }

            hits.Clear();
            using (Graphics g = Graphics.FromImage(buf))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // ClearType needs an opaque background
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int x = pad, sy = (H - slot) / 2, icon = slot - S(10);
                if (appW > 0)
                {
                    Tile(g, new Rectangle(x, ty, appW, th));
                    int ax = x + S(4);
                    foreach (AppItem it in items)
                    {
                        var r = new Rectangle(ax, sy, slot, slot);
                        if (it.Active || hits.Count == hover) Fill(g, r, S(8), Color.FromArgb(it.Active ? 56 : 36, 255, 255, 255));
                        hits.Add(new Hit { R = r, Click = it.Click, Tip = it.Title });
                        if (it.Icon != null) g.DrawImage(it.Icon, new Rectangle(r.X + (slot - icon) / 2, r.Y + (slot - icon) / 2 - S(1), icon, icon));
                        else Center(g, it.Title.Substring(0, 1), fValue, Color.White, r);
                        if (it.Running)
                            using (var dot = new SolidBrush(Blue)) g.FillEllipse(dot, r.X + r.Width / 2 - S(2), r.Bottom - S(4), S(4), S(4));
                        ax += slot + S(4);
                    }
                    x += appW + gap;
                }

                Tile(g, new Rectangle(x, ty, sysW, th));
                Ring(g, new Rectangle(x + S(8), sy, slot, slot), cpu, "CPU");
                Ring(g, new Rectangle(x + S(16) + slot, sy, slot, slot), ram, "RAM");
                x += sysW + gap;

                if (claudeW > 0)
                {
                    var r = new Rectangle(x, ty, claudeW, th);
                    Tile(g, r);
                    hits.Add(new Hit { R = r, Click = () => { Claude.RefreshNow(); Tick(); }, Tip = ClaudeTip() });
                    if (Claude.Session == null && Claude.Week == null)
                    {
                        Center(g, "Claude", fRow, Color.White, new Rectangle(r.X, r.Y + S(3), r.Width, th / 2 - S(2)));
                        Center(g, Claude.Error ?? "Đang tải…", fDate, Color.FromArgb(190, 255, 255, 255), new Rectangle(r.X + S(4), r.Y + th / 2, r.Width - S(8), th / 2 - S(3)));
                    }
                    else
                    {
                        UsageRow(g, r, ty + S(3), "5h", Claude.Session);
                        UsageRow(g, r, ty + th / 2 + S(1), "7d", Claude.Week);
                    }
                    x += claudeW + gap;
                }

                if (showBatt)
                {
                    var r = new Rectangle(x, ty, batW, th);
                    Fill(g, r, S(10), pct <= 20 && !charging ? Color.FromArgb(220, 227, 67, 43) : Color.FromArgb(220, 47, 139, 255));
                    if (charging)
                    {
                        Center(g, "⚡", fBolt, Color.White, new Rectangle(r.X + S(4), r.Y, S(18), r.Height));
                        Center(g, pct.ToString(), fBatt, Color.White, new Rectangle(r.X + S(18), r.Y, r.Width - S(22), r.Height));
                    }
                    else Center(g, pct.ToString(), fBatt, Color.White, r);
                    x += batW + gap;
                }

                Tile(g, new Rectangle(x, ty, clockW, th));
                using (var right = new StringFormat { Alignment = StringAlignment.Far })
                using (var white = new SolidBrush(Color.White))
                using (var dim = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                {
                    g.DrawString(time, fTime, white, new RectangleF(x, ty + S(2), clockW - S(9), S(19)), right);
                    g.DrawString(date, fDate, dim, new RectangleF(x, ty + S(20), clockW - S(9), S(15)), right);
                }
            }
            Invalidate();
        }

        // One "5h ████░░ 33%  2h59" row of the Claude tile.
        void UsageRow(Graphics g, Rectangle tile, int y, string label, Claude.Limit l)
        {
            int rowH = S(15), x = tile.X + S(8);
            Color dim = Color.FromArgb(190, 255, 255, 255);
            using (var left = new StringFormat { LineAlignment = StringAlignment.Center })
            using (var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            using (var dimB = new SolidBrush(dim))
            using (var white = new SolidBrush(Color.White))
            {
                g.DrawString(label, fRow, dimB, new RectangleF(x, y, S(20), rowH), left);
                var bar = new Rectangle(x + S(20), y + rowH / 2 - S(2), S(60), S(4));
                Fill(g, bar, S(2), Color.FromArgb(50, 255, 255, 255));
                if (l == null) return;
                double p = Math.Max(0, Math.Min(100, l.Pct));
                Color c = p >= 90 ? Color.FromArgb(255, 255, 69, 58) : p >= 70 ? Color.FromArgb(255, 255, 159, 10) : Color.FromArgb(255, 52, 199, 89);
                if (p > 0) Fill(g, new Rectangle(bar.X, bar.Y, Math.Max(S(4), (int)(bar.Width * p / 100)), bar.Height), S(2), c);
                g.DrawString(Math.Round(p) + "%", fRow, white, new RectangleF(bar.Right, y, S(36), rowH), right);
                if (l.Reset > DateTime.MinValue)
                    g.DrawString(Until(l.Reset), fRow, dimB, new RectangleF(bar.Right + S(36), y, tile.Right - bar.Right - S(36) - S(8), rowH), right);
            }
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void Fill(Graphics g, Rectangle r, int rad, Color c)
        {
            using (GraphicsPath p = Round(r, rad))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        // Clear-glass tile: a dark translucent pane so text stays readable on any wallpaper,
        // a light sheen fading downward, and a hairline edge.
        void Tile(Graphics g, Rectangle r)
        {
            var edge = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Round(edge, S(10)))
            {
                using (var b = new SolidBrush(Color.FromArgb(118, 14, 14, 18))) g.FillPath(b, p);
                using (var b = new LinearGradientBrush(r, Color.FromArgb(40, 255, 255, 255), Color.FromArgb(4, 255, 255, 255), 90f)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(64, 255, 255, 255), 1f)) g.DrawPath(pen, p);
            }
        }

        void Ring(Graphics g, Rectangle r, int pct, string label)
        {
            float w = S(3);
            var rc = new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            using (var track = new Pen(Color.FromArgb(56, 255, 255, 255), w)) g.DrawEllipse(track, rc);
            if (pct > 0)
                using (var bar = new Pen(Blue, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bar, rc, -90, Math.Min(pct, 100) * 3.6f);
            Center(g, pct.ToString(), fValue, Color.White, new Rectangle(r.X, r.Y + S(5), r.Width, S(14)));
            Center(g, label, fLabel, Color.FromArgb(190, 255, 255, 255), new Rectangle(r.X, r.Y + S(17), r.Width, S(9)));
        }

        static void Center(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
        }

        // Raw 32-bit copy keeps per-pixel alpha, so the desktop shows through transparent pixels.
        protected override void OnPaint(PaintEventArgs e)
        {
            if (buf == null) return;
            BitmapData bd = buf.LockBits(new Rectangle(0, 0, buf.Width, buf.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var bmi = new N.BITMAPINFOHEADER { biSize = 40, biWidth = buf.Width, biHeight = -buf.Height, biPlanes = 1, biBitCount = 32 };
                IntPtr hdc = e.Graphics.GetHdc();
                N.SetDIBitsToDevice(hdc, 0, 0, (uint)buf.Width, (uint)buf.Height, 0, 0, 0, (uint)buf.Height, bd.Scan0, ref bmi, 0);
                e.Graphics.ReleaseHdc(hdc);
            }
            finally { buf.UnlockBits(bd); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = hits.FindIndex(x => x.R.Contains(e.Location));
            if (h == hover) return;
            hover = h;
            if (h >= 0) tip.Show(hits[h].Tip, this, hits[h].R.X, -S(30) - S(16) * hits[h].Tip.Split('\n').Length, 4000);
            else tip.Hide(this);
            Render();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hover == -1) return;
            hover = -1;
            tip.Hide(this);
            Render();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int h = hits.FindIndex(x => x.R.Contains(e.Location));
            if (h >= 0) hits[h].Click();
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            string arg = args.Length > 0 ? args[0] : "";
            if (arg == "--restore") { Taskbar.Restore(); return 0; }
            if (arg == "--selftest") return SelfTest();

            bool fresh;
            using (var mutex = new Mutex(true, "Lilbar.Dock", out fresh))
            {
                if (!fresh) return 0; // already running
                N.SetProcessDPIAware();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // the 4.x runtime may default to older TLS
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Taskbar.Log(e.Exception); Application.Exit(); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Taskbar.Log(e.ExceptionObject as Exception); Taskbar.Restore(); };
                // The loop can end without FormClosing (taskkill posts WM_CLOSE to hidden helper windows too).
                try { Application.Run(new Dock()); }
                finally { Taskbar.Restore(); }
            }
            return 0;
        }

        static int SelfTest()
        {
            int fails = 0;
            Action<bool, string> check = delegate(bool ok, string msg) { Console.WriteLine((ok ? "ok   " : "FAIL ") + msg); if (!ok) fails++; };
            check(Marshal.SizeOf(typeof(N.APPBARDATA)) == (IntPtr.Size == 8 ? 48 : 36), "APPBARDATA size " + Marshal.SizeOf(typeof(N.APPBARDATA)));
            N.RECT tr;
            check(N.GetWindowRect(N.FindWindow("Shell_TrayWnd", null), out tr) && tr.Bottom - tr.Top > 0, "taskbar height " + (tr.Bottom - tr.Top));
            List<Win> w = Sys.Windows();
            check(w.Count > 0 && w.TrueForAll(x => x.Hwnd != IntPtr.Zero && x.Title.Length > 0), "windows " + w.Count);
            check(w.FindAll(x => x.Active).Count <= 1, "at most one active window");
            check(w.FindAll(x => x.Exe.Length > 0).Count > 0, "exe paths resolved");
            List<Pin> p = Sys.Pinned();
            check(p.TrueForAll(x => File.Exists(x.Lnk)), "pinned " + p.Count + ": " + string.Join(", ", p.ConvertAll(x => x.Name + " -> " + x.Exe).ToArray()));
            Sys.Cpu();
            Thread.Sleep(500);
            int c = Sys.Cpu(), r = Sys.Ram();
            check(c >= 0 && c <= 100, "cpu " + c);
            check(r > 0 && r <= 100, "ram " + r);
            check(Sys.IconOf(Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe")) != null, "icon of explorer.exe");
            Claude.Parse("{\"five_hour\":{\"utilization\":33.0,\"resets_at\":\"2026-11-04T05:30:00.230316+00:00\"},\"seven_day\":{\"utilization\":3.0,\"resets_at\":null},\"limits\":[]}");
            check(Claude.Session != null && Claude.Session.Pct == 33 && Claude.Session.Reset.ToUniversalTime().Hour == 5, "claude usage parse (session)");
            check(Claude.Week != null && Claude.Week.Pct == 3 && Claude.Week.Reset == DateTime.MinValue, "claude usage parse (week, null reset)");
            return fails;
        }
    }
}
