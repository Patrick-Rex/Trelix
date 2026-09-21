using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>生成高强度随机应用令牌，并提供格式检查及不可逆摘要。</summary>
public static class ApplicationTokenSecret
{
    /// <summary>生成带固定前缀的 256 位随机 Base64Url 凭证。</summary>
    /// <returns>新生成的凭证原文。</returns>
    public static string Create() => "trlx_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    /// <summary>计算凭证原文的 SHA-256 摘要，用于存储和校验。</summary>
    /// <param name="secret">应用凭证原文。</param>
    /// <returns>大写十六进制摘要。</returns>
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    /// <summary>检查凭证长度、前缀及 Base64Url 字符集。</summary>
    /// <param name="secret">应用凭证原文。</param>
    /// <returns>格式符合约定时为 true；此结果不代表凭证有效或已授权。</returns>
    public static bool HasValidFormat(string secret) => secret.Length == 48 && secret.StartsWith("trlx_", StringComparison.Ordinal)
        && secret.AsSpan(5).IndexOfAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".AsSpan()) < 0;
}
