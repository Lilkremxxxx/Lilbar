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

        // Declared before Current: static fields initialize in order and Normalize reads this.
        // Everything fits a 1920 px screen with the defaults; the rest are one switch away in Settings.
        public static readonly string[][] Defaults =
        {
            new[] { "apps", "1" }, new[] { "folder", "1" }, new[] { "media", "1" }, new[] { "system", "1" },
            new[] { "network", "1" }, new[] { "temp", "0" }, new[] { "claude", "1" }, new[] { "todos", "1" },
            new[] { "timer", "0" }, new[] { "pomodoro", "0" }, new[] { "github", "0" }, new[] { "worldclock", "0" },
            new[] { "tray", "1" }, new[] { "toggles", "0" }, new[] { "volume", "1" }, new[] { "battery", "1" },
            new[] { "today", "1" }, new[] { "flipclock", "0" }, new[] { "clock", "0" },
        };
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
            c.Items.RemoveAll(i => i == null || !Array.Exists(Defaults, d => d[0] == i.Id) || !seen.Add(i.Id));
            foreach (string[] d in Defaults) if (!seen.Contains(d[0])) c.Items.Add(new ItemCfg { Id = d[0], On = d[1] == "1" });
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
        public static int Code = -1;
        public static volatile string City, Error;
        static string lat, lon;
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
                "&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1"));
            Error = null;
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
            }
            if (daily != null)
            {
                Hi = Num(First(daily.TryGetValue("temperature_2m_max", out o) ? o : null));
                Lo = Num(First(daily.TryGetValue("temperature_2m_min", out o) ? o : null));
            }
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
            if (Config.Has("today")) Weather.MaybeFetch(asyncDone);
            if (Config.Has("github")) GitHub.MaybeFetch(asyncDone);
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
            return s.ToString();
        }
    }
}
