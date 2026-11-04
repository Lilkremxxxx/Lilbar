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
        public static readonly Color Dim = Color.FromArgb(200, 255, 255, 255);
        public static Font Time, Date, Value, Label, Batt, Bolt, Row, Day, DateBig, Glyph, Sym, Big, Body, Small, Header, Title, Mdl, MdlSmall, Flip, Strike;
        static readonly Bitmap measureBmp = new Bitmap(1, 1);

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        public static void Init(float scale)
        {
            Scale = scale;
            Time = F("Segoe UI", 14, FontStyle.Bold);
            Date = F("Segoe UI", 10, FontStyle.Regular);
            Value = F("Segoe UI Semibold", 10.5f, FontStyle.Regular);
            Label = F("Segoe UI", 7, FontStyle.Regular);
            Batt = F("Segoe UI", 13, FontStyle.Bold);
            Bolt = F("Segoe UI Symbol", 11, FontStyle.Regular);
            Row = F("Segoe UI Semibold", 9.5f, FontStyle.Regular);
            Day = F("Segoe UI", 9, FontStyle.Bold);
            DateBig = F("Segoe UI", 13, FontStyle.Bold);
            Glyph = F("Segoe UI Symbol", 12, FontStyle.Regular);
            Sym = F("Segoe UI Symbol", 11, FontStyle.Regular);
            Big = F("Segoe UI", 18, FontStyle.Regular);
            Body = F("Segoe UI", 12.5f, FontStyle.Regular);
            Small = F("Segoe UI", 10.5f, FontStyle.Regular);
            Header = F("Segoe UI Semibold", 10, FontStyle.Regular);
            Title = F("Segoe UI Semibold", 17, FontStyle.Regular);
            Mdl = F("Segoe MDL2 Assets", 14, FontStyle.Regular); // Windows' own icon font
            MdlSmall = F("Segoe MDL2 Assets", 11, FontStyle.Regular);
            Flip = F("Segoe UI", 19, FontStyle.Bold);
            Strike = F("Segoe UI", 12.5f, FontStyle.Strikeout);
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

        public static void Tile(Graphics g, Rectangle r) { Pane(g, r, S(10), 118); }

        // Popups sit over arbitrary content, so their pane is darker for legibility.
        public static void Panel(Graphics g, Rectangle r) { Pane(g, r, S(14), 210); }

        static void Pane(Graphics g, Rectangle r, int rad, int alpha)
        {
            var edge = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Round(edge, rad))
            {
                using (var b = new SolidBrush(Color.FromArgb(alpha, 14, 14, 18))) g.FillPath(b, p);
                using (var b = new LinearGradientBrush(r, Color.FromArgb(40, 255, 255, 255), Color.FromArgb(4, 255, 255, 255), 90f)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(64, 255, 255, 255), 1f)) g.DrawPath(pen, p);
            }
        }

        public static void Center(Graphics g, string s, Font f, Color c, Rectangle r) { Text(g, s, f, c, r, StringAlignment.Center); }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, StringAlignment h)
        {
            using (var sf = new StringFormat { Alignment = h, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
        }

        public static void Ring(Graphics g, Rectangle r, int pct, string label)
        {
            float w = S(3);
            var rc = new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            using (var track = new Pen(Color.FromArgb(56, 255, 255, 255), w)) g.DrawEllipse(track, rc);
            if (pct > 0)
                using (var bar = new Pen(Blue, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bar, rc, -90, Math.Min(pct, 100) * 3.6f);
            Center(g, pct.ToString(), Value, Color.White, new Rectangle(r.X, r.Y + S(5), r.Width, S(14)));
            Center(g, label, Label, Color.FromArgb(190, 255, 255, 255), new Rectangle(r.X, r.Y + S(17), r.Width, S(9)));
        }

        public static void Chevron(Graphics g, Rectangle r, bool down)
        {
            float w = 5 * Scale, cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = down ? 1 : -1;
            using (var p = new Pen(Color.White, 1.6f * Scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(p, new[] { new PointF(cx - w, cy - s * w / 2), new PointF(cx, cy + s * w / 2), new PointF(cx + w, cy - s * w / 2) });
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
