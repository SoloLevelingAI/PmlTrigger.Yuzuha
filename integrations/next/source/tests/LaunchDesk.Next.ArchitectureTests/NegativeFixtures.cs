namespace Fixtures.Domain { public sealed class InvalidDomain { public LaunchDesk.Next.Platform.Files.WindowsFileSystem Value = new(); } }
namespace Fixtures.Application { public sealed class InvalidApplication { public LaunchDesk.Next.Platform.Files.WindowsFileSystem Value = new(); } }
namespace Fixtures.Desktop { public sealed class LaunchViewModel { public LaunchDesk.Next.Application.Features.LoginLaunch.ILaunchService? Value; } public sealed class InvalidDesktop { public LaunchDesk.Next.Platform.Files.WindowsFileSystem Value = new(); } }
namespace Fixtures.Dto { public sealed class LaunchInput { public System.Windows.Window? Value; } }
namespace Fixtures.Platform { public sealed class InvalidPlatform { public LaunchDesk.Next.Desktop.Features.Launch.LaunchViewModel Value = new(); } }
namespace Fixtures.CycleA { public sealed class A { public Fixtures.CycleB.B? Value; } }
namespace Fixtures.CycleB { public sealed class B { public Fixtures.CycleA.A? Value; } }
namespace LaunchDesk.Next.Desktop.Services { public sealed class InvalidPlatformService { public LaunchDesk.Next.Platform.Files.WindowsFileSystem Value = new(); } }
namespace Fixtures.Dto { public sealed class InterfaceInput { public System.ComponentModel.INotifyPropertyChanged? Notifications; public System.Windows.Input.ICommand? Command; } }
namespace Fixtures.Infrastructure { public sealed class InvalidLogistics { public LaunchDesk.Next.Application.Features.LoginLaunch.LaunchRequest? Value; } }
namespace Fixtures.PlatformFeature { public sealed class InvalidHost { public LaunchDesk.Next.Application.Features.LoginLaunch.LaunchRequest? Value; } }
