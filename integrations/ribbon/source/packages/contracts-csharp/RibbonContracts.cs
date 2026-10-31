using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
namespace RibbonStudio
{
    public class ImageAsset
    {
        public string mediaType
        {
            get; set;
        }
        public string base64
        {
            get; set;
        }
    }
    public class RibbonButton
    {
        public string sourceId
        {
            get; set;
        }
        public string id
        {
            get; set;
        }
        public string caption
        {
            get; set;
        }
        public string macro
        {
            get; set;
        }
        public string commandKey
        {
            get; set;
        }
        public ImageAsset image
        {
            get; set;
        }
        public string tooltipText
        {
            get; set;
        }
        public string tooltipTitle
        {
            get; set;
        }
    }
    public class RibbonItem
    {
        public string buttonId
        {
            get; set;
        }
        public string size
        {
            get; set;
        }
    }
    public class RibbonGroup
    {
        public string sourceId
        {
            get; set;
        }
        public string id
        {
            get; set;
        }
        public string caption
        {
            get; set;
        }
        public List<RibbonItem> items
        {
            get; set;
        }
    }
    public class RibbonTab
    {
        public string sourceId
        {
            get; set;
        }
        public string id
        {
            get; set;
        }
        public string caption
        {
            get; set;
        }
        public List<RibbonGroup> groups
        {
            get; set;
        }
    }
    public class Origin
    {
        public string fileName
        {
            get; set;
        }
        public string xml
        {
            get; set;
        }
        public string sourcePath
        {
            get; set;
        }
        public string sourceHash
        {
            get; set;
        }
    }
    public class Binding
    {
        public string monPath
        {
            get; set;
        }
        public string manifestName
        {
            get; set;
        }
        public string entryName
        {
            get; set;
        }
        public string registrationPath
        {
            get; set;
        }
        public string filePath
        {
            get; set;
        }
    }
    public class Project
    {
        public int schemaVersion
        {
            get; set;
        }
        public string @namespace
        {
            get; set;
        }
        public string documentId
        {
            get; set;
        }
        public Origin origin
        {
            get; set;
        }
        public Binding preferredBinding
        {
            get; set;
        }
        public List<RibbonButton> buttons
        {
            get; set;
        }
        public List<RibbonTab> tabs
        {
            get; set;
        }
    }
    public class Document
    {
        public string id
        {
            get; set;
        }
        public string label
        {
            get; set;
        }
        public string sourcePath
        {
            get; set;
        }
        public int count
        {
            get; set;
        }
        public string updatedAt
        {
            get; set;
        }
        public List<Binding> bindings
        {
            get; set;
        }
    }
    public partial class Version
    {
        public string id
        {
            get; set;
        }
        public int sequence
        {
            get; set;
        }
        public string kind
        {
            get; set;
        }
        public string note
        {
            get; set;
        }
        public string createdAt
        {
            get; set;
        }
        public bool valid
        {
            get; set;
        }
        public int buttons
        {
            get; set;
        }
        public int groups
        {
            get; set;
        }
        public bool hasOriginal
        {
            get; set;
        }
    }
    public class SaveResult
    {
        public Project project
        {
            get; set;
        }
        public Document document
        {
            get; set;
        }
        public Version version
        {
            get; set;
        }
    }
    public class Documents
    {
        public List<Document> documents
        {
            get; set;
        }
    }
    public class Versions
    {
        public List<Version> versions
        {
            get; set;
        }
    }
    public partial class Target
    {
        public string product { get; set; }
        public string version { get; set; }
        public string versionSource { get; set; }
        public string label {get;set;}
        public string monPath
        {
            get; set;
        }
        public string directory
        {
            get; set;
        }
        public List<string> manifests
        {
            get; set;
        }
    }
    public class Targets
    {
        public List<Target> targets
        {
            get; set;
        }
    }
    public partial class Entry
    {
        public bool template {get;set;}
        public string key
        {
            get; set;
        }
        public string name
        {
            get; set;
        }
        public string path
        {
            get; set;
        }
        public string filePath
        {
            get; set;
        }
        public string origin
        {
            get; set;
        }
        public string reason
        {
            get; set;
        }
        public bool exists
        {
            get; set;
        }
        public bool enabled
        {
            get; set;
        }
        public string documentId
        {
            get; set;
        }
        public string manifestHash
        {
            get; set;
        }
        public string uicHash
        {
            get; set;
        }
    }
    public class Inventory
    {
        public List<Entry> rows
        {
            get; set;
        }
        public string manifestHash
        {
            get; set;
        }
    }
    public partial class ModuleRow
    {
        public string manifestName
        {
            get; set;
        }
        public List<string> entryNames
        {
            get; set;
        }
        public string error
        {
            get; set;
        }
        public bool Selected
        {
            get; set;
        }
        public bool Marked
        {
            get; set;
        }
    }
    public class Modules
    {
        public string monPath
        {
            get; set;
        }
        public List<ModuleRow> modules
        {
            get; set;
        }
    }
    public class RegistrationEntry
    {
        public string name
        {
            get; set;
        }
        public string path
        {
            get; set;
        }
    }
    public class Plan
    {
        public string nextManifestHash { get; set; }
        public string operation
        {
            get; set;
        }
        public string monPath
        {
            get; set;
        }
        public string manifestName
        {
            get; set;
        }
        public string manifestPath
        {
            get; set;
        }
        public string uicPath
        {
            get; set;
        }
        public string entryName
        {
            get; set;
        }
        public string registration
        {
            get; set;
        }
        public string manifestHash
        {
            get; set;
        }
        public string uicHash
        {
            get; set;
        }
        public int uicBytes
        {
            get; set;
        }
        public bool unchanged
        {
            get; set;
        }
        public string beforeXml
        {
            get; set;
        }
        public string afterXml
        {
            get; set;
        }
        public List<RegistrationEntry> beforeEntries
        {
            get; set;
        }
        public List<RegistrationEntry> afterEntries
        {
            get; set;
        }
        public List<RegistrationEntry> addedEntries
        {
            get; set;
        }
        public List<Binding> references
        {
            get; set;
        }
        public List<string> scanErrors
        {
            get; set;
        }
        public string backupDirectory
        {
            get; set;
        }
    }
    public class Issue
    {
        public string code
        {
            get; set;
        }
        public string path
        {
            get; set;
        }
        public string message
        {
            get; set;
        }
    }
    public class Validation
    {
        public bool valid
        {
            get; set;
        }
        public List<Issue> issues
        {
            get; set;
        }
    }
    public class PreparedImage
    {
        public ImageAsset image
        {
            get; set;
        }
    }
    public static class Json
    {
        public static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 200 };
        }
        public static string Write(object value)
        {
            var serializer = Serializer();
            var plain = serializer.DeserializeObject(serializer.Serialize(value));
            Clean(plain);
            return serializer.Serialize(plain);
        }
        static void Clean(object value)
        {
            var map = value as Dictionary<string, object>;
            if (map != null)
            {
                foreach (var key in map.Keys.ToList())
                {
                    if (map[key] == null && new[] { "origin", "sourceId", "preferredBinding", "documentId" }.Contains(key))
                        map.Remove(key);
                    else
                        Clean(map[key]);
                }
            }
            else
            {
                var list = value as System.Collections.IEnumerable;
                if (list != null && !(value is string))
                    foreach (var child in list)
                        Clean(child);
            }
        }
        public static T Read<T>(string value)
        {
            return Serializer().Deserialize<T>(value);
        }
        public static T Copy<T>(T value)
        {
            return Read<T>(Write(value));
        }
    }
}
