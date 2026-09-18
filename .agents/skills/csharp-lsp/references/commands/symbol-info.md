# symbol-info

获取符号（方法、字段、类型等）的完整信息。

## 命令格式

```bash
roslyn-tool symbol-info <project> --file <file> --line <n> --column <n> [--format json|text]
```

## 参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj`、`.sln`、`.slnx` 文件路径（必填） |
| `--file <file>` | 文件路径（必填） |
| `--line <n>` | 行号，从 1 开始（必填） |
| `--column <n>` | 列号，从 1 开始（必填） |
| `--format <format>` | 输出格式：`json`（默认）、`text` |

## 示例

```bash
# 获取 App.cs 第 5 行第 15 列的符号信息
roslyn-tool symbol-info ./MyProject.csproj --file src/App.cs --line 5 --column 15

# 输出到文件
roslyn-tool symbol-info ./MyProject.csproj --file src/App.cs --line 5 --column 15 --format json > result.json
```

## JSON 输出格式

```json
{
  "command": "symbol-info",
  "location": { "file": "src/App.cs", "line": 5, "column": 15 },
  "symbol": "App.Run",
  "found": true,
  "info": {
    "name": "Run",
    "kind": "Method",
    "displayName": "Task<int> App.Run(string[])",
    "namespace": "<global namespace>",
    "containingType": "App",
    "modifiers": ["public", "static", "async"],
    "isAbstract": false,
    "isVirtual": false,
    "isStatic": true,
    "returnType": "System.Threading.Tasks.Task<int>",
    "parameters": ["string[] args"],
    "xmlDoc": "/// <summary>...",
    "definition": { "file": "src/App.cs", "line": 5, "column": 35 }
  }
}
```

## 字段说明

| 字段 | 说明 |
|------|------|
| `kind` | 符号类型：Method、Field、Property、Class、Interface 等 |
| `displayName` | 完整签名（含返回类型和参数） |
| `modifiers` | 修饰符列表 |
| `xmlDoc` | XML 文档注释 |
| `definition` | 定义位置（可能与查询位置不同） |
