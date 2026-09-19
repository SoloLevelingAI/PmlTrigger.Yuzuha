using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YuzuhaToolkit.PmlHost
{
    // Shared by CLR 2 / NET35 and CLR 4 / NET48 hosts. No PML callback execution.
    internal sealed class FormRecordWriter
    {
        private readonly object gate = new object();
        private readonly string sessionId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "_" + Guid.NewGuid().ToString("N");
        private string sessionRoot;
        private long sequence;
        private string previousHash = "";
        private string previousImage = "";
        private string SessionRoot()
        {
            if (sessionRoot == null) sessionRoot = Path.Combine(Path.GetFullPath(DirectoryPath), sessionId);
            Directory.CreateDirectory(Path.Combine(sessionRoot, "images"));
            return sessionRoot;
        }
        private readonly Dictionary<string, JObject> pending = new Dictionary<string, JObject>();

        internal string Begin(Hashtable record) { return Begin(record, Capture); }
        internal bool Complete(Hashtable record, string id) { return Complete(record, id, Capture); }

        internal string Begin(Hashtable record, Action<string> capture)
        {
            lock (gate)
            {
                LastError = ""; LastPath = "";
                try
                {
                    if (pending.Count >= 256) throw new InvalidOperationException("Too many unfinished records.");
                    JArray values = ArrayValue(record, new List<Hashtable>(), 0);
                    string id = Guid.NewGuid().ToString("N");
                    string root = SessionRoot();
                    Directory.CreateDirectory(root);
                    JObject entry = new JObject();
                    entry["recordId"] = id;
                    entry["processId"] = Process.GetCurrentProcess().Id;
                    entry["status"] = "awaitingAfter";
                    entry["beforeArray"] = values;
                    entry["before"] = Snapshot(Path.Combine(root, "images/" + id + "_before.jpg"), capture);
                    string path = Path.Combine(root, "session.jsonl");
                    SaveEntry(path, entry, false);
                    entry["internalLogPath"] = path;
                    pending.Add(id, entry);
                    LastPath = path;
                    LastError = (string)entry["before"]["error"];
                    return id;
                }
                catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; return ""; }
            }
        }

        internal bool Complete(Hashtable record, string id, Action<string> capture)
        {
            lock (gate)
            {
                LastError = ""; LastPath = "";
                JObject entry;
                if (id == null || !pending.TryGetValue(id, out entry))
                { LastError = "Unknown or completed record ID."; return false; }
                try
                {
                    JArray values = ArrayValue(record, new List<Hashtable>(), 0);
                    string path = (string)entry["internalLogPath"];
                    JObject completed = (JObject)entry.DeepClone();
                    completed.Remove("internalLogPath");
                    completed["after"] = Snapshot(Path.Combine(Path.GetDirectoryName(path), "images/" + id + "_after.jpg"), capture);
                    completed["array"] = values;
                    string beforeError = (string)completed["before"]["error"];
                    string afterError = (string)completed["after"]["error"];
                    bool ok = beforeError.Length == 0 && afterError.Length == 0;
                    completed["status"] = ok ? "completed" : "completedWithScreenshotError";
                    SaveEntry(path, completed, true);
                    pending.Remove(id);
                    LastPath = path;
                    LastError = beforeError + (beforeError.Length > 0 && afterError.Length > 0 ? "; " : "") + afterError;
                    return ok;
                }
                catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; return false; }
            }
        }

        private JObject Snapshot(string path, Action<string> capture)
        {
            JObject result = new JObject();
            result["utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            try
            {
                capture(path);
                string hash;
                using (SHA256 sha = SHA256.Create())
                using (FileStream image = File.OpenRead(path))
                    hash = Convert.ToBase64String(sha.ComputeHash(image));
                string relative = "images/" + Path.GetFileName(path);
                bool reused = hash == previousHash && File.Exists(Path.Combine(SessionRoot(), previousImage));
                if (reused) { File.Delete(path); relative = previousImage; }
                else { previousHash = hash; previousImage = relative; }
                result["image"] = relative; result["error"] = ""; result["reused"] = reused;
            }
            catch (Exception ex) { result["image"] = ""; result["error"] = ex.GetType().Name + ": " + ex.Message; }
            return result;
        }

        // Append checkpoints and completions to the same file; never replace the session file.
        private void SaveEntry(string path, JObject entry, bool complete)
        {
            entry["sessionId"] = sessionId;
            entry["sequence"] = ++sequence;
            entry["phase"] = complete ? "After" : "Before";
            byte[] bytes = new UTF8Encoding(false).GetBytes(entry.ToString(Formatting.None) + "\n");
            using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                stream.Write(bytes, 0, bytes.Length);
        }
        internal string LastError = "";
        internal string LastPath = "";
        internal string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YuzuhaToolkit\\Records");

        internal bool Write(Hashtable record) { return Write(record, Capture); }

        internal bool Write(Hashtable record, Action<string> capture)
        {
            lock (gate)
            {
                LastError = "";
                LastPath = "";
                try
                {
                    // Validate/serialize before any capture or disk output.
                    JArray values = ArrayValue(record, new List<Hashtable>(), 0);
                    DateTime utc = DateTime.UtcNow;
                    string id = utc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)
                        + "_" + Guid.NewGuid().ToString("N");
                    string root = SessionRoot();
                    Directory.CreateDirectory(root);
                    string log = Path.Combine(root, "session.jsonl");
                    JObject snapshot = Snapshot(Path.Combine(root, "images/" + id + ".jpg"), capture);
                    JObject entry = new JObject();
                    entry["recordId"] = id;
                    entry["utc"] = utc.ToString("o", CultureInfo.InvariantCulture);
                    entry["processId"] = Process.GetCurrentProcess().Id;
                    entry["snapshot"] = snapshot;
                    entry["array"] = values;
                    entry["status"] = "single";
                    SaveEntry(log, entry, true);
                    LastPath = log;
                    LastError = (string)snapshot["error"];
                    return LastError.Length == 0;
                }
                catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; return false; }
            }
        }

        private static JArray ArrayValue(Hashtable array, List<Hashtable> parents, int depth)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (depth > 32 || parents.Contains(array)) throw new ArgumentException("Cyclic or excessively nested array.");
            parents.Add(array);
            try
            {
                SortedDictionary<double, object> sorted = new SortedDictionary<double, object>();
                foreach (DictionaryEntry item in array)
                {
                    if (!(item.Key is double) && !(item.Key is int))
                        throw new ArgumentException("PML ARRAY indexes must be double or integer.");
                    double key = Convert.ToDouble(item.Key, CultureInfo.InvariantCulture);
                    if (double.IsNaN(key) || double.IsInfinity(key)) throw new ArgumentException("Non-finite index.");
                    sorted.Add(key, item.Value);
                }
                JArray result = new JArray();
                foreach (KeyValuePair<double, object> item in sorted)
                {
                    JObject row = new JObject();
                    row["index"] = item.Key;
                    object value = item.Value;
                    if (value is Hashtable) row["value"] = ArrayValue((Hashtable)value, parents, depth + 1);
                    else if (value == null || value is string || value is bool || value is double || value is int)
                        row["value"] = value == null ? JValue.CreateNull() : new JValue(value);
                    else throw new ArgumentException("Unsupported PML array value: " + value.GetType().FullName);
                    result.Add(row);
                }
                return result;
            }
            finally { parents.RemoveAt(parents.Count - 1); }
        }

        private static void Capture(string path)
        {
            IntPtr previous = IntPtr.Zero;
            try
            {
                try { previous = SetThreadDpiAwarenessContext(new IntPtr(-4)); }
                catch (EntryPointNotFoundException) { }
                Rect bounds = ApplicationBounds();
                int left = Math.Max(bounds.Left, GetSystemMetrics(76));
                int top = Math.Max(bounds.Top, GetSystemMetrics(77));
                int right = Math.Min(bounds.Right, GetSystemMetrics(76) + GetSystemMetrics(78));
                int bottom = Math.Min(bounds.Bottom, GetSystemMetrics(77) + GetSystemMetrics(79));
                int width = right - left, height = bottom - top;
                if (width <= 0 || height <= 0 || (long)width * height > 100000000)
                    throw new InvalidOperationException("Invalid visible window bounds.");
                using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                        graphics.CopyFromScreen(left, top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                    using (MemoryStream encoded = new MemoryStream())
                    {
                        ImageCodecInfo codec = null;
                        foreach (ImageCodecInfo candidate in ImageCodecInfo.GetImageEncoders())
                            if (candidate.MimeType == "image/jpeg") codec = candidate;
                        if (codec == null) throw new InvalidOperationException("JPEG encoder unavailable.");
                        using (EncoderParameters options = new EncoderParameters(1))
                        {
                            options.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                            bitmap.Save(encoded, codec, options);
                        }
                        using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                            encoded.WriteTo(file);
                    }
                }
            }
            finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        }

        // Union of all visible, non-minimized top-level windows in this host process.
        // This includes floating forms, even outside the main window or on another screen.
        private static Rect ApplicationBounds()
        {
            uint own = (uint)Process.GetCurrentProcess().Id;
            bool found = false;
            Rect union = new Rect();
            Rectangle desktop = new Rectangle(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
            EnumWindowsProc visitor = delegate(IntPtr window, IntPtr unused)
            {
                uint pid; GetWindowThreadProcessId(window, out pid);
                if (pid != own || !IsWindowVisible(window) || IsIconic(window)) return true;
                int cloaked = 0;
                try { if (DwmGetWindowAttribute(window, 14, out cloaked, 4) == 0 && cloaked != 0) return true; }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
                Rect r;
                if (!GetWindowRect(window, out r)) return true;
                Rectangle visible = Rectangle.Intersect(desktop, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
                if (visible.Width <= 0 || visible.Height <= 0) return true;
                if (!found) { union.Left = visible.Left; union.Top = visible.Top; union.Right = visible.Right; union.Bottom = visible.Bottom; found = true; }
                else { union.Left = Math.Min(union.Left, visible.Left); union.Top = Math.Min(union.Top, visible.Top); union.Right = Math.Max(union.Right, visible.Right); union.Bottom = Math.Max(union.Bottom, visible.Bottom); }
                return true;
            };
            if (!EnumWindows(visitor, IntPtr.Zero) || !found) throw new InvalidOperationException("No visible AVEVA application windows.");
            return union;
        }
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    }
}
