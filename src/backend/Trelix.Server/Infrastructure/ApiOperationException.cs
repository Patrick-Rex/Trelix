namespace Trelix.Server.Infrastructure;

// Only explicitly authored, safe public messages belong here; never wrap exception.Message.
public sealed class ApiOperationException(int status, string code, string title) : Exception(title)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
