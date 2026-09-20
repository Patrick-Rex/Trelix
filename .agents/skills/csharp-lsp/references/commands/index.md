# 命令速查表

## LSP 命令（基于位置）

| 命令 | 说明 | 详细文档 |
|------|------|----------|
| `symbol-info` | 获取符号完整信息 | [symbol-info.md](symbol-info.md) |
| `find-references` | 查找符号的所有引用 | [find-references.md](find-references.md) |
| `go-to-definition` | 跳转到符号定义 | [go-to-definition.md](go-to-definition.md) |
| `find-derived-types` | 查找派生类型 | [find-derived-types.md](find-derived-types.md) |
| `find-implementations` | 查找接口实现 | [find-implementations.md](find-implementations.md) |
| `method-call-graph` | 获取方法调用图 | [method-call-graph.md](method-call-graph.md) |
| `type-hierarchy` | 获取类型层级 | [type-hierarchy.md](type-hierarchy.md) |

## 查找命令（无需行列）

| 命令 | 说明 | 详细文档 |
|------|------|----------|
| `find-symbols` | 按名称查找源码符号及声明位置 | [find-symbols.md](find-symbols.md) |
| `find-usings` | 查找使用某命名空间/类型的文件 | [find-usings.md](find-usings.md) |

## 代码分析命令

| 命令 | 说明 | 详细文档 |
|------|------|----------|
| `unused-fields` | 查找未使用的 private 字段 | [unused-fields.md](unused-fields.md) |
| `never-assigned-fields` | 查找未赋值的非 public 字段 | [never-assigned-fields.md](never-assigned-fields.md) |

## 通用参数

| 参数 | 说明 |
|------|------|
| `<project>` | 位置查询和 find-symbols 支持 `.csproj`、`.sln`、`.slnx`；字段分析仅 `.csproj`；find-usings 建议使用 `.csproj` |
| `--name <name>` | find-symbols 必填；按简单名称搜索 |
| `--exact` / `--kind <kind>` / `--limit <n>` | find-symbols 的完整名称匹配、种类筛选、输出上限，见命令文档 |
| `--file <file>` | 文件路径（LSP 命令必填） |
| `--line <n>` | 行号，从 1 开始 |
| `--column <n>` | 列号，从 1 开始 |
| `--namespace <ns>` | 命名空间（find-usings 使用） |
| `--type <type>` | 类型名（find-usings 可选） |
| `--depth <n>` | 递归深度（method-call-graph、type-hierarchy） |
| `--format <format>` | 符号查询和 find-usings：json（默认）、text；字段分析：text（默认）、json、markdown |
| `-o, --output <file>` | 仅 unused-fields、never-assigned-fields 支持；其他命令用 shell 重定向 |
