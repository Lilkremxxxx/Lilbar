using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lilbar
{
    // Shared look: clear-glass windows with dark translucent panes, a light sheen and a hairline edge.
    static class Glass
    {
        public static float Scale = 1;
        public static readonly Color Blue = Color.FromArgb(255, 74, 155, 255);
        public static readonly Color Dim = Color.FromArgb(225, 255, 255, 255);
        public static Font Time, Date, Value, Label, Batt, Bolt, Row, Day, DateBig, Glyph, Sym, Big, Body, Small, Header, Title, Mdl, MdlSmall, Flip, Strike, GlyphBig, Huge, Menu, MenuIcon, Fluent, Badge, Caption;
        static readonly Bitmap measureBmp = new Bitmap(1, 1);

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        public static void Init(float scale)
        {
            Scale = scale;
            // Windows 11's own UI font: optical sizes stay legible at taskbar sizes. Windows 10 falls back to Segoe UI.
            string text = Has("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
            string semi = Has("Segoe UI Variable Text Semibold") ? "Segoe UI Variable Text Semibold" : "Segoe UI Semibold";
            string disp = Has("Segoe UI Variable Display") ? "Segoe UI Variable Display" : "Segoe UI";
            Time = F(disp, 16, FontStyle.Bold);
            Date = F(semi, 11.5f, FontStyle.Regular);
            Value = F(semi, 12.5f, FontStyle.Regular);
            Label = F(semi, 8.5f, FontStyle.Regular);
            Batt = F(disp, 15, FontStyle.Bold);
            Bolt = F("Segoe UI Symbol", 11, FontStyle.Regular);
            Row = F(semi, 12, FontStyle.Regular);
            Day = F(semi, 11, FontStyle.Regular);
            DateBig = F(disp, 15, FontStyle.Bold);
            Glyph = F("Segoe UI Symbol", 13, FontStyle.Regular);
            Sym = F("Segoe UI Symbol", 11, FontStyle.Regular);
            Big = F(disp, 18, FontStyle.Regular);
            Body = F(text, 13.5f, FontStyle.Regular);
            Small = F(text, 13.5f, FontStyle.Regular);
            Header = F(semi, 13, FontStyle.Regular);
            Caption = F(semi, 11.5f, FontStyle.Regular);
            Title = F(Has("Segoe UI Variable Display Semib") ? "Segoe UI Variable Display Semib" : "Segoe UI Semibold", 17, FontStyle.Regular);
            Mdl = F("Segoe MDL2 Assets", 14, FontStyle.Regular); // Windows' own icon font
            MdlSmall = F("Segoe MDL2 Assets", 11, FontStyle.Regular);
            Flip = F(disp, 19, FontStyle.Bold);
            Strike = F(text, 12.5f, FontStyle.Strikeout);
            GlyphBig = F("Segoe UI Symbol", 19, FontStyle.Regular);
            Huge = F(disp, 22, FontStyle.Bold);
            Menu = F(semi, 15, FontStyle.Regular);
            MenuIcon = F("Segoe MDL2 Assets", 15, FontStyle.Regular);
            Badge = F("Segoe MDL2 Assets", 7, FontStyle.Bold);
            Fluent = F(Has("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", 20, FontStyle.Regular); // Windows 11's own Quick Settings icons
        }

        static bool Has(string family)
        {
            using (var f = new Font(family, 10)) return f.Name == family; // GDI+ silently substitutes a missing family
        }

        static Font F(string family, float px, FontStyle style) { return new Font(family, px * Scale, style, GraphicsUnit.Pixel); }

        public static int Measure(string s, Font f)
        {
            using (Graphics g = Graphics.FromImage(measureBmp)) return (int)Math.Ceiling(g.MeasureString(s, f).Width);
        }

        public static Graphics Begin(Bitmap b)
        {
            Graphics g = Graphics.FromImage(b);
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // ClearType needs an opaque background
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            return g;
        }

        // Clear glass: per-pixel alpha with no blur, no DWM border; only what we draw is visible.
        public static void MakeClear(IntPtr h)
        {
            int none = unchecked((int)0xFFFFFFFE);
            N.DwmSetWindowAttribute(h, 34, ref none, 4); // DWMWA_BORDER_COLOR = none
            var accent = new N.ACCENT { AccentState = 2, AccentFlags = 2, GradientColor = 0 }; // TRANSPARENTGRADIENT
            int size = Marshal.SizeOf(typeof(N.ACCENT));
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, p, false);
                var d = new N.WCAD { Attribute = 19, Data = p, SizeOfData = size }; // WCA_ACCENT_POLICY
                N.SetWindowCompositionAttribute(h, ref d);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        // Raw 32-bit copy keeps per-pixel alpha, so the desktop shows through transparent pixels.
        public static void Blit(PaintEventArgs e, Bitmap buf)
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

        public static GraphicsPath Round(Rectangle r, int rad)
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

        public static void Fill(Graphics g, Rectangle r, int rad, Color c)
        {
            using (GraphicsPath p = Round(r, rad))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        // Tiles inside the dock's shell: only a hairline, no fill (opacity 0).
        public static void Tile(Graphics g, Rectangle r)
        {
            var edge = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Round(edge, S(9)))
            using (var b = new LinearGradientBrush(r, Color.FromArgb(84, 255, 255, 255), Color.FromArgb(34, 255, 255, 255), 90f))
            using (var pen = new Pen(b, 1f)) g.DrawPath(pen, p);
        }

        // The dock's outer box: fully clear (opacity 0), only a hairline rim. No fill, no blur.
        public static void Shell(Graphics g, Rectangle r)
        {
            using (GraphicsPath p = Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), S(13)))
            using (var b = new LinearGradientBrush(r, Color.FromArgb(110, 255, 255, 255), Color.FromArgb(44, 255, 255, 255), 90f))
            using (var pen = new Pen(b, 1f)) g.DrawPath(pen, p);
        }

        // Popups sit over arbitrary content, so their pane is darker for legibility.
        public static void Panel(Graphics g, Rectangle r) { Pane(g, r, S(16), 236); }

        // Soft drop shadow outside r (callers leave a ShadowMargin around their panel).
        public static int ShadowMargin { get { return S(16); } }

        public static void Shadow(Graphics g, Rectangle r, int rad)
        {
            int m = ShadowMargin;
            for (int i = 1; i <= m; i++)
            {
                float k = 1 - (float)i / (m + 1);
                var rr = new Rectangle(r.X - i, r.Y - i + S(4), r.Width + 2 * i - 1, r.Height + 2 * i - 1);
                using (GraphicsPath p = Round(rr, rad + i))
                using (var pen = new Pen(Color.FromArgb((int)(34 * k * k), 0, 0, 0), 1f)) g.DrawPath(pen, p);
            }
        }

        // Clear glass: a thin dark underlay for legibility, a white frost that fades downwards, a bright rim where
        // light catches the top edge, and a soft dark outline that keeps the pane apart from busy wallpapers. No blur.
        static void Pane(Graphics g, Rectangle r, int rad, int dark)
        {
            var edge = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Round(edge, rad))
            {
                using (var b = new SolidBrush(Color.FromArgb(dark, 12, 14, 20))) g.FillPath(b, p);
                using (var b = new LinearGradientBrush(r, Color.FromArgb(44, 255, 255, 255), Color.FromArgb(14, 255, 255, 255), 90f)) g.FillPath(b, p);
                using (var b = new LinearGradientBrush(r, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(34, 255, 255, 255), 90f))
                using (var pen = new Pen(b, 1f)) g.DrawPath(pen, p);
            }
            using (GraphicsPath o = Round(new Rectangle(r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1), rad + 1))
            using (var pen = new Pen(Color.FromArgb(46, 0, 0, 0), 1f)) g.DrawPath(pen, o);
        }

        public static void Center(Graphics g, string s, Font f, Color c, Rectangle r) { Text(g, s, f, c, r, StringAlignment.Center); }

        // A coolicons line icon when one has that name, otherwise the glyph as text in font f.
        public static void Icon(Graphics g, string nameOrGlyph, Font f, Color c, Rectangle r, int px)
        {
            if (Icons.Has(nameOrGlyph))
            {
                if (g.TextRenderingHint != TextRenderingHint.ClearTypeGridFit && c.A > 120) // same faint shadow as text on clear glass
                    Icons.Draw(g, nameOrGlyph, new Rectangle(r.X, r.Y + 1, r.Width, r.Height), px, Color.FromArgb(c.A * 110 / 255, 0, 0, 0));
                Icons.Draw(g, nameOrGlyph, r, px, c);
            }
            else Center(g, nameOrGlyph, f, c, r);
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, StringAlignment h)
        {
            using (var sf = new StringFormat { Alignment = h, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                // On clear glass (grayscale AA) a faint drop shadow keeps text legible over any wallpaper; opaque windows use ClearType and need none.
                if (g.TextRenderingHint != TextRenderingHint.ClearTypeGridFit && c.A > 120)
                    using (var sh = new SolidBrush(Color.FromArgb(c.A * 110 / 255, 0, 0, 0))) g.DrawString(s, f, sh, new Rectangle(r.X, r.Y + 1, r.Width, r.Height), sf);
                using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
            }
        }

        public static void Ring(Graphics g, Rectangle r, int pct, string label)
        {
            float w = S(3);
            var rc = new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            using (var track = new Pen(Color.FromArgb(56, 255, 255, 255), w)) g.DrawEllipse(track, rc);
            if (pct > 0)
                using (var bar = new Pen(Blue, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bar, rc, -90, Math.Min(pct, 100) * 3.6f);
            Center(g, pct.ToString(), Value, Color.White, new Rectangle(r.X, r.Y + r.Height / 2 - S(10), r.Width, S(14)));
            Center(g, label, Label, Color.FromArgb(200, 255, 255, 255), new Rectangle(r.X, r.Y + r.Height / 2 + S(2), r.Width, S(10)));
        }

        public static void Chevron(Graphics g, Rectangle r, bool down)
        {
            float w = 5 * Scale, cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = down ? 1 : -1;
            using (var p = new Pen(Color.White, 1.6f * Scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(p, new[] { new PointF(cx - w, cy - s * w / 2), new PointF(cx, cy + s * w / 2), new PointF(cx + w, cy - s * w / 2) });
        }

        // Popup entrance: 180 ms on cubic-bezier(0.23, 1, 0.32, 1), a strong ease-out (Emil Kowalski's UI curve).
        public const float EnterMs = 180;

        public static float EaseOut(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            float u = t;
            for (int i = 0; i < 8; i++) // Newton: find u with x(u) = t
            {
                float dx = BezD(u, 0.23f, 0.32f);
                if (dx < 1e-4f) break;
                u = Math.Max(0, Math.Min(1, u - (Bez(u, 0.23f, 0.32f) - t) / dx));
            }
            return Bez(u, 1f, 1f);
        }

        static float Bez(float u, float p1, float p2) { float v = 1 - u; return 3 * v * v * u * p1 + 3 * v * u * u * p2 + u * u * u; }
        static float BezD(float u, float p1, float p2) { float v = 1 - u; return 3 * v * v * p1 + 6 * v * u * (p2 - p1) + 3 * u * u * (1 - p2); }

        // Windows' "Animation effects" switch; off means fade only, no movement.
        public static bool Motion
        {
            get { int on = 1; N.SystemParametersInfo(0x1042, 0, ref on, 0); return on != 0; } // SPI_GETCLIENTAREAANIMATION
        }

        // Opaque popups: fade in while rising 6 px into place. Call before the window is shown.
        public static void Enter(Form f)
        {
            int top = f.Top, dy = Motion ? S(6) : 0;
            f.Opacity = 0;
            f.Top = top + dy;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var t = new Timer { Interval = 10 };
            t.Tick += delegate
            {
                if (f.IsDisposed) { t.Dispose(); return; }
                float e = EaseOut(clock.ElapsedMilliseconds / EnterMs);
                f.Opacity = e;
                f.Top = top + (int)Math.Round(dy * (1 - e));
                if (e >= 1) t.Dispose();
            };
            t.Start();
        }

        public static string DayName(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Sunday: return "Chủ nhật";
                case DayOfWeek.Monday: return "Thứ hai";
                case DayOfWeek.Tuesday: return "Thứ ba";
                case DayOfWeek.Wednesday: return "Thứ tư";
                case DayOfWeek.Thursday: return "Thứ năm";
                case DayOfWeek.Friday: return "Thứ sáu";
                default: return "Thứ bảy";
            }
        }
    }
}
