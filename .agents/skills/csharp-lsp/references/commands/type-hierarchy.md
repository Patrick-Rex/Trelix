# type-hierarchy

获取类型的完整层级关系，包括基类型、接口和派生类型。

## 命令格式

```bash
roslyn-tool type-hierarchy <project> --file <file> --line <n> --column <n> [--depth <n>] [--format json|text]
```

## 参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj`、`.sln`、`.slnx` 文件路径（必填） |
| `--file <file>` | 文件路径（必填） |
| `--line <n>` | 行号，从 1 开始（必填） |
| `--column <n>` | 列号，从 1 开始（必填） |
| `--depth <n>` | 派生类查找深度，默认 1 |
| `--format <format>` | 输出格式：`json`（默认）、`text` |
| `-o, --output <file>` | 输出到文件 |

## 示例

```bash
# 获取类型的层级关系
roslyn-tool type-hierarchy ./MyProject.csproj --file src/MyClass.cs --line 5 --column 15
```

## JSON 输出格式

```json
{
  "command": "type-hierarchy",
  "location": { "file": "src/MyClass.cs", "line": 1, "column": 1 },
  "symbol": { "name": "MyClass", "displayName": "MyNamespace.MyClass" },
  "depth": 1,
  "found": true,
  "type": {
    "name": "MyClass",
    "namespace": "MyNamespace",
    "displayName": "MyNamespace.MyClass",
    "kind": "Class",
    "isAbstract": false,
    "isSealed": false
  },
  "baseType": {
    "name": "BaseClass",
    "namespace": "MyNamespace",
    "displayName": "MyNamespace.BaseClass",
    "file": "src/BaseClass.cs",
    "line": 1
  },
  "interfaces": [
    {
      "name": "IMyInterface",
      "namespace": "MyNamespace",
      "displayName": "MyNamespace.IMyInterface",
      "file": "src/IMyInterface.cs",
      "line": 1
    }
  ],
  "derivedTypes": [
    {
      "name": "DerivedClass",
      "namespace": "MyNamespace",
      "displayName": "MyNamespace.DerivedClass",
      "file": "src/DerivedClass.cs",
      "line": 1,
      "depth": 1
    }
  ]
}
```

## 字段说明

| 字段 | 说明 |
|------|------|
| `baseType` | 基类型（如果是 object 则为 null） |
| `interfaces` | 该类型实现的接口列表 |
| `derivedTypes` | 直接或间接派生类型 |
| `depth` | 派生类查找的递归深度 |

## 使用场景

- 理解类的继承结构
- 分析接口实现关系
- 重构前评估影响范围
