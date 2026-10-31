using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DesignPreview
{
    // Renders the FigCraft design (design.commands.json) with native WPF so the
    // design can be compared against the FigCraft raster.
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string json = null, outDir = ".";
            for (int i = 0; i < args.Length - 1; i += 2)
            {
                if (args[i] == "--json") json = args[i + 1];
                else if (args[i] == "--out") outDir = args[i + 1];
            }
            if (json == null || !File.Exists(json)) { Console.Error.WriteLine("usage: DesignPreview --json design.commands.json --out DIR"); return 1; }
            try { return Run(json, outDir); } catch (Exception ex) { File.WriteAllText(System.IO.Path.Combine(outDir, "error.log"), ex.ToString()); Console.Error.WriteLine(ex.Message); return 2; }
        }
        private static int Run(string json, string outDir)
        {
            Directory.CreateDirectory(outDir);

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
            var commands = serializer.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(json));
            var nodes = new List<Dictionary<string, object>>();
            for (int i = 0; i < commands.Count; i++)
            {
                var p = commands[i]["params"] as Dictionary<string, object>;
                if (p == null) continue;
                p["id"] = i + 2;
                var props = p.ContainsKey("props") ? p["props"] as Dictionary<string, object> : null;
                if (props != null)
                    foreach (var kv in props)
                        if (!p.ContainsKey(kv.Key)) p[kv.Key] = kv.Value;
                nodes.Add(p);
            }
            var byId = nodes.ToDictionary(n => Convert.ToInt32(n["id"]));
            var children = nodes.GroupBy(n => Int(n, "parent", 1)).ToDictionary(g => g.Key, g => g.ToList());

            var frames = nodes.Where(n => Str(n, "type") == "frame" && Int(n, "parent", 1) == 1).ToList();
            foreach (var frame in frames)
            {
                int id = Convert.ToInt32(frame["id"]);
                double w = Num(frame, "width"), h = Num(frame, "height");
                var canvas = new Canvas { Width = w, Height = h, Background = Brush(frame, "fill", "#FFFFFF"), ClipToBounds = true };
                if (children.ContainsKey(id))
                    foreach (var child in children[id]) canvas.Children.Add(Build(child, children, byId));
                canvas.Measure(new Size(w, h));
                canvas.Arrange(new Rect(0, 0, w, h));
                canvas.UpdateLayout();

                var bmp = new RenderTargetBitmap((int)Math.Ceiling(w), (int)Math.Ceiling(h), 96, 96, PixelFormats.Pbgra32);
                bmp.Render(canvas);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                string safe = string.Join("_", Str(frame, "name").Split(System.IO.Path.GetInvalidFileNameChars()));
                using (var fs = File.Create(System.IO.Path.Combine(outDir, safe + ".png"))) encoder.Save(fs);
                Console.WriteLine("rendered " + safe);
            }
            return 0;
        }

        private static FrameworkElement Build(Dictionary<string, object> n, Dictionary<int, List<Dictionary<string, object>>> children, Dictionary<int, Dictionary<string, object>> byId)
        {
            string type = Str(n, "type") ?? (n.ContainsKey("path") ? "vector" : "unknown");
            FrameworkElement element;
            switch (type)
            {
                case "frame":
                    element = new Border
                    {
                        Background = Brush(n, "fill", "#00000000"),
                        BorderBrush = Brush(n, "stroke", null),
                        BorderThickness = new Thickness(Num(n, "strokeWeight", 0)),
                        CornerRadius = Radius(n),
                        ClipToBounds = true
                    };
                    break;
                case "rectangle":
                    element = new Rectangle
                    {
                        Fill = Brush(n, "fill", "#00000000"),
                        Stroke = Brush(n, "stroke", null),
                        StrokeThickness = Num(n, "strokeWeight", 0),
                        RadiusX = Radius(n).TopLeft, RadiusY = Radius(n).TopLeft
                    };
                    break;
                case "ellipse":
                    element = new Ellipse
                    {
                        Fill = Brush(n, "fill", "#00000000"),
                        Stroke = Brush(n, "stroke", null),
                        StrokeThickness = Num(n, "strokeWeight", 0)
                    };
                    break;
                case "vector":
                    var path = Geometry.Parse(Str(n, "path") ?? "M0 0");
                    element = new System.Windows.Shapes.Path
                    {
                        Data = path,
                        Stroke = Brush(n, "stroke", "#00000000"),
                        StrokeThickness = Num(n, "strokeWeight", 1),
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        Fill = Brush(n, "fill", "#00000000")
                    };
                    break;
                case "text":
                    element = new TextBlock
                    {
                        Text = Str(n, "text") ?? "",
                        FontFamily = new FontFamily(Str(n, "fontFamily") ?? "Microsoft YaHei UI"),
                        FontSize = Num(n, "fontSize", 12),
                        FontWeight = Weight(Str(n, "fontStyle")),
                        Foreground = Brush(n, "fill", "#263248"),
                        TextAlignment = Align(Str(n, "textAlign")),
                        TextWrapping = TextWrapping.NoWrap,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                    };
                    break;
                default:
                    element = new Canvas();
                    break;
            }

            Canvas.SetLeft(element, Num(n, "x"));
            Canvas.SetTop(element, Num(n, "y"));
            if (n.ContainsKey("width")) element.Width = Num(n, "width");
            if (n.ContainsKey("height")) element.Height = Num(n, "height");
            double opacity = Num(n, "opacity", 1);
            if (opacity < 1) element.Opacity = opacity;

            if (type == "frame" && children.ContainsKey(Convert.ToInt32(n["id"])))
            {
                var inner = new Canvas { Width = Num(n, "width"), Height = Num(n, "height"), Background = Brushes.Transparent };
                foreach (var c in children[Convert.ToInt32(n["id"])]) inner.Children.Add(Build(c, children, byId));
                ((Border)element).Child = inner;
            }
            return element;
        }

        private static int Int(Dictionary<string, object> n, string key, int fallback)
        {
            object v;
            if (!n.TryGetValue(key, out v) || v == null) return fallback;
            int i;
            return int.TryParse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), out i) ? i : fallback;
        }

        private static string Str(Dictionary<string, object> n, string key)
        {
            object v; return n.TryGetValue(key, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
        }

        private static double Num(Dictionary<string, object> n, string key, double fallback = 0)
        {
            object v;
            if (!n.TryGetValue(key, out v) || v == null) return fallback;
            double d;
            return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d : fallback;
        }

        private static CornerRadius Radius(Dictionary<string, object> n)
        {
            object v;
            if (!n.TryGetValue("cornerRadius", out v) || v == null) return new CornerRadius(0);
            var list = v as object[];
            if (list != null && list.Length == 4)
                return new CornerRadius(C(list[0]), C(list[1]), C(list[2]), C(list[3]));
            return new CornerRadius(Num(n, "cornerRadius"));
        }

        private static double C(object o) { double d; return double.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d : 0; }

        private static Brush Brush(Dictionary<string, object> n, string key, string fallback)
        {
            string hex = Str(n, key) ?? fallback;
            if (string.IsNullOrEmpty(hex)) return null;
            byte a = 255, r, g, b;
            if (hex[0] == '#')
            {
                string s = hex.Substring(1);
                if (s.Length == 8) { a = Hex(s, 0); r = Hex(s, 2); g = Hex(s, 4); b = Hex(s, 6); }
                else if (s.Length == 6) { r = Hex(s, 0); g = Hex(s, 2); b = Hex(s, 4); }
                else if (s.Length == 3) { r = Hex(s.Substring(0, 1) + s.Substring(0, 1), 0); g = Hex(s.Substring(1, 1) + s.Substring(1, 1), 0); b = Hex(s.Substring(2, 1) + s.Substring(2, 1), 0); }
                else return null;
            }
            else
            {
                try { var named = (Color)ColorConverter.ConvertFromString(hex); a = named.A; r = named.R; g = named.G; b = named.B; }
                catch { return null; }
            }
            if (a == 0) return null;
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        private static byte Hex(string s, int i)
        {
            return byte.Parse(s.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static FontWeight Weight(string style)
        {
            switch ((style ?? "Regular").ToLowerInvariant())
            {
                case "bold": return FontWeights.Bold;
                case "medium": return FontWeights.Medium;
                case "semibold": return FontWeights.SemiBold;
                case "light": return FontWeights.Light;
                default: return FontWeights.Normal;
            }
        }

        private static TextAlignment Align(string a)
        {
            switch ((a ?? "left").ToLowerInvariant())
            {
                case "center": return TextAlignment.Center;
                case "right": return TextAlignment.Right;
                default: return TextAlignment.Left;
            }
        }
    }
}
