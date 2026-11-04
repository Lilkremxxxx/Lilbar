using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Lilbar
{
    // Type to search the web or open a path ("search"), or to launch an app from the Start menu ("commands").
    class LauncherPopup : OpaquePopup
    {
        class Hit { public string Title, Sub; public Bitmap Icon; public Action Run; }
        static List<KeyValuePair<string, string>> startMenu; // name, .lnk path
        readonly bool commands;
        readonly TextBox box;
        List<Hit> hits = new List<Hit>();
        int sel;

        public LauncherPopup(Rectangle anchor, bool commands) : base(anchor, S(380), S(64))
        {
            this.commands = commands;
            box = MakeBox(new Rectangle(S(46), S(18), S(318), S(28)), false);
            box.TextChanged += delegate { Refill(); };
            box.KeyDown += OnKey;
            Refill();
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); box.Focus(); }

        static List<KeyValuePair<string, string>> StartMenu()
        {
            if (startMenu != null) return startMenu;
            var list = new List<KeyValuePair<string, string>>();
            foreach (Environment.SpecialFolder f in new[] { Environment.SpecialFolder.CommonStartMenu, Environment.SpecialFolder.StartMenu })
            {
                string dir = Path.Combine(Environment.GetFolderPath(f), "Programs");
                try
                {
                    foreach (string lnk in Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileNameWithoutExtension(lnk);
                        if (name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) < 0 && !list.Exists(p => p.Key == name)) list.Add(new KeyValuePair<string, string>(name, lnk));
                    }
                }
                catch (Exception e) { Taskbar.Log(e); }
            }
            list.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.CurrentCultureIgnoreCase));
            return startMenu = list;
        }

        static void Start(string file, string args)
        {
            try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true }); }
            catch (Exception e) { Taskbar.Log(e); }
        }

        void Refill()
        {
            string q = box.Text.Trim();
            hits = new List<Hit>();
            if (commands)
            {
                var builtins = new[]
                {
                    new Hit { Title = "Cài đặt Windows", Sub = "ms-settings:", Run = () => Sys.Launch("ms-settings:") },
                    new Hit { Title = "Task Manager", Sub = "taskmgr", Run = () => Sys.Launch("taskmgr.exe") },
                    new Hit { Title = "Khoá máy", Sub = "Win+L", Run = () => Start("rundll32.exe", "user32.dll,LockWorkStation") },
                    new Hit { Title = "Cài đặt Lilbar", Sub = "Lilbar", Run = () => Manager.OpenSettings(0) },
                };
                foreach (Hit h in builtins) if (q.Length == 0 || h.Title.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0) hits.Add(h);
                if (q.Length > 0)
                {
                    var starts = new List<Hit>();
                    var contains = new List<Hit>();
                    foreach (KeyValuePair<string, string> p in StartMenu())
                    {
                        int i = p.Key.IndexOf(q, StringComparison.CurrentCultureIgnoreCase);
                        if (i < 0) continue;
                        string lnk = p.Value;
                        var h = new Hit { Title = p.Key, Sub = "Ứng dụng", Icon = Sys.IconOf(lnk), Run = () => Sys.Launch(lnk) };
                        (i == 0 ? starts : contains).Add(h);
                    }
                    hits.InsertRange(0, starts);
                    hits.AddRange(contains);
                }
            }
            else if (q.Length > 0)
            {
                string path = Environment.ExpandEnvironmentVariables(q.Trim('"'));
                if ((path.Contains(":\\") || path.StartsWith("\\\\")) && (Directory.Exists(path) || File.Exists(path)))
                    hits.Add(new Hit { Title = "Mở " + path, Sub = Directory.Exists(path) ? "Thư mục" : "Tệp", Icon = Sys.IconOf(path), Run = () => Sys.Launch(path) });
                bool url = q.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || q.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || (q.Contains(".") && !q.Contains(" ") && !q.Contains("\\"));
                if (url)
                {
                    string u = q.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? q : "https://" + q;
                    hits.Add(new Hit { Title = "Mở " + u, Sub = "Trang web", Run = () => Sys.Launch(u) });
                }
                hits.Add(new Hit { Title = "Tìm \"" + q + "\" trên web", Sub = "Google", Run = () => Sys.Launch("https://www.google.com/search?q=" + Uri.EscapeDataString(q)) });
            }
            if (hits.Count > 7) hits.RemoveRange(7, hits.Count - 7);
            sel = 0;
            Place(S(380), S(64) + hits.Count * S(40) + (hits.Count > 0 ? S(8) : 0));
            Invalidate();
        }

        void OnKey(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { sel = Math.Min(hits.Count - 1, sel + 1); Invalidate(); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { sel = Math.Max(0, sel - 1); Invalidate(); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunAt(sel); }
        }

        void RunAt(int i)
        {
            if (i < 0 || i >= hits.Count) return;
            Action run = hits[i].Run;
            Close();
            run();
        }

        Rectangle RowRect(int i) { return new Rectangle(S(8), S(62) + i * S(40), Width - S(16), S(38)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            Glass.Fill(g, new Rectangle(S(12), S(12), Width - S(24), S(40)), S(10), Color.FromArgb(255, 44, 44, 54));
            Glass.Center(g, commands ? "" : "", Glass.MdlSmall, Glass.Dim, new Rectangle(S(14), S(12), S(30), S(40)));
            if (box.Text.Length == 0)
                Glass.Text(g, commands ? "Gõ tên ứng dụng…" : "Tìm trên web hoặc dán đường dẫn…", Glass.Body, Color.FromArgb(120, 255, 255, 255), box.Bounds, StringAlignment.Near);
            for (int i = 0; i < hits.Count; i++)
            {
                Rectangle r = RowRect(i);
                if (i == sel) Glass.Fill(g, r, S(8), Color.FromArgb(255, 40, 70, 120));
                int tx = r.X + S(12);
                if (hits[i].Icon != null) { g.DrawImage(hits[i].Icon, new Rectangle(r.X + S(10), r.Y + S(9), S(20), S(20))); tx = r.X + S(40); }
                Glass.Text(g, hits[i].Title, Glass.Body, Color.White, new Rectangle(tx, r.Y, r.Width - (tx - r.X) - S(90), r.Height), StringAlignment.Near);
                Glass.Text(g, hits[i].Sub, Glass.Small, Glass.Dim, new Rectangle(r.Right - S(96), r.Y, S(86), r.Height), StringAlignment.Far);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            for (int i = 0; i < hits.Count; i++) if (RowRect(i).Contains(e.Location) && sel != i) { sel = i; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            for (int i = 0; i < hits.Count; i++) if (RowRect(i).Contains(e.Location)) { RunAt(i); return; }
        }
    }

    // A small notepad: one note at a time, browse with the arrows; saved when it closes or you switch notes.
    class NotesPopup : OpaquePopup
    {
        readonly TextBox box;
        int index;
        Rectangle prev, next, add, del;

        public NotesPopup(Rectangle anchor) : base(anchor, S(380), S(300))
        {
            List<string> notes = Config.Current.Notes;
            if (notes.Count == 0) notes.Add("");
            index = notes.Count - 1;
            box = MakeBox(new Rectangle(S(16), S(50), S(348), S(234)), true);
            box.Text = notes[index].Replace("\n", "\r\n");
            prev = new Rectangle(Width - S(150), S(10), S(30), S(30));
            next = new Rectangle(Width - S(116), S(10), S(30), S(30));
            add = new Rectangle(Width - S(78), S(10), S(30), S(30));
            del = new Rectangle(Width - S(44), S(10), S(30), S(30));
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); box.Focus(); box.SelectionStart = box.TextLength; }

        void Keep() { Config.Current.Notes[index] = box.Text.Replace("\r\n", "\n"); }

        void Show(int i)
        {
            Keep();
            index = Math.Max(0, Math.Min(Config.Current.Notes.Count - 1, i));
            box.Text = Config.Current.Notes[index].Replace("\n", "\r\n");
            Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Keep();
            Config.Current.Notes.RemoveAll(n => n.Trim().Length == 0);
            Config.Apply();
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Prepare(g);
            List<string> notes = Config.Current.Notes;
            Glass.Text(g, "Ghi chú " + (index + 1) + "/" + notes.Count, Glass.Header, Color.White, new Rectangle(S(16), S(10), S(160), S(30)), StringAlignment.Near);
            foreach (Rectangle r in new[] { prev, next, add, del }) Glass.Fill(g, r, S(7), Color.FromArgb(255, 44, 44, 54));
            Glass.Center(g, "◀", Glass.Sym, index > 0 ? Color.White : Glass.Dim, prev);
            Glass.Center(g, "▶", Glass.Sym, index < notes.Count - 1 ? Color.White : Glass.Dim, next);
            Glass.Center(g, "+", Glass.Body, Color.White, add);
            Glass.Center(g, "✕", Glass.Sym, Color.FromArgb(255, 255, 120, 110), del);
            Glass.Fill(g, new Rectangle(S(10), S(46), Width - S(20), S(244)), S(8), Color.FromArgb(255, 44, 44, 54));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            List<string> notes = Config.Current.Notes;
            if (prev.Contains(e.Location)) Show(index - 1);
            else if (next.Contains(e.Location)) Show(index + 1);
            else if (add.Contains(e.Location)) { Keep(); notes.Add(""); Show(notes.Count - 1); box.Focus(); }
            else if (del.Contains(e.Location))
            {
                notes.RemoveAt(index);
                if (notes.Count == 0) notes.Add("");
                index = Math.Min(index, notes.Count - 1);
                box.Text = notes[index].Replace("\n", "\r\n");
                Config.Apply();
                Invalidate();
            }
        }
    }
}
