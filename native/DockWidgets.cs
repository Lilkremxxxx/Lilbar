using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace Lilbar
{
    // Tiles for the widget set: media, network, volume, radios, temperature, clocks, timers, todos, GitHub.
    partial class Dock
    {
        const string MdlPlay = "", MdlPause = "", MdlNext = "", MdlPrev = "", MdlMute = "",
            MdlWifi = "", MdlBluetooth = "", MdlMusic = "", MdlBell = "";

        static string Mmss(int secs) { return (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00"); }

        bool RoundButton(Graphics g, Rectangle c, string glyph, Color fill, Action click, string tipText)
        {
            bool hov = AddHit(c, null, b => click(), tipText, false);
            Glass.Fill(g, c, c.Height / 2, hov ? Color.FromArgb(Math.Min(255, fill.A + 40), fill.R, fill.G, fill.B) : fill);
            Glass.Center(g, glyph, Glass.MdlSmall, Color.White, c);
            return hov;
        }

        Seg MediaSeg()
        {
            if (!Media.Has) return null;
            string title = Media.Title, artist = Media.Artist;
            Bitmap thumb = Media.Thumb;
            bool playing = Media.Playing;
            return new Seg
            {
                W = S(250),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var art = new Rectangle(r.X + S(4), r.Y + S(4), r.Height - S(8), r.Height - S(8));
                    if (thumb != null)
                        using (GraphicsPath p = Glass.Round(art, S(6)))
                        {
                            Region old = g.Clip;
                            g.SetClip(p, CombineMode.Intersect);
                            g.DrawImage(thumb, art);
                            g.Clip = old;
                        }
                    else { Glass.Fill(g, art, S(6), Color.FromArgb(50, 255, 255, 255)); Glass.Center(g, MdlMusic, Glass.MdlSmall, Color.White, art); }
                    int bx = r.Right - S(4) - 3 * S(28), tx = art.Right + S(8);
                    AddHit(new Rectangle(art.X, r.Y, bx - art.X, r.Height), null, null, title + (artist.Length > 0 ? "\n" + artist : ""), false);
                    Glass.Text(g, title, Glass.Row, Color.White, new Rectangle(tx, r.Y + S(3), bx - tx - S(4), S(17)), StringAlignment.Near);
                    Glass.Text(g, artist, Glass.Date, Glass.Dim, new Rectangle(tx, r.Y + S(20), bx - tx - S(4), S(16)), StringAlignment.Near);
                    int cy = r.Y + (r.Height - S(26)) / 2;
                    RoundButton(g, new Rectangle(bx, cy, S(26), S(26)), MdlPrev, Color.FromArgb(0, 255, 255, 255), () => Media.Command(2), "Bài trước");
                    RoundButton(g, new Rectangle(bx + S(28), cy, S(26), S(26)), playing ? MdlPause : MdlPlay, Color.FromArgb(70, 255, 255, 255), () => Media.Command(0), playing ? "Tạm dừng" : "Phát");
                    RoundButton(g, new Rectangle(bx + S(56), cy, S(26), S(26)), MdlNext, Color.FromArgb(0, 255, 255, 255), () => Media.Command(1), "Bài sau");
                }
            };
        }

        Seg NetworkSeg()
        {
            double down = Net.Down, up = Net.Up;
            double[] hist = Net.History.ToArray();
            return new Seg
            {
                W = S(112),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, "Tải xuống " + Net.Fmt(down) + "\nTải lên " + Net.Fmt(up), false);
                    if (hist.Length > 1)
                    {
                        double max = 1e4;
                        foreach (double v in hist) max = Math.Max(max, v);
                        var pts = new List<PointF>();
                        float x0 = r.X + S(4), w = r.Width - S(8), base0 = r.Bottom - S(3), h = r.Height - S(10);
                        pts.Add(new PointF(x0, base0));
                        for (int i = 0; i < hist.Length; i++) pts.Add(new PointF(x0 + w * i / (hist.Length - 1), base0 - (float)(h * hist[i] / max)));
                        pts.Add(new PointF(x0 + w, base0));
                        using (var b = new SolidBrush(Color.FromArgb(46, 74, 155, 255))) g.FillPolygon(b, pts.ToArray());
                    }
                    Glass.Text(g, "↓ " + Net.Fmt(down), Glass.Row, Color.White, new Rectangle(r.X + S(8), r.Y + S(3), r.Width - S(12), S(16)), StringAlignment.Near);
                    Glass.Text(g, "↑ " + Net.Fmt(up), Glass.Row, Glass.Dim, new Rectangle(r.X + S(8), r.Y + S(20), r.Width - S(12), S(16)), StringAlignment.Near);
                }
            };
        }

        Seg VolumeSeg()
        {
            if (Audio.Level < 0) return null;
            int level = Audio.Level;
            bool muted = Audio.Muted;
            string glyph = muted ? MdlMute : level == 0 ? "" : level < 34 ? "" : level < 67 ? "" : "";
            return new Seg
            {
                W = S(118),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    string tipText = (Audio.Device.Length > 0 ? Audio.Device + "\n" : "") + "Lăn chuột để chỉnh, bấm biểu tượng để tắt tiếng";
                    Action<int> wheel = d => Audio.Set(Audio.Level + 2 * d);
                    var ic = new Rectangle(r.X + S(4), r.Y, S(28), r.Height);
                    bool hov = AddHit(ic, null, b => Audio.ToggleMute(), tipText, false, wheel);
                    if (hov) Glass.Fill(g, new Rectangle(ic.X, r.Y + S(5), ic.Width, r.Height - S(10)), S(6), Color.FromArgb(36, 255, 255, 255));
                    Glass.Center(g, glyph, Glass.Mdl, muted ? Glass.Dim : Color.White, ic);
                    var bar = new Rectangle(ic.Right + S(4), r.Y + r.Height / 2 - S(2), S(50), S(4));
                    var barHit = new Rectangle(bar.X - S(2), r.Y, bar.Width + S(4), r.Height);
                    AddHit(barHit, null, b =>
                    {
                        Point p = PointToClient(Cursor.Position);
                        Audio.Set((int)Math.Round(100.0 * (p.X - bar.X) / bar.Width));
                    }, tipText, false, wheel);
                    Glass.Fill(g, bar, S(2), Color.FromArgb(50, 255, 255, 255));
                    if (level > 0) Glass.Fill(g, new Rectangle(bar.X, bar.Y, Math.Max(S(4), bar.Width * level / 100), bar.Height), S(2), muted ? Glass.Dim : Glass.Blue);
                    var pct = new Rectangle(bar.Right + S(2), r.Y, r.Right - bar.Right - S(6), r.Height);
                    AddHit(pct, null, null, tipText, false, wheel);
                    Glass.Text(g, level + "%", Glass.Row, Color.White, pct, StringAlignment.Far);
                }
            };
        }

        Seg TogglesSeg()
        {
            List<Radios.R> list = Radios.List;
            if (list.Count == 0) return null;
            int cell = S(32);
            return new Seg
            {
                W = S(6) + list.Count * cell + S(2),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    int x = r.X + S(5);
                    foreach (Radios.R it in list)
                    {
                        Radios.R radio = it;
                        var c = new Rectangle(x, r.Y + (r.Height - S(28)) / 2, S(28), S(28));
                        RoundButton(g, c, radio.WiFi ? MdlWifi : MdlBluetooth, radio.On ? Glass.Blue : Color.FromArgb(50, 255, 255, 255),
                            () => Radios.Toggle(radio), (radio.WiFi ? "Wi-Fi" : "Bluetooth") + ": " + (radio.On ? "đang bật" : "đang tắt") + "\nBấm để " + (radio.On ? "tắt" : "bật"));
                        x += cell;
                    }
                }
            };
        }

        Seg TempSeg()
        {
            int c = Thermal.C;
            if (c == int.MinValue) return null;
            Color col = c >= 80 ? Color.FromArgb(255, 255, 69, 58) : c >= 65 ? Color.FromArgb(255, 255, 159, 10) : Color.White;
            return new Seg
            {
                W = S(62),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, "Nhiệt độ cảm biến bo mạch (ACPI): " + c + "°C\nKhông phải nhiệt độ lõi CPU", false);
                    Glass.Center(g, c + "°", Glass.Time, col, new Rectangle(r.X, r.Y + S(2), r.Width, S(20)));
                    Glass.Center(g, "NHIỆT ĐỘ", Glass.Label, Glass.Dim, new Rectangle(r.X, r.Y + S(23), r.Width, S(12)));
                }
            };
        }

        Seg WorldClockSeg()
        {
            List<string> ids = Config.Current.Cities.FindAll(id => !string.IsNullOrEmpty(id));
            if (ids.Count == 0) return null;
            int col = S(66);
            return new Seg
            {
                W = ids.Count * col + S(4),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tipText = new StringBuilder();
                    int x = r.X + S(2);
                    foreach (string id in ids)
                    {
                        DateTime t = Cities.Now(id);
                        string label = Cities.Label(id);
                        tipText.Append(label).Append(": ").Append(t.ToString("HH:mm, dd/MM")).Append('\n');
                        Glass.Center(g, label.ToUpper(), Glass.Label, Glass.Dim, new Rectangle(x, r.Y + S(4), col, S(12)));
                        Glass.Center(g, t.ToString("HH:mm"), Glass.Time, Color.White, new Rectangle(x, r.Y + S(16), col, S(20)));
                        x += col;
                    }
                    AddHit(r, null, b => Manager.OpenSettings(4), tipText + "Bấm để đổi thành phố", false);
                }
            };
        }

        Seg FlipClockSeg()
        {
            string hhmm = DateTime.Now.ToString("HHmm");
            int card = S(22), gap = S(3), colon = S(10);
            return new Seg
            {
                W = S(8) + 4 * card + 2 * gap + colon,
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), DateTime.Now.ToString("HH:mm, dd/MM/yyyy"), false);
                    int x = r.X + S(4), y = r.Y + S(4), h = r.Height - S(8);
                    for (int i = 0; i < 4; i++)
                    {
                        if (i == 2) { Glass.Center(g, ":", Glass.Flip, Glass.Dim, new Rectangle(x, y - S(2), colon, h)); x += colon; }
                        var c = new Rectangle(x, y, card, h);
                        Glass.Fill(g, c, S(4), Color.FromArgb(235, 26, 26, 30));
                        Glass.Center(g, hhmm[i].ToString(), Glass.Flip, Color.White, c);
                        using (var pen = new Pen(Color.FromArgb(160, 0, 0, 0), Math.Max(1, S(1)))) g.DrawLine(pen, c.X, c.Y + h / 2, c.Right, c.Y + h / 2); // the flap split
                        x += card + (i == 0 || i == 2 ? gap : 0);
                    }
                }
            };
        }

        Seg TimerSeg()
        {
            int left = Countdown.Remaining();
            bool running = Countdown.Running;
            return new Seg
            {
                W = S(122),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var text = new Rectangle(r.X, r.Y, r.Width - S(38), r.Height);
                    AddHit(text, null, b => Countdown.Reset(), "Hẹn giờ " + Config.Current.TimerMinutes + " phút\nBấm để đặt lại, lăn chuột để đổi số phút", false, d => Countdown.Adjust(d));
                    Glass.Text(g, "HẸN GIỜ", Glass.Day, Glass.Blue, new Rectangle(r.X + S(10), r.Y + S(3), S(80), S(14)), StringAlignment.Near);
                    Glass.Text(g, Mmss(left), Glass.Time, left == 0 ? Color.FromArgb(255, 255, 159, 10) : Color.White, new Rectangle(r.X + S(10), r.Y + S(17), S(80), S(20)), StringAlignment.Near);
                    RoundButton(g, new Rectangle(r.Right - S(34), r.Y + (r.Height - S(28)) / 2, S(28), S(28)), running ? MdlPause : MdlPlay, Glass.Blue, Countdown.Toggle, running ? "Tạm dừng" : "Bắt đầu");
                }
            };
        }

        Seg PomodoroSeg()
        {
            int left = Pomodoro.Remaining();
            bool running = Pomodoro.Running, focus = Pomodoro.Phase == 0;
            Color tint = focus ? Color.FromArgb(255, 255, 82, 70) : Color.FromArgb(255, 52, 199, 89);
            string label = Pomodoro.Label();
            return new Seg
            {
                W = S(170),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var text = new Rectangle(r.X, r.Y, r.Width - S(68), r.Height);
                    AddHit(text, null, b => Pomodoro.Reset(), "Pomodoro: " + Config.Current.FocusMin + " phút tập trung, " + Config.Current.BreakMin + " phút nghỉ\nBấm để làm lại từ đầu", false);
                    Glass.Text(g, label.ToUpper(), Glass.Day, tint, new Rectangle(r.X + S(10), r.Y + S(3), text.Width - S(10), S(14)), StringAlignment.Near);
                    Glass.Text(g, Mmss(left), Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(17), text.Width - S(10), S(20)), StringAlignment.Near);
                    int cy = r.Y + (r.Height - S(28)) / 2;
                    RoundButton(g, new Rectangle(r.Right - S(64), cy, S(28), S(28)), MdlNext, Color.FromArgb(50, 255, 255, 255), Pomodoro.Skip, "Bỏ qua");
                    RoundButton(g, new Rectangle(r.Right - S(34), cy, S(28), S(28)), running ? MdlPause : MdlPlay, tint, Pomodoro.Toggle, running ? "Tạm dừng" : "Bắt đầu");
                }
            };
        }

        Seg TodosSeg()
        {
            List<TodoItem> todos = Config.Current.Todos;
            int done = todos.FindAll(t => t.Done).Count, open = todos.Count - done;
            TodoItem next = todos.Find(t => !t.Done);
            return new Seg
            {
                W = S(178),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var ring = new Rectangle(r.X + S(6), r.Y + (r.Height - S(30)) / 2, S(30), S(30));
                    Rectangle ringScreen = ToScreen(r);
                    AddHit(ring, null, b =>
                    {
                        if (next != null) { next.Done = true; Config.Apply(); }
                        else OpenPopup("todos", () => new TodoPopup(ringScreen));
                    }, next != null ? "Đánh dấu xong: " + next.Text : "Không còn việc nào", false);
                    float w = S(3);
                    var rc = new RectangleF(ring.X + w / 2, ring.Y + w / 2, ring.Width - w, ring.Height - w);
                    using (var track = new Pen(Color.FromArgb(56, 255, 255, 255), w)) g.DrawEllipse(track, rc);
                    if (done > 0)
                        using (var bar = new Pen(Glass.Blue, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bar, rc, -90, 360f * done / todos.Count);
                    Glass.Center(g, open.ToString(), Glass.Value, Color.White, ring);
                    var text = new Rectangle(ring.Right + S(8), r.Y, r.Right - ring.Right - S(12), r.Height);
                    AddHit(text, null, b => OpenPopup("todos", () => new TodoPopup(ringScreen)), "Bấm để xem và thêm việc", false);
                    Glass.Text(g, next != null ? next.Text : "Việc cần làm", Glass.Row, Color.White, new Rectangle(text.X, r.Y + S(3), text.Width, S(17)), StringAlignment.Near);
                    Glass.Text(g, todos.Count == 0 ? "Bấm để thêm" : done + "/" + todos.Count + " đã xong", Glass.Date, Glass.Dim, new Rectangle(text.X, r.Y + S(20), text.Width, S(16)), StringAlignment.Near);
                }
            };
        }

        static readonly Color[] Greens = { Color.FromArgb(40, 255, 255, 255), Color.FromArgb(255, 14, 68, 41), Color.FromArgb(255, 0, 109, 50), Color.FromArgb(255, 38, 166, 65), Color.FromArgb(255, 57, 211, 83) };

        Seg GitHubSeg()
        {
            if (!GitHub.HasToken)
                return new Seg
                {
                    W = S(104),
                    Draw = (g, r) =>
                    {
                        Glass.Tile(g, r);
                        AddHit(r, null, b => Manager.OpenSettings(4), "GitHub: bấm để thêm token trong Cài đặt", false);
                        Glass.Center(g, "GitHub", Glass.Row, Color.White, new Rectangle(r.X, r.Y + S(3), r.Width, S(17)));
                        Glass.Center(g, "Thêm token", Glass.Date, Glass.Dim, new Rectangle(r.X, r.Y + S(20), r.Width, S(16)));
                    }
                };
            int[] days = GitHub.Days;
            int unread = GitHub.Unread, weeks = 12, cell = S(4), step = S(5);
            return new Seg
            {
                W = S(10) + weeks * step + S(52),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var t = new StringBuilder("GitHub");
                    if (GitHub.Login != null) t.Append(" @").Append(GitHub.Login).Append("\n").Append(GitHub.Total).Append(" đóng góp trong năm, chuỗi ").Append(GitHub.Streak).Append(" ngày");
                    if (unread >= 0) t.Append("\n").Append(unread >= 50 ? "50+" : unread.ToString()).Append(" thông báo chưa đọc");
                    if (GitHub.Error != null) t.Append("\n").Append(GitHub.Error);
                    AddHit(r, null, b => Sys.Launch("https://github.com/notifications"), t + "\nBấm để mở thông báo", false);
                    int max = 1;
                    foreach (int d in days) max = Math.Max(max, d);
                    int start = Math.Max(0, days.Length - weeks * 7), x = r.X + S(8), y = r.Y + (r.Height - 7 * step) / 2;
                    for (int i = 0; i < weeks * 7; i++)
                    {
                        int k = start + i, v = k < days.Length ? days[k] : 0;
                        int lvl = v == 0 ? 0 : Math.Min(4, 1 + 4 * (v - 1) / max);
                        using (var b = new SolidBrush(Greens[lvl])) g.FillRectangle(b, x + (i / 7) * step, y + (i % 7) * step, cell, cell);
                    }
                    var bell = new Rectangle(r.Right - S(48), r.Y, S(44), r.Height);
                    Glass.Text(g, MdlBell, Glass.MdlSmall, Glass.Dim, new Rectangle(bell.X, bell.Y, S(16), bell.Height), StringAlignment.Near);
                    Glass.Text(g, unread < 0 ? "…" : unread >= 50 ? "50+" : unread.ToString(), Glass.Row, Color.White, new Rectangle(bell.X + S(16), bell.Y, bell.Width - S(16), bell.Height), StringAlignment.Near);
                }
            };
        }
    }

    // Todo list above its tile. An ordinary opaque window, not clear glass, so the text box can render.
    class TodoPopup : Form
    {
        class Hot { public Rectangle R; public Action Click; }
        readonly TextBox input = new TextBox();
        readonly List<Hot> hots = new List<Hot>();
        readonly Rectangle anchor;
        int hover = -1;

        static int S(float v) { return Glass.S(v); }
        const int MaxRows = 10;

        public TodoPopup(Rectangle anchor)
        {
            this.anchor = anchor;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(255, 26, 26, 32);
            input.BorderStyle = BorderStyle.None;
            input.BackColor = Color.FromArgb(255, 44, 44, 54);
            input.ForeColor = Color.White;
            input.Font = Glass.Body;
            input.MaxLength = 200;
            input.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                string text = input.Text.Trim();
                if (text.Length == 0) return;
                Config.Current.Todos.Add(new TodoItem { Text = text });
                input.Text = "";
                Config.Apply();
                Layout2();
            };
            Controls.Add(input);
            Layout2();
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1, round = 2;
            N.DwmSetWindowAttribute(Handle, 20, ref on, 4);    // dark border
            N.DwmSetWindowAttribute(Handle, 33, ref round, 4); // rounded corners
        }

        List<TodoItem> Listed()
        {
            List<TodoItem> all = Config.Current.Todos;
            return all.GetRange(Math.Max(0, all.Count - MaxRows), Math.Min(MaxRows, all.Count));
        }

        // Height follows the list; the popup stays anchored above its tile.
        void Layout2()
        {
            int w = S(320), rows = Math.Max(1, Listed().Count), h = S(46) + rows * S(34) + S(52);
            Rectangle sb = Screen.FromRectangle(anchor).Bounds;
            int x = Math.Max(sb.Left + S(4), Math.Min(anchor.X + anchor.Width / 2 - w / 2, sb.Right - w - S(4)));
            Bounds = new Rectangle(x, anchor.Top - h - S(8), w, h);
            var box = new Rectangle(S(14), h - S(44), w - S(28), S(32));
            input.Bounds = new Rectangle(box.X + S(10), box.Y + (box.Height - input.PreferredHeight) / 2, box.Width - S(20), input.PreferredHeight);
            Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Sys.Foreground(Handle);
            Activate();
            input.Focus();
        }

        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); Close(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            hots.Clear();
            List<TodoItem> all = Config.Current.Todos, list = Listed();
            Glass.Text(g, "Việc cần làm", Glass.Header, Color.White, new Rectangle(S(16), S(10), S(160), S(24)), StringAlignment.Near);
            if (all.Exists(t => t.Done))
            {
                var clear = new Rectangle(Width - S(130), S(10), S(116), S(24));
                Glass.Text(g, "Xoá việc đã xong", Glass.Small, Glass.Blue, clear, StringAlignment.Far);
                hots.Add(new Hot { R = clear, Click = () => { all.RemoveAll(t => t.Done); Config.Apply(); Layout2(); } });
            }
            int y = S(42);
            if (list.Count == 0) Glass.Text(g, "Chưa có việc nào. Gõ bên dưới rồi nhấn Enter.", Glass.Small, Glass.Dim, new Rectangle(S(16), y, Width - S(32), S(34)), StringAlignment.Near);
            foreach (TodoItem t in list)
            {
                TodoItem item = t;
                var row = new Rectangle(S(8), y, Width - S(16), S(32));
                if (hots.Count == hover) Glass.Fill(g, row, S(6), Color.FromArgb(255, 38, 38, 46));
                var box = new Rectangle(row.X + S(8), row.Y, S(24), row.Height);
                Glass.Center(g, t.Done ? "" : "", Glass.Mdl, t.Done ? Glass.Blue : Glass.Dim, box);
                hots.Add(new Hot { R = new Rectangle(row.X, row.Y, row.Width - S(32), row.Height), Click = () => { item.Done = !item.Done; Config.Apply(); Invalidate(); } });
                Glass.Text(g, t.Text, t.Done ? Glass.Strike : Glass.Body, t.Done ? Glass.Dim : Color.White, new Rectangle(box.Right + S(6), row.Y, row.Width - S(76), row.Height), StringAlignment.Near);
                var del = new Rectangle(row.Right - S(30), row.Y, S(28), row.Height);
                Glass.Center(g, "✕", Glass.Sym, Color.FromArgb(120, 255, 255, 255), del);
                hots.Add(new Hot { R = del, Click = () => { all.Remove(item); Config.Apply(); Layout2(); } });
                y += S(34);
            }
            Glass.Fill(g, new Rectangle(S(14), Height - S(44), Width - S(28), S(32)), S(8), input.BackColor);
            if (input.Text.Length == 0 && !input.Focused) Glass.Text(g, "Thêm việc…", Glass.Body, Glass.Dim, input.Bounds, StringAlignment.Near);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = hots.FindIndex(x => x.R.Contains(e.Location));
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Hot h = hots.Find(x => x.R.Contains(e.Location));
            if (h != null) h.Click();
        }
    }
}
