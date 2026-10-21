#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Pml
{
    /// <summary>只读原生窗体查询；名称是单个标识符，不接受 PML 表达式。</summary>
    public sealed class PmlFormQueryRequest
    {
        /// <summary>forms、shown-forms、gadgets 或 gadget。</summary>
        public string Kind { get; set; }
        /// <summary>已加载窗体名称，不带 !!；列表窗体操作为空。</summary>
        public string FormName { get; set; }
        /// <summary>指定控件名称，不带点号；只用于 gadget 操作。</summary>
        public string GadgetName { get; set; }
        /// <summary>原生数组起始索引，从 1 开始。</summary>
        public int StartIndex { get; set; }
        /// <summary>最大扫描槽位数，1 到 1000；普通成员也占扫描预算。</summary>
        public int MaxItems { get; set; }
        /// <summary>返回字符串 UTF-16 总预算，1 到 262144，不含 JSON 包装。</summary>
        public int MaxCharacters { get; set; }
    }

    /// <summary>原生元数据读取结果；无法读取与空字符串明确区分。</summary>
    public sealed class PmlGadgetProperty
    {
        /// <summary>固定元数据属性名称。</summary>
        public string Name { get; set; }
        /// <summary>available 或 unavailable；失败不推断为 false。</summary>
        public string Status { get; set; }
        /// <summary>原生字符串值，未读取成功时为空引用。</summary>
        public string Value { get; set; }
        /// <summary>原生读取失败的原因；成功时为空。</summary>
        public string Error { get; set; }
    }

    /// <summary>已有窗体或控件的元数据，不包含控件模型数据或回调执行结果。</summary>
    public sealed class PmlFormQueryItem
    {
        /// <summary>在原生窗体/属性数组中的索引。</summary>
        public int Index { get; set; }
        /// <summary>原生名称；窗体名称去掉前导 !!。</summary>
        public string Name { get; set; }
        /// <summary>可供人工定位的原生引用文本。</summary>
        public string Reference { get; set; }
        /// <summary>FORM 或原生 gadget.Type() 返回的控件类型。</summary>
        public string Type { get; set; }
        /// <summary>指定控件详情的固定元数据；清单项为空数组。</summary>
        public PmlGadgetProperty[] Properties { get; set; }
    }

    /// <summary>单次查询的有界窗口；跨请求会重新查询原生状态，不保证快照连续性。</summary>
    public sealed class PmlFormQueryResult
    {
        /// <summary>实际执行的固定查询种类。</summary>
        public string Kind { get; set; }
        /// <summary>原生数组 MAXINDEX 上界，不代表控件数量。</summary>
        public int IndexUpperBound { get; set; }
        /// <summary>本次起始原生索引。</summary>
        public int StartIndex { get; set; }
        /// <summary>本次已检查的原生槽位数，包括被过滤的普通成员。</summary>
        public int Scanned { get; set; }
        /// <summary>下一个未返回/未扫描的索引；0 表示末端。</summary>
        public int NextIndex { get; set; }
        /// <summary>是否仍有数据需要继续读取。</summary>
        public bool Truncated { get; set; }
        /// <summary>返回项的所有字符串字段 UTF-16 长度总和。</summary>
        public int Characters { get; set; }
        /// <summary>本窗口确认的窗体或 GADGET；不包含 MENU 或普通成员。</summary>
        public PmlFormQueryItem[] Items { get; set; }
    }

    /// <summary>Host 命令适配点；仅在已核验的 AVEVA 主线程使用。</summary>
    public interface IPmlFormQueryRuntime
    {
        /// <summary>执行查询生成的固定语法或请求专有临时变量赋值/清理；失败必须抛出。</summary>
        void Execute(string command);
        /// <summary>读取刚刚赋值的字符串临时变量。</summary>
        string ReadString(string variable);
        /// <summary>读取刚刚赋值的实数临时变量。</summary>
        double ReadReal(string variable);
    }

    /// <summary>复用 PMLFORMS 的 FMSYS/attributes/attribute/objectType 读取路径，不管理窗体生命周期。</summary>
    public static class PmlFormQuery
    {
        /// <summary>在本机与 Host 两端检查操作、名称和预算，不接受任意成员路径。</summary>
        public static void Validate(PmlFormQueryRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            PmlArrayWindow.Validate(request.StartIndex, request.MaxItems, request.MaxCharacters);
            if (request.Kind != "forms" && request.Kind != "shown-forms" && request.Kind != "gadgets" && request.Kind != "gadget")
                throw new ArgumentException("Unknown native form query kind.");
            if (request.Kind == "forms" || request.Kind == "shown-forms")
            {
                if (request.FormName != null || request.GadgetName != null) throw new ArgumentException("Form lists do not accept names.");
            }
            else
            {
                ValidateName(request.FormName);
                if (request.Kind == "gadget")
                {
                    ValidateName(request.GadgetName);
                    if (request.StartIndex != 1) throw new ArgumentException("A single gadget query starts at index 1.");
                }
                else if (request.GadgetName != null) throw new ArgumentException("Gadget lists do not accept a gadget name.");
            }
        }

        /// <summary>拒绝把只读查询与原始命令、数组读取、删除或结果全局变量混用。</summary>
        public static void ValidateEnvelope(bool readArray, bool returnList, string command, string globalVar, bool deleteGlobalVar)
        {
            if (readArray || returnList || command != null || globalVar != null || deleteGlobalVar)
                throw new ArgumentException("FormQuery cannot be combined with commands, array operations or deletion.");
        }

        /// <summary>查询原生对象并限制扫描与返回体积；所有临时变量仅属于本次请求，结束时尝试逐个清理。</summary>
        public static PmlFormQueryResult Read(PmlFormQueryRequest request, IPmlFormQueryRuntime runtime)
        {
            Validate(request);
            if (runtime == null) throw new ArgumentNullException("runtime");
            return new Reader(request, runtime).Read();
        }

        private static void ValidateName(string name)
        {
            if (String.IsNullOrEmpty(name) || name.Length > 64 || !Regex.IsMatch(name, @"\A[A-Za-z][A-Za-z0-9_]*\z"))
                throw new ArgumentException("A plain form/gadget name of 1..64 ASCII letters, digits or underscore, starting with a letter, is required.");
        }

        /// <summary>单次请求的原生数组引用、标量临时变量与预算状态。</summary>
        private sealed class Reader
        {
            private readonly PmlFormQueryRequest request;
            private readonly IPmlFormQueryRuntime runtime;
            private readonly string snapshot;
            private readonly string value;
            private readonly string size;
            private readonly string exists;

            /// <summary>生成请求专有变量名，不读取或修改原生对象。</summary>
            internal Reader(PmlFormQueryRequest query, IPmlFormQueryRuntime commands)
            {
                request = query; runtime = commands;
                string suffix = Guid.NewGuid().ToString("N").ToUpperInvariant();
                snapshot = "YUZUHAUISNAPSHOT" + suffix;
                value = "YUZUHAUIVALUE" + suffix;
                size = "YUZUHAUISIZE" + suffix;
                exists = "YUZUHAUIEXISTS" + suffix;
            }

            /// <summary>执行查询并尝试清理各类型临时变量，保留主要错误。</summary>
            internal PmlFormQueryResult Read()
            {
                Exception failure = null;
                try { return ReadCore(); }
                catch (Exception error) { failure = error; throw; }
                finally
                {
                    Exception cleanup = null;
                    try { runtime.Execute("!!" + value + ".delete()"); } catch (Exception error) { cleanup = error; }
                    try { runtime.Execute("!!" + size + ".delete()"); } catch (Exception error) { if (cleanup == null) cleanup = error; }
                    try { runtime.Execute("!!" + exists + ".delete()"); } catch (Exception error) { if (cleanup == null) cleanup = error; }
                    try { runtime.Execute("!!" + snapshot + ".delete()"); } catch (Exception error) { cleanup = error; }
                    // A failed cleanup must not turn an already failed query into apparent success.
                    if (cleanup != null && failure == null) throw new InvalidOperationException("Native form query temporary cleanup failed.", cleanup);
                }
            }

            private PmlFormQueryResult ReadCore()
            {
                bool forms = request.Kind == "forms" || request.Kind == "shown-forms";
                string form = "!!" + request.FormName;
                string expression = forms ? (request.Kind == "forms" ? "!!FMSYS.FORMS()" : "!!FMSYS.SHOWNFORMS()")
                    : request.Kind == "gadgets" ? form + ".attributes()" : form + ".attribute('" + request.GadgetName + "')";
                // Native variables keep their scalar type; getters use canonical uppercase names.
                // Initialize every temporary so cleanup does not depend on a failed assignment's result.
                runtime.Execute("!!" + value + " = ''");
                runtime.Execute("!!" + size + " = 0");
                runtime.Execute("!!" + exists + " = FALSE");
                runtime.Execute("!!" + snapshot + " = ARRAY()");
                if (!forms && !String.Equals(Text(form + ".objectType()"), "FORM", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("PML_UI_NOT_FORM: The requested global is not an already loaded FORM.");
                // Keep the gadget reference inside an ARRAY, so cleanup never calls delete() on a native gadget/form object.
                runtime.Execute("!!" + snapshot + (request.Kind == "gadget" ? "[1]" : "") + " = " + expression);
                int upper = request.Kind == "gadget" ? 1 : UpperBound();
                var items = new List<PmlFormQueryItem>();
                int characters = 0, scanned = 0;
                long index = request.StartIndex;
                for (; index <= upper && scanned < request.MaxItems; index++)
                {
                    scanned++;
                    string slot = "!!" + snapshot + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    runtime.Execute("!!" + exists + " = DEFINED(" + slot + ")");
                    if (!String.Equals(Text("!!" + exists + ".string()"), "TRUE", StringComparison.OrdinalIgnoreCase)) continue;
                    PmlFormQueryItem item;
                    if (forms)
                    {
                        string name;
                        try { name = Text(slot + ".name()"); }
                        catch (InvalidOperationException) { name = Text(slot + ".string()"); }
                        if (name.StartsWith("!!", StringComparison.Ordinal)) name = name.Substring(2);
                        item = Item((int)index, name, "!!" + name, "FORM");
                    }
                    else
                    {
                        string name = request.Kind == "gadget" ? request.GadgetName : Text(slot);
                        string gadget = request.Kind == "gadget" ? slot : form + ".attribute(" + slot + ")";
                        string objectType = Text(gadget + ".objectType()");
                        if (!String.Equals(objectType, "GADGET", StringComparison.OrdinalIgnoreCase))
                        {
                            if (request.Kind == "gadget") throw new InvalidOperationException("PML_UI_NOT_GADGET: The requested member is not a GADGET.");
                            continue;
                        }
                        if (!String.Equals(Text(gadget + ".set().string()"), "TRUE", StringComparison.OrdinalIgnoreCase))
                        {
                            if (request.Kind == "gadget") throw new InvalidOperationException("PML_UI_GADGET_UNSET: The requested GADGET reference is unset.");
                            continue;
                        }
                        item = Item((int)index, name, form + "." + name, Text(gadget + ".type()"));
                        if (request.Kind == "gadget")
                            item.Properties = new[] { Property("shown", gadget + ".shown().string()"), Property("active", gadget + ".active.string()"),
                                Property("visible", gadget + ".visible.string()"), Property("tooltip", gadget + ".tooltip"), Property("callback", gadget + ".callback") };
                    }
                    int length = Length(item);
                    if (length > request.MaxCharacters - characters)
                    {
                        if (items.Count == 0) throw new InvalidOperationException("PML_UI_ITEM_TOO_LARGE: One result item exceeds maxCharacters; increase the budget.");
                        break;
                    }
                    items.Add(item); characters += length;
                }
                bool truncated = index <= upper;
                return new PmlFormQueryResult { Kind = request.Kind, IndexUpperBound = upper, StartIndex = request.StartIndex,
                    Scanned = scanned, NextIndex = truncated ? (int)index : 0, Truncated = truncated, Characters = characters, Items = items.ToArray() };
            }

            private int UpperBound()
            {
                runtime.Execute("!!" + size + " = !!" + snapshot + ".MAXINDEX()");
                double upper = runtime.ReadReal(size);
                if (Double.IsNaN(upper) || upper < 0 || upper > Int32.MaxValue || upper != Math.Truncate(upper))
                    throw new InvalidOperationException("Invalid native form query index upper bound.");
                return (int)upper;
            }

            private string Text(string expression)
            {
                runtime.Execute("!!" + value + " = " + expression);
                return runtime.ReadString(value) ?? throw new InvalidOperationException("Native form query returned no string.");
            }

            private PmlGadgetProperty Property(string name, string expression)
            {
                try { return new PmlGadgetProperty { Name = name, Status = "available", Value = Text(expression) }; }
                catch (InvalidOperationException error) { return new PmlGadgetProperty { Name = name, Status = "unavailable", Error = error.Message }; }
            }

            private static PmlFormQueryItem Item(int index, string name, string reference, string type)
            {
                return new PmlFormQueryItem { Index = index, Name = name, Reference = reference, Type = type, Properties = new PmlGadgetProperty[0] };
            }

            private static int Length(PmlFormQueryItem item)
            {
                long length = item.Name.Length + (long)item.Reference.Length + item.Type.Length;
                foreach (PmlGadgetProperty property in item.Properties)
                    length += property.Name.Length + property.Status.Length + (property.Value == null ? 0 : property.Value.Length) + (property.Error == null ? 0 : property.Error.Length);
                if (length > Int32.MaxValue) throw new InvalidOperationException("Native metadata is too large.");
                return (int)length;
            }
        }
    }
}
