using System;

namespace LaunchDesk.Development.RemoteProjectLab
{
    public static class DevelopmentSwitchChecks
    {
        public static int Main()
        {
            var activation = new LocalActivation { Enabled = true, MachineFingerprint = "developer-A", UserSid = "user-A" };
            if (DevelopmentSwitch.Matches(null, "developer-A", "user-A")) return 1;
            if (!DevelopmentSwitch.Matches(activation, "developer-A", "user-A")) return 2;
            if (DevelopmentSwitch.Matches(activation, "developer-B", "user-A")) return 3;
            if (DevelopmentSwitch.Matches(activation, "developer-A", "user-B")) return 4;
            if (DevelopmentSwitch.Matches(activation, "", "user-A")) return 5;
            activation.Enabled = false;
            if (DevelopmentSwitch.Matches(activation, "developer-A", "user-A")) return 6;
            Console.WriteLine("6 activation boundary checks passed; no local activation or VMware operation performed.");
            return 0;
        }
    }
}
