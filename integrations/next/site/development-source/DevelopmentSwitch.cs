using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace LaunchDesk.Development.RemoteProjectLab
{
    // A local development opt-in, not a security boundary or a production setting.
    public sealed class LocalActivation
    {
        public bool Enabled { get; set; }
        public string MachineFingerprint { get; set; }
        public string UserSid { get; set; }
        public string ChangedUtc { get; set; }
    }

    public static class DevelopmentSwitch
    {
        private static string ActivationPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"LaunchDesk.Next.Development\remote-project-lab\activation.local.json"); }
        }

        public static bool Matches(LocalActivation activation, string machine, string user)
        {
            return activation != null && activation.Enabled && !String.IsNullOrWhiteSpace(machine) &&
                !String.IsNullOrWhiteSpace(user) && activation.MachineFingerprint == machine && activation.UserSid == user;
        }

        private static string MachineFingerprint()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                string guid = key == null ? null : key.GetValue("MachineGuid") as string;
                if (String.IsNullOrWhiteSpace(guid)) throw new InvalidOperationException("Machine identity unavailable; development lab stays disabled.");
                using (var hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(guid + "|" + Environment.MachineName))).Replace("-", "");
            }
        }

        public static bool IsEnabled()
        {
            // No directory creation, VMware discovery or runtime loading on this path.
            if (!File.Exists(ActivationPath)) return false;
            try
            {
                var activation = new JavaScriptSerializer().Deserialize<LocalActivation>(File.ReadAllText(ActivationPath));
                return Matches(activation, MachineFingerprint(), WindowsIdentity.GetCurrent().User.Value);
            }
            catch { return false; }
        }

        public static void RequireEnabled()
        {
            if (!IsEnabled()) throw new InvalidOperationException("Development lab disabled. Explicitly run LabControl enable --local on this development computer.");
        }

        public static void SetLocal(bool enabled)
        {
            var activation = new LocalActivation { Enabled = enabled, MachineFingerprint = MachineFingerprint(),
                UserSid = WindowsIdentity.GetCurrent().User.Value, ChangedUtc = DateTime.UtcNow.ToString("o") };
            Directory.CreateDirectory(Path.GetDirectoryName(ActivationPath));
            File.WriteAllText(ActivationPath, new JavaScriptSerializer().Serialize(activation));
        }
    }
}
