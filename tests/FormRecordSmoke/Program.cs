using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using YuzuhaToolkit.PmlHost;

internal static class Program
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static JObject LastEntry(string path)
    {
        string[] lines = File.ReadAllLines(path);
        return JObject.Parse(lines[lines.Length - 1]);
    }
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) { Console.WriteLine(ex.GetType().FullName + ": " + ex.Message); Environment.Exit(1); }
    }
    private static void Run(string[] args)
    {
        FormRecordWriter writer = new FormRecordWriter();
        writer.DirectoryPath = Path.GetFullPath(args[0]);
        Hashtable array = new Hashtable();
        array[10.0] = "中文\r\n\"quoted\"";
        array[0.0] = false;
        Hashtable nested = new Hashtable(); nested[3.0] = 1.5;
        array[2.0] = nested;
        int calls = 0;
        Action<string> fake = delegate(string path) { calls++; File.WriteAllText(path, "test placeholder"); };
        Check(writer.Write(array, fake), writer.LastError);
        JObject first = LastEntry(writer.LastPath);
        Check(calls == 1 && (double)first["array"][0]["index"] == 0.0 &&
            (double)first["array"][1]["value"][0]["index"] == 3.0 &&
            (string)first["array"][2]["value"] == (string)array[10.0], "Sparse/nested/Unicode array failed.");
        string oldPath = writer.LastPath;
        Check(writer.Write(array, fake) && oldPath == writer.LastPath && File.ReadAllLines(oldPath).Length == 2, "Session append failed.");
        Check(!writer.Write(array, delegate(string p) { throw new Exception("capture failure"); }), "Failure not reported.");
        Check(File.Exists(writer.LastPath) && writer.LastError.Contains("capture failure"), "Log lost on screenshot failure.");
        Hashtable cycle = new Hashtable(); cycle[1.0] = cycle;
        Check(!writer.Write(cycle, fake) && writer.LastPath == "", "Cycle validation failed.");
        Hashtable invalid = new Hashtable(); invalid["bad"] = "x";
        Check(!writer.Write(invalid, fake), "Invalid key accepted.");
        int pairCalls = 0;
        Action<string> pairCapture = delegate(string p) { pairCalls++; File.WriteAllText(p, "pair placeholder"); };
        string pair = writer.Begin(array, pairCapture);
        Check(pair.Length > 0 && (string)LastEntry(writer.LastPath)["status"] == "awaitingAfter", "Before checkpoint failed.");
        string nestedPair = writer.Begin(array, pairCapture);
        Check(writer.Complete(array, nestedPair, pairCapture), writer.LastError);
        Check(writer.Complete(array, pair, pairCapture), writer.LastError);
        JObject pairLog = LastEntry(writer.LastPath);
        Check(pairCalls == 4 && (string)pairLog["recordId"] == pair && (string)pairLog["status"] == "completed" &&
            (string)pairLog["before"]["image"] == (string)pairLog["after"]["image"] && (bool)pairLog["after"]["reused"], "Before/After pairing failed.");
        Check(!writer.Complete(array, pair, pairCapture) && pairCalls == 4, "Duplicate completion accepted.");
        string failedPair = writer.Begin(array, delegate(string p) { throw new Exception("before unavailable"); });
        Check(!writer.Complete(array, failedPair, pairCapture) && File.Exists(writer.LastPath), "Failed Before lost log.");
        using (Form floating = new Form())
        using (Form form = new Form())
        {
            form.Text = "Yuzuha screenshot verification";
            form.BackColor = Color.Magenta;
            form.Size = new Size(320, 240);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(100, 100);
            floating.Text = "Floating verification";
            floating.BackColor = Color.Lime;
            floating.Size = new Size(200, 240);
            floating.StartPosition = FormStartPosition.Manual;
            floating.Location = new Point(460, 100);
            floating.TopMost = true;
            form.TopMost = true;
            form.Show(); form.Activate(); form.Refresh(); Application.DoEvents();
            floating.Show(form); floating.Activate(); floating.Refresh(); Application.DoEvents();
            Thread.Sleep(300);
            Check(writer.Write(array), writer.LastError);
            JObject captured = LastEntry(writer.LastPath);
            using (Bitmap image = new Bitmap(Path.Combine(Path.GetDirectoryName(writer.LastPath), (string)captured["snapshot"]["image"])))
            {
                int magenta = 0, green = 0;
                for (int y = 0; y < image.Height; y += 8)
                    for (int x = 0; x < image.Width; x += 8)
                    {
                        Color pixel = image.GetPixel(x, y);
                        if (pixel.R > 200 && pixel.B > 200 && pixel.G < 60) magenta++;
                        if (pixel.G > 200 && pixel.R < 60 && pixel.B < 60) green++;
                    }
                Check(magenta > 100 && green > 100, "Main and floating window pixels not both captured.");
                Check(image.RawFormat.Guid == System.Drawing.Imaging.ImageFormat.Jpeg.Guid, "Not JPEG.");
            }
        }
        string[] allLines = File.ReadAllLines(writer.LastPath);
        long lastSequence = 0;
        foreach (string line in allLines)
        {
            JObject entry = JObject.Parse(line);
            long current = (long)entry["sequence"];
            Check(current > lastSequence, "Non-monotonic sequence."); lastSequence = current;
        }
        Check(Directory.GetFiles(Path.GetDirectoryName(writer.LastPath), "*.json").Length == 0, "Per-event JSON files found.");
        Console.WriteLine("PASS: session JSONL, ordered checkpoints, JPEG main+floating union, deduplication, sparse/nested arrays, Unicode, unique events, capture-failure log, invalid/cyclic input, real window JPEG pixels. CLR=" + Environment.Version);
    }
}
