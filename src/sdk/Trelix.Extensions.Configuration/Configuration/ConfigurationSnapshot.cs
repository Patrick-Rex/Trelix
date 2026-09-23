using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trelix.Extensions.Configuration;

/// <summary>来自同一次读取的发布身份和经过完整校验的配置字典。</summary>
/// <param name="FileId">稳定文件标识。</param>
/// <param name="Version">文件发布版本。</param>
/// <param name="Data">忽略大小写的配置键值快照。</param>
internal sealed record ConfigurationSnapshot(Guid FileId, long Version, Dictionary<string, string?> Data)
{
    /// <summary>校验标准 JSON 并按 .NET 10 的 null、空数组及路径语义展开配置。</summary>
    /// <param name="fileId">响应中的文件标识。</param>
    /// <param name="version">响应中的版本。</param>
    /// <param name="json">响应中的完整配置原文。</param>
    /// <returns>可以原子应用的快照。</returns>
    internal static ConfigurationSnapshot Parse(Guid fileId, long version, string? json)
    {
        if (fileId == Guid.Empty || version <= 0 || string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 1024 * 1024)
            throw new TrelixConfigurationException("invalid_response");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new TrelixConfigurationException("invalid_configuration");
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            Flatten(document.RootElement, null, data);
            return new(fileId, version, data);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new TrelixConfigurationException("invalid_configuration");
        }
    }

    /// <summary>递归校验对象名称并展开键，根对象为空时不生成根键。</summary>
    /// <param name="element">当前 JSON 节点。</param>
    /// <param name="path">当前路径；null 表示根。</param>
    /// <param name="data">本次新快照的私有字典。</param>
    private static void Flatten(JsonElement element, string? path, Dictionary<string, string?> data)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Contains(':') || !names.Add(property.Name))
                    throw new TrelixConfigurationException("invalid_configuration");
                Flatten(property.Value, path is null ? property.Name : path + ":" + property.Name, data);
            }
            if (names.Count == 0 && path is not null)
                data.Add(path, null);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                Flatten(item, path + ":" + (index++).ToString(CultureInfo.InvariantCulture), data);
            if (index == 0)
                data.Add(path!, "");
        }
        else
        {
            data.Add(path!, element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => element.GetString(),
                _ => element.ToString()
            });
        }
    }
}
