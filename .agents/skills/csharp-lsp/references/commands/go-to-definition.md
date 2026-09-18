# go-to-definition

跳转到符号的定义位置。

## 命令格式

```bash
roslyn-tool go-to-definition <project> --file <file> --line <n> --column <n> [--format json|text]
```

## 参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj`、`.sln`、`.slnx` 文件路径（必填） |
| `--file <file>` | 文件路径（必填） |
| `--line <n>` | 行号，从 1 开始（必填） |
| `--column <n>` | 列号，从 1 开始（必填） |
| `--format <format>` | 输出格式：`json`（默认）、`text` |
| `-o, --output <file>` | 输出到文件 |

## 示例

```bash
# 跳转到方法定义
roslyn-tool go-to-definition ./MyProject.csproj --file src/App.cs --line 10 --column 5
```

## JSON 输出格式

```json
{
  "command": "go-to-definition",
  "location": { "file": "src/App.cs", "line": 5, "column": 15 },
  "symbol": "App.Run",
  "found": true,
  "definition": {
    "file": "src/App.cs",
    "line": 5,
    "column": 35,
    "displayName": "Task<int> App.Run(string[])"
  }
}
```

## 使用场景

- 从引用位置跳转到定义
- 查找变量/方法的原始声明
- 定位接口成员的实现
