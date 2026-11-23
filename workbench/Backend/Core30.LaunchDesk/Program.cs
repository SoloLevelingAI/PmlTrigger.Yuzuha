using Core30.Core;
using Core30.LaunchDesk;

return CoreCommandLine.Run(args, [new("launcher", "LaunchDesk 启动器", "运行环境", static () => LaunchDeskFeatures.All())],
    hasLaunchFixture: true);
