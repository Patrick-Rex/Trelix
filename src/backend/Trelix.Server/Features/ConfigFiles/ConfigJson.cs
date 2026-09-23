using System.Text;
using System.Text.Json;
using Trelix.Server.Infrastructure;

namespace Trelix.Server.Features.ConfigFiles;

/// <summary>验证标准 JSON 及 .NET 配置键兼容性，校验失败不暴露正文或属性名。</summary>
public static class ConfigJson
{
    /// <summary>JSON 原文允许的最大 UTF-8 字节数。</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>校验根对象、大小、深度及无歧义属性名称，保留原始正文。</summary>
    /// <param name="json">待保存或发布的 JSON 正文。</param>
    /// <exception cref="ApiOperationException">正文超过限制或无法映射为有效配置。</exception>
    public static void Validate(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new ApiOperationException(400, "invalid_json", "配置不能为空，且 UTF-8 正文不得超过 1 MiB。");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ApiOperationException(400, "invalid_json", "配置 JSON 的根节点必须是对象。");
            ValidateElement(document.RootElement);
        }
        catch (JsonException)
        {
            throw new ApiOperationException(400, "invalid_json", "配置必须为有效的标准 JSON，嵌套深度不得超过 64 层。");
        }
        catch (InvalidOperationException)
        {
            // JsonDocument 延迟解码属性名；不完整的 UTF-16 转义在读取属性名时才被拒绝。
            throw new ApiOperationException(400, "invalid_json", "配置 JSON 包含无效的 Unicode 文本。");
        }
    }

    /// <summary>逐层校验对象属性，禁止忽略大小写的重复名称和路径分隔符。</summary>
    /// <param name="element">已通过大小及深度限制的 JSON 节点。</param>
    private static void ValidateElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Contains(':') || !names.Add(property.Name))
                    throw new ApiOperationException(400, "invalid_json", "对象属性名不得包含冒号或存在忽略大小写的重复名称。");
                ValidateElement(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateElement(item);
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            // 强制解码字符串值，确保应用随后读取时不会遇到无效 UTF-16 转义。
            _ = element.GetString();
        }
    }
}
