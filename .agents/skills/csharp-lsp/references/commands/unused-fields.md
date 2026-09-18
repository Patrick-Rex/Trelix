# unused-fields

查找从未被引用的 private 字段。

## 命令格式

```bash
roslyn-tool unused-fields <project> [--format json|text|markdown] [-o <file>]
```

## 参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj`、`.sln`、`.slnx` 文件路径（必填） |
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
    "file": "MyClass.cs",
    "line": 12,
    "field": "_name",
    "type": "string",
    "class": "MyClass"
  },
  {
    "file": "MyClass.cs",
    "line": 15,
    "field": "_count",
    "type": "int",
    "class": "MyClass"
  }
]
```

## 使用场景

- 清理死代码
- 代码审查时发现潜在问题
- 减少代码体积
