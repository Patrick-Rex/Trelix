# unused-fields

查找从未被引用的 private 字段。

## 命令格式

```bash
roslyn-tool unused-fields <project> [--format json|text|markdown] [-o <file>]
```

## 参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj` 文件路径（必填），不接受解决方案 |
| `--format <format>` | 输出格式：`json`、`text`（默认）、`markdown` |
| `-o, --output <file>` | 输出到文件 |
| `--verbose` | 输出详细信息 |

## 示例

```bash
# 查找未使用的字段
roslyn-tool unused-fields ./MyProject.csproj

# 输出为 JSON 格式
roslyn-tool unused-fields ./MyProject.csproj --format json

# 输出到文件
roslyn-tool unused-fields ./MyProject.csproj --format json -o result.json
```

## JSON 输出格式

```json
[
  {
    "symbol": "MyProject.MyClass._name",
    "location": "src/MyClass.cs:12"
  },
  {
    "symbol": "MyProject.MyClass._count",
    "location": "src/MyClass.cs:15"
  }
]
```

## 使用场景

`location` 是文件路径和行号组成的字符串，运行时路径可能是绝对路径；示例使用项目相对路径。结果仅为静态分析线索，删除前需排查反射、序列化或生成代码访问。

- 清理死代码
- 代码审查时发现潜在问题
- 减少代码体积
