# find-usings

查找使用了某个命名空间或类型的所有文件。

## 命令格式

```bash
roslyn-tool find-usings <project> [--namespace <namespace>] [--type <type>] [--format json|text]
```

## 参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj`、`.sln`、`.slnx` 文件路径（必填） |
| `--namespace <namespace>` | 命名空间筛选（可选，如 `System.Collections`）；可只传 type 或组合使用 |
| `--type <type>` | 类型名（可选，如 `List` 或 `List<int>`） |
| `--format <format>` | 输出格式：`json`（默认）、`text` |

## 示例

```bash
# 查找使用了 System.Collections 的文件
roslyn-tool find-usings ./MyProject.csproj --namespace System.Collections

# 查找使用了 List<T> 的文件
roslyn-tool find-usings ./MyProject.csproj --type List

# 同时指定命名空间和类型
roslyn-tool find-usings ./MyProject.csproj --namespace System.Collections.Generic --type List
```

## JSON 输出格式

```json
{
  "command": "find-usings",
  "namespace": "System.Collections.Generic",
  "type": "List<T>",
  "project": "./MyProject.csproj",
  "found": true,
  "usages": [
    {
      "file": "src/Program.cs",
      "usings": ["using System.Collections.Generic;"],
      "references": [
        {
          "line": 5,
          "column": 10,
          "context": "List<string> list = new List<string>();"
        }
      ]
    }
  ],
  "count": 1
}
```

## 字段说明

| 字段 | 说明 |
|------|------|
| `usings` | 该文件中匹配的 using 语句列表 |
| `references` | 该文件中使用该类型/命名空间的具体位置 |
| `context` | 包含引用的源代码行 |

## 使用场景

- 迁移命名空间时评估影响范围
- 查找特定 API 的使用位置
- 分析代码依赖关系
