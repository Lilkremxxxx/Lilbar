using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace Lilbar
{
    // Lilbar settings. An ordinary opaque window (glass windows make text boxes and dialogs see-through),
    // drawn in the dock's dark-glass palette. Every change is saved and applied at once.
    class SettingsForm : Form
    {
        class Hot { public Rectangle R; public Action Click; public bool Nav; }

        static SettingsForm current;
        static readonly string[] Tabs = { "Thanh", "Ứng dụng", "Khay hệ thống", "Thư mục", "Tiện ích" };
        static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
        {
            { "apps", new[] { "Ứng dụng", "App ghim và cửa sổ đang mở" } },
            { "folder", new[] { "Thư mục", "Nhóm app bấm vào để mở" } },
            { "system", new[] { "CPU / RAM", null } },
            { "claude", new[] { "Claude Code", "Đọc đăng nhập Claude Code, chỉ gửi tới api.anthropic.com" } },
            { "tray", new[] { "Khay hệ thống", "Mũi tên và app ghim cạnh nó" } },
            { "battery", new[] { "Pin", "Tự ẩn trên máy bàn" } },
            { "today", new[] { "Hôm nay & thời tiết", "Vị trí theo IP (ipinfo.io), thời tiết từ open-meteo.com" } },
            { "clock", new[] { "Đồng hồ", null } },
            { "media", new[] { "Đang phát", "Nhạc và video từ bất kỳ app nào báo cho Windows; tự ẩn khi không phát" } },
            { "network", new[] { "Mạng", "Tốc độ tải xuống và tải lên" } },
            { "volume", new[] { "Âm lượng", "Lăn chuột để chỉnh, bấm biểu tượng để tắt tiếng" } },
            { "toggles", new[] { "Wi-Fi / Bluetooth", "Bật tắt nhanh" } },
            { "temp", new[] { "Nhiệt độ", "Cảm biến bo mạch, không phải nhiệt độ lõi CPU" } },
            { "worldclock", new[] { "Đồng hồ thế giới", "Chọn thành phố trong tab Tiện ích" } },
            { "flipclock", new[] { "Đồng hồ lật", null } },
            { "timer", new[] { "Hẹn giờ", "Lăn chuột để đổi số phút" } },
            { "pomodoro", new[] { "Pomodoro", "Tập trung và nghỉ theo vòng" } },
            { "todos", new[] { "Việc cần làm", null } },
            { "github", new[] { "GitHub", "Thông báo và lịch đóng góp; cần token trong tab Tiện ích" } },
        };
        static readonly Color Bg = Color.FromArgb(255, 17, 17, 21), NavBg = Color.FromArgb(255, 23, 23, 29),
            CardBg = Color.FromArgb(255, 33, 33, 41), Edge = Color.FromArgb(255, 52, 52, 62), BoxBg = Color.FromArgb(255, 44, 44, 54);
        readonly List<Hot> hots = new List<Hot>();
        readonly TextBox nameBox = new TextBox(), tokenBox = new TextBox();
        readonly int navW;
        int tab, scroll, contentH;
        bool filling;

        static int S(float v) { return Glass.S(v); }

        public static void Open(int tab)
        {
            if (current == null || current.IsDisposed) { current = new SettingsForm(); current.Show(); }
            current.SetTab(tab);
            if (current.WindowState == FormWindowState.Minimized) current.WindowState = FormWindowState.Normal;
            Sys.Foreground(current.Handle);
            current.Activate();
        }

        public static void Ping() { if (current != null && !current.IsDisposed) current.Invalidate(); }
        public static void CloseIfOpen() { if (current != null && !current.IsDisposed) current.Close(); }

        SettingsForm()
        {
            Text = "Cài đặt Lilbar";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            navW = S(170);
            ClientSize = new Size(S(700), S(560));
            BackColor = Bg;
            DoubleBuffered = true;
            nameBox.BorderStyle = BorderStyle.None;
            nameBox.BackColor = BoxBg;
            nameBox.ForeColor = Color.White;
            nameBox.Font = Glass.Body;
            nameBox.MaxLength = 40;
            nameBox.Visible = false;
            nameBox.TextChanged += delegate
            {
                if (filling) return;
                Config.Current.FolderName = nameBox.Text;
                Config.Apply();
            };
            Controls.Add(nameBox);
            tokenBox.BorderStyle = BorderStyle.None;
            tokenBox.BackColor = BoxBg;
            tokenBox.ForeColor = Color.White;
            tokenBox.Font = Glass.Body;
            tokenBox.MaxLength = 255;
            tokenBox.UseSystemPasswordChar = true;
            tokenBox.Visible = false;
            tokenBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SaveToken(); } };
            Controls.Add(tokenBox);
            Config.Changed += Invalidate;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Config.Changed -= Invalidate;
            current = null;
            base.OnFormClosed(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            N.DwmSetWindowAttribute(Handle, 20, ref on, 4); // dark title bar
        }

        void SetTab(int t)
        {
            tab = Math.Max(0, Math.Min(Tabs.Length - 1, t));
            scroll = 0;
            filling = true;
            nameBox.Text = Config.Current.FolderName;
            filling = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            hots.Clear();

            using (var b = new SolidBrush(NavBg)) g.FillRectangle(b, 0, 0, navW, ClientSize.Height);
            Glass.Text(g, "Lilbar", Glass.Title, Color.White, new Rectangle(S(20), S(18), navW - S(30), S(30)), StringAlignment.Near);
            for (int i = 0; i < Tabs.Length; i++)
            {
                var r = new Rectangle(S(10), S(70) + i * S(40), navW - S(20), S(34));
                if (i == tab) Glass.Fill(g, r, S(8), Color.FromArgb(70, 74, 155, 255));
                Glass.Text(g, Tabs[i], Glass.Body, i == tab ? Color.White : Glass.Dim, new Rectangle(r.X + S(12), r.Y, r.Width - S(12), r.Height), StringAlignment.Near);
                int t = i;
                hots.Add(new Hot { R = r, Nav = true, Click = () => SetTab(t) });
            }

            var area = new Rectangle(navW, 0, ClientSize.Width - navW, ClientSize.Height);
            g.SetClip(area);
            int x = navW + S(24), w = area.Width - S(48), y = S(22) - scroll;
            bool showName = false, showToken = false;
            switch (tab)
            {
                case 0: PaintBar(g, x, w, ref y); break;
                case 1: PaintApps(g, x, w, ref y); break;
                case 2: PaintTray(g, x, w, ref y); break;
                case 3: showName = PaintFolder(g, x, w, ref y); break;
                case 4: showToken = PaintExtras(g, x, w, ref y); break;
            }
            contentH = y + scroll + S(20);
            g.ResetClip();
            if (nameBox.Visible != showName) nameBox.Visible = showName;
            if (tokenBox.Visible != showToken) tokenBox.Visible = showToken;
        }

        void Header(Graphics g, string text, int x, int w, ref int y)
        {
            y += S(6);
            Glass.Text(g, text, Glass.Header, Color.FromArgb(160, 255, 255, 255), new Rectangle(x, y, w, S(20)), StringAlignment.Near);
            y += S(26);
        }

        void Note(Graphics g, string text, int x, int w, ref int y)
        {
            Glass.Text(g, text, Glass.Small, Color.FromArgb(150, 255, 255, 255), new Rectangle(x, y, w, S(18)), StringAlignment.Near);
            y += S(26);
        }

        Rectangle Card(Graphics g, int x, int w, ref int y, Bitmap icon, string label, string sub)
        {
            var r = new Rectangle(x, y, w, S(48));
            Glass.Fill(g, r, S(10), CardBg);
            using (GraphicsPath p = Glass.Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), S(10)))
            using (var pen = new Pen(Edge)) g.DrawPath(pen, p);
            int tx = r.X + S(14);
            if (icon != null) { g.DrawImage(icon, new Rectangle(r.X + S(12), r.Y + S(14), S(20), S(20))); tx = r.X + S(42); }
            int tw = r.Right - tx - S(150);
            if (sub == null) Glass.Text(g, label, Glass.Body, Color.White, new Rectangle(tx, r.Y, tw, r.Height), StringAlignment.Near);
            else
            {
                Glass.Text(g, label, Glass.Body, Color.White, new Rectangle(tx, r.Y + S(5), tw, S(22)), StringAlignment.Near);
                Glass.Text(g, sub, Glass.Small, Color.FromArgb(150, 255, 255, 255), new Rectangle(tx, r.Y + S(25), tw, S(18)), StringAlignment.Near);
            }
            y += S(54);
            return r;
        }

        // Controls are laid out right to left; each returns the next free right edge.
        int Toggle(Graphics g, Rectangle row, int right, bool on, Action click)
        {
            var r = new Rectangle(right - S(40), row.Y + (row.Height - S(22)) / 2, S(40), S(22));
            Glass.Fill(g, r, S(11), on ? Glass.Blue : Color.FromArgb(255, 70, 70, 82));
            int k = S(16);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, on ? r.Right - k - S(3) : r.X + S(3), r.Y + S(3), k, k);
            hots.Add(new Hot { R = r, Click = click });
            return r.X - S(10);
        }

        int Button(Graphics g, Rectangle row, int right, string glyph, bool enabled, Action click)
        {
            var r = new Rectangle(right - S(28), row.Y + (row.Height - S(28)) / 2, S(28), S(28));
            Glass.Fill(g, r, S(7), Color.FromArgb(255, 48, 48, 58));
            Glass.Center(g, glyph, Glass.Sym, enabled ? Color.White : Color.FromArgb(70, 255, 255, 255), r);
            if (enabled) hots.Add(new Hot { R = r, Click = click });
            return r.X - S(6);
        }

        int Segment(Graphics g, Rectangle row, int right, string text, bool on, Action click)
        {
            var r = new Rectangle(right - S(60), row.Y + (row.Height - S(28)) / 2, S(60), S(28));
            Glass.Fill(g, r, S(7), on ? Glass.Blue : Color.FromArgb(255, 48, 48, 58));
            Glass.Center(g, text, Glass.Small, Color.White, r);
            hots.Add(new Hot { R = r, Click = click });
            return r.X - S(6);
        }

        static void Swap<T>(List<T> l, int a, int b)
        {
            T t = l[a]; l[a] = l[b]; l[b] = t;
            Config.Apply();
        }

        void PaintBar(Graphics g, int x, int w, ref int y)
        {
            Header(g, "VỊ TRÍ", x, w, ref y);
            Rectangle row = Card(g, x, w, ref y, null, "Căn các ô trên thanh", null);
            int right = row.Right - S(12);
            right = Segment(g, row, right, "Trái", Config.Current.Align == "left", () => { Config.Current.Align = "left"; Config.Apply(); });
            Segment(g, row, right, "Giữa", Config.Current.Align != "left", () => { Config.Current.Align = "center"; Config.Apply(); });

            Header(g, "WIDGET TRÊN THANH, TỪ TRÁI SANG PHẢI", x, w, ref y);
            List<ItemCfg> items = Config.Current.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ItemCfg it = items[i];
                int idx = i;
                string[] name;
                if (!Names.TryGetValue(it.Id, out name)) name = new[] { it.Id, null };
                row = Card(g, x, w, ref y, null, name[0], name[1]);
                right = row.Right - S(12);
                right = Toggle(g, row, right, it.On, () => { it.On = !it.On; Config.Apply(); });
                right = Button(g, row, right, "▼", idx < items.Count - 1, () => Swap(items, idx, idx + 1));
                Button(g, row, right, "▲", idx > 0, () => Swap(items, idx, idx - 1));
            }
        }

        void PaintApps(Graphics g, int x, int w, ref int y)
        {
            Header(g, "ỨNG DỤNG GHIM", x, w, ref y);
            Note(g, "Ghim thêm app: chuột phải app trong Start, chọn \"Ghim vào thanh tác vụ\".", x, w, ref y);
            List<Pin> pins = Model.OrderedPins(true);
            for (int i = 0; i < pins.Count; i++)
            {
                Pin p = pins[i];
                int idx = i;
                bool shown = !Config.Current.PinHidden.Contains(p.Name);
                Rectangle row = Card(g, x, w, ref y, Sys.IconOf(p.Exe.Length > 0 ? p.Exe : p.Lnk), p.Name, null);
                int right = row.Right - S(12);
                right = Toggle(g, row, right, shown, () =>
                {
                    if (shown) Config.Current.PinHidden.Add(p.Name); else Config.Current.PinHidden.Remove(p.Name);
                    Config.Apply();
                });
                right = Button(g, row, right, "▼", idx < pins.Count - 1, () => MovePin(pins, idx, 1));
                Button(g, row, right, "▲", idx > 0, () => MovePin(pins, idx, -1));
            }
            if (pins.Count == 0) Note(g, "Chưa có app ghim nào.", x, w, ref y);
        }

        static void MovePin(List<Pin> pins, int i, int d)
        {
            List<string> order = pins.ConvertAll(p => p.Name);
            string t = order[i]; order[i] = order[i + d]; order[i + d] = t;
            Config.Current.PinOrder = order;
            Config.Apply();
        }

        static List<string> WindowsPinned()
        {
            var l = new List<string>();
            foreach (TrayIcon t in TrayHost.Icons)
                if (t.Exe.Length > 0 && TrayHost.WindowsPromoted(t.Exe) && !l.Contains(t.Exe)) l.Add(t.Exe);
            return l;
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int n = s.IndexOfAny(new[] { '\r', '\n' });
            return (n >= 0 ? s.Substring(0, n) : s).Trim();
        }

        void PaintTray(Graphics g, int x, int w, ref int y)
        {
            Header(g, "KHAY HỆ THỐNG", x, w, ref y);
            Note(g, "App được ghim hiện cạnh mũi tên; các app khác nằm trong mũi tên.", x, w, ref y);
            bool follow = Config.Current.TrayPinned == null;
            Rectangle row = Card(g, x, w, ref y, null, "Theo lựa chọn của Windows", "Dùng danh sách biểu tượng khay trong Cài đặt Windows");
            Toggle(g, row, row.Right - S(12), follow, () => { Config.Current.TrayPinned = follow ? WindowsPinned() : null; Config.Apply(); });

            Header(g, "ĐANG CHẠY", x, w, ref y);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TrayIcon t in TrayHost.Icons.ToArray())
            {
                if (t.Exe.Length == 0 || !seen.Add(t.Exe)) continue;
                string exe = t.Exe;
                bool pinned = TrayHost.IsPinned(t);
                string name = FirstLine(t.Tip);
                if (name.Length == 0) name = Path.GetFileNameWithoutExtension(exe);
                row = Card(g, x, w, ref y, t.Icon, name, Path.GetFileName(exe));
                Toggle(g, row, row.Right - S(12), pinned, () =>
                {
                    if (Config.Current.TrayPinned == null) Config.Current.TrayPinned = WindowsPinned();
                    List<string> l = Config.Current.TrayPinned;
                    l.RemoveAll(s => string.Equals(s, exe, StringComparison.OrdinalIgnoreCase));
                    if (!pinned) l.Add(exe);
                    Config.Apply();
                });
            }
            if (seen.Count == 0) Note(g, "Chưa có app nào trong khay.", x, w, ref y);
        }

        bool PaintFolder(Graphics g, int x, int w, ref int y)
        {
            Header(g, "THƯ MỤC", x, w, ref y);
            Rectangle row = Card(g, x, w, ref y, null, "Tên", null);
            var box = new Rectangle(row.X + S(80), row.Y + (row.Height - S(30)) / 2, row.Width - S(96), S(30));
            Glass.Fill(g, box, S(6), BoxBg);
            var tb = new Rectangle(box.X + S(8), box.Y + (box.Height - nameBox.PreferredHeight) / 2, box.Width - S(16), nameBox.PreferredHeight);
            if (nameBox.Bounds != tb) nameBox.Bounds = tb;
            bool visible = box.Top >= 0 && box.Bottom <= ClientSize.Height;

            Header(g, "ỨNG DỤNG TRONG THƯ MỤC", x, w, ref y);
            List<string> apps = Config.Current.FolderApps;
            for (int i = 0; i < apps.Count; i++)
            {
                string path = apps[i];
                int idx = i;
                row = Card(g, x, w, ref y, Sys.IconOf(path), Path.GetFileNameWithoutExtension(path), path);
                int right = row.Right - S(12);
                right = Button(g, row, right, "✕", true, () => { apps.RemoveAt(idx); Config.Apply(); });
                right = Button(g, row, right, "▼", idx < apps.Count - 1, () => Swap(apps, idx, idx + 1));
                Button(g, row, right, "▲", idx > 0, () => Swap(apps, idx, idx - 1));
            }
            var add = new Rectangle(x, y, w, S(44));
            Glass.Fill(g, add, S(10), Color.FromArgb(255, 38, 72, 124));
            Glass.Center(g, "+  Thêm ứng dụng…", Glass.Body, Color.White, add);
            hots.Add(new Hot { R = add, Click = AddApps });
            y += S(52);
            return visible;
        }

        // A "label: value" row with − / + style stepper buttons on the right.
        void Stepper(Graphics g, int x, int w, ref int y, string label, string value, string less, string more, Action down, Action up)
        {
            Rectangle row = Card(g, x, w, ref y, null, label, null);
            int right = row.Right - S(12);
            right = Button(g, row, right, more, true, up);
            right = Button(g, row, right, less, true, down);
            Glass.Text(g, value, Glass.Body, Color.White, new Rectangle(right - S(130), row.Y, S(124), row.Height), StringAlignment.Far);
        }

        static int Step(int v, int d, int min, int max) { return Math.Max(min, Math.Min(max, v + d)); }

        bool PaintExtras(Graphics g, int x, int w, ref int y)
        {
            Config c = Config.Current;
            Header(g, "ĐỒNG HỒ THẾ GIỚI", x, w, ref y);
            for (int i = 0; i < c.Cities.Count; i++)
            {
                int slot = i;
                Action<int> move = d =>
                {
                    int n = Cities.All.Length, k = (Cities.IndexOf(c.Cities[slot]) + d + n) % n;
                    c.Cities[slot] = Cities.All[k][0];
                    Config.Apply();
                };
                Stepper(g, x, w, ref y, "Thành phố " + (i + 1), Cities.Label(c.Cities[i]), "◀", "▶", () => move(-1), () => move(1));
            }

            Header(g, "HẸN GIỜ VÀ POMODORO", x, w, ref y);
            Stepper(g, x, w, ref y, "Hẹn giờ", c.TimerMinutes + " phút", "−", "+",
                () => Countdown.Adjust(-1), () => Countdown.Adjust(1));
            Stepper(g, x, w, ref y, "Tập trung", c.FocusMin + " phút", "−", "+",
                () => { c.FocusMin = Step(c.FocusMin, -5, 5, 120); Pomodoro.Reset(); Config.Apply(); }, () => { c.FocusMin = Step(c.FocusMin, 5, 5, 120); Pomodoro.Reset(); Config.Apply(); });
            Stepper(g, x, w, ref y, "Nghỉ ngắn", c.BreakMin + " phút", "−", "+",
                () => { c.BreakMin = Step(c.BreakMin, -1, 1, 30); Pomodoro.Reset(); Config.Apply(); }, () => { c.BreakMin = Step(c.BreakMin, 1, 1, 30); Pomodoro.Reset(); Config.Apply(); });
            Stepper(g, x, w, ref y, "Nghỉ dài", c.LongBreakMin + " phút", "−", "+",
                () => { c.LongBreakMin = Step(c.LongBreakMin, -5, 5, 60); Pomodoro.Reset(); Config.Apply(); }, () => { c.LongBreakMin = Step(c.LongBreakMin, 5, 5, 60); Pomodoro.Reset(); Config.Apply(); });

            Header(g, "GITHUB", x, w, ref y);
            Note(g, "Tạo token tại github.com/settings/tokens, cần quyền notifications và read:user.", x, w, ref y);
            Rectangle row = Card(g, x, w, ref y, null, GitHub.HasToken ? "Token mới" : "Token", null);
            int right = Segment(g, row, row.Right - S(12), "Lưu", false, SaveToken);
            var box = new Rectangle(row.X + S(110), row.Y + (row.Height - S(30)) / 2, right - row.X - S(116), S(30));
            Glass.Fill(g, box, S(6), BoxBg);
            var tb = new Rectangle(box.X + S(8), box.Y + (box.Height - tokenBox.PreferredHeight) / 2, box.Width - S(16), tokenBox.PreferredHeight);
            if (tokenBox.Bounds != tb) tokenBox.Bounds = tb;
            bool visible = box.Top >= 0 && box.Bottom <= ClientSize.Height;
            if (GitHub.HasToken)
            {
                string status = GitHub.Error ?? (GitHub.Login != null ? "Đã kết nối @" + GitHub.Login : "Đã lưu, đang kết nối…");
                row = Card(g, x, w, ref y, null, "Đã lưu token", status);
                Button(g, row, row.Right - S(12), "✕", true, () =>
                {
                    c.GitHubToken = "";
                    GitHub.Login = null;
                    GitHub.Unread = -1;
                    Config.Apply();
                });
            }
            return visible;
        }

        void SaveToken()
        {
            string t = tokenBox.Text.Trim();
            if (t.Length == 0 || t.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0) return; // tokens are a single word
            Config.Current.GitHubToken = Secret.Protect(t);
            tokenBox.Text = "";
            GitHub.Error = null;
            GitHub.RefreshNow();
            Config.Apply();
        }

        void AddApps()
        {
            using (var d = new OpenFileDialog
            {
                Title = "Chọn ứng dụng cho thư mục",
                Filter = "Ứng dụng|*.exe;*.lnk;*.url|Tất cả|*.*",
                Multiselect = true,
                DereferenceLinks = false,
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                foreach (string f in d.FileNames)
                    if (!Config.Current.FolderApps.Contains(f)) Config.Current.FolderApps.Add(f);
                Config.Apply();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            foreach (Hot h in hots.ToArray())
                if (h.R.Contains(e.Location) && (h.Nav ? e.X < navW : e.X >= navW)) { h.Click(); Invalidate(); return; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int max = Math.Max(0, contentH - ClientSize.Height);
            scroll = Math.Max(0, Math.Min(max, scroll - e.Delta / 120 * S(54)));
            Invalidate();
        }
    }
}
