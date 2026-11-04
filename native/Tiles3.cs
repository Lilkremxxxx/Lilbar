using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Lilbar
{
    // Batch 2 tiles: stocks, crypto, LeetCode, wallpapers, GitHub activity and overview.
    partial class Dock
    {
        static Color Trend(double v) { return v >= 0 ? Color.FromArgb(255, 52, 199, 89) : Color.FromArgb(255, 255, 82, 70); }
        static string Pct(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.00", CultureInfo.InvariantCulture) + "%"; }
        static string Price(double v) { return double.IsNaN(v) ? "…" : v.ToString(v >= 1000 ? "#,0.00" : v >= 1 ? "0.00" : "0.0000", CultureInfo.InvariantCulture); }

        static void Spark(Graphics g, RectangleF r, List<double> pts, Color c, bool fill)
        {
            if (pts.Count < 2) return;
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (double v in pts) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            if (hi - lo < 1e-9) hi = lo + 1;
            var line = new PointF[pts.Count];
            for (int i = 0; i < pts.Count; i++) line[i] = new PointF(r.X + r.Width * i / (pts.Count - 1), r.Bottom - (float)((pts[i] - lo) / (hi - lo) * r.Height));
            if (fill)
            {
                var poly = new List<PointF>(line) { new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) };
                using (var b = new SolidBrush(Color.FromArgb(40, c))) g.FillPolygon(b, poly.ToArray());
            }
            using (var p = new Pen(c, 1.4f * Glass.Scale) { LineJoin = LineJoin.Round }) g.DrawLines(p, line);
        }

        Seg StockSeg(bool chart)
        {
            List<string> syms = Stocks.Symbols();
            Stocks.Quote q;
            if (syms.Count == 0 || !Stocks.Quotes.TryGetValue(syms[0], out q)) return null;
            double ch = Stocks.Change(q);
            return new Seg
            {
                W = chart ? S(190) : S(130),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.Launch("https://finance.yahoo.com/quote/" + Uri.EscapeDataString(q.Symbol)), q.Symbol + ": " + Price(q.Price) + " " + q.Currency + " (" + Pct(ch) + " so với phiên trước)\nNguồn: Yahoo Finance, trễ vài phút", false);
                    int tw = chart ? S(84) : r.Width - S(16);
                    Glass.Text(g, q.Symbol, Glass.Day, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(3), tw, S(14)), StringAlignment.Near);
                    Glass.Text(g, Price(q.Price), Glass.Row, Color.White, new Rectangle(r.X + S(10), r.Y + S(19), tw, S(17)), StringAlignment.Near);
                    Glass.Text(g, Pct(ch), Glass.Date, Trend(ch), new Rectangle(r.X + S(10), r.Y + S(3), tw - S(4), S(14)), StringAlignment.Far);
                    if (chart) Spark(g, new RectangleF(r.X + S(98), r.Y + S(6), r.Width - S(106), r.Height - S(12)), q.Line, Trend(ch), true);
                }
            };
        }

        Seg WatchlistSeg()
        {
            var list = new List<Stocks.Quote>();
            foreach (string s in Stocks.Symbols()) { Stocks.Quote q; if (Stocks.Quotes.TryGetValue(s, out q)) list.Add(q); if (list.Count == 3) break; }
            if (list.Count == 0) return null;
            int col = S(70);
            return new Seg
            {
                W = list.Count * col + S(8),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tip = new StringBuilder();
                    int x = r.X + S(4);
                    foreach (Stocks.Quote q in list)
                    {
                        double ch = Stocks.Change(q);
                        tip.Append(q.Symbol).Append(": ").Append(Price(q.Price)).Append(' ').Append(q.Currency).Append(" (").Append(Pct(ch)).Append(")\n");
                        Glass.Center(g, q.Symbol, Glass.Day, Glass.Dim, new Rectangle(x, r.Y + S(3), col, S(13)));
                        Glass.Center(g, Price(q.Price), Glass.Row, Color.White, new Rectangle(x, r.Y + S(14), col, S(13)));
                        Glass.Center(g, Pct(ch), Glass.Label, Trend(ch), new Rectangle(x, r.Y + S(27), col, S(10)));
                        x += col;
                    }
                    AddHit(r, null, b => Manager.OpenSettings(5), tip + "Đổi mã trong Cài đặt → Tài khoản", false);
                }
            };
        }

        Seg MetricSeg()
        {
            if (double.IsNaN(Crypto.Price) || Crypto.Symbol == null) return null;
            string pair = Crypto.Pair(Crypto.Symbol);
            double ch = Crypto.ChangePct;
            return new Seg
            {
                W = S(140),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Manager.OpenSettings(5), pair + ": " + Price(Crypto.Price) + " (" + Pct(ch) + " trong 24 giờ)\nNguồn: Binance", false);
                    Glass.Text(g, pair, Glass.Day, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(14)), StringAlignment.Near);
                    Glass.Text(g, Pct(ch), Glass.Date, Trend(ch), new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(14)), StringAlignment.Far);
                    Glass.Text(g, Price(Crypto.Price), Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(17), r.Width - S(20), S(20)), StringAlignment.Near);
                }
            };
        }

        Seg LeetCodeSeg()
        {
            if (!Acct.Has("leetcode")) return Hint("LeetCode", "Thêm tên người dùng");
            if (LeetCode.All < 0) return Hint("LeetCode", LeetCode.Error ?? "Đang tải…");
            return new Seg
            {
                W = S(170),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.Launch("https://leetcode.com/u/" + Uri.EscapeDataString(LeetCode.User ?? "")), "@" + LeetCode.User + ": " + LeetCode.All + " bài\nDễ " + LeetCode.Easy + ", Vừa " + LeetCode.Medium + ", Khó " + LeetCode.Hard + "\nChuỗi " + LeetCode.Streak + " ngày", false);
                    Glass.Text(g, LeetCode.All.ToString(), Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(2), S(56), S(20)), StringAlignment.Near);
                    Glass.Text(g, "BÀI", Glass.Label, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(23), S(56), S(10)), StringAlignment.Near);
                    int x = r.X + S(62);
                    foreach (var p in new[] { new { V = LeetCode.Easy, C = Color.FromArgb(255, 0, 184, 163) }, new { V = LeetCode.Medium, C = Color.FromArgb(255, 255, 184, 0) }, new { V = LeetCode.Hard, C = Color.FromArgb(255, 239, 71, 67) } })
                    {
                        Glass.Center(g, p.V.ToString(), Glass.Row, p.C, new Rectangle(x, r.Y + S(4), S(32), S(16)));
                        x += S(32);
                    }
                    Glass.Text(g, "chuỗi " + LeetCode.Streak + " ngày", Glass.Label, Glass.Dim, new Rectangle(r.X + S(62), r.Y + S(23), r.Width - S(70), S(10)), StringAlignment.Near);
                }
            };
        }

        // A tile that only says what is missing; clicking it opens the Accounts tab.
        Seg Hint(string title, string sub)
        {
            return new Seg
            {
                W = Math.Max(Glass.Measure(title, Glass.Row), Glass.Measure(sub, Glass.Date)) + S(22),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Manager.OpenSettings(5), title + ": " + sub + "\nBấm để mở Cài đặt → Tài khoản", false);
                    Glass.Text(g, title, Glass.Row, Color.White, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(14), S(17)), StringAlignment.Near);
                    Glass.Text(g, sub, Glass.Date, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(20), r.Width - S(14), S(16)), StringAlignment.Near);
                }
            };
        }

        Seg WallpaperSeg(int slot)
        {
            List<Wallpapers.Pic> pics = Wallpapers.Pics;
            Bitmap thumb = pics.Count > 0 ? pics[0].Thumb : null;
            return new Seg
            {
                W = S(118),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    Rectangle anchor = ToScreen(r);
                    bool hov = AddHit(r, null, b => { if (pics.Count > 0) OpenPopup("wallpaper", () => new WallpaperPopup(anchor)); }, pics.Count > 0 ? "Ảnh Bing hôm nay: " + pics[0].Title + "\nBấm để chọn hình nền" : Wallpapers.Error ?? "Đang tải ảnh…", false);
                    if (hov) Glass.Fill(g, r, S(10), Color.FromArgb(28, 255, 255, 255));
                    var t = new Rectangle(r.X + S(5), r.Y + S(5), S(48), r.Height - S(10));
                    if (thumb != null)
                        using (GraphicsPath p = Glass.Round(t, S(5))) { Region old = g.Clip; g.SetClip(p, CombineMode.Intersect); g.DrawImage(thumb, t); g.Clip = old; }
                    else Glass.Fill(g, t, S(5), Color.FromArgb(40, 255, 255, 255));
                    Glass.Text(g, "Hình nền", Glass.Row, Color.White, new Rectangle(t.Right + S(6), r.Y + S(3), r.Right - t.Right - S(10), S(17)), StringAlignment.Near);
                    Glass.Text(g, pics.Count + " ảnh Bing", Glass.Date, Glass.Dim, new Rectangle(t.Right + S(6), r.Y + S(20), r.Right - t.Right - S(10), S(16)), StringAlignment.Near);
                }
            };
        }

        Seg GitHubActivitySeg()
        {
            if (!GitHub.HasToken) return Hint("GitHub", "Thêm token");
            int[] days = GitHub.Days;
            int weeks = 20, cell = S(4), step = S(5);
            return new Seg
            {
                W = weeks * step + S(76),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.Launch("https://github.com/" + (GitHub.Login ?? "")), "@" + GitHub.Login + ": " + GitHub.Total + " đóng góp trong năm\nChuỗi " + GitHub.Streak + " ngày", false);
                    int max = 1;
                    foreach (int d in days) max = Math.Max(max, d);
                    int start = Math.Max(0, days.Length - weeks * 7), x = r.X + S(8), y = r.Y + (r.Height - 7 * step) / 2;
                    for (int i = 0; i < weeks * 7; i++)
                    {
                        int k = start + i, v = k < days.Length ? days[k] : 0;
                        using (var b = new SolidBrush(Greens[v == 0 ? 0 : Math.Min(4, 1 + 4 * (v - 1) / max)])) g.FillRectangle(b, x + (i / 7) * step, y + (i % 7) * step, cell, cell);
                    }
                    var t = new Rectangle(x + weeks * step + S(6), r.Y, r.Right - x - weeks * step - S(10), r.Height);
                    Glass.Center(g, GitHub.Streak.ToString(), Glass.Time, Color.White, new Rectangle(t.X, r.Y + S(3), t.Width, S(19)));
                    Glass.Center(g, "NGÀY LIỀN", Glass.Label, Glass.Dim, new Rectangle(t.X, r.Y + S(24), t.Width, S(10)));
                }
            };
        }

        Seg GitHubOverviewSeg()
        {
            if (!GitHub.HasToken) return Hint("GitHub", "Thêm token");
            var stats = new[]
            {
                new { G = MdlBell, V = GitHub.Unread, T = "thông báo", U = "https://github.com/notifications" },
                new { G = "", V = GitHub.OpenPRs, T = "PR mở", U = "https://github.com/pulls" },
                new { G = "", V = GitHub.Stars, T = "sao", U = "https://github.com/" + (GitHub.Login ?? "") + "?tab=repositories" },
                new { G = "", V = GitHub.Reviews, T = "cần review", U = "https://github.com/pulls/review-requested" },
            };
            int col = S(50);
            return new Seg
            {
                W = stats.Length * col + S(8),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    int x = r.X + S(4);
                    foreach (var s in stats)
                    {
                        string url = s.U;
                        var c = new Rectangle(x, r.Y, col, r.Height);
                        bool hov = AddHit(c, null, b => Sys.Launch(url), (s.V < 0 ? "…" : s.V.ToString()) + " " + s.T + (GitHub.Error != null ? "\n" + GitHub.Error : ""), false);
                        if (hov) Glass.Fill(g, new Rectangle(c.X, c.Y + S(3), c.Width, c.Height - S(6)), S(7), Color.FromArgb(30, 255, 255, 255));
                        Glass.Center(g, s.G, Glass.MdlSmall, Glass.Dim, new Rectangle(c.X, r.Y + S(4), c.Width, S(14)));
                        Glass.Center(g, s.V < 0 ? "…" : s.V.ToString(), Glass.Row, Color.White, new Rectangle(c.X, r.Y + S(19), c.Width, S(16)));
                        x += col;
                    }
                }
            };
        }
    }

    // Eight Bing images; click one to make it the wallpaper.
    class WallpaperPopup : OpaquePopup
    {
        readonly List<Wallpapers.Pic> pics = Wallpapers.Pics;
        int hover = -1;

        public WallpaperPopup(Rectangle anchor) : base(anchor, S(4 * 170 + 24), S(2 * 100 + 60)) { }

        Rectangle Cell(int i) { return new Rectangle(S(12) + (i % 4) * S(170), S(44) + (i / 4) * S(100), S(160), S(90)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            Glass.Text(g, "Hình nền Bing, bấm để đặt", Glass.Header, Color.White, new Rectangle(S(14), S(10), Width - S(28), S(26)), StringAlignment.Near);
            for (int i = 0; i < Math.Min(8, pics.Count); i++)
            {
                Rectangle c = Cell(i);
                if (pics[i].Thumb != null)
                    using (GraphicsPath p = Glass.Round(c, S(8))) { Region old = g.Clip; g.SetClip(p, CombineMode.Intersect); g.DrawImage(pics[i].Thumb, c); g.Clip = old; }
                if (i == hover) using (GraphicsPath p = Glass.Round(c, S(8))) using (var pen = new Pen(Glass.Blue, 2.5f * Glass.Scale)) g.DrawPath(pen, p);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < Math.Min(8, pics.Count); i++) if (Cell(i).Contains(e.Location)) h = i;
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (hover < 0) return;
            Wallpapers.Set(pics[hover]);
            Close();
        }
    }
}
