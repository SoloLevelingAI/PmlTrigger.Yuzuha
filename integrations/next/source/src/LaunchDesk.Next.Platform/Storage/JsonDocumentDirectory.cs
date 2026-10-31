using LaunchDesk.Next.Application.Infrastructure.Contracts;
namespace LaunchDesk.Next.Platform.Storage;

public sealed class JsonDocumentDirectory(string root) : IJsonDocumentDirectory
{
    public IJsonDocumentStore Open(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Document name must be a simple identifier.");
        return new JsonDocumentStore(Path.Combine(root, name + ".json"));
    }
}
