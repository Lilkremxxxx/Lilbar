using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lilbar
{
    // Recent files, Downloads, screenshots, recycle bin and drive space. Cheap stats, read every few seconds on the UI thread.
    static class Files
    {
        public class Entry { public string Path, Name; public DateTime At; }
        public static List<Entry> Recent = new List<Entry>(), Shots = new List<Entry>();
        public static Entry Download;
        public static long BinItems = -1, BinBytes;
        public static long Free, Total;
        static readonly Dictionary<string, Bitmap> thumbs = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);
        [StructLayout(LayoutKind.Sequential, Pack = 4)] struct SHQUERYRBINFO { public int cbSize; public long i64Size, i64NumItems; }

        static string Known(string guid)
        {
            Guid id = new Guid(guid);
            IntPtr p;
            if (N.SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out p) != 0) return null;
            try { return Marshal.PtrToStringUni(p); }
            finally { Marshal.FreeCoTaskMem(p); }
        }

        static List<Entry> Newest(string dir, string pattern, int n)
        {
            var list = new List<Entry>();
            if (dir == null || !Directory.Exists(dir)) return list;
            try
            {
                FileInfo[] files = new DirectoryInfo(dir).GetFiles(pattern);
                Array.Sort(files, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                for (int i = 0; i < files.Length && list.Count < n; i++)
                    if ((files[i].Attributes & FileAttributes.Hidden) == 0)
                        list.Add(new Entry { Path = files[i].FullName, Name = System.IO.Path.GetFileNameWithoutExtension(files[i].Name), At = files[i].LastWriteTime });
            }
            catch (Exception e) { Taskbar.Log(e); }
            return list;
        }

        public static void Tick(bool recent, bool downloads, bool shots, bool bin, bool storage)
        {
            if (recent) Recent = Newest(Environment.GetFolderPath(Environment.SpecialFolder.Recent), "*.lnk", 3);
            if (downloads) { List<Entry> d = Newest(Known("374DE290-123F-4565-9164-39C4925E467B"), "*", 1); Download = d.Count > 0 ? d[0] : null; }
            if (shots) Shots = Newest(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"), "*.png", 3);
            if (bin)
            {
                var q = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
                if (SHQueryRecycleBin(null, ref q) == 0) { BinItems = q.i64NumItems; BinBytes = q.i64Size; }
            }
            if (storage)
                try { var d = new DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory)); Free = d.AvailableFreeSpace; Total = d.TotalSize; }
                catch (Exception e) { Taskbar.Log(e); }
        }

        // flags 0: Windows shows its own "are you sure" dialog, so a misclick can't silently delete anything.
        public static void EmptyBin(IntPtr owner) { SHEmptyRecycleBin(owner, null, 0); }

        public static Bitmap Thumb(string path)
        {
            Bitmap b;
            if (thumbs.TryGetValue(path, out b)) return b;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image img = Image.FromStream(fs))
                {
                    int side = Math.Min(img.Width, img.Height);
                    b = new Bitmap(64, 64);
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(img, new Rectangle(0, 0, 64, 64), new Rectangle((img.Width - side) / 2, (img.Height - side) / 2, side, side), GraphicsUnit.Pixel);
                    }
                }
            }
            catch (Exception) { b = null; }
            if (thumbs.Count > 30) thumbs.Clear();
            thumbs[path] = b;
            return b;
        }

        public static string Size(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.#") + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0") + " MB";
            return (bytes / 1024) + " KB";
        }
    }

    // Keyboard layout of the window the user is typing in, and Windows' light/dark setting.
    static class Desk
    {
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
        const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static string LangCode() { string full; return Lang(out full); }

        public static string Lang(out string full)
        {
            uint pid;
            uint tid = N.GetWindowThreadProcessId(N.GetForegroundWindow(), out pid);
            int langId = (int)((long)GetKeyboardLayout(tid) & 0xFFFF);
            try
            {
                var c = new CultureInfo(langId);
                full = c.DisplayName;
                return c.ThreeLetterWindowsLanguageName;
            }
            catch (Exception) { full = "0x" + langId.ToString("X4"); return "?"; }
        }

        // The layout belongs to the foreground window's thread, so the request goes there.
        public static void NextLang() { N.PostMessage(N.GetForegroundWindow(), 0x50, IntPtr.Zero, (IntPtr)1); } // WM_INPUTLANGCHANGEREQUEST, HKL_NEXT

        public static bool Dark()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Personalize))
            {
                object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                return v is int && (int)v == 0;
            }
        }

        public static void ToggleTheme()
        {
            int light = Dark() ? 1 : 0;
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Personalize))
            {
                k.SetValue("AppsUseLightTheme", light, RegistryValueKind.DWord);
                k.SetValue("SystemUsesLightTheme", light, RegistryValueKind.DWord);
            }
            // Without this broadcast nothing repaints until the next sign-in.
            IntPtr r, s = Marshal.StringToHGlobalUni("ImmersiveColorSet");
            try { N.SendMessageTimeout((IntPtr)0xFFFF, 0x1A, IntPtr.Zero, s, 0x2, 1000, out r); }
            finally { Marshal.FreeHGlobal(s); }
        }
    }

    static class Stopwatch2
    {
        public static bool Running;
        public static readonly List<TimeSpan> Laps = new List<TimeSpan>();
        static TimeSpan banked;
        static DateTime since;

        public static TimeSpan Elapsed() { return Running ? banked + (DateTime.Now - since) : banked; }
        public static void Toggle() { if (Running) { banked = Elapsed(); Running = false; } else { since = DateTime.Now; Running = true; } }
        public static void Lap() { if (Running) Laps.Add(Elapsed()); }
        public static void Reset() { Running = false; banked = TimeSpan.Zero; Laps.Clear(); }
        public static string Fmt(TimeSpan t) { return t.TotalHours >= 1 ? ((int)t.TotalHours) + ":" + t.ToString("mm\\:ss") : t.ToString("mm\\:ss"); }
    }

    // Exchange rates from open.er-api.com (no key), refreshed hourly.
    static class Currency
    {
        public static volatile Dictionary<string, double> Rates;
        public static volatile string Base, Error;
        static DateTime next = DateTime.MinValue;
        static int busy;

        public static void MaybeFetch(Action done)
        {
            string from = Acct.Get("cur_from").ToUpperInvariant();
            if (from != Base) next = DateTime.MinValue;
            if (DateTime.Now < next || Interlocked.Exchange(ref busy, 1) == 1) return;
            next = DateTime.Now.AddHours(1);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (!Regex.IsMatch(from, "^[A-Z]{3}$")) { Error = "Mã tiền tệ không hợp lệ"; return; } // goes into a URL
                    var req = (HttpWebRequest)WebRequest.Create("https://open.er-api.com/v6/latest/" + from);
                    req.UserAgent = "Lilbar";
                    req.Timeout = 15000;
                    using (WebResponse resp = req.GetResponse())
                    using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) Parse(from, r.ReadToEnd());
                    Error = null;
                }
                catch (WebException) { Error = "Không có mạng"; next = DateTime.Now.AddMinutes(2); }
                catch (Exception e) { Error = "Không đọc được tỷ giá"; Taskbar.Log(e); }
                finally { Interlocked.Exchange(ref busy, 0); done(); }
            });
        }

        public static void Parse(string from, string json)
        {
            var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            object o;
            var rates = d != null && d.TryGetValue("rates", out o) ? o as Dictionary<string, object> : null;
            if (rates == null) throw new InvalidDataException("no rates");
            var r = new Dictionary<string, double>();
            foreach (KeyValuePair<string, object> kv in rates) r[kv.Key] = Convert.ToDouble(kv.Value, CultureInfo.InvariantCulture);
            Rates = r;
            Base = from;
        }

        public static double Amount()
        {
            double a;
            return double.TryParse(Acct.Get("cur_amount"), NumberStyles.Float, CultureInfo.InvariantCulture, out a) ? a : 1;
        }

        public static double Result(out string to)
        {
            to = Acct.Get("cur_to").ToUpperInvariant();
            double r;
            Dictionary<string, double> rates = Rates;
            return rates != null && Base == Acct.Get("cur_from").ToUpperInvariant() && rates.TryGetValue(to, out r) ? Amount() * r : double.NaN;
        }

        public static void Swap()
        {
            string from = Acct.Get("cur_from"), to = Acct.Get("cur_to");
            Config.Current.Values["cur_from"] = to;
            Config.Current.Values["cur_to"] = from;
            Config.Apply();
        }

        public static void Scale(int dir)
        {
            double a = dir > 0 ? Amount() * 10 : Math.Max(1, Amount() / 10);
            Config.Current.Values["cur_amount"] = a.ToString("0.##", CultureInfo.InvariantCulture);
            Config.Apply();
        }

        public static string Fmt(double v) { return v.ToString(v >= 100 ? "#,0" : "0.##", CultureInfo.GetCultureInfo("vi-VN")); }
    }

    // Next events from an iCal (ICS) address.
    static class Calendar
    {
        public class Ev { public DateTime Start; public string Title; public bool AllDay; }
        public static volatile List<Ev> Upcoming = new List<Ev>();
        public static volatile string Error;
        static DateTime next = DateTime.MinValue;
        static int busy;

        public static void RefreshNow() { next = DateTime.MinValue; }

        public static void MaybeFetch(Action done)
        {
            if (!Acct.Has("ics") || DateTime.Now < next || Interlocked.Exchange(ref busy, 1) == 1) return;
            next = DateTime.Now.AddMinutes(15);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string url = Acct.Get("ics");
                    if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url.Substring(9);
                    if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { Error = "Địa chỉ iCal phải bắt đầu bằng https://"; return; }
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "Lilbar";
                    req.Timeout = 20000;
                    using (WebResponse resp = req.GetResponse())
                    using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) Upcoming = Parse(r.ReadToEnd(), DateTime.Now, 7);
                    Error = null;
                }
                catch (WebException) { Error = "Không tải được lịch"; next = DateTime.Now.AddMinutes(2); }
                catch (Exception e) { Error = "Không đọc được lịch"; Taskbar.Log(e); }
                finally { Interlocked.Exchange(ref busy, 0); done(); }
            });
        }

        static DateTime When(string name, string value, out bool allDay)
        {
            allDay = value.Length == 8 || (name.Contains("VALUE=DATE") && !name.Contains("VALUE=DATE-TIME"));
            string[] formats = { "yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd'T'HHmmss", "yyyyMMdd" };
            DateTime t;
            if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return DateTime.MinValue;
            // shortcut: TZID is ignored and read as local time; correct for calendars kept in the user's own zone
            return value.EndsWith("Z") ? DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime() : t;
        }

        public static List<Ev> Parse(string ics, DateTime now, int days)
        {
            string unfolded = Regex.Replace(ics, @"\r?\n[ \t]", ""); // continuation lines
            var list = new List<Ev>();
            DateTime until = now.AddDays(days);
            foreach (Match m in Regex.Matches(unfolded, @"BEGIN:VEVENT(.*?)END:VEVENT", RegexOptions.Singleline))
            {
                DateTime start = DateTime.MinValue;
                bool allDay = false;
                string title = "", rule = null;
                foreach (string raw in m.Groups[1].Value.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    int c = line.IndexOf(':');
                    if (c < 0) continue;
                    string name = line.Substring(0, c), value = line.Substring(c + 1);
                    if (name.StartsWith("DTSTART")) start = When(name, value, out allDay);
                    else if (name == "SUMMARY") title = value.Replace("\\,", ",").Replace("\\;", ";").Replace("\\n", " ");
                    else if (name == "RRULE") rule = value;
                }
                if (start == DateTime.MinValue) continue;
                foreach (DateTime t in Occurrences(start, rule, now.Date, until))
                    if (allDay ? t.Date >= now.Date : t >= now.AddMinutes(-5)) list.Add(new Ev { Start = t, Title = title, AllDay = allDay });
            }
            list.Sort((a, b) => a.Start.CompareTo(b.Start));
            return list;
        }

        static IEnumerable<DateTime> Occurrences(DateTime start, string rule, DateTime from, DateTime until)
        {
            if (rule == null) { if (start >= from && start <= until) yield return start; yield break; }
            var p = new Dictionary<string, string>();
            foreach (string part in rule.Split(';')) { int e = part.IndexOf('='); if (e > 0) p[part.Substring(0, e)] = part.Substring(e + 1); }
            string freq, s;
            // shortcut: only DAILY and WEEKLY repeat (BYDAY ignored); other rules show their first date only
            if (!p.TryGetValue("FREQ", out freq) || (freq != "DAILY" && freq != "WEEKLY")) { if (start >= from && start <= until) yield return start; yield break; }
            int interval = 1, count = int.MaxValue;
            if (p.TryGetValue("INTERVAL", out s)) int.TryParse(s, out interval);
            if (p.TryGetValue("COUNT", out s)) int.TryParse(s, out count);
            DateTime end = until;
            bool ad;
            if (p.TryGetValue("UNTIL", out s)) { DateTime u = When("UNTIL", s, out ad); if (u != DateTime.MinValue && u < end) end = u; }
            TimeSpan step = TimeSpan.FromDays((freq == "DAILY" ? 1 : 7) * Math.Max(1, interval));
            int n = 0;
            for (DateTime t = start; t <= end && n < count; t += step, n++)
                if (t >= from) yield return t;
        }
    }

    // Foreground app time, kept only on this PC in %APPDATA%\Lilbar\screentime\yyyy-MM-dd.json. Idle time is not counted.
    static class ScreenTime
    {
        public class Day { public Dictionary<string, int> Apps { get; set; } public List<int> Slots { get; set; } } // 144 ten-minute slots, -1 empty
        public static Day Today;
        static string dayKey;
        static int slot = -1, dirty;
        public static readonly string[] Kinds = { "Trình duyệt", "Lập trình", "Trò chuyện", "Trò chơi", "Giải trí", "Khác" };
        public static readonly Color[] KindColors =
        {
            Color.FromArgb(255, 74, 155, 255), Color.FromArgb(255, 52, 199, 89), Color.FromArgb(255, 175, 82, 222),
            Color.FromArgb(255, 255, 69, 58), Color.FromArgb(255, 255, 159, 10), Color.FromArgb(255, 142, 142, 147),
        };
        static readonly int[] tally = new int[6];

        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO i);
        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize, dwTime; }

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lilbar", "screentime"); } }

        public static int Kind(string exe)
        {
            string n = Path.GetFileNameWithoutExtension(exe ?? "").ToLowerInvariant();
            if (Regex.IsMatch(n, "^(chrome|msedge|firefox|brave|opera|vivaldi|arc|zen)$")) return 0;
            if (Regex.IsMatch(n, "^(code|code - insiders|devenv|idea64|pycharm64|rider64|windowsterminal|powershell|pwsh|cmd|wt|cursor|sublime_text|notepad\\+\\+)$")) return 1;
            if (Regex.IsMatch(n, "^(discord|zalo|telegram|ms-teams|teams|slack|messenger|whatsapp|signal)$")) return 2;
            if (Regex.IsMatch(n, "^(steam|riotclientservices|valorant.*|leagueclient|epicgameslauncher|minecraft.*|javaw|robloxplayerbeta)$")) return 3;
            if (Regex.IsMatch(n, "^(spotify|vlc|wmplayer|potplayer.*|mpc-hc.*|netflix|photos)$")) return 4;
            return 5;
        }

        static void Load()
        {
            dayKey = DateTime.Today.ToString("yyyy-MM-dd");
            Today = null;
            try
            {
                string f = Path.Combine(Dir, dayKey + ".json");
                if (File.Exists(f)) Today = new JavaScriptSerializer().Deserialize<Day>(File.ReadAllText(f, Encoding.UTF8));
            }
            catch (Exception e) { Taskbar.Log(e); }
            if (Today == null) Today = new Day();
            if (Today.Apps == null) Today.Apps = new Dictionary<string, int>();
            if (Today.Slots == null || Today.Slots.Count != 144) { Today.Slots = new List<int>(); for (int i = 0; i < 144; i++) Today.Slots.Add(-1); }
            slot = -1;
        }

        public static void Tick()
        {
            if (Today == null || dayKey != DateTime.Today.ToString("yyyy-MM-dd")) { Flush(); Load(); }
            var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (GetLastInputInfo(ref li) && unchecked((uint)Environment.TickCount - li.dwTime) > 120000) return; // idle for 2 minutes
            string exe = Sys.ExeOfWindow(N.GetForegroundWindow());
            if (exe.Length == 0) return;
            string name = Path.GetFileNameWithoutExtension(exe);
            int v;
            Today.Apps[name] = (Today.Apps.TryGetValue(name, out v) ? v : 0) + 1;
            DateTime now = DateTime.Now;
            int s = (now.Hour * 60 + now.Minute) / 10;
            if (s != slot) { Array.Clear(tally, 0, tally.Length); slot = s; }
            tally[Kind(exe)]++;
            int best = 0;
            for (int k = 1; k < tally.Length; k++) if (tally[k] > tally[best]) best = k;
            Today.Slots[s] = best;
            if (++dirty >= 60) Flush();
        }

        public static void Flush()
        {
            if (Today == null || dirty == 0) return;
            dirty = 0;
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, dayKey + ".json"), new JavaScriptSerializer().Serialize(Today), Encoding.UTF8);
            }
            catch (Exception e) { Taskbar.Log(e); }
        }

        public static int TotalSeconds()
        {
            int t = 0;
            if (Today != null) foreach (int v in Today.Apps.Values) t += v;
            return t;
        }

        // Filled ten-minute slots per hour (0..6).
        public static int[] Hours()
        {
            var h = new int[24];
            if (Today != null) for (int i = 0; i < 144; i++) if (Today.Slots[i] >= 0) h[i / 6]++;
            return h;
        }

        public static string Fmt(int secs) { return secs >= 3600 ? (secs / 3600) + "h " + (secs % 3600 / 60).ToString("00") + "m" : (secs / 60) + "m"; }

        public static List<KeyValuePair<string, int>> Top(int n)
        {
            var l = Today == null ? new List<KeyValuePair<string, int>>() : new List<KeyValuePair<string, int>>(Today.Apps);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            return l.GetRange(0, Math.Min(n, l.Count));
        }
    }
}
