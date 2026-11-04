using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Lilbar
{
    // Tile builder for every catalog id, plus the clock, weather, system, file, productivity and media variants.
    partial class Dock
    {
        static readonly CultureInfo Vi = new CultureInfo("vi-VN");
        static readonly Color Orange = Color.FromArgb(255, 255, 159, 10), Red = Color.FromArgb(255, 255, 82, 70), Green = Color.FromArgb(255, 52, 199, 89);

        Seg Build(string id, int th, int slot)
        {
            switch (id)
            {
                case "apps": return AppsSeg(slot);
                case "folder": return FolderSeg(slot);
                case "system": return SystemSeg(slot);
                case "claude": return ClaudeSeg(th);
                case "tray": return TraySeg();
                case "battery": return BatterySeg();
                case "today": return TodaySeg();
                case "clock": return ClockSeg();
                case "media": return MediaSeg();
                case "network": return NetworkSeg();
                case "volume": return VolumeSeg();
                case "toggles": return TogglesSeg();
                case "temp": return TempSeg();
                case "worldclock": return WorldClockSeg();
                case "flipclock": return FlipClockSeg();
                case "timer": return TimerSeg();
                case "pomodoro": return PomodoroSeg();
                case "todos": return TodosSeg();
                case "github": return GitHubSeg();
                case "segment": return SegmentSeg();
                case "analog": return AnalogSeg(slot, 0);
                case "analogdark": return AnalogSeg(slot, 1);
                case "analogmin": return AnalogSeg(slot, 2);
                case "worldanalog": return WorldAnalogSeg();
                case "worldone": return WorldOneSeg();
                case "date": return DateSeg();
                case "daynum": return DayNumSeg(slot);
                case "weekstrip": return WeekStripSeg();
                case "clockweather": return ClockWeatherSeg(slot);
                case "cpu": return RingSeg(slot, Model.Cpu, "CPU");
                case "ram": return RingSeg(slot, Model.Ram, "RAM");
                case "language": return LanguageSeg();
                case "theme": return ThemeSeg(slot);
                case "storage": return StorageSeg();
                case "dayprog": return ProgressSeg("Hôm nay", DayFraction(), DateTime.Now.ToString("HH:mm"));
                case "monthprog": return ProgressSeg("Tháng " + DateTime.Now.Month, MonthFraction(), DateTime.Now.Day + "/" + DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));
                case "yearprog": return YearSeg();
                case "recent": return FileIconsSeg(slot, Files.Recent, false, "Mở gần đây");
                case "screenshots": return FileIconsSeg(slot, Files.Shots, true, "Ảnh chụp màn hình");
                case "downloads": return DownloadSeg();
                case "snip": return GlyphSeg(slot, "", "Chụp màn hình (Win+Shift+S)", () => Sys.Launch("ms-screenclip:"));
                case "recycle": return RecycleSeg();
                case "search": return LauncherSeg("search", "", "Tìm kiếm");
                case "commands": return LauncherSeg("commands", "", "Lệnh");
                case "mediacompact": return MediaCompactSeg();
                case "mediamini": return MediaMiniSeg(slot);
                case "mediabar": return MediaBarSeg();
                case "stopwatch": return StopwatchSeg();
                case "notes": return NotesSeg();
                case "calendar": return CalendarSeg();
                case "currency": return CurrencySeg();
                case "screentime": return ScreenTimeSeg();
                case "timeline": return TimelineSeg();
                case "weather": return WeatherSeg();
                case "weatherdetail": return WeatherDetailSeg();
                case "weatherstatus": return WeatherStatusSeg();
                case "hourly": return HourlySeg();
                case "forecast": return ForecastSeg(4, false);
                case "todayforecast": return ForecastSeg(3, true);
                case "sun": return SunSeg();
            }
            return null;
        }

        // ---------- clocks ----------

        static void Hand(Graphics g, float cx, float cy, double turn, float len, float width, Color c)
        {
            double a = turn * 2 * Math.PI;
            using (var p = new Pen(c, width) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(p, cx, cy, cx + (float)(Math.Sin(a) * len), cy - (float)(Math.Cos(a) * len));
        }

        // style 0 light face, 1 dark face, 2 bare ticks and hands
        static void Face(Graphics g, Rectangle r, DateTime t, int style, bool seconds)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, rad = Math.Min(r.Width, r.Height) / 2f - 1;
            Color ink = style == 0 ? Color.FromArgb(255, 28, 28, 32) : Color.White;
            if (style != 2)
                using (var b = new SolidBrush(style == 0 ? Color.FromArgb(255, 244, 244, 247) : Color.FromArgb(255, 30, 30, 36))) g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
            for (int i = 0; i < 12; i++)
            {
                double a = i * Math.PI / 6;
                bool major = i % 3 == 0;
                float r1 = rad * (major ? 0.70f : 0.80f), r2 = rad * 0.9f;
                using (var p = new Pen(Color.FromArgb(major ? 255 : 120, ink), (major ? 1.5f : 1f) * Glass.Scale))
                    g.DrawLine(p, cx + (float)Math.Sin(a) * r1, cy - (float)Math.Cos(a) * r1, cx + (float)Math.Sin(a) * r2, cy - (float)Math.Cos(a) * r2);
            }
            Hand(g, cx, cy, (t.Hour % 12 + t.Minute / 60.0) / 12, rad * 0.48f, 2.2f * Glass.Scale, ink);
            Hand(g, cx, cy, (t.Minute + t.Second / 60.0) / 60, rad * 0.74f, 1.6f * Glass.Scale, ink);
            if (seconds) Hand(g, cx, cy, t.Second / 60.0, rad * 0.8f, 1f * Glass.Scale, style == 0 ? Red : Orange);
            float d = 3 * Glass.Scale;
            using (var b = new SolidBrush(seconds ? (style == 0 ? Red : Orange) : ink)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
        }

        Seg AnalogSeg(int slot, int style)
        {
            DateTime now = DateTime.Now;
            return new Seg
            {
                W = slot + S(10),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), now.ToString("HH:mm:ss, dd/MM/yyyy"), false);
                    Face(g, new Rectangle(r.X + (r.Width - slot) / 2, r.Y + (r.Height - slot) / 2, slot, slot), now, style, style != 2);
                }
            };
        }

        Seg WorldAnalogSeg()
        {
            List<string> ids = Config.Current.Cities.FindAll(id => !string.IsNullOrEmpty(id));
            if (ids.Count == 0) return null;
            int col = S(40);
            return new Seg
            {
                W = ids.Count * col + S(6),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tip = new StringBuilder();
                    int x = r.X + S(3), face = S(24);
                    foreach (string id in ids)
                    {
                        DateTime t = Cities.Now(id);
                        tip.Append(Cities.Label(id)).Append(": ").Append(t.ToString("HH:mm")).Append('\n');
                        Face(g, new Rectangle(x + (col - face) / 2, r.Y + S(3), face, face), t, t.Hour >= 6 && t.Hour < 18 ? 0 : 1, false);
                        Glass.Center(g, Cities.Label(id), Glass.Label, Glass.Dim, new Rectangle(x, r.Y + S(28), col, S(10)));
                        x += col;
                    }
                    AddHit(r, null, b => Manager.OpenSettings(4), tip + "Mặt sáng là ban ngày ở đó", false);
                }
            };
        }

        Seg WorldOneSeg()
        {
            string id = Config.Current.Cities.Find(c => !string.IsNullOrEmpty(c));
            if (id == null) return null;
            DateTime t = Cities.Now(id);
            double diff = Math.Round((t - DateTime.Now).TotalHours * 2) / 2;
            string label = Cities.Label(id), off = diff == 0 ? "cùng giờ" : (diff > 0 ? "+" : "") + diff.ToString("0.#", CultureInfo.InvariantCulture) + " giờ";
            int w = Math.Max(Glass.Measure(t.ToString("HH:mm"), Glass.Huge), Glass.Measure(label + "  " + off, Glass.Label)) + S(20);
            return new Seg
            {
                W = w,
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Manager.OpenSettings(4), label + ": " + t.ToString("HH:mm, dd/MM") + " (" + off + ")", false);
                    Glass.Text(g, label.ToUpper(Vi) + "  " + off, Glass.Label, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(11)), StringAlignment.Near);
                    Glass.Text(g, t.ToString("HH:mm"), Glass.Huge, Color.White, new Rectangle(r.X + S(8), r.Y + S(11), r.Width - S(16), S(28)), StringAlignment.Near);
                }
            };
        }

        static readonly int[] SegBits = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F }; // a..g as bits 0..6

        static void Seven(Graphics g, RectangleF r, int digit, Color on)
        {
            float t = Math.Max(2, r.Width * 0.2f), w = r.Width, h = r.Height, x = r.X, y = r.Y, half = h / 2;
            var segs = new[]
            {
                new RectangleF(x + t / 2, y, w - t, t), new RectangleF(x + w - t, y + t / 2, t, half - t / 2), new RectangleF(x + w - t, y + half, t, half - t / 2),
                new RectangleF(x + t / 2, y + h - t, w - t, t), new RectangleF(x, y + half, t, half - t / 2), new RectangleF(x, y + t / 2, t, half - t / 2),
                new RectangleF(x + t / 2, y + half - t / 2, w - t, t),
            };
            for (int i = 0; i < 7; i++)
                using (var b = new SolidBrush((SegBits[digit] >> i & 1) != 0 ? on : Color.FromArgb(22, on)))
                    g.FillRectangle(b, segs[i].X + 0.5f, segs[i].Y + 0.5f, segs[i].Width - 1, segs[i].Height - 1);
        }

        Seg SegmentSeg()
        {
            DateTime now = DateTime.Now;
            string hhmm = now.ToString("HHmm"), top = Glass.DayName(now.DayOfWeek).ToUpper(Vi) + " " + now.ToString("dd/MM");
            Color led = Color.FromArgb(255, 120, 240, 170);
            int dw = S(11), gap = S(4), colon = S(7);
            return new Seg
            {
                W = Math.Max(4 * dw + 3 * gap + colon, Glass.Measure(top, Glass.Label)) + S(18),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), now.ToString("HH:mm, dd/MM/yyyy"), false);
                    Glass.Center(g, top, Glass.Label, Glass.Dim, new Rectangle(r.X, r.Y + S(2), r.Width, S(10)));
                    float x = r.X + (r.Width - (4 * dw + 3 * gap + colon)) / 2f, y = r.Y + S(14), h = S(20);
                    for (int i = 0; i < 4; i++)
                    {
                        Seven(g, new RectangleF(x, y, dw, h), hhmm[i] - '0', led);
                        x += dw + gap;
                        if (i == 1)
                        {
                            using (var b = new SolidBrush(now.Second % 2 == 0 ? led : Color.FromArgb(40, led)))
                            {
                                g.FillRectangle(b, x + colon / 2f - S(1.5f) - gap / 2f, y + h * 0.3f, S(3), S(3));
                                g.FillRectangle(b, x + colon / 2f - S(1.5f) - gap / 2f, y + h * 0.65f, S(3), S(3));
                            }
                            x += colon;
                        }
                    }
                }
            };
        }

        Seg DateSeg()
        {
            DateTime now = DateTime.Now;
            string day = now.Day.ToString(), top = Glass.DayName(now.DayOfWeek), bottom = "tháng " + now.Month + ", " + now.Year;
            int dw = Glass.Measure(day, Glass.Huge), tw = Math.Max(Glass.Measure(top, Glass.Row), Glass.Measure(bottom, Glass.Date));
            return new Seg
            {
                W = dw + tw + S(26),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), now.ToString("dddd, d MMMM yyyy", Vi), false);
                    Glass.Text(g, day, Glass.Huge, Color.White, new Rectangle(r.X + S(8), r.Y, dw + S(4), r.Height), StringAlignment.Near);
                    Glass.Text(g, top, Glass.Row, Red, new Rectangle(r.X + S(14) + dw, r.Y + S(4), tw + S(6), S(16)), StringAlignment.Near);
                    Glass.Text(g, bottom, Glass.Date, Glass.Dim, new Rectangle(r.X + S(14) + dw, r.Y + S(20), tw + S(6), S(16)), StringAlignment.Near);
                }
            };
        }

        Seg DayNumSeg(int slot)
        {
            DateTime now = DateTime.Now;
            return new Seg
            {
                W = slot + S(10),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), now.ToString("dddd, d MMMM yyyy", Vi), false);
                    var page = new Rectangle(r.X + (r.Width - S(28)) / 2, r.Y + S(3), S(28), r.Height - S(6));
                    Glass.Fill(g, page, S(5), Color.FromArgb(255, 246, 246, 248));
                    var band = new Rectangle(page.X, page.Y, page.Width, S(10));
                    using (GraphicsPath p = Glass.Round(page, S(5))) { Region old = g.Clip; g.SetClip(p, CombineMode.Intersect); using (var b = new SolidBrush(Red)) g.FillRectangle(b, band); g.Clip = old; }
                    Glass.Center(g, "TH" + now.Month, Glass.Label, Color.White, band);
                    Glass.Center(g, now.Day.ToString(), Glass.DateBig, Color.FromArgb(255, 28, 28, 32), new Rectangle(page.X, band.Bottom, page.Width, page.Bottom - band.Bottom));
                }
            };
        }

        Seg WeekStripSeg()
        {
            DateTime today = DateTime.Today;
            DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            bool hasW = !double.IsNaN(Weather.Temp) && Weather.Code >= 0;
            int cell = S(22), wx = hasW ? S(46) : 0;
            return new Seg
            {
                W = 7 * cell + wx + S(10),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), "Tuần " + CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(today, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday), false);
                    int x = r.X + S(5);
                    for (int i = 0; i < 7; i++)
                    {
                        DateTime d = monday.AddDays(i);
                        var c = new Rectangle(x, r.Y + S(3), cell, r.Height - S(6));
                        if (d == today) Glass.Fill(g, c, S(6), Glass.Blue);
                        Glass.Center(g, d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "T" + ((int)d.DayOfWeek + 1), Glass.Label, d == today ? Color.White : Glass.Dim, new Rectangle(c.X, c.Y + S(2), c.Width, S(11)));
                        Glass.Center(g, d.Day.ToString(), Glass.Row, d < today ? Color.FromArgb(120, 255, 255, 255) : Color.White, new Rectangle(c.X, c.Y + S(14), c.Width, S(16)));
                        x += cell;
                    }
                    if (hasW)
                    {
                        var wr = new Rectangle(x + S(4), r.Y, wx - S(4), r.Height);
                        Glass.Center(g, Weather.Glyph(Weather.Code), Glass.Glyph, Weather.GlyphColor(Weather.Code), new Rectangle(wr.X, wr.Y + S(3), wr.Width, S(16)));
                        Glass.Center(g, Math.Round(Weather.Temp) + "°", Glass.Row, Color.White, new Rectangle(wr.X, wr.Y + S(20), wr.Width, S(16)));
                    }
                }
            };
        }

        Seg ClockWeatherSeg(int slot)
        {
            DateTime now = DateTime.Now;
            bool hasW = !double.IsNaN(Weather.Temp);
            int block = slot;
            return new Seg
            {
                W = 3 * block + 2 * S(4) + S(8),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.WinKey(0x4E), WeatherTip(), false);
                    int x = r.X + S(4), y = r.Y + (r.Height - block) / 2;
                    var a = new Rectangle(x, y, block, block);
                    Glass.Fill(g, a, S(7), Color.FromArgb(230, 64, 120, 255));
                    Glass.Center(g, now.ToString("HH"), Glass.Row, Color.White, new Rectangle(a.X, a.Y + S(2), a.Width, block / 2));
                    Glass.Center(g, now.ToString("mm"), Glass.Row, Color.FromArgb(220, 255, 255, 255), new Rectangle(a.X, a.Y + block / 2 - S(2), a.Width, block / 2));
                    var b2 = new Rectangle(a.Right + S(4), y, block, block);
                    Glass.Fill(g, b2, S(7), Color.FromArgb(230, 150, 90, 230));
                    Glass.Center(g, now.Day.ToString(), Glass.Row, Color.White, new Rectangle(b2.X, b2.Y + S(2), b2.Width, block / 2));
                    Glass.Center(g, "TH" + now.Month, Glass.Label, Color.FromArgb(220, 255, 255, 255), new Rectangle(b2.X, b2.Y + block / 2, b2.Width, block / 2 - S(2)));
                    var c = new Rectangle(b2.Right + S(4), y, block, block);
                    Glass.Fill(g, c, S(7), Color.FromArgb(230, 255, 140, 40));
                    Glass.Center(g, hasW ? Math.Round(Weather.Temp) + "°" : "–", Glass.Row, Color.White, c);
                }
            };
        }

        // ---------- system ----------

        Seg RingSeg(int slot, int pct, string label)
        {
            return new Seg
            {
                W = slot + S(10),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.Launch("taskmgr.exe"), label + " " + pct + "%\nBấm để mở Task Manager", false);
                    Glass.Ring(g, new Rectangle(r.X + (r.Width - slot) / 2, r.Y + (r.Height - slot) / 2, slot, slot), pct, label);
                }
            };
        }

        Seg LanguageSeg()
        {
            string full, code = Desk.Lang(out full);
            return new Seg
            {
                W = S(50),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Desk.NextLang(), full + "\nBấm để đổi bố cục bàn phím (như Win+Space)", false);
                    Glass.Center(g, code, Glass.Row, Color.White, new Rectangle(r.X, r.Y + S(4), r.Width, S(16)));
                    Glass.Center(g, "", Glass.MdlSmall, Glass.Dim, new Rectangle(r.X, r.Y + S(20), r.Width, S(14))); // keyboard
                }
            };
        }

        Seg ThemeSeg(int slot)
        {
            bool dark = Desk.Dark();
            return GlyphSeg(slot, dark ? "" : "", dark ? "Windows đang ở chế độ tối\nBấm để chuyển sang sáng" : "Windows đang ở chế độ sáng\nBấm để chuyển sang tối", Desk.ToggleTheme);
        }

        Seg GlyphSeg(int slot, string glyph, string tipText, Action click)
        {
            return new Seg
            {
                W = slot + S(10),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    bool hov = AddHit(r, null, b => click(), tipText, false);
                    if (hov) Glass.Fill(g, r, S(10), Color.FromArgb(30, 255, 255, 255));
                    Glass.Center(g, glyph, Glass.Mdl, Color.White, r);
                }
            };
        }

        Seg StorageSeg()
        {
            if (Files.Total <= 0) return null;
            long free = Files.Free, total = Files.Total;
            double used = 1 - free / (double)total;
            return new Seg
            {
                W = S(128),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Sys.Launch("shell:MyComputerFolder"), "Ổ hệ thống: trống " + Files.Size(free) + " / " + Files.Size(total), false);
                    Glass.Text(g, "Ổ C", Glass.Row, Color.White, new Rectangle(r.X + S(10), r.Y + S(3), S(40), S(16)), StringAlignment.Near);
                    Glass.Text(g, Files.Size(free) + " trống", Glass.Date, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(16)), StringAlignment.Far);
                    var bar = new Rectangle(r.X + S(10), r.Y + S(25), r.Width - S(20), S(5));
                    Glass.Fill(g, bar, S(2), Color.FromArgb(50, 255, 255, 255));
                    Glass.Fill(g, new Rectangle(bar.X, bar.Y, Math.Max(S(5), (int)(bar.Width * used)), bar.Height), S(2), used > 0.9 ? Red : used > 0.75 ? Orange : Glass.Blue);
                }
            };
        }

        static double DayFraction() { return DateTime.Now.TimeOfDay.TotalSeconds / 86400.0; }
        static double MonthFraction() { DateTime n = DateTime.Now; return (n.Day - 1 + n.TimeOfDay.TotalHours / 24) / DateTime.DaysInMonth(n.Year, n.Month); }

        Seg ProgressSeg(string label, double f, string right)
        {
            return new Seg
            {
                W = S(130),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, label + ": đã qua " + Math.Floor(f * 100) + "%", false);
                    Glass.Text(g, label, Glass.Row, Color.White, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(16)), StringAlignment.Near);
                    Glass.Text(g, Math.Floor(f * 100) + "%", Glass.Row, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(16)), StringAlignment.Far);
                    var bar = new Rectangle(r.X + S(10), r.Y + S(25), r.Width - S(20), S(5));
                    Glass.Fill(g, bar, S(2), Color.FromArgb(50, 255, 255, 255));
                    Glass.Fill(g, new Rectangle(bar.X, bar.Y, Math.Max(S(5), (int)(bar.Width * f)), bar.Height), S(2), Glass.Blue);
                }
            };
        }

        Seg YearSeg()
        {
            DateTime n = DateTime.Now;
            int week = Math.Min(52, n.DayOfYear / 7), cols = 13, dot = S(4), step = S(6);
            double f = n.DayOfYear / (DateTime.IsLeapYear(n.Year) ? 366.0 : 365.0);
            return new Seg
            {
                W = cols * step + S(56),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, "Năm " + n.Year + ": đã qua " + Math.Floor(f * 100) + "% (tuần " + (week + 1) + "/52)", false);
                    int x0 = r.X + S(9), y0 = r.Y + (r.Height - 4 * step) / 2 + S(1);
                    for (int i = 0; i < 52; i++)
                        using (var b = new SolidBrush(i < week ? Glass.Blue : i == week ? Color.White : Color.FromArgb(50, 255, 255, 255)))
                            g.FillEllipse(b, x0 + (i % cols) * step, y0 + (i / cols) * step, dot, dot);
                    var t = new Rectangle(x0 + cols * step + S(4), r.Y, r.Right - x0 - cols * step - S(10), r.Height);
                    Glass.Center(g, n.Year.ToString(), Glass.Label, Glass.Dim, new Rectangle(t.X, r.Y + S(5), t.Width, S(11)));
                    Glass.Center(g, Math.Floor(f * 100) + "%", Glass.Row, Color.White, new Rectangle(t.X, r.Y + S(17), t.Width, S(16)));
                }
            };
        }

        // ---------- apps & files ----------

        Seg FileIconsSeg(int slot, List<Files.Entry> list, bool thumbs, string title)
        {
            if (list.Count == 0) return null;
            return new Seg
            {
                W = S(8) + list.Count * (slot + S(4)) - S(4),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    int x = r.X + S(4), sy = r.Y + (r.Height - slot) / 2;
                    foreach (Files.Entry e in list)
                    {
                        string path = e.Path;
                        var c = new Rectangle(x, sy, slot, slot);
                        bool hov = AddHit(c, null, b => Sys.Launch(path), title + ": " + e.Name + "\n" + e.At.ToString("HH:mm dd/MM"), false);
                        if (hov) Glass.Fill(g, c, S(8), Color.FromArgb(36, 255, 255, 255));
                        Bitmap img = thumbs ? Files.Thumb(path) : Sys.IconOf(path);
                        if (img != null)
                        {
                            Rectangle ir = thumbs ? new Rectangle(c.X + S(2), c.Y + S(2), slot - S(4), slot - S(4)) : new Rectangle(c.X + S(5), c.Y + S(5), slot - S(10), slot - S(10));
                            using (GraphicsPath p = Glass.Round(ir, S(5))) { Region old = g.Clip; g.SetClip(p, CombineMode.Intersect); g.DrawImage(img, ir); g.Clip = old; }
                        }
                        x += slot + S(4);
                    }
                }
            };
        }

        Seg DownloadSeg()
        {
            Files.Entry d = Files.Download;
            if (d == null) return null;
            return new Seg
            {
                W = S(180),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    string path = d.Path;
                    AddHit(r, null, b => Sys.Launch(path), "Tải về: " + Path.GetFileName(path) + "\nBấm để mở", false);
                    Bitmap ic = Sys.IconOf(path);
                    if (ic != null) g.DrawImage(ic, new Rectangle(r.X + S(8), r.Y + (r.Height - S(24)) / 2, S(24), S(24)));
                    Glass.Text(g, d.Name, Glass.Row, Color.White, new Rectangle(r.X + S(40), r.Y + S(3), r.Width - S(48), S(17)), StringAlignment.Near);
                    Glass.Text(g, "Tải về " + Ago(d.At), Glass.Date, Glass.Dim, new Rectangle(r.X + S(40), r.Y + S(20), r.Width - S(48), S(16)), StringAlignment.Near);
                }
            };
        }

        static string Ago(DateTime t)
        {
            TimeSpan d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "vừa xong";
            if (d.TotalHours < 1) return (int)d.TotalMinutes + " phút trước";
            if (d.TotalDays < 1) return (int)d.TotalHours + " giờ trước";
            return (int)d.TotalDays + " ngày trước";
        }

        Seg RecycleSeg()
        {
            long items = Files.BinItems;
            if (items < 0) return null;
            return new Seg
            {
                W = S(130),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    bool hov = AddHit(r, null, b => { if (items > 0) Files.EmptyBin(Handle); }, items == 0 ? "Thùng rác trống" : "Bấm để dọn thùng rác (Windows sẽ hỏi lại)", false);
                    if (hov && items > 0) Glass.Fill(g, r, S(10), Color.FromArgb(28, 255, 255, 255));
                    Glass.Center(g, "", Glass.Mdl, items > 0 ? Color.White : Glass.Dim, new Rectangle(r.X + S(4), r.Y, S(30), r.Height));
                    Glass.Text(g, items == 0 ? "Trống" : items + " mục", Glass.Row, Color.White, new Rectangle(r.X + S(38), r.Y + S(3), r.Width - S(44), S(17)), StringAlignment.Near);
                    Glass.Text(g, items == 0 ? "Thùng rác" : Files.Size(Files.BinBytes), Glass.Date, Glass.Dim, new Rectangle(r.X + S(38), r.Y + S(20), r.Width - S(44), S(16)), StringAlignment.Near);
                }
            };
        }

        Seg LauncherSeg(string key, string glyph, string label)
        {
            return new Seg
            {
                W = Glass.Measure(label, Glass.Row) + S(46),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    Rectangle anchor = ToScreen(r);
                    bool hov = AddHit(r, null, b => OpenPopup(key, () => new LauncherPopup(anchor, key == "commands")), key == "commands" ? "Gõ tên app để mở" : "Tìm trên web hoặc mở thư mục", false);
                    if (hov || popupKey == key) Glass.Fill(g, r, S(10), Color.FromArgb(30, 255, 255, 255));
                    Glass.Center(g, glyph, Glass.MdlSmall, Glass.Dim, new Rectangle(r.X + S(6), r.Y, S(24), r.Height));
                    Glass.Text(g, label, Glass.Row, Color.White, new Rectangle(r.X + S(32), r.Y, r.Width - S(38), r.Height), StringAlignment.Near);
                }
            };
        }

        // ---------- media variants ----------

        Seg MediaCompactSeg()
        {
            if (!Media.Has) return null;
            string title = Media.Title, artist = Media.Artist;
            bool playing = Media.Playing;
            return new Seg
            {
                W = S(190),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(new Rectangle(r.X, r.Y, r.Width - S(38), r.Height), null, null, title + "\n" + artist, false);
                    Glass.Text(g, title, Glass.Row, Color.White, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(50), S(17)), StringAlignment.Near);
                    Glass.Text(g, artist, Glass.Date, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(20), r.Width - S(50), S(16)), StringAlignment.Near);
                    RoundButton(g, new Rectangle(r.Right - S(34), r.Y + (r.Height - S(28)) / 2, S(28), S(28)), playing ? MdlPause : MdlPlay, Color.FromArgb(70, 255, 255, 255), () => Media.Command(0), playing ? "Tạm dừng" : "Phát");
                }
            };
        }

        Seg MediaMiniSeg(int slot)
        {
            if (!Media.Has) return null;
            bool playing = Media.Playing;
            double prog = Media.Progress();
            string title = Media.Title, artist = Media.Artist;
            return new Seg
            {
                W = slot + S(10),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var c = new Rectangle(r.X + (r.Width - slot) / 2, r.Y + (r.Height - slot) / 2, slot, slot);
                    AddHit(c, null, b => Media.Command(0), title + "\n" + artist + "\nBấm để " + (playing ? "tạm dừng" : "phát"), false);
                    float w = S(3);
                    var rc = new RectangleF(c.X + w / 2, c.Y + w / 2, c.Width - w, c.Height - w);
                    using (var track = new Pen(Color.FromArgb(56, 255, 255, 255), w)) g.DrawEllipse(track, rc);
                    if (prog > 0) using (var bar = new Pen(Color.White, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(bar, rc, -90, (float)(360 * prog));
                    Glass.Center(g, playing ? MdlPause : MdlPlay, Glass.MdlSmall, Color.White, c);
                }
            };
        }

        Seg MediaBarSeg()
        {
            if (!Media.Has) return null;
            string title = Media.Title, artist = Media.Artist;
            double prog = Media.Progress();
            return new Seg
            {
                W = S(220),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(new Rectangle(r.X, r.Y, r.Width - S(38), r.Height), null, b => Media.Command(0), title + "\n" + artist + "\nBấm để phát/tạm dừng", false);
                    Glass.Text(g, title + (artist.Length > 0 ? "  ·  " + artist : ""), Glass.Row, Color.White, new Rectangle(r.X + S(10), r.Y + S(4), r.Width - S(50), S(17)), StringAlignment.Near);
                    var bar = new Rectangle(r.X + S(10), r.Y + S(26), r.Width - S(52), S(4));
                    Glass.Fill(g, bar, S(2), Color.FromArgb(50, 255, 255, 255));
                    if (prog > 0) Glass.Fill(g, new Rectangle(bar.X, bar.Y, Math.Max(S(4), (int)(bar.Width * prog)), bar.Height), S(2), Color.White);
                    RoundButton(g, new Rectangle(r.Right - S(34), r.Y + (r.Height - S(28)) / 2, S(28), S(28)), MdlNext, Color.FromArgb(0, 255, 255, 255), () => Media.Command(1), "Bài sau");
                }
            };
        }

        // ---------- productivity ----------

        Seg StopwatchSeg()
        {
            TimeSpan t = Stopwatch2.Elapsed();
            bool running = Stopwatch2.Running;
            int laps = Stopwatch2.Laps.Count;
            return new Seg
            {
                W = S(150),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tip = new StringBuilder("Bấm vào số để đặt lại");
                    for (int i = Math.Max(0, laps - 8); i < laps; i++) tip.Append("\nVòng ").Append(i + 1).Append(": ").Append(Stopwatch2.Fmt(Stopwatch2.Laps[i]));
                    AddHit(new Rectangle(r.X, r.Y, r.Width - S(68), r.Height), null, b => { if (!Stopwatch2.Running) Stopwatch2.Reset(); }, tip.ToString(), false);
                    Glass.Text(g, laps > 0 ? "BẤM GIỜ · " + laps + " VÒNG" : "BẤM GIỜ", Glass.Day, Orange, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(78), S(14)), StringAlignment.Near);
                    Glass.Text(g, Stopwatch2.Fmt(t), Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(17), r.Width - S(78), S(20)), StringAlignment.Near);
                    int cy = r.Y + (r.Height - S(28)) / 2;
                    RoundButton(g, new Rectangle(r.Right - S(64), cy, S(28), S(28)), "", Color.FromArgb(50, 255, 255, 255), Stopwatch2.Lap, "Ghi vòng");
                    RoundButton(g, new Rectangle(r.Right - S(34), cy, S(28), S(28)), running ? MdlPause : MdlPlay, Orange, Stopwatch2.Toggle, running ? "Tạm dừng" : "Bắt đầu");
                }
            };
        }

        Seg NotesSeg()
        {
            List<string> notes = Config.Current.Notes;
            string latest = notes.Count > 0 ? notes[notes.Count - 1] : "";
            int nl = latest.IndexOf('\n');
            string first = (nl >= 0 ? latest.Substring(0, nl) : latest).Trim();
            return new Seg
            {
                W = S(170),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    Rectangle anchor = ToScreen(r);
                    bool hov = AddHit(r, null, b => OpenPopup("notes", () => new NotesPopup(anchor)), "Bấm để viết", false);
                    if (hov || popupKey == "notes") Glass.Fill(g, r, S(10), Color.FromArgb(28, 255, 255, 255));
                    var pad = new Rectangle(r.X + S(6), r.Y + (r.Height - S(28)) / 2, S(24), S(28));
                    Glass.Fill(g, pad, S(4), Color.FromArgb(255, 255, 214, 92));
                    using (var pen = new Pen(Color.FromArgb(120, 150, 110, 20))) for (int i = 1; i <= 3; i++) g.DrawLine(pen, pad.X + S(4), pad.Y + i * S(7), pad.Right - S(4), pad.Y + i * S(7));
                    Glass.Text(g, first.Length > 0 ? first : "Ghi chú", Glass.Row, Color.White, new Rectangle(r.X + S(38), r.Y + S(3), r.Width - S(44), S(17)), StringAlignment.Near);
                    Glass.Text(g, notes.Count == 0 ? "Bấm để viết" : notes.Count + " ghi chú", Glass.Date, Glass.Dim, new Rectangle(r.X + S(38), r.Y + S(20), r.Width - S(44), S(16)), StringAlignment.Near);
                }
            };
        }

        Seg CalendarSeg()
        {
            List<Calendar.Ev> evs = Calendar.Upcoming;
            Calendar.Ev ev = evs.Count > 0 ? evs[0] : null;
            DateTime now = DateTime.Now;
            string line1 = !Acct.Has("ics") ? "Lịch" : ev != null ? ev.Title : "Không có sự kiện",
                line2 = !Acct.Has("ics") ? "Thêm địa chỉ iCal" : Calendar.Error ?? (ev == null ? "7 ngày tới trống" : When(ev));
            return new Seg
            {
                W = S(190),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tip = new StringBuilder();
                    foreach (Calendar.Ev e in evs.GetRange(0, Math.Min(6, evs.Count))) tip.Append(e.Start.ToString(e.AllDay ? "dd/MM" : "dd/MM HH:mm")).Append("  ").Append(e.Title).Append('\n');
                    AddHit(r, null, b => { if (!Acct.Has("ics")) Manager.OpenSettings(5); else { Calendar.RefreshNow(); Sys.WinKey(0x4E); } }, tip.Length > 0 ? tip.ToString().TrimEnd() : line1 + "\n" + line2, false);
                    var badge = new Rectangle(r.X + S(6), r.Y + (r.Height - S(30)) / 2, S(28), S(30));
                    DateTime d = ev != null ? ev.Start : now;
                    Glass.Fill(g, badge, S(6), Color.FromArgb(60, 255, 255, 255));
                    Glass.Center(g, d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "T" + ((int)d.DayOfWeek + 1), Glass.Label, Red, new Rectangle(badge.X, badge.Y + S(2), badge.Width, S(10)));
                    Glass.Center(g, d.Day.ToString(), Glass.Row, Color.White, new Rectangle(badge.X, badge.Y + S(11), badge.Width, S(17)));
                    Glass.Text(g, line1, Glass.Row, Color.White, new Rectangle(r.X + S(42), r.Y + S(3), r.Width - S(48), S(17)), StringAlignment.Near);
                    Glass.Text(g, line2, Glass.Date, Glass.Dim, new Rectangle(r.X + S(42), r.Y + S(20), r.Width - S(48), S(16)), StringAlignment.Near);
                }
            };
        }

        static string When(Calendar.Ev e)
        {
            if (e.AllDay) return e.Start.Date == DateTime.Today ? "Hôm nay, cả ngày" : e.Start.ToString("dd/MM") + ", cả ngày";
            TimeSpan d = e.Start - DateTime.Now;
            if (d.TotalMinutes < 1) return "Đang diễn ra";
            if (d.TotalMinutes < 60) return "Sau " + (int)d.TotalMinutes + " phút";
            if (e.Start.Date == DateTime.Today) return "Hôm nay " + e.Start.ToString("HH:mm");
            return e.Start.ToString("dd/MM HH:mm");
        }

        Seg CurrencySeg()
        {
            string to, from = Acct.Get("cur_from").ToUpperInvariant();
            double res = Currency.Result(out to), amount = Currency.Amount();
            return new Seg
            {
                W = S(170),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, b => Currency.Swap(), (Currency.Error ?? "Tỷ giá open.er-api.com") + "\nBấm để đổi chiều, lăn chuột để ×10 / ÷10", false, d => Currency.Scale(d));
                    Glass.Text(g, Currency.Fmt(amount) + " " + from, Glass.Date, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(20), S(15)), StringAlignment.Near);
                    Glass.Text(g, double.IsNaN(res) ? "…" : Currency.Fmt(res) + " " + to, Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(17), r.Width - S(20), S(20)), StringAlignment.Near);
                }
            };
        }

        Seg ScreenTimeSeg()
        {
            int total = ScreenTime.TotalSeconds();
            int[] hours = ScreenTime.Hours();
            return new Seg
            {
                W = S(170),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tip = new StringBuilder("Hôm nay " + ScreenTime.Fmt(total));
                    foreach (KeyValuePair<string, int> kv in ScreenTime.Top(5)) tip.Append('\n').Append(kv.Key).Append(": ").Append(ScreenTime.Fmt(kv.Value));
                    AddHit(r, null, null, tip + "\nChỉ lưu trên máy này", false);
                    Glass.Text(g, "DÙNG MÁY", Glass.Day, Glass.Blue, new Rectangle(r.X + S(10), r.Y + S(3), S(70), S(14)), StringAlignment.Near);
                    Glass.Text(g, ScreenTime.Fmt(total), Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(17), S(80), S(20)), StringAlignment.Near);
                    int bx = r.X + S(84), bw = r.Right - S(8) - bx, baseY = r.Bottom - S(6), maxH = r.Height - S(14);
                    float step = bw / 24f;
                    for (int i = 0; i < 24; i++)
                    {
                        int h = (int)(maxH * hours[i] / 6.0);
                        using (var b = new SolidBrush(i == DateTime.Now.Hour ? Glass.Blue : Color.FromArgb(hours[i] > 0 ? 150 : 40, 255, 255, 255)))
                            g.FillRectangle(b, bx + i * step, baseY - Math.Max(1, h), Math.Max(1, step - 1), Math.Max(1, h));
                    }
                }
            };
        }

        Seg TimelineSeg()
        {
            ScreenTime.Day day = ScreenTime.Today;
            DateTime now = DateTime.Now;
            return new Seg
            {
                W = S(220),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    var tip = new StringBuilder("Hôm nay theo loại app");
                    for (int k = 0; k < ScreenTime.Kinds.Length; k++)
                    {
                        int n = 0;
                        if (day != null) foreach (int s in day.Slots) if (s == k) n++;
                        if (n > 0) tip.Append('\n').Append(ScreenTime.Kinds[k]).Append(": ").Append(ScreenTime.Fmt(n * 600));
                    }
                    AddHit(r, null, null, tip.ToString(), false);
                    var strip = new Rectangle(r.X + S(10), r.Y + S(8), r.Width - S(20), S(12));
                    Glass.Fill(g, strip, S(3), Color.FromArgb(40, 255, 255, 255));
                    float step = strip.Width / 144f;
                    if (day != null)
                        for (int i = 0; i < 144; i++)
                            if (day.Slots[i] >= 0)
                                using (var b = new SolidBrush(ScreenTime.KindColors[day.Slots[i]])) g.FillRectangle(b, strip.X + i * step, strip.Y, Math.Max(1, step), strip.Height);
                    float nx = strip.X + (float)(now.TimeOfDay.TotalMinutes / 1440 * strip.Width);
                    using (var p = new Pen(Color.White, 1.5f * Glass.Scale)) g.DrawLine(p, nx, strip.Y - S(3), nx, strip.Bottom + S(3));
                    foreach (int h in new[] { 0, 6, 12, 18 })
                        Glass.Text(g, h + "h", Glass.Label, Glass.Dim, new Rectangle(strip.X + (int)(h * 6 * step), strip.Bottom + S(4), S(30), S(10)), StringAlignment.Near);
                    if (Weather.Sunrise != DateTime.MinValue)
                        foreach (DateTime t in new[] { Weather.Sunrise, Weather.Sunset })
                        {
                            float sx = strip.X + (float)(t.TimeOfDay.TotalMinutes / 1440 * strip.Width);
                            using (var b = new SolidBrush(Color.FromArgb(255, 255, 204, 0))) g.FillEllipse(b, sx - S(2), strip.Y - S(5), S(4), S(4));
                        }
                }
            };
        }

        // ---------- weather variants ----------

        bool HasWeather { get { return !double.IsNaN(Weather.Temp) && Weather.Code >= 0; } }

        Seg WeatherSeg()
        {
            if (!HasWeather) return null;
            string temp = Math.Round(Weather.Temp) + "°", range = "C " + Math.Round(Weather.Hi) + "°  T " + Math.Round(Weather.Lo) + "°";
            return new Seg
            {
                W = Math.Max(Glass.Measure(temp, Glass.Huge), Glass.Measure(range, Glass.Date)) + S(52),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, WeatherTip(), false);
                    Glass.Center(g, Weather.Glyph(Weather.Code), Glass.GlyphBig, Weather.GlyphColor(Weather.Code), new Rectangle(r.X + S(4), r.Y, S(36), r.Height));
                    Glass.Text(g, temp, Glass.Time, Color.White, new Rectangle(r.X + S(42), r.Y + S(2), r.Width - S(46), S(19)), StringAlignment.Near);
                    Glass.Text(g, range, Glass.Date, Glass.Dim, new Rectangle(r.X + S(42), r.Y + S(20), r.Width - S(46), S(16)), StringAlignment.Near);
                }
            };
        }

        Seg WeatherDetailSeg()
        {
            if (!HasWeather) return null;
            List<Weather.WDay> days = Weather.Days;
            int rain = days.Count > 0 ? days[0].Rain : -1;
            string a = "Gió " + (double.IsNaN(Weather.Wind) ? "–" : Math.Round(Weather.Wind) + " km/h") + "   Ẩm " + (double.IsNaN(Weather.Humidity) ? "–" : Math.Round(Weather.Humidity) + "%"),
                b2 = "Mưa " + (rain < 0 ? "–" : rain + "%") + "   " + Math.Round(Weather.Lo) + "° – " + Math.Round(Weather.Hi) + "°";
            return new Seg
            {
                W = Math.Max(Glass.Measure(a, Glass.Row), Glass.Measure(b2, Glass.Row)) + S(22),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, WeatherTip(), false);
                    Glass.Text(g, a, Glass.Row, Color.White, new Rectangle(r.X + S(10), r.Y + S(3), r.Width - S(14), S(17)), StringAlignment.Near);
                    Glass.Text(g, b2, Glass.Row, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(20), r.Width - S(14), S(17)), StringAlignment.Near);
                }
            };
        }

        Seg WeatherStatusSeg()
        {
            if (!HasWeather) return null;
            string top = Weather.Describe(Weather.Code), bottom = (Weather.City ?? "") + (Weather.City != null ? " · " : "") + Math.Round(Weather.Temp) + "°C";
            return new Seg
            {
                W = Math.Max(Glass.Measure(top, Glass.Time), Glass.Measure(bottom, Glass.Date)) + S(22),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, WeatherTip(), false);
                    Glass.Text(g, top, Glass.Time, Color.White, new Rectangle(r.X + S(10), r.Y + S(2), r.Width - S(14), S(19)), StringAlignment.Near);
                    Glass.Text(g, bottom, Glass.Date, Glass.Dim, new Rectangle(r.X + S(10), r.Y + S(21), r.Width - S(14), S(15)), StringAlignment.Near);
                }
            };
        }

        Seg HourlySeg()
        {
            List<Weather.WHour> hours = Weather.Hours;
            if (hours.Count < 2) return null;
            List<Weather.WHour> next = hours.GetRange(1, Math.Min(6, hours.Count - 1));
            int col = S(34);
            return new Seg
            {
                W = next.Count * col + S(8),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, WeatherTip(), false);
                    int x = r.X + S(4);
                    foreach (Weather.WHour h in next)
                    {
                        Glass.Center(g, h.T.Hour + "h", Glass.Label, Glass.Dim, new Rectangle(x, r.Y + S(2), col, S(10)));
                        Glass.Center(g, Weather.Glyph(h.Code), Glass.Sym, Weather.GlyphColor(h.Code), new Rectangle(x, r.Y + S(11), col, S(14)));
                        Glass.Center(g, double.IsNaN(h.Temp) ? "–" : Math.Round(h.Temp) + "°", Glass.Row, Color.White, new Rectangle(x, r.Y + S(24), col, S(14)));
                        x += col;
                    }
                }
            };
        }

        Seg ForecastSeg(int n, bool withToday)
        {
            List<Weather.WDay> days = Weather.Days;
            if (days.Count < n + 1 || !HasWeather) return null;
            List<Weather.WDay> next = days.GetRange(1, n);
            int col = S(36), today = withToday ? S(70) : 0;
            return new Seg
            {
                W = today + n * col + S(8),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, WeatherTip(), false);
                    int x = r.X + S(4);
                    if (withToday)
                    {
                        Glass.Center(g, Weather.Glyph(Weather.Code), Glass.GlyphBig, Weather.GlyphColor(Weather.Code), new Rectangle(x, r.Y, S(30), r.Height));
                        Glass.Text(g, Math.Round(Weather.Temp) + "°", Glass.Time, Color.White, new Rectangle(x + S(30), r.Y, S(38), r.Height), StringAlignment.Near);
                        x += today;
                        using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255))) g.DrawLine(pen, x - S(3), r.Y + S(8), x - S(3), r.Bottom - S(8));
                    }
                    foreach (Weather.WDay d in next)
                    {
                        Glass.Center(g, Weather.DayShort(d.D), Glass.Label, Glass.Dim, new Rectangle(x, r.Y + S(2), col, S(10)));
                        Glass.Center(g, Weather.Glyph(d.Code), Glass.Sym, Weather.GlyphColor(d.Code), new Rectangle(x, r.Y + S(11), col, S(14)));
                        Glass.Center(g, double.IsNaN(d.Hi) ? "–" : Math.Round(d.Hi) + "°", Glass.Row, Color.White, new Rectangle(x, r.Y + S(24), col, S(14)));
                        x += col;
                    }
                }
            };
        }

        Seg SunSeg()
        {
            DateTime rise = Weather.Sunrise, set = Weather.Sunset;
            if (rise == DateTime.MinValue || set <= rise) return null;
            DateTime now = DateTime.Now;
            double f = Math.Max(0, Math.Min(1, (now - rise).TotalSeconds / (set - rise).TotalSeconds));
            bool day = now > rise && now < set;
            return new Seg
            {
                W = S(140),
                Draw = (g, r) =>
                {
                    Glass.Tile(g, r);
                    AddHit(r, null, null, "Mặt trời mọc " + rise.ToString("HH:mm") + ", lặn " + set.ToString("HH:mm") + "\nNgày dài " + (set - rise).ToString("h\\:mm"), false);
                    var arc = new RectangleF(r.X + S(24), r.Y + S(6), r.Width - S(48), (r.Height - S(10)) * 2);
                    using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1.4f * Glass.Scale) { DashStyle = DashStyle.Dot }) g.DrawArc(pen, arc, 180, 180);
                    using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255))) g.DrawLine(pen, r.X + S(8), arc.Y + arc.Height / 2, r.Right - S(8), arc.Y + arc.Height / 2);
                    double a = Math.PI * (1 - f);
                    float sx = arc.X + arc.Width / 2 + (float)(Math.Cos(a) * arc.Width / 2), sy = arc.Y + arc.Height / 2 - (float)(Math.Sin(a) * arc.Height / 2);
                    float d = S(8);
                    using (var b = new SolidBrush(day ? Color.FromArgb(255, 255, 204, 0) : Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, sx - d / 2, sy - d / 2, d, d);
                    Glass.Text(g, rise.ToString("HH:mm"), Glass.Label, Glass.Dim, new Rectangle(r.X + S(4), r.Bottom - S(12), S(40), S(10)), StringAlignment.Near);
                    Glass.Text(g, set.ToString("HH:mm"), Glass.Label, Glass.Dim, new Rectangle(r.Right - S(44), r.Bottom - S(12), S(40), S(10)), StringAlignment.Far);
                }
            };
        }
    }
}
