using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace Lilbar
{
    // Lilbar settings: a normal window with Windows 11's acrylic backdrop behind the whole client area, drawn as
    // translucent glass cards with hairline edges. Text entry goes through InputDialog (text boxes can't sit on glass).
    // Every change is saved and applied at once.
    class SettingsForm : Form
    {
        class Hot { public Rectangle R; public Action Click; public bool Nav; }
        // A row that can be dragged within its list; Move(from, to) gets indices in the list without the dragged row.
        class Row { public Rectangle R; public string List, Label, Sub; public Bitmap Icon; public int Index; public Action<int, int> Move; }

        static SettingsForm current;
        static readonly string[] Tabs = { "Thanh", "Ứng dụng", "Khay hệ thống", "Thư mục", "Tiện ích", "Tài khoản" };
        static readonly Color Bg = Color.FromArgb(70, 14, 16, 22), NavBg = Color.FromArgb(60, 0, 0, 0),
            CardBg = Color.FromArgb(20, 255, 255, 255), Edge = Color.FromArgb(40, 255, 255, 255), Plate = Color.FromArgb(34, 255, 255, 255);
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref N.RECT margins);
        readonly List<Hot> hots = new List<Hot>();
        Bitmap buf;
        readonly List<Row> rows = new List<Row>();
        Row pressed, dragged;
        Point downAt;
        int dragY;
        readonly int navW;
        int tab, scroll, contentH;

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
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            Config.Changed += Invalidate;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Config.Changed -= Invalidate;
            current = null;
            if (buf != null) buf.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1, acrylic = 3;
            N.DwmSetWindowAttribute(Handle, 20, ref on, 4); // dark title bar
            N.DwmSetWindowAttribute(Handle, 38, ref acrylic, 4); // DWMWA_SYSTEMBACKDROP_TYPE: acrylic (Windows 11 22H2+)
            var all = new N.RECT { Left = -1, Top = -1, Right = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(Handle, ref all); // the backdrop shows wherever our pixels are translucent
        }

        void SetTab(int t)
        {
            tab = Math.Max(0, Math.Min(Tabs.Length - 1, t));
            scroll = 0;
            Invalidate();
        }

        // Drawn into a premultiplied bitmap and copied raw, so translucent pixels let the acrylic through.
        protected override void OnPaint(PaintEventArgs e)
        {
            if (buf == null || buf.Size != ClientSize)
            {
                if (buf != null) buf.Dispose();
                buf = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            }
            using (Graphics g = Glass.Begin(buf)) PaintAll(g);
            Glass.Blit(e, buf);
        }

        void PaintAll(Graphics g)
        {
            using (var b = new SolidBrush(Bg)) g.FillRectangle(b, ClientRectangle);
            hots.Clear();
            rows.Clear();

            using (var b = new SolidBrush(NavBg)) g.FillRectangle(b, 0, 0, navW, ClientSize.Height);
            Glass.Text(g, "Lilbar", Glass.Title, Color.White, new Rectangle(S(20), S(18), navW - S(30), S(30)), StringAlignment.Near);
            for (int i = 0; i < Tabs.Length; i++)
            {
                var r = new Rectangle(S(10), S(70) + i * S(40), navW - S(20), S(34));
                if (i == tab) Glass.Fill(g, r, S(8), Color.FromArgb(110, 74, 155, 255));
                Glass.Text(g, Tabs[i], Glass.Body, i == tab ? Color.White : Glass.Dim, new Rectangle(r.X + S(12), r.Y, r.Width - S(12), r.Height), StringAlignment.Near);
                int t = i;
                hots.Add(new Hot { R = r, Nav = true, Click = () => SetTab(t) });
            }

            var area = new Rectangle(navW, 0, ClientSize.Width - navW, ClientSize.Height);
            g.SetClip(area);
            int x = navW + S(24), w = area.Width - S(48), y = S(22) - scroll;
            switch (tab)
            {
                case 0: PaintBar(g, x, w, ref y); break;
                case 1: PaintApps(g, x, w, ref y); break;
                case 2: PaintTray(g, x, w, ref y); break;
                case 3: PaintFolder(g, x, w, ref y); break;
                case 4: PaintExtras(g, x, w, ref y); break;
                case 5: PaintAccounts(g, x, w, ref y); break;
            }
            contentH = y + scroll + S(20);
            if (dragged != null) PaintDrag(g, x, w);
            g.ResetClip();
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
            if (icon != null) { Glass.Image(g, icon, new Rectangle(r.X + S(12), r.Y + S(14), S(20), S(20))); tx = r.X + S(42); }
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
            Glass.Fill(g, r, S(11), on ? Glass.Blue : Color.FromArgb(70, 255, 255, 255));
            int k = S(16);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, on ? r.Right - k - S(3) : r.X + S(3), r.Y + S(3), k, k);
            hots.Add(new Hot { R = r, Click = click });
            return r.X - S(10);
        }

        int Button(Graphics g, Rectangle row, int right, string glyph, bool enabled, Action click)
        {
            var r = new Rectangle(right - S(28), row.Y + (row.Height - S(28)) / 2, S(28), S(28));
            Glass.Fill(g, r, S(7), Plate);
            Glass.Center(g, glyph, Glass.Sym, enabled ? Color.White : Color.FromArgb(70, 255, 255, 255), r);
            if (enabled) hots.Add(new Hot { R = r, Click = click });
            return r.X - S(6);
        }

        int Segment(Graphics g, Rectangle row, int right, string text, bool on, Action click)
        {
            var r = new Rectangle(right - S(60), row.Y + (row.Height - S(28)) / 2, S(60), S(28));
            Glass.Fill(g, r, S(7), on ? Glass.Blue : Plate);
            Glass.Center(g, text, Glass.Small, Color.White, r);
            hots.Add(new Hot { R = r, Click = click });
            return r.X - S(6);
        }

        static void MoveIn<T>(List<T> l, int from, int to)
        {
            T t = l[from];
            l.RemoveAt(from);
            l.Insert(Math.Max(0, Math.Min(l.Count, to)), t);
            Config.Apply();
        }

        // A card that can be dragged up or down within its list (a grip on the left shows it).
        Rectangle DragCard(Graphics g, int x, int w, ref int y, string list, int index, Bitmap icon, string label, string sub, Action<int, int> move)
        {
            bool isDragged = dragged != null && dragged.List == list && dragged.Index == index;
            Rectangle r;
            if (isDragged)
            {
                r = new Rectangle(x, y, w, S(48));
                using (GraphicsPath p = Glass.Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), S(10)))
                using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255)) { DashStyle = DashStyle.Dash }) g.DrawPath(pen, p);
                y += S(54);
            }
            else r = Card(g, x + S(18), w - S(18), ref y, icon, label, sub);
            if (!isDragged) Glass.Center(g, "", Glass.MdlSmall, Color.FromArgb(110, 255, 255, 255), new Rectangle(x, r.Y, S(18), r.Height)); // grip
            rows.Add(new Row { R = new Rectangle(x, r.Y, w, r.Height), List = list, Label = label, Sub = sub, Icon = icon, Index = index, Move = move });
            return r;
        }

        // Insertion point: before the first other row of the list whose centre is below the pointer.
        int DropAt(List<Row> list)
        {
            int i = 0;
            foreach (Row r in list)
            {
                if (r.Index == dragged.Index) continue;
                if (dragY < r.R.Y + r.R.Height / 2) return i;
                i++;
            }
            return i;
        }

        void PaintDrag(Graphics g, int x, int w)
        {
            List<Row> list = rows.FindAll(q => q.List == dragged.List && q.Index != dragged.Index);
            int at = DropAt(rows.FindAll(q => q.List == dragged.List));
            if (list.Count > 0)
            {
                int ly = at < list.Count ? list[at].R.Y - S(4) : list[list.Count - 1].R.Bottom + S(2);
                Glass.Fill(g, new Rectangle(x, ly - S(1), w, S(3)), S(1), Glass.Blue);
            }
            int cy = dragY - S(24), y = cy;
            Rectangle r = Card(g, x + S(18), w - S(18), ref y, dragged.Icon, dragged.Label, dragged.Sub);
            using (GraphicsPath p = Glass.Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), S(10)))
            using (var pen = new Pen(Glass.Blue, 1.5f)) g.DrawPath(pen, p);
        }

        void PaintBar(Graphics g, int x, int w, ref int y)
        {
            Header(g, "VỊ TRÍ", x, w, ref y);
            Rectangle row = Card(g, x, w, ref y, null, "Căn các ô trên thanh", null);
            int right = row.Right - S(12);
            right = Segment(g, row, right, "Trái", Config.Current.Align == "left", () => { Config.Current.Align = "left"; Config.Apply(); });
            Segment(g, row, right, "Giữa", Config.Current.Align != "left", () => { Config.Current.Align = "center"; Config.Apply(); });
            int ts = Config.Current.TextScale;
            Stepper(g, x, w, ref y, "Cỡ chữ", ts + "%", "−", "+",
                () => { Config.Current.TextScale = Step(ts, -10, 80, 120); Config.Apply(); Manager.ApplyTextScale(); },
                () => { Config.Current.TextScale = Step(ts, 10, 80, 120); Config.Apply(); Manager.ApplyTextScale(); });
            bool repl = Config.Current.Replace;
            row = Card(g, x, w, ref y, null, "Thay hẳn taskbar Windows", "Thử nghiệm · tắt thì widget nằm ngay trên taskbar gốc · Lilbar tự khởi động lại");
            Toggle(g, row, row.Right - S(12), repl, () => { Config.Current.Replace = !repl; Config.Apply(); Startup.Restart(); });
            bool auto = Startup.On;
            row = Card(g, x, w, ref y, null, "Khởi động cùng Windows", "Mở Lilbar khi đăng nhập");
            Toggle(g, row, row.Right - S(12), auto, () => Startup.Set(!auto));

            Header(g, "ĐANG DÙNG, TỪ TRÁI SANG PHẢI", x, w, ref y);
            Note(g, "Quá nhiều ô thì các ô cuối sẽ không vừa màn hình và bị ẩn.", x, w, ref y);
            List<ItemCfg> items = Config.Current.Items;
            List<int> on = new List<int>();
            for (int i = 0; i < items.Count; i++) if (items[i].On) on.Add(i);
            Note(g, "Kéo thả để đổi thứ tự, hoặc chuột phải thanh → Chỉnh sửa thanh để kéo ngay trên dock.", x, w, ref y);
            Action<int, int> moveOn = (from, to) =>
            {
                List<ItemCfg> active = items.FindAll(i => i.On);
                ItemCfg m = active[from];
                active.RemoveAt(from);
                active.Insert(Math.Max(0, Math.Min(active.Count, to)), m);
                List<ItemCfg> off = items.FindAll(i => !i.On);
                items.Clear();
                items.AddRange(active);
                items.AddRange(off);
                Config.Apply();
            };
            for (int k = 0; k < on.Count; k++)
            {
                ItemCfg it = items[on[k]];
                CatalogItem c = Catalog.Get(it.Id);
                row = DragCard(g, x, w, ref y, "bar", k, null, c.Name, c.Group + (c.Sub != null ? " · " + c.Sub : ""), moveOn);
                int rr = Toggle(g, row, row.Right - S(12), true, () => { it.On = false; Config.Apply(); });
                if (!Manager.Replace && it.Id != "apps" && it.Id != "tray")
                    Segment(g, row, rr, it.Side == "left" ? "Trái" : "Phải", false, () => { it.Side = it.Side == "left" ? null : "left"; Config.Apply(); });
            }

            Header(g, "THƯ VIỆN WIDGET", x, w, ref y);
            Note(g, "Bật một widget để thêm vào cuối thanh, rồi kéo nó tới chỗ muốn đặt.", x, w, ref y);
            foreach (string group in Catalog.Groups)
            {
                List<ItemCfg> lib = items.FindAll(i => !i.On && Catalog.Get(i.Id).Group == group);
                if (lib.Count == 0) continue;
                Header(g, group.ToUpper(), x, w, ref y);
                foreach (ItemCfg it in lib)
                {
                    ItemCfg item = it;
                    CatalogItem c = Catalog.Get(it.Id);
                    row = Card(g, x, w, ref y, null, c.Name, c.Sub);
                    Toggle(g, row, row.Right - S(12), false, () => { items.Remove(item); item.On = true; items.Add(item); Config.Apply(); });
                }
            }
        }

        void PaintAccounts(Graphics g, int x, int w, ref int y)
        {
            Header(g, "TÀI KHOẢN VÀ THÔNG SỐ", x, w, ref y);
            Note(g, "Key được mã hoá theo tài khoản Windows của bạn và chỉ gửi tới đúng dịch vụ của nó.", x, w, ref y);
            string service = null;
            foreach (AccountField f in Acct.Fields)
            {
                if (f.Service != service) { service = f.Service; Header(g, service.ToUpper(), x, w, ref y); }
                AccountField field = f;
                string v = Acct.Get(f.Key);
                Rectangle row = Card(g, x, w, ref y, null, f.Label, v.Length == 0 ? "Chưa đặt" : f.Secret ? "Đã lưu" : v);
                int right = Segment(g, row, row.Right - S(12), "Sửa", false, () =>
                {
                    string nv = InputDialog.Ask(this, field.Service + ": " + field.Label, field.Hint, field.Secret ? "" : Acct.Get(field.Key), field.Secret);
                    if (nv == null) return;
                    Acct.Set(field.Key, nv);
                    if (field.Key == "ics") Calendar.RefreshNow();
                });
                if (v.Length > 0 && field.Secret) Button(g, row, right, "✕", true, () => Acct.Set(field.Key, ""));
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
                Rectangle row = DragCard(g, x, w, ref y, "pins", idx, Sys.IconOf(p.Exe.Length > 0 ? p.Exe : p.Lnk, S(20)), p.Name, null, (from, to) =>
                {
                    List<string> order = pins.ConvertAll(pp => pp.Name);
                    Config.Current.PinOrder = order;
                    MoveIn(order, from, to);
                });
                Toggle(g, row, row.Right - S(12), shown, () =>
                {
                    if (shown) Config.Current.PinHidden.Add(p.Name); else Config.Current.PinHidden.Remove(p.Name);
                    Config.Apply();
                });
            }
            if (pins.Count == 0) Note(g, "Chưa có app ghim nào.", x, w, ref y);
            else Note(g, "Kéo thả để đổi thứ tự.", x, w, ref y);
        }

        internal static List<string> WindowsPinned()
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

        void PaintFolder(Graphics g, int x, int w, ref int y)
        {
            Header(g, "THƯ MỤC", x, w, ref y);
            Rectangle row = Card(g, x, w, ref y, null, "Tên", Config.Current.FolderName);
            Segment(g, row, row.Right - S(12), "Sửa", false, () =>
            {
                string v = InputDialog.Ask(this, "Tên thư mục", "Tên hiện trên ô thư mục", Config.Current.FolderName, false);
                if (v == null) return;
                Config.Current.FolderName = v.Length > 40 ? v.Substring(0, 40) : v;
                Config.Apply();
            });

            Header(g, "ỨNG DỤNG TRONG THƯ MỤC", x, w, ref y);
            List<string> apps = Config.Current.FolderApps;
            for (int i = 0; i < apps.Count; i++)
            {
                string path = apps[i];
                int idx = i;
                row = DragCard(g, x, w, ref y, "folder", idx, Sys.IconOf(path, S(20)), Path.GetFileNameWithoutExtension(path), path, (from, to) => MoveIn(apps, from, to));
                Button(g, row, row.Right - S(12), "✕", true, () => { apps.RemoveAt(idx); Config.Apply(); });
            }
            var add = new Rectangle(x, y, w, S(44));
            Glass.Fill(g, add, S(10), Color.FromArgb(150, 74, 155, 255));
            Glass.Center(g, "+  Thêm ứng dụng…", Glass.Body, Color.White, add);
            hots.Add(new Hot { R = add, Click = AddApps });
            y += S(52);
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

        void PaintExtras(Graphics g, int x, int w, ref int y)
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
            Rectangle row = Card(g, x, w, ref y, null, GitHub.HasToken ? "Token mới" : "Token", GitHub.HasToken ? "Thay token đã lưu" : "Chưa đặt");
            Segment(g, row, row.Right - S(12), "Nhập", false, () =>
            {
                string v = InputDialog.Ask(this, "GitHub: token", "Personal access token (notifications, read:user)", "", true);
                if (v != null) SaveToken(v);
            });
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
        }

        void SaveToken(string raw)
        {
            string t = raw.Trim();
            if (t.Length == 0 || t.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0) return; // tokens are a single word
            Config.Current.GitHubToken = Secret.Protect(t);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = null;
            if (e.Button != MouseButtons.Left || e.X < navW) return;
            if (hots.Exists(h => !h.Nav && h.R.Contains(e.Location))) return; // a switch or button, not a drag
            pressed = rows.Find(r => r.R.Contains(e.Location));
            downAt = e.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressed == null || e.Button != MouseButtons.Left) return;
            if (dragged == null && Math.Abs(e.Y - downAt.Y) < S(5)) return;
            dragged = pressed;
            dragY = e.Y;
            Cursor = Cursors.SizeNS;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragged != null)
            {
                Row d = dragged;
                int to = DropAt(rows.FindAll(r => r.List == d.List));
                dragged = null;
                pressed = null;
                Cursor = Cursors.Default;
                if (to != d.Index) d.Move(d.Index, to);
                Invalidate();
                return;
            }
            pressed = null;
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
