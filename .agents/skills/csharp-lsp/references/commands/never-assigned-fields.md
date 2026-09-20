# never-assigned-fields

查找从未被赋值的非 public 字段。

## 命令格式

```bash
roslyn-tool never-assigned-fields <project> [--format json|text|markdown] [-o <file>]
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
    "symbol": "MyProject.MyClass._count",
    "location": "src/MyClass.cs:15"
  }
]
```

## 使用场景

`location` 是文件路径和行号组成的字符串，运行时路径可能是绝对路径；示例使用项目相对路径。不能据此排除反射、序列化或框架注入产生的赋值。

- 发现可能遗漏的初始化
- 识别需要依赖注入但未配置的字段
- 代码审查时发现潜在 bug
