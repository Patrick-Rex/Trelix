using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Trelix.Server.Infrastructure.Authentication;

public static class ApplicationTokenSecret
{
    public static string Create() => "trlx_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    public static bool HasValidFormat(string secret) => secret.Length == 48 && secret.StartsWith("trlx_", StringComparison.Ordinal)
        && secret.AsSpan(5).IndexOfAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".AsSpan()) < 0;
}
