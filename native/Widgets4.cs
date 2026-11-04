using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Lilbar
{
    class Sale { public DateTime At; public double Net; public string Cur; }

    // Revenue over the last 30 days from each store's own API, with the key the user pasted. A key only goes to its store.
    static class Revenue
    {
        public class Src
        {
            public string Id, Name, Currency = "", Error;
            public double Total30 = double.NaN, Week;
            public double[] Days14 = new double[14];
            public DateTime Next = DateTime.MinValue;
            public int Busy;
        }

        public static readonly Src[] All =
        {
            new Src { Id = "stripe", Name = "Stripe" }, new Src { Id = "polar", Name = "Polar" }, new Src { Id = "dodo", Name = "Dodo Payments" },
            new Src { Id = "shopify", Name = "Shopify" }, new Src { Id = "lemon", Name = "Lemon Squeezy" }, new Src { Id = "paddle", Name = "Paddle" },
            new Src { Id = "gumroad", Name = "Gumroad" },
        };
        public static int Stamp;
        static readonly string[] ZeroDecimal = { "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" };

        public static Src Get(string id) { return Array.Find(All, s => s.Id == id); }

        public static bool Configured(string id)
        {
            return id == "shopify" ? Acct.Has("shopify_shop") && Acct.Has("shopify_key") : Acct.Has(id + "_key");
        }

        public static void MaybeFetch(string id, Action done)
        {
            Src s = Get(id);
            if (s == null || !Configured(id)) return;
            Web.Poll(ref s.Next, ref s.Busy, TimeSpan.FromMinutes(15), delegate { Aggregate(s, Fetch(id)); Stamp++; },
                e => { s.Error = e; Stamp++; }, delegate { Interlocked.Exchange(ref s.Busy, 0); done(); });
        }

        public static double Minor(double v, string cur) { return Array.IndexOf(ZeroDecimal, (cur ?? "").ToUpperInvariant()) >= 0 ? v : v / 100; }
        static double Z(double v) { return double.IsNaN(v) ? 0 : v; }

        public static DateTime Iso(object o)
        {
            DateTimeOffset t;
            return DateTimeOffset.TryParse(Web.Str(o), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out t) ? t.LocalDateTime : DateTime.MinValue;
        }

        public static void Aggregate(Src s, List<Sale> sales)
        {
            DateTime today = DateTime.Today;
            double total = 0, week = 0;
            var days = new double[14];
            var cur = new Dictionary<string, int>();
            foreach (Sale x in sales)
            {
                int d = (int)(today - x.At.Date).TotalDays;
                if (d < 0 || d >= 30) continue;
                total += x.Net;
                if (d < 14) days[13 - d] += x.Net;
                if (d < 7) week += x.Net;
                if (!string.IsNullOrEmpty(x.Cur)) { int n; cur[x.Cur] = cur.TryGetValue(x.Cur, out n) ? n + 1 : 1; }
            }
            string best = "";
            int bn = 0;
            foreach (KeyValuePair<string, int> kv in cur) if (kv.Value > bn) { best = kv.Key; bn = kv.Value; }
            s.Total30 = total;
            s.Week = week;
            s.Days14 = days;
            s.Currency = best.ToUpperInvariant();
        }

        static string Since(int days) { return DateTime.UtcNow.Date.AddDays(-days).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"); }

        static List<Sale> Fetch(string id)
        {
            var all = new List<Sale>();
            bool more;
            string key = Acct.Get(id == "shopify" ? "shopify_key" : id + "_key");
            switch (id)
            {
                case "stripe":
                {
                    long since = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
                    string after = null;
                    for (int page = 0; page < 20; page++)
                    {
                        string json = Web.Get("https://api.stripe.com/v1/balance_transactions?limit=100&created%5Bgte%5D=" + since + (after != null ? "&starting_after=" + Uri.EscapeDataString(after) : ""), null, "Authorization", "Bearer " + key);
                        all.AddRange(ParseStripe(json, out more, out after));
                        if (!more) break;
                    }
                    break;
                }
                case "polar":
                    for (int page = 1; page <= 20; page++)
                    {
                        all.AddRange(ParsePolar(Web.Get("https://api.polar.sh/v1/orders/?limit=100&sorting=-created_at&page=" + page, null, "Authorization", "Bearer " + key), out more));
                        if (!more) break;
                    }
                    break;
                case "dodo":
                    for (int page = 0; page < 20; page++)
                    {
                        all.AddRange(ParseDodo(Web.Get("https://live.dodopayments.com/payments?page_size=100&page_number=" + page + "&created_at_gte=" + Uri.EscapeDataString(Since(30)), null, "Authorization", "Bearer " + key), 100, out more));
                        if (!more) break;
                    }
                    break;
                case "shopify":
                {
                    string shop = Acct.Get("shopify_shop").ToLowerInvariant().Replace("https://", "").TrimEnd('/');
                    if (!Regex.IsMatch(shop, @"^[a-z0-9][a-z0-9\-]*\.myshopify\.com$")) throw new InvalidDataException("shop must be <name>.myshopify.com");
                    // shortcut: one page of 250 orders; a busier store needs Link-header paging
                    all.AddRange(ParseShopify(Web.Get("https://" + shop + "/admin/api/2024-10/orders.json?status=any&limit=250&created_at_min=" + Uri.EscapeDataString(Since(30)), null, "X-Shopify-Access-Token", key)));
                    break;
                }
                case "lemon":
                    for (int page = 1; page <= 20; page++)
                    {
                        all.AddRange(ParseLemon(Web.Get("https://api.lemonsqueezy.com/v1/orders?page%5Bsize%5D=100&page%5Bnumber%5D=" + page, null, "Authorization", "Bearer " + key, "Accept", "application/vnd.api+json"), out more));
                        if (!more) break;
                    }
                    break;
                case "paddle":
                {
                    string url = "https://api.paddle.com/transactions?status=completed&per_page=200&created_at%5BGTE%5D=" + Uri.EscapeDataString(Since(30));
                    for (int page = 0; page < 20 && url != null; page++)
                    {
                        string next;
                        all.AddRange(ParsePaddle(Web.Get(url, null, "Authorization", "Bearer " + key), out next));
                        url = next != null && next.StartsWith("https://api.paddle.com/") ? next : null; // only follow paging links on Paddle's own host
                    }
                    break;
                }
                case "gumroad":
                {
                    string pageKey = null;
                    for (int page = 0; page < 20; page++)
                    {
                        string json = Web.Get("https://api.gumroad.com/v2/sales?after=" + DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd") + (pageKey != null ? "&page_key=" + Uri.EscapeDataString(pageKey) : ""), null, "Authorization", "Bearer " + key);
                        all.AddRange(ParseGumroad(json, out pageKey));
                        if (pageKey == null) break;
                    }
                    break;
                }
            }
            return all;
        }

        public static List<Sale> ParseStripe(string json, out bool more, out string lastId)
        {
            object d = Web.Json(json);
            var list = new List<Sale>();
            lastId = null;
            var data = Web.At(d, "data") as IList;
            if (data != null)
                foreach (object t in data)
                {
                    lastId = Web.Str(Web.At(t, "id"));
                    string type = Web.Str(Web.At(t, "type"));
                    if (type.StartsWith("payout") || type.StartsWith("transfer") || type.StartsWith("topup")) continue; // money moving out, not revenue
                    string cur = Web.Str(Web.At(t, "currency")).ToUpperInvariant();
                    list.Add(new Sale { At = DateTimeOffset.FromUnixTimeSeconds((long)Web.Num(Web.At(t, "created"))).LocalDateTime, Net = Minor(Web.Num(Web.At(t, "net")), cur), Cur = cur });
                }
            more = Web.At(d, "has_more") is bool && (bool)Web.At(d, "has_more");
            return list;
        }

        public static List<Sale> ParsePolar(string json, out bool more)
        {
            object d = Web.Json(json);
            var list = new List<Sale>();
            var items = Web.At(d, "items") as IList;
            if (items != null)
                foreach (object o in items)
                {
                    string cur = Web.Str(Web.At(o, "currency")).ToUpperInvariant();
                    double net = Web.Num(Web.At(o, "net_amount"));
                    if (double.IsNaN(net)) net = Web.Num(Web.At(o, "amount"));
                    list.Add(new Sale { At = Iso(Web.At(o, "created_at")), Net = Minor(Z(net) - Z(Web.Num(Web.At(o, "refunded_amount"))), cur), Cur = cur });
                }
            more = Web.Int(Web.At(d, "pagination.max_page")) > Web.Int(Web.At(d, "pagination.page")) && list.Count > 0 && list[list.Count - 1].At > DateTime.Now.AddDays(-30);
            return list;
        }

        public static List<Sale> ParseDodo(string json, int pageSize, out bool more)
        {
            var list = new List<Sale>();
            var items = Web.At(Web.Json(json), "items") as IList;
            if (items != null)
                foreach (object o in items)
                {
                    if (Web.Str(Web.At(o, "status")) != "succeeded") continue;
                    string cur = Web.Str(Web.At(o, "currency")).ToUpperInvariant();
                    list.Add(new Sale { At = Iso(Web.At(o, "created_at")), Net = Minor(Z(Web.Num(Web.At(o, "total_amount"))) - Z(Web.Num(Web.At(o, "tax"))), cur), Cur = cur });
                }
            more = items != null && items.Count >= pageSize;
            return list;
        }

        public static List<Sale> ParseShopify(string json)
        {
            var list = new List<Sale>();
            var orders = Web.At(Web.Json(json), "orders") as IList;
            if (orders != null)
                foreach (object o in orders)
                {
                    if (Web.At(o, "cancelled_at") != null) continue;
                    double total = Web.Num(Web.At(o, "current_total_price"));
                    if (double.IsNaN(total)) total = Web.Num(Web.At(o, "total_price"));
                    double tax = Web.Num(Web.At(o, "current_total_tax"));
                    if (double.IsNaN(tax)) tax = Web.Num(Web.At(o, "total_tax"));
                    list.Add(new Sale { At = Iso(Web.At(o, "created_at")), Net = Z(total) - Z(tax), Cur = Web.Str(Web.At(o, "currency")).ToUpperInvariant() }); // Shopify prices are major units
                }
            return list;
        }

        public static List<Sale> ParseLemon(string json, out bool more)
        {
            object d = Web.Json(json);
            var list = new List<Sale>();
            var data = Web.At(d, "data") as IList;
            if (data != null)
                foreach (object o in data)
                {
                    object a = Web.At(o, "attributes");
                    string cur = Web.Str(Web.At(a, "currency")).ToUpperInvariant();
                    double net = Z(Web.Num(Web.At(a, "total"))) - Z(Web.Num(Web.At(a, "tax"))) - Z(Web.Num(Web.At(a, "refunded_amount")));
                    list.Add(new Sale { At = Iso(Web.At(a, "created_at")), Net = Minor(net, cur), Cur = cur });
                }
            more = Web.Int(Web.At(d, "meta.page.currentPage")) < Web.Int(Web.At(d, "meta.page.lastPage")) && list.Count > 0 && list[list.Count - 1].At > DateTime.Now.AddDays(-30);
            return list;
        }

        public static List<Sale> ParsePaddle(string json, out string next)
        {
            object d = Web.Json(json);
            var list = new List<Sale>();
            var data = Web.At(d, "data") as IList;
            if (data != null)
                foreach (object o in data)
                {
                    string cur = Web.Str(Web.At(o, "currency_code")).ToUpperInvariant();
                    double earn = Web.Num(Web.At(o, "details.totals.earnings"));
                    if (double.IsNaN(earn)) earn = Z(Web.Num(Web.At(o, "details.totals.grand_total"))) - Z(Web.Num(Web.At(o, "details.totals.tax")));
                    list.Add(new Sale { At = Iso(Web.At(o, "created_at")), Net = Minor(earn, cur), Cur = cur });
                }
            object hm = Web.At(d, "meta.pagination.has_more");
            next = hm is bool && (bool)hm ? Web.Str(Web.At(d, "meta.pagination.next")) : null;
            return list;
        }

        public static List<Sale> ParseGumroad(string json, out string nextKey)
        {
            object d = Web.Json(json);
            var list = new List<Sale>();
            var sales = Web.At(d, "sales") as IList;
            if (sales != null)
                foreach (object o in sales)
                {
                    object refunded = Web.At(o, "refunded");
                    if (refunded is bool && (bool)refunded) continue;
                    string cur = Web.Str(Web.At(o, "currency")).ToUpperInvariant();
                    if (cur.Length == 0) cur = "USD";
                    list.Add(new Sale { At = Iso(Web.At(o, "created_at")), Net = Minor(Z(Web.Num(Web.At(o, "price"))) - Z(Web.Num(Web.At(o, "gumroad_fee"))), cur), Cur = cur });
                }
            string k = Web.Str(Web.At(d, "next_page_key"));
            nextKey = k.Length > 0 ? k : null;
            return list;
        }

        // Stores in the most common currency are added up; others are listed in the tooltip only.
        public static double Total(out string currency, out string other)
        {
            var sums = new Dictionary<string, double>();
            foreach (Src s in All)
                if (Configured(s.Id) && !double.IsNaN(s.Total30) && s.Currency.Length > 0)
                {
                    double v;
                    sums[s.Currency] = (sums.TryGetValue(s.Currency, out v) ? v : 0) + s.Total30;
                }
            currency = "";
            double best = double.NaN;
            var rest = new StringBuilder();
            foreach (KeyValuePair<string, double> kv in sums)
                if (double.IsNaN(best) || kv.Value > best) { if (!double.IsNaN(best)) rest.Append(Money(best, currency)).Append("; "); best = kv.Value; currency = kv.Key; }
                else rest.Append(Money(kv.Value, kv.Key)).Append("; ");
            other = rest.ToString().TrimEnd(';', ' ');
            return best;
        }

        public static string Money(double v, string cur)
        {
            if (double.IsNaN(v)) return "…";
            string n = Math.Abs(v) >= 100000 ? (v / 1000).ToString("#,0", CultureInfo.InvariantCulture) + "k" : v.ToString(Math.Abs(v) >= 100 ? "#,0" : "0.00", CultureInfo.InvariantCulture);
            return n + " " + cur;
        }
    }

    // One-number services: analytics, developer tools, social profiles, mail, AI balances.
    class Stat { public volatile string Value, Sub, Error; public DateTime Next = DateTime.MinValue; public int Busy; }

    static class Svc
    {
        static readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
        public static int Stamp;
        public static readonly string[] Ids = { "plausible", "dub", "resend", "linear", "supabase", "youtube", "x", "instagram", "tiktok", "gmail", "deepseek" };

        public static Stat Get(string id)
        {
            lock (stats)
            {
                Stat s;
                if (!stats.TryGetValue(id, out s)) stats[id] = s = new Stat();
                return s;
            }
        }

        // What the user still has to fill in, or null when the tile can fetch.
        public static string Missing(string id)
        {
            switch (id)
            {
                case "plausible": return Acct.Has("plausible_key") && Acct.Has("plausible_site") ? null : "Thêm key và tên miền";
                case "supabase": return Acct.Has("supabase_key") && Acct.Has("supabase_ref") ? null : "Thêm token và mã dự án";
                case "youtube": return Acct.Has("youtube_key") && Acct.Has("youtube_channel") ? null : "Thêm key và kênh";
                case "x": return Acct.Has("x_key") && Acct.Has("x_user") ? null : "Thêm token và tên";
                case "tiktok": return Acct.Has("tiktok_user") ? null : "Thêm tên người dùng";
                case "gmail": return Acct.Has("gmail_user") && Acct.Has("gmail_pass") ? null : "Thêm email và mật khẩu ứng dụng";
                default: return Acct.Has(id + "_key") ? null : "Thêm key";
            }
        }

        public static void MaybeFetch(string id, Action done)
        {
            if (Missing(id) != null) return;
            Stat s = Get(id);
            Web.Poll(ref s.Next, ref s.Busy, TimeSpan.FromMinutes(id == "gmail" ? 3 : 15), delegate { Fetch(id, s); Stamp++; },
                e => { s.Error = e; Stamp++; }, delegate { Interlocked.Exchange(ref s.Busy, 0); done(); });
        }

        public static string Compact(double n)
        {
            if (double.IsNaN(n)) return "…";
            if (n >= 1e6) return (n / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            if (n >= 1e4) return (n / 1e3).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            return n.ToString("#,0", CultureInfo.InvariantCulture);
        }

        static string Bearer(string key) { return "Bearer " + key; }

        static void Fetch(string id, Stat s)
        {
            string key = Acct.Get(id + "_key");
            switch (id)
            {
                case "plausible":
                {
                    string host = Acct.Get("plausible_host").TrimEnd('/');
                    if (!Regex.IsMatch(host, @"^https://[A-Za-z0-9.\-]+(:\d+)?$")) throw new InvalidDataException("bad plausible host");
                    ParsePlausible(Web.Get(host + "/api/v1/stats/aggregate?site_id=" + Uri.EscapeDataString(Acct.Get("plausible_site")) + "&period=7d&metrics=visitors&compare=previous_period", null, "Authorization", Bearer(key)), s);
                    break;
                }
                case "dub": ParseDub(Web.Get("https://api.dub.co/analytics?event=clicks&groupBy=count&interval=7d", null, "Authorization", Bearer(key)), s); break;
                case "resend": ParseResend(Web.Get("https://api.resend.com/emails?limit=100", null, "Authorization", Bearer(key)), s); break;
                case "linear":
                    ParseLinear(Web.Post("https://api.linear.app/graphql",
                        new JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "query", "query{viewer{assignedIssues(first:100,filter:{state:{type:{nin:[\"completed\",\"canceled\"]}}}){nodes{state{type}}}}}" } }),
                        "application/json", "Authorization", key), s); // Linear personal keys are sent as-is, without "Bearer"
                    break;
                case "supabase":
                {
                    string r = Acct.Get("supabase_ref");
                    if (!Regex.IsMatch(r, "^[a-z0-9]{20}$")) throw new InvalidDataException("bad project ref");
                    ParseSupabase(Web.Get("https://api.supabase.com/v1/projects/" + r + "/health?services=auth&services=db&services=rest&services=realtime&services=storage", null, "Authorization", Bearer(key)), s);
                    break;
                }
                case "youtube":
                {
                    string ch = Acct.Get("youtube_channel");
                    string sel = ch.StartsWith("@") ? "forHandle=" + Uri.EscapeDataString(ch) : "id=" + Uri.EscapeDataString(ch);
                    ParseYouTube(Web.Get("https://www.googleapis.com/youtube/v3/channels?part=statistics,snippet&" + sel + "&key=" + Uri.EscapeDataString(key), null), s);
                    break;
                }
                case "x":
                {
                    string u = Acct.Get("x_user").TrimStart('@');
                    if (!Regex.IsMatch(u, "^[A-Za-z0-9_]{1,15}$")) throw new InvalidDataException("bad X username");
                    ParseX(Web.Get("https://api.x.com/2/users/by/username/" + u + "?user.fields=public_metrics", null, "Authorization", Bearer(key)), s);
                    break;
                }
                case "instagram": ParseInstagram(Web.Get("https://graph.instagram.com/me?fields=username,followers_count&access_token=" + Uri.EscapeDataString(key), null), s); break;
                case "tiktok":
                {
                    string u = Acct.Get("tiktok_user").TrimStart('@');
                    if (!Regex.IsMatch(u, @"^[A-Za-z0-9_.]{2,24}$")) throw new InvalidDataException("bad TikTok username");
                    // shortcut: reads the public profile page; TikTok can change or block this at any time
                    ParseTikTok(Web.Get("https://www.tiktok.com/@" + u, "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"), u, s);
                    break;
                }
                case "gmail":
                {
                    string user = Acct.Get("gmail_user");
                    int n = Imap.Unseen("imap.gmail.com", user, Acct.Get("gmail_pass").Replace(" ", ""));
                    s.Value = n.ToString();
                    s.Sub = user;
                    break;
                }
                case "deepseek": ParseDeepSeek(Web.Get("https://api.deepseek.com/user/balance", null, "Authorization", Bearer(key)), s); break;
            }
        }

        public static void ParsePlausible(string json, Stat s)
        {
            object v = Web.At(Web.Json(json), "results.visitors");
            double n = Web.Num(Web.At(v, "value")), ch = Web.Num(Web.At(v, "change"));
            s.Value = Compact(n);
            s.Sub = "7 ngày" + (double.IsNaN(ch) ? "" : ", " + (ch >= 0 ? "+" : "") + ch.ToString("0", CultureInfo.InvariantCulture) + "%");
        }

        public static void ParseDub(string json, Stat s)
        {
            object d = Web.Json(json);
            double n = Web.Num(Web.At(d, "clicks"));
            if (double.IsNaN(n) && d is IList && ((IList)d).Count > 0) n = Web.Num(Web.At(((IList)d)[0], "clicks"));
            s.Value = Compact(n);
            s.Sub = "lượt bấm, 7 ngày";
        }

        public static void ParseResend(string json, Stat s)
        {
            var data = Web.At(Web.Json(json), "data") as IList;
            int sent = 0, delivered = 0;
            if (data != null)
                foreach (object e in data)
                {
                    if (Revenue.Iso(Web.At(e, "created_at")) < DateTime.Now.AddDays(-7)) continue;
                    sent++;
                    if (Web.Str(Web.At(e, "last_event")) == "delivered") delivered++;
                }
            s.Value = sent.ToString();
            s.Sub = "email 7 ngày, " + (sent == 0 ? "–" : (100 * delivered / sent) + "%") + " đã giao";
        }

        public static void ParseLinear(string json, Stat s)
        {
            var nodes = Web.At(Web.Json(json), "data.viewer.assignedIssues.nodes") as IList;
            if (nodes == null) throw new InvalidDataException("no issues");
            int started = 0;
            foreach (object n in nodes) if (Web.Str(Web.At(n, "state.type")) == "started") started++;
            s.Value = nodes.Count.ToString();
            s.Sub = "việc được giao, " + started + " đang làm";
        }

        public static void ParseSupabase(string json, Stat s)
        {
            var list = Web.Json(json) as IList;
            if (list == null) throw new InvalidDataException("no health list");
            int ok = 0;
            var bad = new List<string>();
            foreach (object x in list) { object h = Web.At(x, "healthy"); if (h is bool && (bool)h) ok++; else bad.Add(Web.Str(Web.At(x, "name"))); }
            s.Value = ok + "/" + list.Count;
            s.Sub = bad.Count == 0 ? "dịch vụ ổn định" : "lỗi: " + string.Join(", ", bad.ToArray());
        }

        public static void ParseYouTube(string json, Stat s)
        {
            object c = Web.At(Web.Json(json), "items.0");
            if (c == null) throw new InvalidDataException("channel not found");
            s.Value = Compact(Web.Num(Web.At(c, "statistics.subscriberCount")));
            s.Sub = Web.Str(Web.At(c, "snippet.title"));
        }

        public static void ParseX(string json, Stat s)
        {
            object u = Web.At(Web.Json(json), "data");
            if (u == null) throw new InvalidDataException("user not found");
            s.Value = Compact(Web.Num(Web.At(u, "public_metrics.followers_count")));
            s.Sub = "@" + Web.Str(Web.At(u, "username"));
        }

        public static void ParseInstagram(string json, Stat s)
        {
            object d = Web.Json(json);
            s.Value = Compact(Web.Num(Web.At(d, "followers_count")));
            s.Sub = "@" + Web.Str(Web.At(d, "username"));
        }

        public static void ParseTikTok(string html, string user, Stat s)
        {
            Match m = Regex.Match(html, "\"followerCount\":(\\d+)");
            if (!m.Success) throw new InvalidDataException("no follower count on page");
            s.Value = Compact(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            s.Sub = "@" + user;
        }

        public static void ParseDeepSeek(string json, Stat s)
        {
            object b = Web.At(Web.Json(json), "balance_infos.0");
            if (b == null) throw new InvalidDataException("no balance");
            string cur = Web.Str(Web.At(b, "currency"));
            s.Value = (cur == "CNY" ? "¥" : cur == "USD" ? "$" : cur + " ") + Web.Num(Web.At(b, "total_balance")).ToString("0.00", CultureInfo.InvariantCulture);
            s.Sub = "số dư";
        }
    }

    // Unread count over IMAP with a Gmail app password. Only talks to the given host, over TLS.
    static class Imap
    {
        public static int Unseen(string host, string user, string pass)
        {
            using (var tcp = new TcpClient())
            {
                IAsyncResult ar = tcp.BeginConnect(host, 993, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(15000)) throw new TimeoutException("IMAP connect timed out");
                tcp.EndConnect(ar);
                using (var ssl = new SslStream(tcp.GetStream()))
                {
                    ssl.ReadTimeout = ssl.WriteTimeout = 15000;
                    ssl.AuthenticateAsClient(host);
                    var r = new StreamReader(ssl, Encoding.ASCII);
                    var w = new StreamWriter(ssl, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                    r.ReadLine(); // greeting
                    List<string> login = Cmd(r, w, "a1", "LOGIN " + Quote(user) + " " + Quote(pass));
                    if (!login[login.Count - 1].StartsWith("a1 OK")) throw new UnauthorizedAccessException("IMAP login failed");
                    int n = ParseStatus(Cmd(r, w, "a2", "STATUS INBOX (UNSEEN)"));
                    Cmd(r, w, "a3", "LOGOUT");
                    return n;
                }
            }
        }

        static string Quote(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }

        static List<string> Cmd(StreamReader r, StreamWriter w, string tag, string cmd)
        {
            w.WriteLine(tag + " " + cmd);
            var lines = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                string line = r.ReadLine();
                if (line == null) break;
                lines.Add(line);
                if (line.StartsWith(tag + " ")) break;
            }
            return lines;
        }

        public static int ParseStatus(List<string> lines)
        {
            foreach (string l in lines)
            {
                Match m = Regex.Match(l, @"^\* STATUS .*\(.*UNSEEN (\d+)", RegexOptions.IgnoreCase);
                if (m.Success) return int.Parse(m.Groups[1].Value);
            }
            throw new InvalidDataException("no STATUS reply");
        }
    }
}
