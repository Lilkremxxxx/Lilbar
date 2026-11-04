using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Lilbar
{
    public class ItemCfg
    {
        public string Id { get; set; }
        public bool On { get; set; }
    }

    public class TodoItem
    {
        public string Text { get; set; }
        public bool Done { get; set; }
    }

    // User settings in %APPDATA%\Lilbar\config.json. Public with properties so JavaScriptSerializer can read it.
    public class Config
    {
        public List<ItemCfg> Items { get; set; }       // dock tiles, left to right
        public List<string> PinOrder { get; set; }     // pinned app names, in display order
        public List<string> PinHidden { get; set; }    // pinned app names not shown on the dock
        public List<string> TrayPinned { get; set; }   // exe paths shown next to the arrow; null = follow Windows
        public string FolderName { get; set; }
        public List<string> FolderApps { get; set; }   // .exe/.lnk paths
        public string Align { get; set; }              // "center" | "left"
        public List<string> Cities { get; set; }       // world clock time zone ids, "" = slot off
        public int TimerMinutes { get; set; }
        public int FocusMin { get; set; }
        public int BreakMin { get; set; }
        public int LongBreakMin { get; set; }
        public List<TodoItem> Todos { get; set; }
        public string GitHubToken { get; set; }        // DPAPI-protected, base64
        public Dictionary<string, string> Keys { get; set; }    // API keys and other secrets, DPAPI-protected
        public Dictionary<string, string> Values { get; set; }  // plain settings: symbols, usernames, currency codes
        public List<string> Notes { get; set; }

        public static Config Current = Normalize(new Config());
        public static int Version;
        public static event Action Changed;

        static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lilbar", "config.json"); } }

        public static void Load()
        {
            Config c = null;
            try { if (File.Exists(FilePath)) c = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(FilePath, Encoding.UTF8)); }
            catch (Exception e) { Taskbar.Log(e); } // a broken file falls back to defaults rather than blocking the dock
            Current = Normalize(c ?? new Config());
        }

        public static Config Normalize(Config c)
        {
            if (c.Items == null) c.Items = new List<ItemCfg>();
            var seen = new HashSet<string>();
            c.Items.RemoveAll(i => i == null || Catalog.Get(i.Id) == null || !seen.Add(i.Id));
            foreach (CatalogItem d in Catalog.All) if (!seen.Contains(d.Id)) c.Items.Add(new ItemCfg { Id = d.Id, On = d.On });
            if (c.Keys == null) c.Keys = new Dictionary<string, string>();
            if (c.Values == null) c.Values = new Dictionary<string, string>();
            if (c.Notes == null) c.Notes = new List<string>();
            if (c.PinOrder == null) c.PinOrder = new List<string>();
            if (c.PinHidden == null) c.PinHidden = new List<string>();
            if (c.FolderApps == null) c.FolderApps = new List<string>();
            if (string.IsNullOrEmpty(c.FolderName)) c.FolderName = "Thư mục";
            if (c.Align != "left") c.Align = "center";
            if (c.Cities == null) c.Cities = new List<string> { "Tokyo Standard Time", "GMT Standard Time", "Eastern Standard Time" };
            while (c.Cities.Count < 3) c.Cities.Add("");
            if (c.Cities.Count > 3) c.Cities.RemoveRange(3, c.Cities.Count - 3);
            if (c.TimerMinutes <= 0) c.TimerMinutes = 5;
            if (c.FocusMin <= 0) c.FocusMin = 25;
            if (c.BreakMin <= 0) c.BreakMin = 5;
            if (c.LongBreakMin <= 0) c.LongBreakMin = 15;
            if (c.Todos == null) c.Todos = new List<TodoItem>();
            c.Todos.RemoveAll(t => t == null || string.IsNullOrEmpty(t.Text));
            return c;
        }

        public static bool Has(string id) { return Current.Items.Exists(i => i.Id == id && i.On); }

        public static void Apply()
        {
            Version++;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(Current), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception e) { Taskbar.Log(e); }
            if (Changed != null) Changed();
        }
    }

    // Location from the IP (ipinfo.io, as lumabar does), weather from open-meteo.com. No keys, polled every 15 minutes.
    static class Weather
    {
        public static double Temp = double.NaN, Hi = double.NaN, Lo = double.NaN;
        public static int Code = -1, Stamp;
        public static double Wind = double.NaN, Humidity = double.NaN;
        public static DateTime Sunrise, Sunset;
        public static volatile List<WHour> Hours = new List<WHour>();
        public static volatile List<WDay> Days = new List<WDay>();
        public static volatile string City, Error;
        static string lat, lon;

        public class WHour { public DateTime T; public double Temp; public int Code; }
        public class WDay { public DateTime D; public double Hi, Lo; public int Code, Rain; }
        static DateTime next = DateTime.MinValue;
        static int busy;

        public static void MaybeFetch(Action done)
        {
            if (DateTime.Now < next || Interlocked.Exchange(ref busy, 1) == 1) return;
            next = DateTime.Now.AddMinutes(15);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Fetch(); }
                catch (WebException) { Error = "Không có mạng"; next = DateTime.Now.AddMinutes(2); }
                catch (Exception e) { Error = "Không đọc được thời tiết"; Taskbar.Log(e); }
                finally { Interlocked.Exchange(ref busy, 0); done(); }
            });
        }

        static Dictionary<string, object> Get(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Lilbar";
            req.Timeout = 15000;
            using (WebResponse resp = req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(r.ReadToEnd());
        }

        static void Fetch()
        {
            if (lat == null)
            {
                Dictionary<string, object> ip = Get("https://ipinfo.io/json");
                object o;
                string loc = ip.TryGetValue("loc", out o) ? o as string : null;
                string[] p = loc == null ? new string[0] : loc.Split(',');
                double a, b; // validated before going into a URL
                if (p.Length != 2 || !double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a) || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                { Error = "Không xác định được vị trí"; return; }
                lat = a.ToString(CultureInfo.InvariantCulture);
                lon = b.ToString(CultureInfo.InvariantCulture);
                City = ip.TryGetValue("city", out o) ? o as string : null;
            }
            Parse(Get("https://api.open-meteo.com/v1/forecast?latitude=" + lat + "&longitude=" + lon +
                "&current=temperature_2m,weather_code,wind_speed_10m,relative_humidity_2m&hourly=temperature_2m,weather_code&forecast_hours=7" +
                "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,precipitation_probability_max&timezone=auto&forecast_days=5"));
            Error = null;
        }

        static List<object> L(Dictionary<string, object> d, string k)
        {
            object o;
            var l = d != null && d.TryGetValue(k, out o) ? o as IList : null;
            var r = new List<object>();
            if (l != null) foreach (object x in l) r.Add(x);
            return r;
        }

        static DateTime Time(object o)
        {
            DateTime t;
            return o is string && DateTime.TryParse((string)o, CultureInfo.InvariantCulture, DateTimeStyles.None, out t) ? t : DateTime.MinValue;
        }

        public static void Parse(Dictionary<string, object> w)
        {
            object o;
            var cur = w.TryGetValue("current", out o) ? o as Dictionary<string, object> : null;
            var daily = w.TryGetValue("daily", out o) ? o as Dictionary<string, object> : null;
            if (cur != null)
            {
                Temp = Num(cur.TryGetValue("temperature_2m", out o) ? o : null);
                double c = Num(cur.TryGetValue("weather_code", out o) ? o : null);
                Code = double.IsNaN(c) ? -1 : (int)c;
                Wind = Num(cur.TryGetValue("wind_speed_10m", out o) ? o : null);
                Humidity = Num(cur.TryGetValue("relative_humidity_2m", out o) ? o : null);
            }
            if (daily != null)
            {
                Hi = Num(First(daily.TryGetValue("temperature_2m_max", out o) ? o : null));
                Lo = Num(First(daily.TryGetValue("temperature_2m_min", out o) ? o : null));
                List<object> t = L(daily, "time"), hi = L(daily, "temperature_2m_max"), lo = L(daily, "temperature_2m_min"),
                    code = L(daily, "weather_code"), rain = L(daily, "precipitation_probability_max"), rise = L(daily, "sunrise"), set = L(daily, "sunset");
                var days = new List<WDay>();
                for (int i = 0; i < t.Count; i++)
                    days.Add(new WDay
                    {
                        D = Time(t[i]), Hi = i < hi.Count ? Num(hi[i]) : double.NaN, Lo = i < lo.Count ? Num(lo[i]) : double.NaN,
                        Code = i < code.Count && code[i] != null ? (int)Num(code[i]) : -1, Rain = i < rain.Count && rain[i] != null ? (int)Num(rain[i]) : -1,
                    });
                Days = days;
                if (rise.Count > 0) Sunrise = Time(rise[0]);
                if (set.Count > 0) Sunset = Time(set[0]);
            }
            var hourly = w.TryGetValue("hourly", out o) ? o as Dictionary<string, object> : null;
            if (hourly != null)
            {
                List<object> t = L(hourly, "time"), temp = L(hourly, "temperature_2m"), code = L(hourly, "weather_code");
                var hours = new List<WHour>();
                for (int i = 0; i < t.Count; i++)
                    hours.Add(new WHour { T = Time(t[i]), Temp = i < temp.Count ? Num(temp[i]) : double.NaN, Code = i < code.Count && code[i] != null ? (int)Num(code[i]) : -1 });
                Hours = hours;
            }
            Stamp++;
        }

        public static string DayShort(DateTime d)
        {
            if (d.Date == DateTime.Today) return "Nay";
            return d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "T" + ((int)d.DayOfWeek + 1);
        }

        static object First(object o) { var l = o as IList; return l != null && l.Count > 0 ? l[0] : null; }
        static double Num(object o) { return o == null ? double.NaN : Convert.ToDouble(o, CultureInfo.InvariantCulture); }

        // WMO weather codes as Segoe UI Symbol glyphs.
        public static string Glyph(int code)
        {
            if (code <= 1) return "☀";
            if (code == 2) return "⛅";
            if (code == 3 || code == 45 || code == 48) return "☁";
            if (code >= 71 && code <= 77) return "❄";
            if (code >= 95) return "⚡";
            return "☂";
        }

        public static Color GlyphColor(int code)
        {
            if (code <= 2 || code >= 95) return Color.FromArgb(255, 255, 204, 0);
            if (code == 3 || code == 45 || code == 48 || (code >= 71 && code <= 77)) return Color.FromArgb(255, 225, 230, 240);
            return Color.FromArgb(255, 110, 185, 255);
        }

        public static string Describe(int code)
        {
            if (code == 0) return "Trời quang";
            if (code <= 2) return "Ít mây";
            if (code == 3) return "Nhiều mây";
            if (code == 45 || code == 48) return "Sương mù";
            if (code >= 51 && code <= 57) return "Mưa phùn";
            if (code >= 61 && code <= 67) return "Mưa";
            if (code >= 71 && code <= 77) return "Tuyết";
            if (code >= 80 && code <= 82) return "Mưa rào";
            if (code >= 95) return "Dông";
            return "";
        }
    }

    // State shared by every dock, refreshed once a second.
    static class Model
    {
        public static List<Pin> Pins = new List<Pin>();
        public static List<Win> Wins = new List<Win>();
        public static int Cpu, Ram;
        static int tick;

        public static void Tick(Action asyncDone)
        {
            if (tick % 30 == 0) Pins = Sys.Pinned();
            if (tick % 2 == 0) { Cpu = Sys.Cpu(); Ram = Sys.Ram(); }
            tick++;
            Wins = Sys.Windows();
            TrayHost.Prune();
            if (Config.Has("claude")) Claude.MaybeFetch(asyncDone);
            if (Config.Current.Items.Exists(i => i.On && WeatherIds.Contains(i.Id))) Weather.MaybeFetch(asyncDone);
            if (Config.Has("currency")) Currency.MaybeFetch(asyncDone);
            if (Config.Has("calendar")) Calendar.MaybeFetch(asyncDone);
            if (Config.Has("screentime") || Config.Has("timeline")) ScreenTime.Tick();
            if (tick % 5 == 1) Files.Tick(Config.Has("recent"), Config.Has("downloads"), Config.Has("screenshots"), Config.Has("recycle"), Config.Has("storage"));
            if (Config.Has("github") || Config.Has("githubact") || Config.Has("githubov")) GitHub.MaybeFetch(asyncDone);
            if (Config.Has("stock") || Config.Has("stockchart") || Config.Has("watchlist")) Stocks.MaybeFetch(asyncDone);
            if (Config.Has("metric")) Crypto.MaybeFetch(asyncDone);
            if (Config.Has("leetcode")) LeetCode.MaybeFetch(asyncDone);
            if (Config.Has("wallpaper")) Wallpapers.MaybeFetch(asyncDone);
            bool total = Config.Has("revenue_total");
            foreach (Revenue.Src r in Revenue.All)
                if (total || Config.Has(r.Id) || Config.Has(r.Id + "_chart")) Revenue.MaybeFetch(r.Id, asyncDone);
            foreach (string id in Svc.Ids) if (Config.Has(id)) Svc.MaybeFetch(id, asyncDone);
            if (Config.Has("network")) Net.Tick();
            if (Config.Has("volume")) Audio.Tick();
            Countdown.Tick(); // timers keep running even while their tiles are hidden
            Pomodoro.Tick();
        }

        public static List<Pin> OrderedPins(bool includeHidden)
        {
            List<string> order = Config.Current.PinOrder;
            var list = new List<Pin>(Pins);
            list.Sort((a, b) =>
            {
                int ia = order.IndexOf(a.Name), ib = order.IndexOf(b.Name);
                if (ia < 0) ia = int.MaxValue;
                if (ib < 0) ib = int.MaxValue;
                return ia != ib ? ia.CompareTo(ib) : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            if (!includeHidden) list.RemoveAll(p => Config.Current.PinHidden.Contains(p.Name));
            return list;
        }

        static readonly List<string> WeatherIds = new List<string> { "today", "weekstrip", "clockweather", "weather", "weatherdetail", "weatherstatus", "hourly", "forecast", "todayforecast", "sun", "timeline" };
        static readonly string[] SecondIds = { "analog", "analogdark", "segment" }; // tiles that change every second

        static string LimitSig(Claude.Limit l) { return l == null ? "-" : l.Pct + "@" + l.Reset.ToString("HHmm"); }

        // Everything a dock draws; docks repaint only when this changes.
        public static string Signature()
        {
            var s = new StringBuilder();
            foreach (Win w in Wins) s.Append(w.Hwnd).Append(w.Active ? '*' : '-').Append(w.Title).Append('|');
            PowerStatus ps = SystemInformation.PowerStatus;
            s.Append(Pins.Count).Append(Cpu).Append('/').Append(Ram).Append(DateTime.Now.ToString("HH:mm d"));
            s.Append(ps.BatteryLifePercent).Append(ps.PowerLineStatus);
            s.Append(Claude.Available).Append(Claude.Error).Append(LimitSig(Claude.Session)).Append(LimitSig(Claude.Week));
            s.Append(Weather.Temp).Append(Weather.Code).Append(Weather.Error);
            s.Append('t').Append(TrayHost.Version).Append('c').Append(Config.Version);
            s.Append('m').Append(Media.Has).Append(Media.Title).Append(Media.Playing).Append(Media.Thumb != null);
            s.Append('n').Append(Net.Fmt(Net.Down)).Append(Net.Fmt(Net.Up)).Append('v').Append(Audio.Level).Append(Audio.Muted);
            foreach (Radios.R r in Radios.List) s.Append(r.WiFi).Append(r.On);
            s.Append('h').Append(Thermal.C).Append('x').Append(Countdown.Remaining()).Append(Countdown.Running);
            s.Append('p').Append(Pomodoro.Remaining()).Append(Pomodoro.Running).Append(Pomodoro.Phase).Append(Pomodoro.Round);
            s.Append('g').Append(GitHub.Unread).Append(GitHub.Total).Append(GitHub.Error);
            s.Append('w').Append(Weather.Stamp).Append('s').Append(Stopwatch2.Elapsed().TotalSeconds.ToString("0")).Append(Stopwatch2.Laps.Count);
            if (Array.Exists(SecondIds, Config.Has)) s.Append(DateTime.Now.Second);
            if (Media.Has) s.Append((int)(Media.Progress() * 200)); // media bars move in half-percent steps
            s.Append('f').Append(Files.Recent.Count > 0 ? Files.Recent[0].Path : "").Append(Files.Download != null ? Files.Download.Path : "")
                .Append(Files.Shots.Count > 0 ? Files.Shots[0].Path : "").Append(Files.BinItems).Append(Files.Free >> 26);
            s.Append('k').Append(Config.Has("language") ? Desk.LangCode() : "").Append(Config.Has("theme") && Desk.Dark());
            s.Append('e').Append(Calendar.Upcoming.Count > 0 ? Calendar.Upcoming[0].Title + Calendar.Upcoming[0].Start.Ticks : "").Append(Calendar.Error);
            s.Append('$').Append(Currency.Base).Append(Currency.Rates != null).Append(Currency.Error);
            if (Config.Has("screentime") || Config.Has("timeline")) s.Append(ScreenTime.TotalSeconds() / 60);
            s.Append('q').Append(Stocks.Stamp).Append(Crypto.Price).Append(LeetCode.All).Append(LeetCode.Error).Append(Wallpapers.Pics.Count)
                .Append(GitHub.OpenPRs).Append(GitHub.Stars).Append(GitHub.Reviews);
            s.Append('r').Append(Revenue.Stamp).Append('v').Append(Svc.Stamp);
            return s.ToString();
        }
    }
}
