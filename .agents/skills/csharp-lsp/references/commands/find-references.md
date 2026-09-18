# find-references

查找符号在项目中的所有引用位置。

## 命令格式

```bash
roslyn-tool find-references <project> --file <file> --line <n> --column <n> [--format json|text]
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
# 查找某方法的所有引用
roslyn-tool find-references ./MyProject.csproj --file src/App.cs --line 10 --column 5
```

## JSON 输出格式

```json
{
  "command": "find-references",
  "location": { "file": "src/App.cs", "line": 5, "column": 15 },
  "symbol": "App.Run",
  "found": true,
  "references": [
    {
      "file": "src/Program.cs",
      "line": 10,
      "column": 5,
      "kind": "Method",
      "definition": false
    }
  ],
  "count": 1
}
```

## 字段说明

| 字段 | 说明 |
|------|------|
| `definition` | `true` 表示是定义位置，`false` 表示是引用位置 |
| `count` | 引用总数 |
