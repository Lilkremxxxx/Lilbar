using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Lilbar
{
    class CatalogItem
    {
        public string Id, Group, Name, Sub;
        public bool On;
        public CatalogItem(string id, string group, string name, string sub, bool on) { Id = id; Group = group; Name = name; Sub = sub; On = on; }
    }

    // Every tile Lilbar can show. Order here is the default order on the bar; tiles turned on later go to the end.
    static class Catalog
    {
        public const string Clocks = "Đồng hồ", Sys = "Hệ thống", Files = "Ứng dụng & tệp", MediaG = "Media", Work = "Năng suất",
            WeatherG = "Thời tiết", AI = "AI", Online = "Trực tuyến", Stocks = "Chứng khoán", Money = "Doanh thu", Stats = "Phân tích",
            Dev = "Lập trình", Social = "Mạng xã hội";
        public static readonly string[] Groups = { Clocks, Sys, Files, MediaG, Work, WeatherG, AI, Online, Stocks, Money, Stats, Dev, Social };

        public static readonly CatalogItem[] All =
        {
            // on by default, in bar order
            new CatalogItem("apps", Files, "Ứng dụng", "App ghim và cửa sổ đang mở", true),
            new CatalogItem("folder", Files, "Thư mục", "Nhóm app bấm vào để mở", true),
            new CatalogItem("media", MediaG, "Đang phát", "Ảnh bìa, tên bài và nút điều khiển; tự ẩn khi không phát", true),
            new CatalogItem("system", Sys, "CPU / RAM", null, true),
            new CatalogItem("network", Sys, "Mạng", "Tốc độ tải xuống và tải lên", true),
            new CatalogItem("claude", AI, "Claude Code", "Đọc đăng nhập Claude Code, chỉ gửi tới api.anthropic.com", true),
            new CatalogItem("todos", Work, "Việc cần làm", null, true),
            new CatalogItem("tray", Sys, "Khay hệ thống", "Mũi tên và app ghim cạnh nó", true),
            new CatalogItem("volume", MediaG, "Âm lượng", "Lăn chuột để chỉnh, bấm biểu tượng để tắt tiếng", true),
            new CatalogItem("battery", Sys, "Pin", "Tự ẩn trên máy bàn", true),
            new CatalogItem("today", Clocks, "Hôm nay & thời tiết", "Vị trí theo IP (ipinfo.io), thời tiết từ open-meteo.com", true),
            // library
            new CatalogItem("clock", Clocks, "Đồng hồ", null, false),
            new CatalogItem("flipclock", Clocks, "Đồng hồ lật", null, false),
            new CatalogItem("segment", Clocks, "Đồng hồ LED", "Số bảy đoạn dưới thứ và ngày", false),
            new CatalogItem("analog", Clocks, "Đồng hồ kim, mặt sáng", null, false),
            new CatalogItem("analogdark", Clocks, "Đồng hồ kim, mặt tối", null, false),
            new CatalogItem("analogmin", Clocks, "Đồng hồ kim tối giản", "Chỉ kim và vạch giờ", false),
            new CatalogItem("worldclock", Clocks, "Đồng hồ thế giới", "Chọn thành phố trong tab Tiện ích", false),
            new CatalogItem("worldanalog", Clocks, "Đồng hồ thế giới, kim", "Ba mặt kim nhỏ", false),
            new CatalogItem("worldone", Clocks, "Một thành phố", "Thành phố đầu tiên, chữ lớn, kèm chênh lệch giờ", false),
            new CatalogItem("date", Clocks, "Ngày", "Ngày tháng chữ lớn", false),
            new CatalogItem("daynum", Clocks, "Tờ lịch", null, false),
            new CatalogItem("weekstrip", Clocks, "7 ngày", "Tuần này và nhiệt độ hôm nay", false),
            new CatalogItem("clockweather", Clocks, "Giờ, ngày & nhiệt độ", "Ba khối màu", false),

            new CatalogItem("cpu", Sys, "CPU", null, false),
            new CatalogItem("ram", Sys, "RAM", null, false),
            new CatalogItem("temp", Sys, "Nhiệt độ", "Cảm biến bo mạch, không phải nhiệt độ lõi CPU", false),
            new CatalogItem("toggles", Sys, "Wi-Fi / Bluetooth", "Bật tắt nhanh", false),
            new CatalogItem("language", Sys, "Bàn phím", "Bố cục bàn phím đang dùng; bấm để đổi", false),
            new CatalogItem("theme", Sys, "Sáng / tối", "Đổi chế độ màu của Windows", false),
            new CatalogItem("storage", Sys, "Ổ đĩa", "Dung lượng trống ổ C", false),
            new CatalogItem("dayprog", Sys, "Tiến độ ngày", null, false),
            new CatalogItem("monthprog", Sys, "Tiến độ tháng", null, false),
            new CatalogItem("yearprog", Sys, "Tiến độ năm", "Mỗi chấm là một tuần", false),

            new CatalogItem("recent", Files, "Mở gần đây", "Cần bật \"Hiện mục mở gần đây\" trong Cài đặt Windows → Cá nhân hoá → Bắt đầu", false),
            new CatalogItem("downloads", Files, "Tải về", "Tệp mới nhất trong Downloads", false),
            new CatalogItem("screenshots", Files, "Ảnh chụp màn hình", "Ba ảnh mới nhất", false),
            new CatalogItem("snip", Files, "Chụp màn hình", "Mở công cụ cắt màn hình của Windows", false),
            new CatalogItem("recycle", Files, "Thùng rác", "Bấm để dọn, Windows sẽ hỏi lại trước khi xoá", false),
            new CatalogItem("search", Files, "Tìm kiếm", "Tìm trên web hoặc mở một thư mục", false),
            new CatalogItem("commands", Files, "Lệnh", "Gõ tên app để mở", false),

            new CatalogItem("mediacompact", MediaG, "Đang phát, gọn", "Tên bài và nút phát", false),
            new CatalogItem("mediamini", MediaG, "Đang phát, nút tròn", "Nút phát có vòng tiến độ", false),
            new CatalogItem("mediabar", MediaG, "Đang phát, thanh tiến độ", null, false),

            new CatalogItem("timer", Work, "Hẹn giờ", "Lăn chuột để đổi số phút", false),
            new CatalogItem("pomodoro", Work, "Pomodoro", "Tập trung và nghỉ theo vòng", false),
            new CatalogItem("stopwatch", Work, "Bấm giờ", "Có ghi vòng", false),
            new CatalogItem("notes", Work, "Ghi chú", null, false),
            new CatalogItem("calendar", Work, "Lịch", "Sự kiện tiếp theo từ địa chỉ iCal trong tab Tài khoản", false),
            new CatalogItem("currency", Work, "Đổi tiền", "Tỷ giá từ open.er-api.com", false),
            new CatalogItem("screentime", Work, "Thời gian dùng máy", "Chỉ lưu trên máy này", false),
            new CatalogItem("timeline", Work, "Dòng thời gian trong ngày", "Giờ dùng máy theo loại app", false),

            new CatalogItem("weather", WeatherG, "Thời tiết", null, false),
            new CatalogItem("weatherdetail", WeatherG, "Chi tiết thời tiết", "Gió, độ ẩm, khả năng mưa", false),
            new CatalogItem("weatherstatus", WeatherG, "Tình trạng thời tiết", "Bằng chữ, kèm thành phố", false),
            new CatalogItem("hourly", WeatherG, "Theo giờ", "Sáu giờ tới", false),
            new CatalogItem("forecast", WeatherG, "Dự báo", "Bốn ngày tới", false),
            new CatalogItem("todayforecast", WeatherG, "Hôm nay & dự báo", null, false),
            new CatalogItem("sun", WeatherG, "Mặt trời", "Giờ mọc và lặn", false),

            new CatalogItem("github", Online, "GitHub", "Thông báo và lịch đóng góp; token trong tab Tiện ích", false),
            new CatalogItem("githubact", Online, "GitHub, hoạt động", "Lưới đóng góp 20 tuần và chuỗi ngày", false),
            new CatalogItem("githubov", Online, "GitHub, tổng quan", "Thông báo, PR mở, sao, cần review", false),
            new CatalogItem("leetcode", Online, "LeetCode", "Số bài và chuỗi ngày theo tên người dùng", false),
            new CatalogItem("metric", Online, "Giá crypto", "Một cặp từ Binance, mặc định BTCUSDT", false),
            new CatalogItem("wallpaper", Online, "Hình nền", "Ảnh Bing mỗi ngày, bấm để đặt làm hình nền", false),

            new CatalogItem("stock", Stocks, "Cổ phiếu", "Mã đầu tiên trong danh sách, từ Yahoo Finance", false),
            new CatalogItem("stockchart", Stocks, "Cổ phiếu, biểu đồ", "Giá trong phiên", false),
            new CatalogItem("watchlist", Stocks, "Danh sách theo dõi", "Ba mã đầu tiên", false),
        };

        public static CatalogItem Get(string id) { return Array.Find(All, c => c.Id == id); }
    }

    // A value Lilbar needs from the user: an API key, a username, a symbol. Secret ones are kept DPAPI-encrypted.
    class AccountField
    {
        public string Key, Service, Label, Hint, Default;
        public bool Secret;
        public AccountField(string key, string service, string label, bool secret, string hint, string def)
        { Key = key; Service = service; Label = label; Secret = secret; Hint = hint; Default = def; }
    }

    static class Acct
    {
        public static readonly List<AccountField> Fields = new List<AccountField>
        {
            new AccountField("ics", "Lịch", "Địa chỉ iCal", true, "Google Calendar: Cài đặt → lịch của bạn → Địa chỉ bí mật ở định dạng iCal. Chỉ gửi tới chính địa chỉ đó.", ""),
            new AccountField("cur_from", "Đổi tiền", "Đổi từ", false, "Mã tiền tệ, ví dụ USD", "USD"),
            new AccountField("cur_to", "Đổi tiền", "Sang", false, "Mã tiền tệ, ví dụ VND", "VND"),
            new AccountField("cur_amount", "Đổi tiền", "Số tiền", false, "Ví dụ 100", "1"),
            new AccountField("stocks", "Chứng khoán", "Mã theo dõi", false, "Mã Yahoo Finance, cách nhau bằng dấu phẩy. Cổ phiếu Việt Nam thêm .VN, ví dụ VNM.VN", "AAPL,MSFT,NVDA"),
            new AccountField("metric", "Crypto", "Cặp giá", false, "Cặp trên Binance, ví dụ BTCUSDT, ETHUSDT", "BTCUSDT"),
            new AccountField("leetcode", "LeetCode", "Tên người dùng", false, "Tên trong đường dẫn leetcode.com/u/<tên>", ""),
        };

        public static AccountField Field(string key) { return Fields.Find(f => f.Key == key); }

        public static string Get(string key)
        {
            AccountField f = Field(key);
            string v;
            if (f != null && f.Secret) return Config.Current.Keys.TryGetValue(key, out v) ? Secret.Unprotect(v) ?? "" : "";
            if (Config.Current.Values.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            return f != null ? f.Default : "";
        }

        public static bool Has(string key) { return Get(key).Length > 0; }

        public static void Set(string key, string value)
        {
            AccountField f = Field(key);
            value = (value ?? "").Trim();
            if (f != null && f.Secret)
            {
                if (value.Length == 0) Config.Current.Keys.Remove(key);
                else Config.Current.Keys[key] = Secret.Protect(value);
            }
            else if (value.Length == 0) Config.Current.Values.Remove(key);
            else Config.Current.Values[key] = value;
            Config.Apply();
        }
    }

    // A one-line input dialog in the settings palette.
    class InputDialog : Form
    {
        readonly TextBox box = new TextBox();

        static int S(float v) { return Glass.S(v); }

        InputDialog(string title, string hint, string value, bool secret)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(255, 26, 26, 32);
            ForeColor = Color.White;
            Font = Glass.Small;
            ClientSize = new Size(S(460), S(150));
            var label = new Label { Text = hint ?? "", AutoSize = false, Bounds = new Rectangle(S(16), S(12), S(428), S(40)), ForeColor = Glass.Dim };
            box.Bounds = new Rectangle(S(16), S(58), S(428), S(28));
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.FromArgb(255, 44, 44, 54);
            box.ForeColor = Color.White;
            box.Font = Glass.Body;
            box.Text = value ?? "";
            box.UseSystemPasswordChar = secret;
            var ok = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Bounds = new Rectangle(S(266), S(104), S(84), S(30)), FlatStyle = FlatStyle.Flat, BackColor = Glass.Blue };
            var cancel = new Button { Text = "Huỷ", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(S(360), S(104), S(84), S(30)), FlatStyle = FlatStyle.Flat };
            Controls.AddRange(new Control[] { label, box, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            N.DwmSetWindowAttribute(Handle, 20, ref on, 4);
        }

        // Returns the new value, or null when cancelled.
        public static string Ask(IWin32Window owner, string title, string hint, string value, bool secret)
        {
            using (var d = new InputDialog(title, hint, value, secret))
                return d.ShowDialog(owner) == DialogResult.OK ? d.box.Text.Trim() : null;
        }
    }

    // Popups that need real text input: opaque (glass makes text boxes see-through), rounded, close on focus loss.
    class OpaquePopup : Form
    {
        protected readonly Rectangle AnchorRect;
        protected static int S(float v) { return Glass.S(v); }

        public OpaquePopup(Rectangle anchor, int width, int height)
        {
            AnchorRect = anchor;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(255, 26, 26, 32);
            ForeColor = Color.White;
            Place(width, height);
        }

        protected void Place(int w, int h)
        {
            Rectangle sb = Screen.FromRectangle(AnchorRect).Bounds;
            int x = Math.Max(sb.Left + S(4), Math.Min(AnchorRect.X + AnchorRect.Width / 2 - w / 2, sb.Right - w - S(4)));
            Bounds = new Rectangle(x, AnchorRect.Top - h - S(8), w, h);
        }

        protected TextBox MakeBox(Rectangle r, bool multiline)
        {
            var b = new TextBox
            {
                BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(255, 44, 44, 54), ForeColor = Color.White,
                Font = Glass.Body, Multiline = multiline, Bounds = r,
            };
            if (multiline) { b.ScrollBars = ScrollBars.Vertical; b.AcceptsReturn = true; }
            Controls.Add(b);
            return b;
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
            N.DwmSetWindowAttribute(Handle, 20, ref on, 4);
            N.DwmSetWindowAttribute(Handle, 33, ref round, 4);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Sys.Foreground(Handle);
            Activate();
        }

        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); Close(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }
    }
}
