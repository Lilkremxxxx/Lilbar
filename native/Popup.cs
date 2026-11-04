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
    }

    // Clear-glass popup above the dock: a grid of icons (tray overflow, folder) or a short menu.
    // It takes focus so it can close itself when the user clicks anywhere else.
    class Popup : Form
    {
        readonly string title;
        readonly List<PopItem> items;
        readonly bool grid;
        readonly List<Rectangle> cells = new List<Rectangle>();
        readonly ToolTip tip = new ToolTip();
        Bitmap buf;
        int hover = -1;

        static int S(float v) { return Glass.S(v); }

        public Popup(string title, List<PopItem> items, bool grid, Rectangle anchor)
        {
            this.title = title;
            this.items = items;
            this.grid = grid;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            tip.ShowAlways = true;

            int pad = S(10), titleH = title != null ? S(28) : 0, w, h;
            if (grid)
            {
                int cell = S(44), cols = Math.Max(1, Math.Min(5, items.Count)), rows = Math.Max(1, (items.Count + cols - 1) / cols);
                w = Math.Max(pad * 2 + cols * cell, title != null ? S(170) : 0);
                h = pad * 2 + titleH + rows * cell;
                if (items.Count == 0) { w = S(220); h = pad * 2 + titleH + S(36); }
                int ox = (w - cols * cell) / 2;
                for (int i = 0; i < items.Count; i++) cells.Add(new Rectangle(ox + (i % cols) * cell, pad + titleH + (i / cols) * cell, cell, cell));
            }
            else
            {
                int rowH = S(36);
                w = S(200);
                h = pad * 2 + items.Count * rowH;
                for (int i = 0; i < items.Count; i++) cells.Add(new Rectangle(pad, pad + i * rowH, w - pad * 2, rowH));
            }
            Rectangle sb = Screen.FromRectangle(anchor).Bounds;
            int x = Math.Max(sb.Left + S(4), Math.Min(anchor.X + anchor.Width / 2 - w / 2, sb.Right - w - S(4)));
            Bounds = new Rectangle(x, anchor.Top - h - S(8), w, h);
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
                Glass.Panel(g, new Rectangle(0, 0, Width, Height));
                if (title != null) Glass.Text(g, title, Glass.Row, Glass.Dim, new Rectangle(S(14), S(8), Width - S(28), S(24)), StringAlignment.Near);
                if (items.Count == 0)
                    Glass.Center(g, "Không có biểu tượng ẩn", Glass.Small, Glass.Dim, new Rectangle(0, (title != null ? S(28) : 0) + S(10), Width, S(36)));
                for (int i = 0; i < items.Count; i++)
                {
                    PopItem it = items[i];
                    Rectangle c = cells[i];
                    if (i == hover) Glass.Fill(g, c, S(8), Color.FromArgb(40, 255, 255, 255));
                    if (grid)
                    {
                        int s = S(24);
                        if (it.Icon != null) g.DrawImage(it.Icon, new Rectangle(c.X + (c.Width - s) / 2, c.Y + (c.Height - s) / 2, s, s));
                        else if (it.Glyph != null) Glass.Center(g, it.Glyph, Glass.Glyph, Color.White, c);
                    }
                    else
                    {
                        int tx = c.X + S(10);
                        if (it.Glyph != null) { Glass.Center(g, it.Glyph, Glass.Glyph, Color.White, new Rectangle(c.X + S(4), c.Y, S(26), c.Height)); tx = c.X + S(34); }
                        Glass.Text(g, it.Text ?? "", Glass.Small, Color.White, new Rectangle(tx, c.Y, c.Right - tx - S(6), c.Height), StringAlignment.Near);
                    }
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) { Glass.Blit(e, buf); }

        int At(Point p) { return cells.FindIndex(c => c.Contains(p)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = At(e.Location);
            if (h == hover) return;
            hover = h;
            if (h >= 0 && !string.IsNullOrEmpty(items[h].Tip)) tip.Show(items[h].Tip, this, cells[h].X, cells[h].Bottom + S(4), 3000);
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
