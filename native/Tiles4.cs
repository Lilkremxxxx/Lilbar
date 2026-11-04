using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Lilbar
{
    // Batch 3 tiles: store revenue (number, chart, total with goal) and one-number services.
    partial class Dock
    {
        static readonly Color Mint = Color.FromArgb(255, 72, 220, 160);

        Seg RevenueSeg(string id, bool chart)
        {
            Revenue.Src s = Revenue.Get(id);
            if (!Revenue.Configured(id)) return Hint(s.Name, "Thêm key");
            if (double.IsNaN(s.Total30)) return Hint(s.Name, s.Error ?? "Đang tải…");
            string value = Revenue.Money(s.Total30, s.Currency);
            var days = new List<double>(s.Days14);
            return new Seg
            {
                W = chart ? S(200) : Math.Max(S(120), Glass.Measure(value, Glass.Time) + S(24)),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, s.Name + ": " + value + " trong 30 ngày\nTuần này " + Revenue.Money(s.Week, s.Currency) + (s.Error != null ? "\n" + s.Error : ""), false);
                    int tw = chart ? S(100) : r.Width - S(20);
                    Glass.Text(g, s.Name.ToUpper(Vi), Glass.Day, Mint, new Rectangle(r.X + S(10), r.Y + S(3), tw, S(14)), StringAlignment.Near);
                    Glass.Text(g, chart ? Revenue.Money(s.Week, s.Currency) : value, Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(17), tw, S(20)), StringAlignment.Near);
                    if (chart)
                    {
                        Glass.Text(g, "tuần này", Glass.Label, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(3), tw, S(14)), StringAlignment.Far);
                        Spark(g, new RectangleF(r.X + S(114), r.Y + S(6), r.Width - S(122), r.Height - S(12)), days, Mint, true);
                    }
                }
            };
        }

        Seg RevenueTotalSeg()
        {
            string cur, other;
            double total = Revenue.Total(out cur, out other);
            if (double.IsNaN(total)) return Hint("Tổng doanh thu", "Thêm key của một cửa hàng");
            double goal;
            bool hasGoal = double.TryParse(Acct.Get("rev_goal"), NumberStyles.Float, CultureInfo.InvariantCulture, out goal) && goal > 0;
            DateTime n = DateTime.Now;
            double expected = hasGoal ? goal * n.Day / DateTime.DaysInMonth(n.Year, n.Month) : 0; // where 30-day revenue should be by today
            string value = Revenue.Money(total, cur);
            return new Seg
            {
                W = S(190),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tip = new StringBuilder("Tổng 30 ngày: " + value);
                    foreach (Revenue.Src s in Revenue.All) if (Revenue.Configured(s.Id)) tip.Append('\n').Append(s.Name).Append(": ").Append(s.Error ?? Revenue.Money(s.Total30, s.Currency));
                    if (other.Length > 0) tip.Append("\nKhác tiền tệ: ").Append(other);
                    if (hasGoal) tip.Append("\nMục tiêu tháng ").Append(Revenue.Money(goal, cur)).Append(total >= expected ? ", đang vượt nhịp" : ", đang chậm hơn nhịp");
                    AddHit(r, null, b => Manager.OpenSettings(5), tip.ToString(), false);
                    Glass.Text(g, "DOANH THU 30 NGÀY", Glass.Day, Mint, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(14)), StringAlignment.Near);
                    Glass.Text(g, value, Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(16), r.Width - S(20), S(18)), StringAlignment.Near);
                    if (hasGoal)
                    {
                        var bar = new Rectangle(r.X + S(10), r.Bottom - S(6), r.Width - S(20), S(3));
                        Glass.Fill(g, bar, S(1), Color.FromArgb(50, 255, 255, 255));
                        Glass.Fill(g, new Rectangle(bar.X, bar.Y, Math.Max(S(3), (int)(bar.Width * Math.Min(1, total / goal))), bar.Height), S(1), total >= expected ? Mint : Orange);
                    }
                }
            };
        }

        static readonly Dictionary<string, string[]> SvcLabels = new Dictionary<string, string[]>
        {
            // title, Segoe MDL2 glyph, color (hex), link
            { "plausible", new[] { "Plausible", "", "5B8CFF", "https://plausible.io/sites" } },
            { "dub", new[] { "Dub", "", "F2F2F2", "https://app.dub.co" } },
            { "resend", new[] { "Resend", "", "F2F2F2", "https://resend.com/emails" } },
            { "linear", new[] { "Linear", "", "8C8CFF", "https://linear.app" } },
            { "supabase", new[] { "Supabase", "", "3ECF8E", "https://supabase.com/dashboard" } },
            { "youtube", new[] { "YouTube", "", "FF4040", "https://studio.youtube.com" } },
            { "x", new[] { "X", "", "F2F2F2", "https://x.com" } },
            { "instagram", new[] { "Instagram", "", "E1306C", "https://instagram.com" } },
            { "tiktok", new[] { "TikTok", "", "25F4EE", "https://tiktok.com" } },
            { "gmail", new[] { "Gmail", "", "EA4335", "https://mail.google.com" } },
            { "deepseek", new[] { "DeepSeek", "", "4D6BFE", "https://platform.deepseek.com/usage" } },
        };

        Seg StatSeg(string id)
        {
            string[] l = SvcLabels[id];
            string missing = Svc.Missing(id);
            if (missing != null) return Hint(l[0], missing);
            Stat s = Svc.Get(id);
            if (s.Value == null) return Hint(l[0], s.Error ?? "Đang tải…");
            Color c = Color.FromArgb(255, Convert.ToInt32(l[2].Substring(0, 2), 16), Convert.ToInt32(l[2].Substring(2, 2), 16), Convert.ToInt32(l[2].Substring(4, 2), 16));
            string value = s.Value, sub = s.Sub ?? "", url = l[3];
            return new Seg
            {
                W = Math.Max(Glass.Measure(value, Glass.Time) + S(10), Glass.Measure(sub, Glass.Date)) + S(52),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.Launch(url), l[0] + ": " + value + " " + sub + (s.Error != null ? "\n" + s.Error : ""), false);
                    var badge = new Rectangle(r.X + S(6), r.Y + (r.Height - S(28)) / 2, S(28), S(28));
                    Glass.Fill(g, badge, S(8), Color.FromArgb(60, c));
                    Glass.Center(g, l[1], Glass.MdlSmall, c, badge);
                    Glass.Text(g, value, Glass.Time, Color.White, new Rectangle(badge.Right + S(8), r.Y + S(2), r.Right - badge.Right - S(12), S(19)), StringAlignment.Near);
                    Glass.Text(g, sub, Glass.Date, Glass.Dim, new Rectangle(badge.Right + S(8), r.Y + S(21), r.Right - badge.Right - S(12), S(15)), StringAlignment.Near);
                }
            };
        }
    }
}
