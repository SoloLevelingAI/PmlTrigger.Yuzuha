using System.Collections.Generic;
namespace RibbonStudio
{
    public class XmlRegistration
    {
        public string uicPath { get; set; }
        public string xmlPath { get; set; }
        public string monPath { get; set; }
        public string manifestName { get; set; }
        public string entryName { get; set; }
        public string origin { get; set; }
        public string product { get; set; }
        public string version { get; set; }
        public string versionSource { get; set; }
    }
    public class RegistrationScan
    {
        public List<Target> targets { get; set; }
        public List<XmlRegistration> rows { get; set; }
        public List<string> errors { get; set; }
        public string scannedAt { get; set; }
    }
    public class DeploymentProfile
    {
        public string id { get; set; }
        public string name { get; set; }
        public string monPath { get; set; }
        public List<string> manifests { get; set; }
    }
    public class DeploymentProfiles { public List<DeploymentProfile> profiles { get; set; } }
}
