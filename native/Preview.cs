using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lilbar
{
    // Live window previews above an app on the dock, like the Windows taskbar: one card per window with its
    // title and a DWM thumbnail; click a card to switch to it, ✕ to close it. A pinned app with no window
    // just shows its name. It never takes focus.
    class PreviewPopup : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
        [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumb);
        [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref Props p);
        [DllImport("dwmapi.dll")] static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out N.SIZE size);
        [StructLayout(LayoutKind.Sequential)]
        struct Props { public int Flags; public N.RECT Dest, Src; public byte Opacity; public int Visible, ClientOnly; }

        class Card { public Win W; public Rectangle R, Thumb, Close; public IntPtr H; }

        readonly string name;
        readonly Bitmap icon;
        readonly List<Card> cards = new List<Card>();
        readonly int M = Glass.ShadowMargin;
        Bitmap buf;
        int hover = -1;
        bool hoverClose;

        static int S(float v) { return Glass.S(v); }

        public PreviewPopup(string name, Bitmap icon, List<Win> wins, Rectangle anchor)
        {
            this.name = name;
            this.icon = icon;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);

            Rectangle sb = Screen.FromRectangle(anchor).Bounds;
            int pad = S(8), cardW = S(216), titleH = S(30), thumbH = S(122), gap = S(6), w, h;
            int max = Math.Max(1, (sb.Width - S(40)) / (cardW + gap));
            if (wins.Count == 0)
            {
                w = Glass.Measure(name, Glass.Menu) + S(32);
                h = S(38);
            }
            else
            {
                int n = Math.Min(max, wins.Count);
                w = pad * 2 + n * cardW + (n - 1) * gap;
                h = pad * 2 + titleH + thumbH;
                for (int i = 0; i < n; i++)
                {
                    var r = new Rectangle(M + pad + i * (cardW + gap), M + pad, cardW, titleH + thumbH);
                    cards.Add(new Card
                    {
                        W = wins[i], R = r,
                        Thumb = new Rectangle(r.X + S(8), r.Y + titleH, r.Width - S(16), thumbH - S(8)),
                        Close = new Rectangle(r.Right - S(30), r.Y + S(4), S(24), S(22)),
                    });
                }
            }
            int x = Math.Max(sb.Left + S(4), Math.Min(anchor.X + anchor.Width / 2 - w / 2, sb.Right - w - S(4)));
            Bounds = new Rectangle(x - M, anchor.Top - S(10) - h - M, w + 2 * M, h + 2 * M);
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
            Glass.MakeClear(Handle);
            foreach (Card c in cards)
            {
                IntPtr t;
                if (DwmRegisterThumbnail(Handle, c.W.Hwnd, out t) != 0) continue;
                c.H = t;
                N.SIZE s;
                DwmQueryThumbnailSourceSize(t, out s);
                if (s.cx <= 0 || s.cy <= 0 || N.IsIconic(c.W.Hwnd)) continue; // minimized windows have no live image; the icon stands in
                // Fit the window into the card keeping its aspect, centred.
                float k = Math.Min((float)c.Thumb.Width / s.cx, (float)c.Thumb.Height / s.cy);
                int tw = (int)(s.cx * k), th = (int)(s.cy * k);
                var d = new Rectangle(c.Thumb.X + (c.Thumb.Width - tw) / 2, c.Thumb.Y + (c.Thumb.Height - th) / 2, tw, th);
                var p = new Props { Flags = 0x1 | 0x4 | 0x8, Dest = new N.RECT { Left = d.Left, Top = d.Top, Right = d.Right, Bottom = d.Bottom }, Opacity = 255, Visible = 1 };
                DwmUpdateThumbnailProperties(t, ref p);
                c.Thumb = d;
            }
            Render();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (Card c in cards) if (c.H != IntPtr.Zero) DwmUnregisterThumbnail(c.H);
            if (buf != null) buf.Dispose();
            base.OnFormClosed(e);
        }

        void Render()
        {
            if (buf == null) buf = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Glass.Begin(buf))
            {
                var panel = new Rectangle(M, M, Width - 2 * M, Height - 2 * M);
                Glass.Shadow(g, panel, S(16));
                Glass.Panel(g, panel);
                if (cards.Count == 0) { Glass.Center(g, name, Glass.Menu, Color.White, panel); return; }
                for (int i = 0; i < cards.Count; i++)
                {
                    Card c = cards[i];
                    if (i == hover) Glass.Fill(g, c.R, S(10), Color.FromArgb(34, 255, 255, 255));
                    if (c.W.Active) Glass.Fill(g, new Rectangle(c.R.X + c.R.Width / 2 - S(10), c.R.Bottom - S(3), S(20), S(3)), S(1), Glass.Blue);
                    int tx = c.R.X + S(10);
                    if (icon != null) { g.DrawImage(icon, new Rectangle(tx, c.R.Y + S(7), S(16), S(16))); tx += S(22); }
                    int right = i == hover ? c.Close.X - S(2) : c.R.Right - S(8);
                    Glass.Text(g, c.W.Title, Glass.Row, Color.White, new Rectangle(tx, c.R.Y + S(4), right - tx, S(22)), StringAlignment.Near);
                    if (i == hover)
                    {
                        if (hoverClose) Glass.Fill(g, c.Close, S(5), Color.FromArgb(255, 196, 43, 28));
                        Glass.Center(g, "", Glass.MdlSmall, Color.White, c.Close);
                    }
                    if (c.H == IntPtr.Zero || N.IsIconic(c.W.Hwnd))
                    {
                        Glass.Fill(g, c.Thumb, S(6), Color.FromArgb(22, 255, 255, 255));
                        if (icon != null) g.DrawImage(icon, new Rectangle(c.Thumb.X + c.Thumb.Width / 2 - S(16), c.Thumb.Y + c.Thumb.Height / 2 - S(16), S(32), S(32)));
                    }
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) { Glass.Blit(e, buf); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = cards.FindIndex(c => c.R.Contains(e.Location));
            bool hc = h >= 0 && cards[h].Close.Contains(e.Location);
            if (h == hover && hc == hoverClose) return;
            hover = h;
            hoverClose = hc;
            Render();
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; hoverClose = false; Render(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int h = cards.FindIndex(k => k.R.Contains(e.Location));
            if (h < 0 || e.Button != MouseButtons.Left) return;
            Card c = cards[h];
            if (c.Close.Contains(e.Location)) N.PostMessage(c.W.Hwnd, 0x10, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE: the app may still ask to save
            else Sys.Activate(c.W.Hwnd);
            Close();
            Manager.Refresh();
        }
    }
}
