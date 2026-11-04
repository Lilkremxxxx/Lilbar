using System;
using System.Collections;
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

namespace Lilbar
{
    // Small shared HTTP + JSON helpers for the internet widgets.
    static class Web
    {
        public static string Get(string url, string ua, params string[] headers)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = ua ?? "Lilbar";
            req.Timeout = 15000;
            for (int i = 0; i + 1 < headers.Length; i += 2) SetHeader(req, headers[i], headers[i + 1]);
            using (WebResponse resp = req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return r.ReadToEnd();
        }

        public static string Post(string url, string body, string contentType, params string[] headers)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Lilbar";
            req.Timeout = 15000;
            req.Method = "POST";
            req.ContentType = contentType;
            for (int i = 0; i + 1 < headers.Length; i += 2) SetHeader(req, headers[i], headers[i + 1]);
            byte[] b = Encoding.UTF8.GetBytes(body);
            using (Stream s = req.GetRequestStream()) s.Write(b, 0, b.Length);
            using (WebResponse resp = req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return r.ReadToEnd();
        }

        static void SetHeader(HttpWebRequest req, string name, string value)
        {
            if (name == "Referer") req.Referer = value;
            else if (name == "Accept") req.Accept = value;
            else req.Headers[name] = value;
        }

        public static object Json(string s) { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.DeserializeObject(s); }

        // Path lookup through dictionaries and arrays: "chart.result.0.meta".
        public static object At(object o, string path)
        {
            foreach (string part in path.Split('.'))
            {
                if (o == null) return null;
                int i;
                var d = o as Dictionary<string, object>;
                var l = o as IList;
                if (d != null) { object v; o = d.TryGetValue(part, out v) ? v : null; }
                else if (l != null && int.TryParse(part, out i)) o = i < l.Count ? l[i] : null;
                else return null;
            }
            return o;
        }

        public static double Num(object o)
        {
            if (o == null) return double.NaN;
            double d;
            if (o is string) return double.TryParse((string)o, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : double.NaN;
            return Convert.ToDouble(o, CultureInfo.InvariantCulture);
        }

        public static int Int(object o) { double d = Num(o); return double.IsNaN(d) ? 0 : (int)d; }

        public static string Str(object o) { return o as string ?? ""; }

        // Runs fetch on the thread pool at most every `every`, reporting failures as a short message.
        public static void Poll(ref DateTime next, ref int busy, TimeSpan every, Action fetch, Action<string> error, Action done)
        {
            if (DateTime.Now < next || Interlocked.Exchange(ref busy, 1) == 1) return;
            next = DateTime.Now + every;
            int[] flag = { 0 };
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { fetch(); error(null); }
                catch (WebException e)
                {
                    var r = e.Response as HttpWebResponse;
                    error(r == null ? "Không có mạng" : (int)r.StatusCode == 401 || (int)r.StatusCode == 403 ? "Key sai hoặc thiếu quyền" : (int)r.StatusCode == 404 ? "Không tìm thấy" : "Lỗi " + (int)r.StatusCode);
                    if (r != null) r.Close();
                }
                catch (Exception e) { error("Không đọc được dữ liệu"); Taskbar.Log(e); }
                finally { done(); }
            });
        }
    }

    // Quotes from Yahoo Finance's public chart endpoint; symbols from the "stocks" setting.
    static class Stocks
    {
        public class Quote { public string Symbol, Currency; public double Price, Prev; public List<double> Line = new List<double>(); }
        public static volatile Dictionary<string, Quote> Quotes = new Dictionary<string, Quote>();
        public static volatile string Error;
        public static int Stamp;
        static DateTime next = DateTime.MinValue;
        static int busy;

        public static List<string> Symbols()
        {
            var l = new List<string>();
            foreach (string s in Acct.Get("stocks").Split(','))
            {
                string t = s.Trim().ToUpperInvariant();
                if (Regex.IsMatch(t, @"^[A-Z0-9.^=\-]{1,15}$") && !l.Contains(t)) l.Add(t); // goes into a URL
            }
            return l;
        }

        public static void MaybeFetch(Action done)
        {
            Web.Poll(ref next, ref busy, TimeSpan.FromMinutes(2), delegate
            {
                var q = new Dictionary<string, Quote>();
                foreach (string s in Symbols())
                    q[s] = Parse(s, Web.Get("https://query1.finance.yahoo.com/v8/finance/chart/" + Uri.EscapeDataString(s) + "?range=1d&interval=15m", "Mozilla/5.0"));
                Quotes = q;
                Stamp++;
            }, e => Error = e, delegate { Interlocked.Exchange(ref busy, 0); done(); });
        }

        public static Quote Parse(string symbol, string json)
        {
            object r = Web.At(Web.Json(json), "chart.result.0");
            if (r == null) throw new InvalidDataException("no chart result");
            var q = new Quote { Symbol = symbol, Price = Web.Num(Web.At(r, "meta.regularMarketPrice")), Currency = Web.Str(Web.At(r, "meta.currency")) };
            q.Prev = Web.Num(Web.At(r, "meta.chartPreviousClose"));
            if (double.IsNaN(q.Prev)) q.Prev = Web.Num(Web.At(r, "meta.previousClose"));
            var closes = Web.At(r, "indicators.quote.0.close") as IList;
            if (closes != null) foreach (object c in closes) if (c != null) q.Line.Add(Web.Num(c));
            return q;
        }

        public static double Change(Quote q) { return double.IsNaN(q.Prev) || q.Prev == 0 ? 0 : (q.Price - q.Prev) / q.Prev * 100; }
    }

    // One live number from Binance's public ticker (no key), e.g. BTCUSDT.
    static class Crypto
    {
        public static volatile string Symbol, Error;
        public static double Price = double.NaN, ChangePct;
        static DateTime next = DateTime.MinValue;
        static int busy;

        public static void MaybeFetch(Action done)
        {
            string sym = Acct.Get("metric").ToUpperInvariant();
            if (sym != Symbol) next = DateTime.MinValue;
            Web.Poll(ref next, ref busy, TimeSpan.FromMinutes(1), delegate
            {
                if (!Regex.IsMatch(sym, "^[A-Z0-9]{5,20}$")) throw new InvalidDataException("bad symbol");
                Parse(sym, Web.Get("https://api.binance.com/api/v3/ticker/24hr?symbol=" + sym, null));
            }, e => Error = e, delegate { Interlocked.Exchange(ref busy, 0); done(); });
        }

        public static void Parse(string sym, string json)
        {
            object d = Web.Json(json);
            Price = Web.Num(Web.At(d, "lastPrice"));
            ChangePct = Web.Num(Web.At(d, "priceChangePercent"));
            Symbol = sym;
        }

        public static string Pair(string sym)
        {
            foreach (string q in new[] { "USDT", "USDC", "BUSD", "FDUSD", "USD", "BTC", "ETH", "EUR" })
                if (sym.Length > q.Length && sym.EndsWith(q)) return sym.Substring(0, sym.Length - q.Length) + "/" + q;
            return sym;
        }
    }

    // Solved counts and streak from LeetCode's public GraphQL, by username.
    static class LeetCode
    {
        public static volatile string User, Error;
        public static int All = -1, Easy, Medium, Hard, Streak;
        static DateTime next = DateTime.MinValue;
        static int busy;

        public static void MaybeFetch(Action done)
        {
            string user = Acct.Get("leetcode");
            if (user.Length == 0) return;
            if (user != User) next = DateTime.MinValue;
            Web.Poll(ref next, ref busy, TimeSpan.FromMinutes(30), delegate
            {
                var body = new JavaScriptSerializer().Serialize(new Dictionary<string, object>
                {
                    { "query", "query($u:String!){matchedUser(username:$u){submitStatsGlobal{acSubmissionNum{difficulty count}} userCalendar{streak}}}" },
                    { "variables", new Dictionary<string, object> { { "u", user } } },
                });
                Parse(user, Web.Post("https://leetcode.com/graphql", body, "application/json", "Referer", "https://leetcode.com"));
            }, e => Error = e, delegate { Interlocked.Exchange(ref busy, 0); done(); });
        }

        public static void Parse(string user, string json)
        {
            object u = Web.At(Web.Json(json), "data.matchedUser");
            if (u == null) throw new InvalidDataException("user not found");
            var list = Web.At(u, "submitStatsGlobal.acSubmissionNum") as IList;
            if (list != null)
                foreach (object x in list)
                {
                    int c = Web.Int(Web.At(x, "count"));
                    switch (Web.Str(Web.At(x, "difficulty"))) { case "All": All = c; break; case "Easy": Easy = c; break; case "Medium": Medium = c; break; case "Hard": Hard = c; break; }
                }
            Streak = Web.Int(Web.At(u, "userCalendar.streak"));
            User = user;
        }
    }

    // Bing's daily images: browse the last eight and set one as the desktop wallpaper.
    static class Wallpapers
    {
        public class Pic { public string Url, Title, Id; public Bitmap Thumb; }
        public static volatile List<Pic> Pics = new List<Pic>();
        public static volatile string Error;
        static DateTime next = DateTime.MinValue;
        static int busy;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lilbar", "wallpapers"); } }

        public static void MaybeFetch(Action done)
        {
            Web.Poll(ref next, ref busy, TimeSpan.FromHours(6), delegate
            {
                List<Pic> list = Parse(Web.Get("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=8&mkt=en-US", null));
                foreach (Pic p in list)
                    try
                    {
                        using (var wc = new WebClient())
                        using (var ms = new MemoryStream(wc.DownloadData(p.Url + "&w=320&h=180")))
                        using (Image img = Image.FromStream(ms)) p.Thumb = new Bitmap(img, 160, 90);
                    }
                    catch (Exception e) { Taskbar.Log(e); }
                Pics = list;
            }, e => Error = e, delegate { Interlocked.Exchange(ref busy, 0); done(); });
        }

        public static List<Pic> Parse(string json)
        {
            var list = new List<Pic>();
            var images = Web.At(Web.Json(json), "images") as IList;
            if (images == null) return list;
            foreach (object i in images)
            {
                string u = Web.Str(Web.At(i, "url"));
                if (!u.StartsWith("/th?id=")) continue; // only Bing's own image path, made absolute on bing.com
                string id = Regex.Match(u, @"id=([A-Za-z0-9_.\-]+)").Groups[1].Value;
                list.Add(new Pic { Url = "https://www.bing.com" + u, Title = Web.Str(Web.At(i, "copyright")), Id = id });
            }
            return list;
        }

        public static void Set(Pic p)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Directory.CreateDirectory(Dir);
                    string file = Path.Combine(Dir, Regex.Replace(p.Id, @"[^A-Za-z0-9_.\-]", "_"));
                    if (!file.EndsWith(".jpg")) file += ".jpg";
                    if (!File.Exists(file)) using (var wc = new WebClient()) wc.DownloadFile(p.Url, file);
                    SystemParametersInfo(0x14, 0, file, 3); // SPI_SETDESKWALLPAPER, update ini + broadcast
                }
                catch (Exception e) { Error = "Không đặt được hình nền"; Taskbar.Log(e); }
            });
        }
    }
}
