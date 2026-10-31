using System;
using System.IO;
using System.Text;
using System.Linq;
namespace RibbonStudio.Backend
{
    public sealed class WorkspaceFeature
    {
        public readonly WorkspaceFiles Store; public WorkspaceFeature(WorkspaceFiles store)
        {
            Store = store;
        }
        public SaveResult Import(object input)
        {
            byte[] raw = null;
            string path = Data.Text(input, "path"), xml = Data.Text(input, "xml", null);
            if (Data.Get(input, "bytesBase64") != null)
                try
                {
                    raw = Convert.FromBase64String(Data.Text(input, "bytesBase64"));
                }
                catch { throw new ApiError(422, "UIC Base64 无效"); }
            if (path != "")
            {
                if (!Path.IsPathRooted(path) || !path.EndsWith(".uic", StringComparison.OrdinalIgnoreCase))
                    throw new ApiError(422, "请输入绝对 .uic 文件路径");
                path = Path.GetFullPath(path);
                raw = Data.Read(path);
                if (raw == null)
                    throw new ApiError(404, "UIC 文件不存在");
            }
            if (raw != null)
            {
                if (raw.Length > 12 * 1024 * 1024)
                    throw new ApiError(422, "UIC 超过 12 MiB");
                Encoding encoding;
                bool bom;
                xml = Manifests.Decode(raw, out encoding, out bom);
            }
            var p = UicFiles.Import(xml, Data.Text(input, "fileName", path == "" ? "imported.uic" : Path.GetFileName(path)), path == "" ? null : path);
            if (path != "")
            {
                var record = Store.List().FirstOrDefault(d => Data.Same(Data.Text(d, "sourcePath"), path));
                if (record != null)
                    p.documentId = Data.Text(record, "id");
            }
            return Store.Save(p, "import", "导入原始 UIC，未写回源文件", raw == null ? (object)new
            {
                originalXml = xml
            } : new
            {
                originalUicBase64 = Convert.ToBase64String(raw)
            });
        }
        public SaveResult Restore(string documentId, string versionId)
        {
            var v = Store.Version(documentId, versionId);
            var p = Data.As<Project>(v["project"]);
            p.documentId = documentId;
            return Store.Save(p, "restore", "恢复编辑器到版本 " + v["sequence"] + "；未写回 UIC 文件");
        }
        public byte[] Original(string documentId, string versionId)
        {
            var v = Store.Version(documentId, versionId);
            var a = Data.Get(v, "artifacts");
            if (Data.Text(a, "originalUicBase64") != "")
                return Convert.FromBase64String(Data.Text(a, "originalUicBase64"));
            if (Data.Text(a, "originalXml") != "")
                return Encoding.UTF8.GetBytes(Data.Text(a, "originalXml"));
            throw new ApiError(404, "该版本没有原始文件快照");
        }
    }
}
