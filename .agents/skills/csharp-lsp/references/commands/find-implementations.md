# find-implementations

查找接口或抽象类的所有实现。

## 命令格式

```bash
roslyn-tool find-implementations <project> --file <file> --line <n> --column <n> [--format json|text]
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
# 查找接口的所有实现
roslyn-tool find-implementations ./MyProject.csproj --file src/IService.cs --line 5 --column 15
```

## JSON 输出格式

```json
{
  "command": "find-implementations",
  "location": { "file": "src/IMyInterface.cs", "line": 1, "column": 1 },
  "symbol": "IMyInterface",
  "found": true,
  "implementations": [
    {
      "name": "MyClass",
      "namespace": "MyNamespace",
      "displayName": "MyNamespace.MyClass",
      "file": "src/MyClass.cs",
      "line": 1
    }
  ],
  "count": 1
}
```

## 使用场景

- 查找接口的所有实现类
- 查找抽象类的具体实现
- 理解依赖注入的服务实现
