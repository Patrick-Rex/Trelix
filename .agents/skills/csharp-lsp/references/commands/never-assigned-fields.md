# never-assigned-fields

查找从未被赋值的非 public 字段。

## 命令格式

```bash
roslyn-tool never-assigned-fields <project> [--format json|text|markdown] [-o <file>]
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
# 查找未赋值的字段
roslyn-tool never-assigned-fields ./MyProject.csproj

# 输出为 JSON 格式
roslyn-tool never-assigned-fields ./MyProject.csproj --format json

# 输出到文件
roslyn-tool never-assigned-fields ./MyProject.csproj --format json -o result.json
```

## JSON 输出格式

```json
[
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

- 发现可能遗漏的初始化
- 识别需要依赖注入但未配置的字段
- 代码审查时发现潜在 bug
