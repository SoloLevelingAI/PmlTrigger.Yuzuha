using System.Text.Json;
Console.WriteLine("LaunchDesk.Next isolated process fixture");
var output = Environment.GetEnvironmentVariable("LD_NEXT_OUTPUT");
if (!string.IsNullOrEmpty(output))
{
    // Publish only after the writer has closed; existence of the final path is the completion signal.
    var pending = output + ".pending";
    await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(new { Arguments = args, ChildValue = Environment.GetEnvironmentVariable("LD_NEXT_FIXTURE"), Licence = Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST") }));
    File.Move(pending, output);
    return;
}
await Task.Delay(TimeSpan.FromSeconds(20));
