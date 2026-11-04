using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Windows.Devices.Radios;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Lilbar
{
    // WinRT from the in-box compiler: no await bridge is referenced, so async operations are polled on a worker thread.
    static class WinRt
    {
        public static T Wait<T>(IAsyncOperation<T> op)
        {
            var sw = Stopwatch.StartNew();
            while (op.Status == AsyncStatus.Started)
            {
                if (sw.ElapsedMilliseconds > 5000) { op.Cancel(); throw new TimeoutException("WinRT call timed out"); }
                Thread.Sleep(5);
            }
            if (op.Status != AsyncStatus.Completed) throw new InvalidOperationException("WinRT call " + op.Status);
            return op.GetResults();
        }
    }

    // Slow or blocking reads (WinRT media and radios, thermal counters) run here, once a second, off the UI thread.
    static class Bg
    {
        public static void Start()
        {
            var t = new Thread(Loop) { IsBackground = true, Name = "Lilbar.Bg" };
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
        }

        static void Loop()
        {
            for (int n = 0; ; n++)
            {
                Step(Config.Has("media"), Media.Poll);
                if (n % 5 == 0) Step(Config.Has("toggles"), Radios.Poll);
                if (n % 3 == 0) Step(Config.Has("temp"), Thermal.Poll);
                Thread.Sleep(1000);
            }
        }

        static void Step(bool on, Action a)
        {
            if (!on) return;
            try { a(); }
            catch (Exception e) { Taskbar.Log(e); }
        }
    }

    // Now playing, from whatever app reports to Windows' media controls.
    static class Media
    {
        public static volatile bool Has, Playing;
        public static volatile string Title = "", Artist = "", App = "";
        public static volatile Bitmap Thumb;
        static GlobalSystemMediaTransportControlsSessionManager mgr;
        static string lastKey;

        public static void Poll()
        {
            if (mgr == null) mgr = WinRt.Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
            GlobalSystemMediaTransportControlsSession s = mgr.GetCurrentSession();
            if (s == null) { Has = false; return; }
            Playing = s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            GlobalSystemMediaTransportControlsSessionMediaProperties p = WinRt.Wait(s.TryGetMediaPropertiesAsync());
            string key = s.SourceAppUserModelId + "|" + p.Title + "|" + p.Artist;
            if (key != lastKey)
            {
                lastKey = key;
                Title = p.Title ?? "";
                Artist = p.Artist ?? "";
                App = s.SourceAppUserModelId ?? "";
                Thumb = LoadThumb(p.Thumbnail);
            }
            Has = Title.Length > 0;
        }

        static Bitmap LoadThumb(IRandomAccessStreamReference r)
        {
            if (r == null) return null;
            try
            {
                using (IRandomAccessStreamWithContentType st = WinRt.Wait(r.OpenReadAsync()))
                using (var dr = new DataReader(st))
                {
                    uint n = WinRt.Wait(dr.LoadAsync((uint)Math.Min(st.Size, 8 * 1024 * 1024)));
                    var bytes = new byte[n];
                    dr.ReadBytes(bytes);
                    using (var ms = new MemoryStream(bytes))
                    using (Image img = Image.FromStream(ms))
                    {
                        var b = new Bitmap(64, 64);
                        int side = Math.Min(img.Width, img.Height); // centre square crop
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(img, new Rectangle(0, 0, 64, 64), new Rectangle((img.Width - side) / 2, (img.Height - side) / 2, side, side), GraphicsUnit.Pixel);
                        }
                        return b;
                    }
                }
            }
            catch (Exception e) { Taskbar.Log(e); return null; }
        }

        // 0 play/pause, 1 next, 2 previous.
        public static void Command(int which)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    GlobalSystemMediaTransportControlsSession s = mgr == null ? null : mgr.GetCurrentSession();
                    if (s == null) return;
                    WinRt.Wait(which == 0 ? s.TryTogglePlayPauseAsync() : which == 1 ? s.TrySkipNextAsync() : s.TrySkipPreviousAsync());
                    Thread.Sleep(300);
                    Poll();
                }
                catch (Exception e) { Taskbar.Log(e); }
                Manager.PostRefresh();
            });
        }
    }

    // Wi-Fi and Bluetooth radios (Windows.Devices.Radios), toggled like Quick Settings does.
    static class Radios
    {
        public class R { public Radio Radio; public bool WiFi, On; public string Name; }
        public static volatile List<R> List = new List<R>();
        static bool asked;

        public static void Poll()
        {
            if (!asked) { asked = true; WinRt.Wait(Radio.RequestAccessAsync()); }
            var l = new List<R>();
            foreach (Radio r in WinRt.Wait(Radio.GetRadiosAsync()))
                if (r.Kind == RadioKind.WiFi || r.Kind == RadioKind.Bluetooth)
                    l.Add(new R { Radio = r, WiFi = r.Kind == RadioKind.WiFi, On = r.State == RadioState.On, Name = r.Name });
            l.Sort((a, b) => b.WiFi.CompareTo(a.WiFi)); // Wi-Fi first
            List = l;
        }

        public static void Toggle(R r)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { WinRt.Wait(r.Radio.SetStateAsync(r.On ? RadioState.Off : RadioState.On)); Poll(); }
                catch (Exception e) { Taskbar.Log(e); }
                Manager.PostRefresh();
            });
        }
    }

    // ACPI thermal zones (no admin needed). These are board sensors, not per-core CPU temperatures.
    static class Thermal
    {
        public static volatile int C = int.MinValue;
        static PerformanceCounter[] counters;

        public static void Poll()
        {
            if (counters == null)
            {
                try
                {
                    var cat = new PerformanceCounterCategory("Thermal Zone Information");
                    counters = Array.ConvertAll(cat.GetInstanceNames(), n => new PerformanceCounter("Thermal Zone Information", "Temperature", n, true));
                }
                catch (Exception) { counters = new PerformanceCounter[0]; }
            }
            float max = float.MinValue;
            foreach (PerformanceCounter p in counters) max = Math.Max(max, p.NextValue());
            C = counters.Length == 0 ? int.MinValue : (int)Math.Round(max - 273.15); // counters report Kelvin
        }
    }

    // Throughput of the interfaces that carry the default route, so VPN overlays aren't counted twice.
    static class Net
    {
        public static double Down, Up;
        public static readonly Queue<double> History = new Queue<double>();
        static NetworkInterface[] nics;
        static long lastRx, lastTx;
        static DateTime lastAt = DateTime.MinValue, pickedAt = DateTime.MinValue;

        public static void Tick()
        {
            DateTime now = DateTime.Now;
            if (nics == null || (now - pickedAt).TotalSeconds > 30)
            {
                nics = Array.FindAll(NetworkInterface.GetAllNetworkInterfaces(), n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && HasGateway(n));
                pickedAt = now;
                lastAt = DateTime.MinValue;
            }
            long rx = 0, tx = 0;
            foreach (NetworkInterface n in nics)
            {
                try { IPInterfaceStatistics s = n.GetIPStatistics(); rx += s.BytesReceived; tx += s.BytesSent; }
                catch (NetworkInformationException) { }
            }
            if (lastAt != DateTime.MinValue)
            {
                double dt = (now - lastAt).TotalSeconds;
                if (dt > 0) { Down = Math.Max(0, (rx - lastRx) / dt); Up = Math.Max(0, (tx - lastTx) / dt); }
                History.Enqueue(Down);
                while (History.Count > 30) History.Dequeue();
            }
            lastRx = rx; lastTx = tx; lastAt = now;
        }

        static bool HasGateway(NetworkInterface n)
        {
            try
            {
                foreach (GatewayIPAddressInformation g in n.GetIPProperties().GatewayAddresses)
                    if (g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)) return true;
            }
            catch (NetworkInformationException) { }
            return false;
        }

        public static string Fmt(double bps)
        {
            if (bps >= 1e6) return (bps / 1e6).ToString("0.0") + " MB/s";
            if (bps >= 1e3) return Math.Round(bps / 1e3) + " KB/s";
            return Math.Round(bps) + " B/s";
        }
    }

    // Default output device volume through Core Audio.
    static class Audio
    {
        public static int Level = -1;
        public static bool Muted;
        public static string Device = "";
        static IAudioEndpointVolume ep;
        static DateTime pickedAt = DateTime.MinValue;

        public static void Tick()
        {
            try
            {
                if (ep == null || (DateTime.Now - pickedAt).TotalSeconds > 10) Pick(); // follows a new default device
                float v;
                bool m;
                Marshal.ThrowExceptionForHR(ep.GetMasterVolumeLevelScalar(out v));
                Marshal.ThrowExceptionForHR(ep.GetMute(out m));
                Level = (int)Math.Round(v * 100);
                Muted = m;
            }
            catch (Exception) { ep = null; Level = -1; }
        }

        static void Pick()
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice dev;
            Marshal.ThrowExceptionForHR(en.GetDefaultAudioEndpoint(0, 1, out dev)); // eRender, eMultimedia
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            object o;
            Marshal.ThrowExceptionForHR(dev.Activate(ref iid, 23, IntPtr.Zero, out o)); // CLSCTX_ALL
            if (ep != null) Marshal.ReleaseComObject(ep);
            ep = (IAudioEndpointVolume)o;
            Device = FriendlyName(dev);
            pickedAt = DateTime.Now;
        }

        static string FriendlyName(IMMDevice dev)
        {
            try
            {
                IPropertyStore ps;
                if (dev.OpenPropertyStore(0, out ps) != 0) return "";
                var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 }; // PKEY_Device_FriendlyName
                PROPVARIANT v;
                if (ps.GetValue(ref key, out v) != 0) return "";
                string s = v.vt == 31 ? Marshal.PtrToStringUni(v.p1) : "";
                PropVariantClear(ref v);
                return s ?? "";
            }
            catch (Exception) { return ""; }
        }

        public static void Set(int pct)
        {
            if (ep == null) return;
            Guid ctx = Guid.Empty;
            ep.SetMasterVolumeLevelScalar(Math.Max(0, Math.Min(100, pct)) / 100f, ref ctx);
            if (Muted && pct > 0) ep.SetMute(false, ref ctx);
            Tick();
        }

        public static void ToggleMute()
        {
            if (ep == null) return;
            Guid ctx = Guid.Empty;
            ep.SetMute(!Muted, ref ctx);
            Tick();
        }

        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT v);
        [StructLayout(LayoutKind.Sequential, Pack = 4)] struct PROPERTYKEY { public Guid fmtid; public uint pid; }
        [StructLayout(LayoutKind.Sequential)] struct PROPVARIANT { public ushort vt, r1, r2, r3; public IntPtr p1, p2; }
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
            int OpenPropertyStore(int access, out IPropertyStore store);
        }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            int GetCount(out uint n);
            int GetAt(uint i, out PROPERTYKEY k);
            int GetValue(ref PROPERTYKEY k, out PROPVARIANT v);
        }
        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr n);
            int UnregisterControlChangeNotify(IntPtr n);
            int GetChannelCount(out uint c);
            int SetMasterVolumeLevel(float db, ref Guid ctx);
            int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
            int GetMasterVolumeLevel(out float db);
            int GetMasterVolumeLevelScalar(out float level);
            int SetChannelVolumeLevel(uint ch, float db, ref Guid ctx);
            int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
            int GetChannelVolumeLevel(uint ch, out float db);
            int GetChannelVolumeLevelScalar(uint ch, out float level);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }
    }

    // A countdown timer; its length is kept in Config.TimerMinutes.
    static class Countdown
    {
        public static bool Running;
        static DateTime endAt;
        static int left = -1;

        public static int Remaining()
        {
            if (left < 0) left = Config.Current.TimerMinutes * 60;
            return Running ? Math.Max(0, (int)Math.Ceiling((endAt - DateTime.Now).TotalSeconds)) : left;
        }

        public static void Toggle()
        {
            if (Running) { left = Remaining(); Running = false; return; }
            if (Remaining() <= 0) left = Config.Current.TimerMinutes * 60;
            endAt = DateTime.Now.AddSeconds(left);
            Running = true;
        }

        public static void Reset() { Running = false; left = Config.Current.TimerMinutes * 60; }

        public static void Adjust(int minutes)
        {
            if (Running) return;
            Config.Current.TimerMinutes = Math.Max(1, Math.Min(180, Config.Current.TimerMinutes + minutes));
            left = Config.Current.TimerMinutes * 60;
            Config.Apply();
        }

        public static void Tick()
        {
            if (Running && Remaining() == 0) { Running = false; left = 0; Manager.Notify("Hết giờ", "Hẹn giờ " + Config.Current.TimerMinutes + " phút đã xong."); }
        }
    }

    // Focus rounds and breaks: four focus rounds, a long break after the fourth.
    static class Pomodoro
    {
        public static int Phase, Round = 1; // phase 0 focus, 1 short break, 2 long break
        public static bool Running;
        static DateTime endAt;
        static int left = -1;

        static int Length(int phase) { return 60 * (phase == 0 ? Config.Current.FocusMin : phase == 1 ? Config.Current.BreakMin : Config.Current.LongBreakMin); }

        public static int Remaining()
        {
            if (left < 0) left = Length(Phase);
            return Running ? Math.Max(0, (int)Math.Ceiling((endAt - DateTime.Now).TotalSeconds)) : left;
        }

        public static void Toggle()
        {
            if (Running) { left = Remaining(); Running = false; return; }
            if (Remaining() <= 0) left = Length(Phase);
            endAt = DateTime.Now.AddSeconds(left);
            Running = true;
        }

        public static void Skip()
        {
            if (Phase == 0) Phase = Round % 4 == 0 ? 2 : 1;
            else { Round = Phase == 2 ? 1 : Round + 1; Phase = 0; }
            Running = false;
            left = Length(Phase);
        }

        public static void Reset() { Phase = 0; Round = 1; Running = false; left = Length(0); }

        public static void Tick()
        {
            if (!Running || Remaining() > 0) return;
            bool wasFocus = Phase == 0;
            Skip();
            Manager.Notify(wasFocus ? "Hết phiên tập trung" : "Hết giờ nghỉ", wasFocus ? "Nghỉ một chút nhé." : "Bắt đầu phiên tập trung " + Round + "/4.");
        }

        public static string Label() { return Phase == 0 ? "Tập trung " + Round + "/4" : Phase == 1 ? "Nghỉ ngắn" : "Nghỉ dài"; }
    }

    // Unread notifications and the contribution calendar, with a personal access token kept DPAPI-encrypted.
    static class GitHub
    {
        public static volatile string Login, Error;
        public static volatile int Unread = -1, Total, Streak;
        public static volatile int[] Days = new int[0];
        static DateTime next = DateTime.MinValue;
        static int busy;

        public static bool HasToken { get { return !string.IsNullOrEmpty(Config.Current.GitHubToken); } }
        public static void RefreshNow() { next = DateTime.MinValue; }

        public static void MaybeFetch(Action done)
        {
            if (!HasToken || DateTime.Now < next || Interlocked.Exchange(ref busy, 1) == 1) return;
            next = DateTime.Now.AddMinutes(5);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Fetch(); Error = null; }
                catch (WebException e)
                {
                    var r = e.Response as HttpWebResponse;
                    Error = r == null ? "Không có mạng" : (int)r.StatusCode == 401 ? "Token sai hoặc đã hết hạn" : "GitHub trả lỗi " + (int)r.StatusCode;
                    if (r != null) r.Close();
                }
                catch (Exception e) { Error = "Không đọc được GitHub"; Taskbar.Log(e); }
                finally { Interlocked.Exchange(ref busy, 0); done(); }
            });
        }

        static string Call(string url, string token, string body)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Lilbar";
            req.Headers["Authorization"] = "Bearer " + token;
            req.Accept = "application/vnd.github+json";
            req.Timeout = 15000;
            if (body != null)
            {
                req.Method = "POST";
                req.ContentType = "application/json";
                byte[] b = Encoding.UTF8.GetBytes(body);
                using (Stream s = req.GetRequestStream()) s.Write(b, 0, b.Length);
            }
            using (WebResponse resp = req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return r.ReadToEnd();
        }

        static void Fetch()
        {
            string token = Secret.Unprotect(Config.Current.GitHubToken);
            if (string.IsNullOrEmpty(token)) { Error = "Token không đọc được, nhập lại"; return; }
            var js = new JavaScriptSerializer();
            var list = js.DeserializeObject(Call("https://api.github.com/notifications?per_page=50", token, null)) as object[];
            Unread = list == null ? 0 : list.Length;
            ParseCalendar(Call("https://api.github.com/graphql", token,
                "{\"query\":\"query{viewer{login contributionsCollection{contributionCalendar{totalContributions weeks{contributionDays{contributionCount}}}}}}\"}"));
        }

        public static void ParseCalendar(string json)
        {
            var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            var viewer = Get(Get(d, "data"), "viewer");
            var cal = Get(Get(viewer, "contributionsCollection"), "contributionCalendar");
            if (cal == null) throw new InvalidDataException("no contribution calendar");
            Login = viewer["login"] as string;
            Total = Convert.ToInt32(cal["totalContributions"]);
            var days = new List<int>();
            foreach (object w in (object[])cal["weeks"])
                foreach (object day in (object[])((Dictionary<string, object>)w)["contributionDays"])
                    days.Add(Convert.ToInt32(((Dictionary<string, object>)day)["contributionCount"]));
            int streak = 0, i = days.Count - 1;
            if (i >= 0 && days[i] == 0) i--; // today may not have a contribution yet
            for (; i >= 0 && days[i] > 0; i--) streak++;
            Streak = streak;
            Days = days.ToArray();
        }

        static Dictionary<string, object> Get(Dictionary<string, object> d, string k)
        {
            object o;
            return d != null && d.TryGetValue(k, out o) ? o as Dictionary<string, object> : null;
        }
    }

    // DPAPI, bound to this Windows user: the token is unreadable elsewhere, even from a copied config.json.
    static class Secret
    {
        public static string Protect(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));
        }

        public static string Unprotect(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser)); }
            catch (Exception) { return null; }
        }
    }

    static class Cities
    {
        // Windows time zone id and the label shown on the tile.
        public static readonly string[][] All =
        {
            new[] { "", "Tắt" },
            new[] { "SE Asia Standard Time", "Hà Nội" }, new[] { "China Standard Time", "Bắc Kinh" }, new[] { "Tokyo Standard Time", "Tokyo" },
            new[] { "Korea Standard Time", "Seoul" }, new[] { "Singapore Standard Time", "Singapore" }, new[] { "India Standard Time", "New Delhi" },
            new[] { "Arabian Standard Time", "Dubai" }, new[] { "Russian Standard Time", "Moscow" }, new[] { "W. Europe Standard Time", "Berlin" },
            new[] { "Romance Standard Time", "Paris" }, new[] { "GMT Standard Time", "London" }, new[] { "UTC", "UTC" },
            new[] { "E. South America Standard Time", "São Paulo" }, new[] { "Eastern Standard Time", "New York" }, new[] { "Central Standard Time", "Chicago" },
            new[] { "Pacific Standard Time", "Los Angeles" }, new[] { "AUS Eastern Standard Time", "Sydney" },
        };

        public static int IndexOf(string id) { return Math.Max(0, Array.FindIndex(All, c => c[0] == (id ?? ""))); }
        public static string Label(string id) { return All[IndexOf(id)][1]; }

        public static DateTime Now(string id)
        {
            try { return TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById(id)); }
            catch (Exception) { return DateTime.UtcNow; }
        }
    }
}
