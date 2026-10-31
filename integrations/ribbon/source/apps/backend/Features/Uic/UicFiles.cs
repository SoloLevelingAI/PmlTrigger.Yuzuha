using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public static class UicFiles
    {
        public const string Namespace = "www.aveva.com";
        public static XmlDocument ParseXml(string xml)
        {
            if (xml == null || Encoding.UTF8.GetByteCount(xml) > 12 * 1024 * 1024)
                throw new ApiError(422, "XML 为空或超过 12 MiB");
            var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                doc.Load(reader);
            return doc;
        }
        public static XmlDocument Parse(string xml)
        {
            var doc = ParseXml(xml);
            if (doc.DocumentElement.LocalName != "UserInterfaceCustomization" || doc.DocumentElement.NamespaceURI != Namespace)
                throw new ApiError(422, "不是 AVEVA UIC XML");
            return doc;
        }
        public static List<XmlElement> Children(XmlNode node, string name = null)
        {
            return node == null ? new List<XmlElement>() : node.ChildNodes.OfType<XmlElement>().Where(n => name == null || n.LocalName == name).ToList();
        }
        public static XmlElement Child(XmlNode node, string name)
        {
            return Children(node, name).FirstOrDefault();
        }
        public static string Value(XmlNode node, string name)
        {
            var child = Child(node, name);
            return child == null ? "" : child.InnerText;
        }
        static XmlElement Add(XmlNode node, string name, string text = null)
        {
            var doc = node as XmlDocument ?? node.OwnerDocument;
            var item = doc.CreateElement(name, Namespace);
            if (text != null)
                item.InnerText = text;
            node.AppendChild(item);
            return item;
        }
        static void Replace(XmlElement node, string name, XmlElement fresh)
        {
            foreach (var old in Children(node, name))
                node.RemoveChild(old);
            var source = Child(fresh, name);
            if (source != null)
                node.AppendChild(node.OwnerDocument.ImportNode(source, true));
        }
        public static string Write(XmlDocument doc)
        {
            var builder = new StringBuilder();
            var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineChars = "\n", Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize };
            using (var stream = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(stream, settings))
                    doc.Save(writer);
                return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
            }
        }
        public static Project Import(string xml, string fileName, string sourcePath)
        {
            var root = Parse(xml).DocumentElement;
            var p = new Project { schemaVersion = 1, @namespace = Value(root, "Namespace"), buttons = new List<RibbonButton>(), tabs = new List<RibbonTab>(), origin = new Origin { xml = xml, fileName = fileName, sourcePath = sourcePath, sourceHash = Data.Hash(xml) } };
            if (String.IsNullOrEmpty(p.@namespace))
                p.@namespace = "ImportedRibbon";
            foreach (var tool in Children(Child(root, "Tools"), "ButtonTool"))
            {
                var command = Child(tool, "Command");
                if (Value(command, "Type") != "Macro")
                    continue;
                string id = tool.GetAttribute("Name"), image = String.Concat(Value(tool, "Image").Where(c => !Char.IsWhiteSpace(c)));
                p.buttons.Add(new RibbonButton { id = id, sourceId = id, caption = Value(tool, "Caption"), macro = Value(command, "Macro"), commandKey = Value(command, "Key"), tooltipText = Value(tool, "Tooltip"), tooltipTitle = Value(tool, "TooltipTitle"), image = image.Length == 0 ? null : new ImageAsset { mediaType = image.StartsWith("iVBOR") ? "image/png" : "image/jpeg", base64 = image } });
            }
            foreach (var tab in Children(Child(root, "Tabs"), "Tab"))
            {
                string context = Value(Child(tab, "ContextualTabGroup"), "Item");
                if (context != "" && context != "None")
                    throw new ApiError(422, "暂不支持上下文 Tab，原文件未修改");
                var t = new RibbonTab { id = tab.GetAttribute("Name"), sourceId = tab.GetAttribute("Name"), caption = Value(tab, "Caption"), groups = new List<RibbonGroup>() };
                foreach (var group in Children(Child(tab, "Groups"), "Group"))
                {
                    var g = new RibbonGroup { id = group.GetAttribute("Name"), sourceId = group.GetAttribute("Name"), caption = Value(group, "Caption"), items = new List<RibbonItem>() };
                    foreach (var item in Children(Child(group, "Tools")))
                    {
                        if (item.LocalName != "Tool" || !p.buttons.Any(b => b.id == item.GetAttribute("Name")))
                            throw new ApiError(422, "Ribbon 存在暂不支持的可见工具或布局，未丢弃原内容");
                        g.items.Add(new RibbonItem { buttonId = item.GetAttribute("Name"), size = item.GetAttribute("LargeImage").Equals("True", StringComparison.OrdinalIgnoreCase) ? "large" : "small" });
                    }
                    t.groups.Add(g);
                }
                p.tabs.Add(t);
            }
            return ProjectRules.Normalize(p);
        }
        sealed class Definition
        {
            public RibbonButton Button; public string Id; public int Size;
        }
        public static string Export(Project p)
        {
            ProjectRules.Require(p);
            var doc = new XmlDocument { XmlResolver = null };
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            var root = doc.CreateElement("UserInterfaceCustomization", Namespace);
            doc.AppendChild(root);
            Add(root, "Version", "1.0");
            Add(root, "FormsMetaData");
            var tools = Add(root, "Tools");
            var definitions = new List<Definition>();
            var names = new Dictionary<string, string>();
            var allocated = new HashSet<string>(p.buttons.Select(b => b.id).Concat(p.tabs.SelectMany(t => new[] { t.id }.Concat(t.groups.Select(g => g.id)))));
            if (p.origin != null)
                foreach (XmlElement node in Parse(p.origin.xml).GetElementsByTagName("*"))
                    if (node.HasAttribute("Name"))
                        allocated.Add(node.GetAttribute("Name"));
            foreach (var b in p.buttons)
            {
                var sizes = p.tabs.SelectMany(t => t.groups).SelectMany(g => g.items).Where(i => i.buttonId == b.id).Select(i => i.size).Distinct().ToList();
                var ordered = sizes.Contains("large") ? new List<string> { "large" } : new List<string> { sizes.Contains("small") ? "small" : "large" };
                if (ordered[0] == "large" && sizes.Contains("small"))
                    ordered.Add("small");
                int index = 0;
                foreach (var size in ordered)
                {
                    string id = b.id;
                    if (index++ > 0)
                    {
                        id = b.id + ".SmallImage";
                        int suffix = 2;
                        while (allocated.Contains(id))
                            id = b.id + ".SmallImage." + suffix++;
                        allocated.Add(id);
                    }
                    names[b.id + "\0" + size] = id;
                    definitions.Add(new Definition { Button = b, Id = id, Size = size == "large" ? 32 : 16 });
                }
            }
            foreach (var d in definitions)
            {
                var b = d.Button;
                var tool = Add(tools, "ButtonTool");
                tool.SetAttribute("Name", d.Id);
                if (b.image != null)
                    Add(tool, "Image", IconImages.Resize(b.image, d.Size).base64);
                var cmd = Add(tool, "Command");
                Add(cmd, "Type", "Macro");
                Add(cmd, "Macro", b.macro);
                Add(cmd, "Key", b.commandKey);
                Add(cmd, "Arguments");
                Add(tool, "Caption", b.caption);
                Add(tool, "HelpContext");
                Add(tool, "Tooltip", b.tooltipText);
                Add(tool, "TooltipFormattedText", String.IsNullOrEmpty(b.tooltipText) ? "" : "<p>" + System.Security.SecurityElement.Escape(b.tooltipText).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br/>") + "</p>");
                Add(tool, "TooltipTitle", b.tooltipTitle);
                Add(tool, "DisplayStyle", "Default");
            }
            foreach (var name in new[] { "InstanceTools", "MenuBar", "CommandBars", "TaskPanes", "ContextMenus", "AreaLeftTools", "AreaRightTools", "FooterTools", "NavigationMenuTools", "QATTools", "ContextualTabGroups", "TabToolbarTools" })
                Add(root, name);
            var tabs = Add(root, "Tabs");
            foreach (var tab in p.tabs)
            {
                var t = Add(tabs, "Tab");
                t.SetAttribute("Name", tab.id);
                Add(t, "Caption", tab.caption);
                Add(Add(t, "ContextualTabGroup"), "Item", "None");
                var groups = Add(t, "Groups");
                foreach (var group in tab.groups)
                {
                    var g = Add(groups, "Group");
                    g.SetAttribute("Name", group.id);
                    var refs = Add(g, "Tools");
                    foreach (var item in group.items)
                    {
                        var r = Add(refs, "Tool");
                        r.SetAttribute("Name", names[item.buttonId + "\0" + item.size]);
                        r.SetAttribute("LargeImage", item.size == "large" ? "True" : "False");
                    }
                    Add(g, "Caption", group.caption);
                }
            }
            Add(root, "MiniToolbarTools");
            Add(root, "Namespace", p.@namespace);
            return p.origin == null ? Write(doc) : Merge(p, doc, definitions);
        }
        static string Merge(Project p, XmlDocument fresh, List<Definition> definitions)
        {
            var doc = Parse(p.origin.xml);
            var root = doc.DocumentElement;
            var oldTools = Children(Child(root, "Tools"));
            var editable = oldTools.Where(n => n.LocalName == "ButtonTool" && Value(Child(n, "Command"), "Type") == "Macro").ToList();
            var retained = new HashSet<string>(p.buttons.Select(b => b.sourceId ?? b.id));
            foreach (var container in Children(root).Where(n => n.LocalName != "Tools" && n.LocalName != "Tabs"))
                foreach (XmlElement reference in container.GetElementsByTagName("Tool", Namespace))
                {
                    string id = reference.GetAttribute("Name");
                    if (editable.Any(n => n.GetAttribute("Name") == id) && !retained.Contains(id))
                        throw new ApiError(422, "不能删除被其他 UIC 容器引用的按钮：" + id);
                    var renamed = p.buttons.FirstOrDefault(b => b.sourceId == id);
                    if (renamed != null)
                        reference.SetAttribute("Name", renamed.id);
                }
            var tools = Child(root, "Tools");
            foreach (var old in editable)
                tools.RemoveChild(old);
            foreach (var d in definitions)
            {
                if (oldTools.Any(n => n.GetAttribute("Name") == d.Id && !editable.Contains(n)))
                    throw new ApiError(422, "标识被不可编辑工具占用");
                var generated = Children(Child(fresh.DocumentElement, "Tools")).First(n => n.GetAttribute("Name") == d.Id);
                var old = editable.FirstOrDefault(n => n.GetAttribute("Name") == (d.Button.sourceId ?? d.Button.id));
                if (old == null)
                {
                    tools.AppendChild(doc.ImportNode(generated, true));
                    continue;
                }
                var node = (XmlElement)old.CloneNode(true);
                node.SetAttribute("Name", d.Id);
                Replace(node, "Image", generated);
                Replace(node, "Caption", generated);
                if (Value(old, "Tooltip") != d.Button.tooltipText)
                {
                    Replace(node, "Tooltip", generated);
                    Replace(node, "TooltipFormattedText", generated);
                }
                if (Value(old, "TooltipTitle") != d.Button.tooltipTitle)
                    Replace(node, "TooltipTitle", generated);
                if (d.Button.image != null)
                    foreach (var resource in Children(node, "ImageResourceId"))
                        node.RemoveChild(resource);
                foreach (var name in new[] { "Type", "Macro", "Key" })
                    Replace(Child(node, "Command"), name, Child(generated, "Command"));
                tools.AppendChild(node);
            }
            var tabs = Child(root, "Tabs");
            var originals = Children(tabs, "Tab");
            var originalRefs = originals.SelectMany(t => t.GetElementsByTagName("Tool", Namespace).Cast<XmlElement>()).ToList();
            foreach (var old in originals)
                tabs.RemoveChild(old);
            for (int ti = 0; ti < p.tabs.Count; ti++)
            {
                var t = p.tabs[ti];
                var generated = Children(Child(fresh.DocumentElement, "Tabs"), "Tab")[ti];
                var original = originals.FirstOrDefault(n => n.GetAttribute("Name") == (t.sourceId ?? t.id));
                if (original == null)
                {
                    tabs.AppendChild(doc.ImportNode(generated, true));
                    continue;
                }
                var node = (XmlElement)original.CloneNode(true);
                node.SetAttribute("Name", t.id);
                Replace(node, "Caption", generated);
                var groupList = Child(node, "Groups");
                var oldGroups = Children(groupList, "Group");
                foreach (var old in oldGroups)
                    groupList.RemoveChild(old);
                for (int gi = 0; gi < t.groups.Count; gi++)
                {
                    var g = t.groups[gi];
                    var next = Children(Child(generated, "Groups"), "Group")[gi];
                    var old = oldGroups.FirstOrDefault(n => n.GetAttribute("Name") == (g.sourceId ?? g.id));
                    if (old == null)
                    {
                        groupList.AppendChild(doc.ImportNode(next, true));
                        continue;
                    }
                    var group = (XmlElement)old.CloneNode(true);
                    group.SetAttribute("Name", g.id);
                    Replace(group, "Caption", next);
                    var refs = (XmlElement)doc.ImportNode(Child(next, "Tools"), true);
                    foreach (XmlAttribute attr in Child(old, "Tools").Attributes)
                        refs.SetAttribute(attr.Name, attr.Value);
                    int index = 0;
                    foreach (var r in Children(refs, "Tool"))
                    {
                        string buttonId = g.items[index++].buttonId;
                        var button = p.buttons.First(b => b.id == buttonId);
                        var oldRef = Children(Child(old, "Tools"), "Tool").Concat(originalRefs).FirstOrDefault(n => n.GetAttribute("Name") == (button.sourceId ?? button.id));
                        if (oldRef != null)
                            foreach (XmlAttribute attr in oldRef.Attributes)
                                if (attr.Name != "Name" && attr.Name != "LargeImage")
                                    r.SetAttribute(attr.Name, attr.Value);
                    }
                    foreach (var r in Children(group, "Tools"))
                        group.RemoveChild(r);
                    group.AppendChild(refs);
                    groupList.AppendChild(group);
                }
                tabs.AppendChild(node);
            }
            Replace(root, "Namespace", fresh.DocumentElement);
            return Write(doc);
        }
    }
}
