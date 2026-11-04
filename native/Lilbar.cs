// Lilbar: a glass dock that replaces the Windows taskbar.
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
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Lilbar
{
    static class N
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);
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
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
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

    // Auto-hide drops the Windows taskbar's work-area reservation; SW_HIDE removes it from view.
    // State file is shared with the Electron build (win32.js): {"state":<ABM_GETSTATE>}.
    static class Taskbar
    {
        static readonly string StateFile = Path.Combine(Path.GetTempPath(), "lilbar-taskbar-state.json");

        static N.APPBARDATA Abd(IntPtr h)
        {
            var d = new N.APPBARDATA();
            d.cbSize = (uint)Marshal.SizeOf(typeof(N.APPBARDATA));
            d.hWnd = h;
            d.uEdge = 3; // ABE_BOTTOM
            return d;
        }

        static IntPtr Tray() { return N.FindWindow("Shell_TrayWnd", null); }

        public static void Hide()
        {
            IntPtr tray = Tray();
            var d = Abd(tray);
            if (!File.Exists(StateFile)) File.WriteAllText(StateFile, "{\"state\":" + (ulong)N.SHAppBarMessage(4, ref d) + "}"); // ABM_GETSTATE
            d = Abd(tray);
            d.lParam = (IntPtr)1; // ABS_AUTOHIDE
            N.SHAppBarMessage(10, ref d); // ABM_SETSTATE
            N.ShowWindow(tray, 0);
        }

        // Idempotent: without a state file there is nothing to undo except visibility.
        public static void Restore()
        {
            IntPtr tray = Tray();
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
                var d = Abd(tray);
                d.lParam = (IntPtr)state;
                N.SHAppBarMessage(10, ref d);
            }
            N.ShowWindow(tray, 5);
        }

        // Reserve a strip at the bottom edge so maximized windows stop above the dock.
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
        readonly float scale;
        readonly Font fTime, fDate, fValue, fLabel, fBatt, fBolt;
        readonly NotifyIcon tray;
        readonly ToolTip tip = new ToolTip();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly List<Hit> hits = new List<Hit>();
        readonly Bitmap measureBmp = new Bitmap(1, 1);
        List<Pin> pins = new List<Pin>();
        List<Win> wins = new List<Win>();
        Bitmap buf;
        int cpu, ram, tick, hover = -1;
        string sig = "";
        bool reserved;

        int S(float v) { return (int)Math.Round(v * scale); }

        public Dock()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "Lilbar";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);

            fTime = new Font("Segoe UI", S(15), FontStyle.Bold, GraphicsUnit.Pixel);
            fDate = new Font("Segoe UI", S(10), FontStyle.Regular, GraphicsUnit.Pixel);
            fValue = new Font("Segoe UI Semibold", S(11), FontStyle.Regular, GraphicsUnit.Pixel);
            fLabel = new Font("Segoe UI", S(7.5f), FontStyle.Regular, GraphicsUnit.Pixel);
            fBatt = new Font("Segoe UI", S(14), FontStyle.Bold, GraphicsUnit.Pixel);
            fBolt = new Font("Segoe UI Symbol", S(12), FontStyle.Regular, GraphicsUnit.Pixel);

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
            int on = 1, round = 2;
            N.DwmSetWindowAttribute(Handle, 20, ref on, 4);    // DWMWA_USE_IMMERSIVE_DARK_MODE
            N.DwmSetWindowAttribute(Handle, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
            // Acrylic blur of whatever is behind the dock, composed by DWM on the GPU.
            var accent = new N.ACCENT { AccentState = 4, AccentFlags = 2, GradientColor = 0x55181414 }; // ACRYLICBLURBEHIND, tint AABBGGRR
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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Taskbar.Hide();
            Rectangle b = Screen.PrimaryScreen.Bounds;
            Taskbar.Reserve(Handle, new Rectangle(b.X, b.Bottom - S(72), b.Width, S(72)));
            reserved = true;
            Tick();
            timer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            if (reserved) Taskbar.Unreserve(Handle);
            reserved = false;
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
            wins = Sys.Windows();
            PowerStatus ps = SystemInformation.PowerStatus;
            var s = new StringBuilder();
            foreach (Win w in wins) s.Append(w.Hwnd).Append(w.Active ? '*' : '-').Append(w.Title).Append('|');
            s.Append(pins.Count).Append(cpu).Append('/').Append(ram).Append(DateTime.Now.ToString("HH:mm d"));
            s.Append(ps.BatteryLifePercent).Append(ps.PowerLineStatus);
            if (s.ToString() == sig) return; // nothing visible changed: no repaint
            sig = s.ToString();
            Render();
        }

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

            int H = S(56), th = S(44), ty = (H - th) / 2, pad = S(6), gap = S(6);
            int appW = items.Count == 0 ? 0 : S(12) + items.Count * S(40) - S(4);
            int sysW = S(100), batW = showBatt ? S(62) : 0, clockW = Math.Max(S(78), clockText) + S(20);
            int W = pad + (appW > 0 ? appW + gap : 0) + sysW + gap + (showBatt ? batW + gap : 0) + clockW + pad;

            if (buf == null || buf.Width != W || buf.Height != H)
            {
                if (buf != null) buf.Dispose();
                buf = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            }
            if (Width != W || Height != H)
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                SetBounds(b.X + (b.Width - W) / 2, b.Bottom - H - S(8), W, H);
            }

            hits.Clear();
            using (Graphics g = Graphics.FromImage(buf))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // ClearType needs an opaque background
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int x = pad;
                if (appW > 0)
                {
                    Tile(g, new Rectangle(x, ty, appW, th));
                    int ax = x + S(6);
                    foreach (AppItem it in items)
                    {
                        var r = new Rectangle(ax, (H - S(36)) / 2, S(36), S(36));
                        if (it.Active || hits.Count == hover) Fill(g, r, S(10), Color.FromArgb(it.Active ? 46 : 32, 255, 255, 255));
                        hits.Add(new Hit { R = r, Click = it.Click, Tip = it.Title });
                        if (it.Icon != null) g.DrawImage(it.Icon, new Rectangle(r.X + S(5), r.Y + S(5), S(26), S(26)));
                        else Center(g, it.Title.Substring(0, 1), fValue, Color.White, r);
                        if (it.Running)
                            using (var dot = new SolidBrush(Blue)) g.FillEllipse(dot, r.X + r.Width / 2 - S(2), r.Bottom - S(3), S(4), S(4));
                        ax += S(40);
                    }
                    x += appW + gap;
                }

                Tile(g, new Rectangle(x, ty, sysW, th));
                Ring(g, new Rectangle(x + S(10), (H - S(36)) / 2, S(36), S(36)), cpu, "CPU");
                Ring(g, new Rectangle(x + S(54), (H - S(36)) / 2, S(36), S(36)), ram, "RAM");
                x += sysW + gap;

                if (showBatt)
                {
                    var r = new Rectangle(x, ty, batW, th);
                    Fill(g, r, S(12), pct <= 20 && !charging ? Color.FromArgb(230, 227, 67, 43) : Color.FromArgb(230, 47, 139, 255));
                    if (charging)
                    {
                        Center(g, "⚡", fBolt, Color.White, new Rectangle(r.X + S(4), r.Y, S(20), r.Height));
                        Center(g, pct.ToString(), fBatt, Color.White, new Rectangle(r.X + S(20), r.Y, r.Width - S(24), r.Height));
                    }
                    else Center(g, pct.ToString(), fBatt, Color.White, r);
                    x += batW + gap;
                }

                Tile(g, new Rectangle(x, ty, clockW, th));
                using (var right = new StringFormat { Alignment = StringAlignment.Far })
                using (var white = new SolidBrush(Color.White))
                using (var dim = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
                {
                    g.DrawString(time, fTime, white, new RectangleF(x, ty + S(4), clockW - S(10), S(20)), right);
                    g.DrawString(date, fDate, dim, new RectangleF(x, ty + S(24), clockW - S(10), S(16)), right);
                }
            }
            Invalidate();
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
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

        // Frosted tile: light top-to-bottom sheen plus a hairline edge, the glassmorphism look.
        void Tile(Graphics g, Rectangle r)
        {
            var edge = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Round(edge, S(12)))
            {
                using (var b = new LinearGradientBrush(r, Color.FromArgb(38, 255, 255, 255), Color.FromArgb(14, 255, 255, 255), 90f)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(46, 255, 255, 255), 1f)) g.DrawPath(pen, p);
            }
        }

        void Ring(Graphics g, Rectangle r, int pct, string label)
        {
            float w = S(3);
            var rc = new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            using (var track = new Pen(Color.FromArgb(46, 255, 255, 255), w)) g.DrawEllipse(track, rc);
            if (pct > 0)
                using (var bar = new Pen(Blue, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bar, rc, -90, Math.Min(pct, 100) * 3.6f);
            Center(g, pct.ToString(), fValue, Color.White, new Rectangle(r.X, r.Y + S(6), r.Width, S(15)));
            Center(g, label, fLabel, Color.FromArgb(180, 255, 255, 255), new Rectangle(r.X, r.Y + S(19), r.Width, S(10)));
        }

        static void Center(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
        }

        // Raw 32-bit copy keeps per-pixel alpha, so DWM shows the acrylic through transparent pixels.
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
            if (h >= 0) tip.Show(hits[h].Tip, this, hits[h].R.X, -S(34), 2500);
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
            return fails;
        }
    }
}
