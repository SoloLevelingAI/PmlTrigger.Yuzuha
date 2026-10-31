using System;
using System.Linq;
using System.Xml;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public static class ProjectRules
    {
        public static Validation Validate(Project p)
        {
            var result = new Validation { valid = true, issues = new List<Issue>() };
            Action<string, string> add = (path, message) => result.issues.Add(new Issue { code = "validation", path = path, message = message });
            Action<string, string, bool> text = (value, path, required) => { if (value == null || required && String.IsNullOrWhiteSpace(value)) { add(path, "请填写有效文本"); return; } try { XmlConvert.VerifyXmlChars(value); } catch { add(path, "包含 XML 不支持的字符"); } };
            if (p == null)
            {
                add("", "项目必须是对象");
                result.valid = false;
                return result;
            }
            if (p.schemaVersion != 1)
                add("schemaVersion", "需要 schemaVersion=1");
            text(p.@namespace, "namespace", true);
            if (p.buttons == null || p.tabs == null)
            {
                add("", "需要 buttons/tabs 数组");
                result.valid = false;
                return result;
            }
            if (p.origin != null)
                try
                {
                    UicFiles.Parse(p.origin.xml);
                }
                catch (Exception error) { add("origin", error.Message); }
            var ids = new HashSet<string>();
            var keys = new HashSet<string>();
            Action<string, string> id = (value, path) => { text(value, path, true); if (value != null && !ids.Add(value)) add(path, "标识必须唯一"); };
            int index = 0;
            foreach (var b in p.buttons)
            {
                string path = "buttons[" + index++ + "]";
                if (b == null)
                {
                    add(path, "按钮必须是对象");
                    continue;
                }
                id(b.id, path + ".id");
                text(b.caption, path + ".caption", true);
                text(b.macro, path + ".macro", true);
                text(b.commandKey, path + ".commandKey", true);
                text(b.tooltipText, path + ".tooltipText", false);
                text(b.tooltipTitle, path + ".tooltipTitle", false);
                if (!keys.Add(b.commandKey ?? ""))
                    add(path + ".commandKey", "命令 Key 必须唯一");
                if (b.image != null)
                    try
                    {
                        IconImages.Resize(b.image, 32);
                    }
                    catch (Exception error) { add(path + ".image", error.Message); }
            }
            int groups = 0, items = 0, ti = 0;
            foreach (var t in p.tabs)
            {
                string path = "tabs[" + ti++ + "]";
                if (t == null || t.groups == null)
                {
                    add(path, "Tab 需要 groups");
                    continue;
                }
                id(t.id, path + ".id");
                text(t.caption, path + ".caption", true);
                foreach (var g in t.groups)
                {
                    groups++;
                    if (g == null || g.items == null)
                    {
                        add(path, "Group 需要 items");
                        continue;
                    }
                    id(g.id, path + ".groups.id");
                    text(g.caption, path + ".groups.caption", true);
                    foreach (var item in g.items)
                    {
                        items++;
                        if (item == null || !p.buttons.Any(b => b != null && b.id == item.buttonId))
                            add(path, "失效按钮引用");
                        if (item == null || item.size != "large" && item.size != "small")
                            add(path, "请选择 Large 或 Small");
                    }
                }
            }
            if (p.tabs.Count == 0 || groups == 0 || items == 0)
                add("tabs", "至少需要一个 Tab、Group 和按钮布局");
            result.valid = result.issues.Count == 0;
            return result;
        }
        public static void Require(Project p)
        {
            var check = Validate(p);
            if (!check.valid)
                throw new ApiError(422, "项目校验失败", check.issues);
        }
        public static Project Normalize(Project input)
        {
            var p = Json.Copy(input);
            if (p != null && p.buttons != null)
                foreach (var b in p.buttons.Where(b => b != null))
                {
                    if (String.IsNullOrEmpty(b.id))
                        b.id = "Custom.Button." + Guid.NewGuid();
                    if (String.IsNullOrEmpty(b.commandKey))
                        b.commandKey = "Macro:" + Guid.NewGuid();
                }
            if (p != null && p.tabs != null)
                foreach (var t in p.tabs.Where(t => t != null))
                {
                    if (String.IsNullOrEmpty(t.id))
                        t.id = "Custom.Tab." + Guid.NewGuid();
                    if (t.groups != null)
                        foreach (var g in t.groups.Where(g => g != null))
                            if (String.IsNullOrEmpty(g.id))
                                g.id = "Custom.Group." + Guid.NewGuid();
                }
            Require(p);
            foreach (var b in p.buttons)
                if (b.image != null)
                    b.image = IconImages.Resize(b.image, 32);
            return p;
        }
    }
}
