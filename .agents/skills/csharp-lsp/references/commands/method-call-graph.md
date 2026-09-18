# method-call-graph

获取方法内部的调用关系图。

## 命令格式

```bash
roslyn-tool method-call-graph <project> --file <file> --line <n> --column <n> [--depth <n>] [--format json|text]
```

## 参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj`、`.sln`、`.slnx` 文件路径（必填） |
| `--file <file>` | 文件路径（必填） |
| `--line <n>` | 行号，从 1 开始（必填） |
| `--column <n>` | 列号，从 1 开始（必填） |
| `--depth <n>` | 递归深度，默认 1（只查找直接调用） |
| `--format <format>` | 输出格式：`json`（默认）、`text` |
| `-o, --output <file>` | 输出到文件 |

## 示例

```bash
# 获取方法的直接调用
roslyn-tool method-call-graph ./MyProject.csproj --file src/MyClass.cs --line 10 --column 5

# 获取深度为 2 的调用关系
roslyn-tool method-call-graph ./MyProject.csproj --file src/MyClass.cs --line 10 --column 5 --depth 2
```

## JSON 输出格式

```json
{
  "command": "method-call-graph",
  "location": { "file": "src/MyClass.cs", "line": 10, "column": 5 },
  "symbol": { "name": "MyMethod", "displayName": "void MyMethod()" },
  "depth": 1,
  "found": true,
  "method": {
    "name": "MyMethod",
    "displayName": "void MyMethod()",
    "file": "src/MyClass.cs",
    "line": 10
  },
  "calls": [
    {
      "name": "HelperMethod",
      "displayName": "int HelperMethod(string)",
      "kind": "Method",
      "containingType": "MyClass",
      "file": "src/MyClass.cs",
      "line": 20,
      "isExternal": false
    },
    {
      "name": "Console.WriteLine",
      "displayName": "void Console.WriteLine(string)",
      "kind": "Method",
      "containingType": "Console",
      "file": "src/MyClass.cs",
      "line": 21,
      "isExternal": true
    }
  ],
  "count": 2
}
```

## 字段说明

| 字段 | 说明 |
|------|------|
| `depth` | 递归深度，1 表示只查直接调用 |
| `isExternal` | `true` 表示外部方法（如 .NET 库），无法继续追踪 |

## 使用场景

- 理解方法的执行流程
- 分析代码依赖关系
- 性能分析时定位热点调用
