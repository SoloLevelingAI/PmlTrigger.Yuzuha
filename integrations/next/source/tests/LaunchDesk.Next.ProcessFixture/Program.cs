using System.Text.Json;
Console.WriteLine("LaunchDesk.Next isolated process fixture");
var output = Environment.GetEnvironmentVariable("LD_NEXT_OUTPUT");
if (!string.IsNullOrEmpty(output))
{
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Arguments = args, ChildValue = Environment.GetEnvironmentVariable("LD_NEXT_FIXTURE"), Licence = Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST") }));
    return;
}
await Task.Delay(TimeSpan.FromSeconds(20));
