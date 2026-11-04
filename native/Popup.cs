using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Lilbar
{
    class PopItem
    {
        public Bitmap Icon;
        public string Glyph, Text, Tip;
        public Action<MouseButtons> Down, Up;
        public bool Right; // forwards right clicks too (tray icons)
        public bool Separator; // a hairline above this menu row
        public string Badge; // a small blue glyph badge on a grid icon (edit mode: "pin this")
    }

    // Clear-glass popup above the dock: a grid of icons (tray overflow, folder) or a short menu.
    // Apple-style material: a dense pane with a bright rim, a soft shadow (drawn in a margin around the pane),
    // generous padding and rounded hover plates. It takes focus so it can close itself when the user clicks elsewhere.
    class Popup : Form
    {
        readonly string title;
        readonly List<PopItem> items;
        readonly bool grid, labels;
        readonly List<Rectangle> cells = new List<Rectangle>();
        readonly GlassTip tip = new GlassTip();
        readonly int M = Glass.ShadowMargin;
        Bitmap buf, frame;
        int hover = -1;
        readonly bool motion = Glass.Motion;
        readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        Timer anim;

        static int S(float v) { return Glass.S(v); }

        public Popup(string title, List<PopItem> items, bool grid, Rectangle anchor)
        {
            this.title = title;
            this.items = items;
            this.grid = grid;
            labels = grid && items.Exists(i => i.Text != null); // folder apps get names under their icons; tray icons don't
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            tip.ShowAlways = true;

            int pad = S(10), titleH = title != null ? S(34) : 0, w, h;
            if (grid)
            {
                int cw = labels ? S(76) : S(44), ch = labels ? S(72) : S(44);
                int cols = Math.Max(1, Math.Min(labels ? 4 : 5, items.Count)), rows = Math.Max(1, (items.Count + cols - 1) / cols);
                w = Math.Max(pad * 2 + cols * cw, title != null ? S(180) : 0);
                h = pad * 2 + titleH + rows * ch;
                if (items.Count == 0) { w = S(240); h = pad * 2 + titleH + S(40); }
                int ox = (w - cols * cw) / 2;
                for (int i = 0; i < items.Count; i++) cells.Add(new Rectangle(M + ox + (i % cols) * cw, M + pad + titleH + (i / cols) * ch, cw, ch));
            }
            else
            {
                int rowH = S(40), sep = S(9), y = pad, widest = 0;
                foreach (PopItem it in items) widest = Math.Max(widest, Glass.Measure(it.Text ?? "", Glass.Menu));
                w = Math.Max(S(220), widest + S(70));
                foreach (PopItem it in items)
                {
                    if (it.Separator) y += sep;
                    cells.Add(new Rectangle(M + S(6), M + y, w - S(12), rowH));
                    y += rowH;
                }
                h = y + pad;
            }
            Rectangle sb = Screen.FromRectangle(anchor).Bounds;
            int x = Math.Max(sb.Left + S(4), Math.Min(anchor.X + anchor.Width / 2 - w / 2, sb.Right - w - S(4)));
            Bounds = new Rectangle(x - M, anchor.Top - h - S(10) - M, w + 2 * M, h + 2 * M);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8; // TOOLWINDOW | TOPMOST
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Glass.MakeClear(Handle); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Render();
            clock.Start();
            anim = new Timer { Interval = 10 };
            anim.Tick += delegate { Invalidate(); if (clock.ElapsedMilliseconds >= Glass.EnterMs) { anim.Dispose(); anim = null; } };
            anim.Start();
            Sys.Foreground(Handle);
            Activate();
        }

        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); Close(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        void Render()
        {
            if (buf == null || buf.Width != Width || buf.Height != Height)
            {
                if (buf != null) buf.Dispose();
                buf = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            }
            using (Graphics g = Glass.Begin(buf))
            {
                var panel = new Rectangle(M, M, Width - 2 * M, Height - 2 * M);
                Glass.Shadow(g, panel, S(16));
                Glass.Panel(g, panel);
                if (title != null) Glass.Text(g, title, Glass.Header, Color.FromArgb(190, 255, 255, 255), new Rectangle(M + S(16), M + S(10), panel.Width - S(32), S(24)), StringAlignment.Near);
                if (items.Count == 0)
                    Glass.Center(g, "Không có biểu tượng ẩn", Glass.Small, Glass.Dim, new Rectangle(M, M + (title != null ? S(34) : 0) + S(10), panel.Width, S(40)));
                for (int i = 0; i < items.Count; i++)
                {
                    PopItem it = items[i];
                    Rectangle c = cells[i];
                    if (it.Separator)
                        using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawLine(pen, c.X + S(10), c.Y - S(5), c.Right - S(10), c.Y - S(5));
                    if (i == hover) Glass.Fill(g, Rectangle.Inflate(c, -S(2), -S(2)), S(10), Color.FromArgb(38, 255, 255, 255));
                    if (grid)
                    {
                        int s = it.Icon != null && it.Icon.Width < S(labels ? 32 : 24) ? it.Icon.Width : S(labels ? 32 : 24); // never upscale a small tray icon
                        int iy = labels ? c.Y + S(9) : c.Y + (c.Height - s) / 2;
                        if (it.Icon != null) g.DrawImage(it.Icon, new Rectangle(c.X + (c.Width - s) / 2, iy, s, s));
                        else if (it.Glyph != null) Glass.Center(g, it.Glyph, Glass.Glyph, Color.White, new Rectangle(c.X, iy, c.Width, s));
                        if (labels) Glass.Center(g, it.Text ?? "", Glass.Label, Color.White, new Rectangle(c.X + S(3), c.Y + S(46), c.Width - S(6), S(18)));
                        if (it.Badge != null)
                        {
                            var bd = new Rectangle(c.Right - S(17), c.Y + S(3), S(14), S(14));
                            using (var b = new SolidBrush(Glass.Blue)) g.FillEllipse(b, bd);
                            Glass.Center(g, it.Badge, Glass.Badge, Color.White, new Rectangle(bd.X, bd.Y + S(1), bd.Width, bd.Height));
                        }
                    }
                    else
                    {
                        var ic = new Rectangle(c.X + S(8), c.Y, S(28), c.Height);
                        if (!string.IsNullOrEmpty(it.Glyph)) Glass.Center(g, it.Glyph, Glass.MenuIcon, Color.White, ic);
                        Glass.Text(g, it.Text ?? "", Glass.Menu, Color.White, new Rectangle(ic.Right + S(8), c.Y, c.Right - ic.Right - S(14), c.Height), StringAlignment.Near);
                    }
                }
            }
            Invalidate();
        }

        // Entrance: fade in and grow from 96 % with the origin at the bottom centre, where the dock tile is.
        protected override void OnPaint(PaintEventArgs e)
        {
            float t = clock.IsRunning ? Glass.EaseOut(clock.ElapsedMilliseconds / Glass.EnterMs) : 0;
            if (t >= 1 || buf == null) { Glass.Blit(e, buf); return; }
            if (frame == null || frame.Size != buf.Size) { if (frame != null) frame.Dispose(); frame = new Bitmap(buf.Width, buf.Height, PixelFormat.Format32bppPArgb); }
            float sc = motion ? 0.96f + 0.04f * t : 1;
            int w = (int)Math.Round(buf.Width * sc), h = (int)Math.Round(buf.Height * sc);
            using (Graphics g = Graphics.FromImage(frame))
            using (var ia = new ImageAttributes())
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                ia.SetColorMatrix(new ColorMatrix { Matrix33 = t });
                g.DrawImage(buf, new Rectangle((buf.Width - w) / 2, buf.Height - h, w, h), 0, 0, buf.Width, buf.Height, GraphicsUnit.Pixel, ia);
            }
            Glass.Blit(e, frame);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (anim != null) anim.Dispose();
            if (frame != null) frame.Dispose();
            if (buf != null) buf.Dispose();
            base.OnFormClosed(e);
        }

        int At(Point p) { return cells.FindIndex(c => c.Contains(p)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = At(e.Location);
            if (h == hover) return;
            hover = h;
            if (h >= 0 && !labels && !string.IsNullOrEmpty(items[h].Tip)) tip.Show(items[h].Tip, this, cells[h].X, cells[h].Bottom + S(4), 3000);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = At(e.Location);
            if (h >= 0 && items[h].Down != null && (e.Button == MouseButtons.Left || items[h].Right)) items[h].Down(e.Button);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int h = At(e.Location);
            if (h >= 0 && items[h].Up != null && (e.Button == MouseButtons.Left || items[h].Right)) items[h].Up(e.Button);
        }
    }
}

namespace Lilbar
{
    // Replaces the WinForms ToolTip (a yellow classic box when visual styles are off) with a small glass card.
    // Same Show/Hide calls as ToolTip; a negative y puts the card above the control (the dock), otherwise below.
    class GlassTip
    {
        TipForm form;
        public bool ShowAlways { get; set; }

        public void Show(string text, Control owner, int x, int y, int ms)
        {
            Hide(owner);
            if (string.IsNullOrEmpty(text)) return;
            Point p = owner.PointToScreen(new Point(x, y < 0 ? 0 : y));
            form = new TipForm(text, p, y < 0);
            form.Show();
        }

        public void Hide(Control owner)
        {
            if (form == null) return;
            if (!form.IsDisposed) form.Close();
            form = null;
        }

        class TipForm : Form
        {
            readonly string[] lines;
            readonly int M = Glass.ShadowMargin;
            Bitmap buf;

            static int S(float v) { return Glass.S(v); }

            public TipForm(string text, Point at, bool above)
            {
                lines = text.Split('\n');
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
                int w = 0;
                for (int i = 0; i < lines.Length; i++) w = Math.Max(w, Glass.Measure(lines[i], i == 0 ? Glass.Row : Glass.Small));
                w += S(24);
                int h = S(12) + lines.Length * S(20);
                Rectangle sb = Screen.FromPoint(at).Bounds;
                int x = Math.Max(sb.Left + S(4), Math.Min(at.X - S(8), sb.Right - w - S(4)));
                int y = above ? at.Y - S(8) - h : at.Y;
                Bounds = new Rectangle(x - M, y - M, w + 2 * M, h + 2 * M);
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x80 | 0x08000000 | 0x8 | 0x20; // TOOLWINDOW | NOACTIVATE | TOPMOST | TRANSPARENT
                    return cp;
                }
            }

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; } // WM_NCHITTEST: HTTRANSPARENT, never in the way
                base.WndProc(ref m);
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                Glass.MakeClear(Handle);
                buf = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Glass.Begin(buf))
                {
                    var panel = new Rectangle(M, M, Width - 2 * M, Height - 2 * M);
                    Glass.Shadow(g, panel, S(10));
                    Glass.Panel(g, panel);
                    for (int i = 0; i < lines.Length; i++)
                        Glass.Text(g, lines[i], i == 0 ? Glass.Row : Glass.Small, i == 0 ? Color.White : Glass.Dim, new Rectangle(M + S(12), M + S(6) + i * S(20), panel.Width - S(20), S(20)), StringAlignment.Near);
                }
            }

            protected override void OnPaint(PaintEventArgs e) { Glass.Blit(e, buf); }

            protected override void OnFormClosed(FormClosedEventArgs e) { if (buf != null) buf.Dispose(); base.OnFormClosed(e); }
        }
    }
}
