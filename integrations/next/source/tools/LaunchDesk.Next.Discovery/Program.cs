using System.Text.Json;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
using LaunchDesk.Next.Platform.Files;
var options = args.Length > 0 ? JsonSerializer.Deserialize<DiscoveryOptions>(File.ReadAllText(args[0]))! : FindInstalledAveva.DefaultOptions;
var report = await new FindInstalledAveva(new WindowsFileSystem()).DiscoverAsync(options);
Console.WriteLine(JsonSerializer.Serialize(new { Options = options, Report = report }, new JsonSerializerOptions { WriteIndented = true }));
return report.Candidates.Count > 0 ? 0 : 2;
