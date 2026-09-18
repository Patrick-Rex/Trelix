# find-derived-types

查找继承自某类型的所有子类型。

## 命令格式

```bash
roslyn-tool find-derived-types <project> --file <file> --line <n> --column <n> [--format json|text]
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
# 查找某基类的所有派生类
roslyn-tool find-derived-types ./MyProject.csproj --file src/BaseClass.cs --line 1 --column 1
```

## JSON 输出格式

```json
{
  "command": "find-derived-types",
  "location": { "file": "src/BaseClass.cs", "line": 1, "column": 1 },
  "symbol": "BaseClass",
  "found": true,
  "derivedTypes": [
    {
      "name": "DerivedClass",
      "namespace": "MyNamespace",
      "displayName": "MyNamespace.DerivedClass",
      "file": "src/DerivedClass.cs",
      "line": 1
    }
  ],
  "count": 1
}
```

## 使用场景

- 查找某抽象类的所有具体实现
- 分析类继承层次
- 重构时评估影响范围
