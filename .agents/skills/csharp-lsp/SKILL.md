---
name: csharp-lsp
description: >-
  使用本机提供的 Roslyn CLI 分析 C# 符号、引用、定义、接口实现、类型层级、方法调用图和可疑字段，
  用于理解代码及重构前的影响分析。提供语义查询，不执行重命名或修改源代码；
  普通文件路径与文本搜索使用 rg，不用于 Vue/TypeScript 分析。
---

# C# LSP 技能

提供基于 Roslyn 的 C# 代码分析能力，支持符号信息查询、引用查找、类型层级分析等功能。
通过本机提供的 CLI 执行，不会自动注册为编辑器 LSP 服务或 Codex MCP 工具。

## Trelix 使用约定

- 从包含 `Trelix.slnx` 的项目根目录运行；不要因 Git 顶层位于父目录而扫描其他项目。
- 单项目分析优先传入相关 `.csproj`；跨项目引用分析使用 `Trelix.slnx`，并明确查询覆盖范围。
- 用 `rg -n` 或读取源码核实符号当前行列，位置必须落在目标标识符上。编辑后重新确认位置。
- 项目和源码参数优先使用 `Resolve-Path` 得到的绝对路径，避免工作目录影响文件定位。
- 与 `csharp-refactoring` 配合时，本技能负责语义查询和影响分析；实际编辑及行为验证遵循重构技能与项目指引。
- 工具失败或工作区加载不完整时说明限制；搜索不到符号或引用不能直接推断“没有使用”。可先检查项目路径、SDK、依赖还原和加载错误。
- `find-references` 的结果可能包含 `isDefinition: true` 的定义项，`count` 也包含它；统计调用/使用数量时排除定义项。
- 若确需编译，遵循项目“只读取 error 与退出码”的要求；语义分析输出照常读取，不能当作编译或测试通过的证据。

## 工具路径

可执行文件不纳入 Git 跟踪。首次克隆后，需自行提供可信的对应平台工具并放入技能目录的 `scripts/` 子目录；缺少工具时说明无法执行语义查询，不自动下载来源不明的二进制：

```
Windows: scripts/roslyn-tool.exe
Linux:   scripts/roslyn-tool
```

在项目根目录执行；从其他目录运行时改用实际技能目录的绝对路径。

```powershell
# Windows / PowerShell
$roslynTool = (Resolve-Path '.\.agents\skills\csharp-lsp\scripts\roslyn-tool.exe').Path
& $roslynTool --help
& $roslynTool symbol-info --help
```

```bash
# Linux：需自行提供工具，本项目当前只验证了 Windows 工具
./.agents/skills/csharp-lsp/scripts/roslyn-tool --help
```

下文和参考文档中的 `roslyn-tool` 是命令简写；PowerShell 中替换为 `& $roslynTool`，不假定工具已加入 PATH。参考文档的 Bash 续行符 `\` 不适用于 PowerShell，可将命令写成一行。

## 命令速查表

### LSP 命令（基于位置）

| 命令 | 说明 | 必要参数 |
|------|------|----------|
| `symbol-info` | 获取符号完整信息 | project, file, line, column |
| `find-references` | 查找符号的所有引用 | project, file, line, column |
| `go-to-definition` | 跳转到符号定义 | project, file, line, column |
| `find-derived-types` | 查找派生类型 | project, file, line, column |
| `find-implementations` | 查找接口实现 | project, file, line, column |
| `method-call-graph` | 获取方法调用图 | project, file, line, column；depth 可选 |
| `type-hierarchy` | 获取类型层级 | project, file, line, column；depth 可选 |

### 查找命令（基于命名空间）

| 命令 | 说明 | 必要参数 |
|------|------|----------|
| `find-usings` | 查找使用某命名空间/类型的文件 | project；按需求指定 namespace 或 type，也可同时指定 |

### 代码分析命令

| 命令 | 说明 | 必要参数 |
|------|------|----------|
| `unused-fields` | 查找未使用的 private 字段 | project |
| `never-assigned-fields` | 查找未赋值的非 public 字段 | project |

## 通用参数

| 参数 | 说明 |
|------|------|
| `<project>` | `.csproj`、`.sln`、`.slnx` 文件路径（必填） |
| `--file <file>` | 文件路径（LSP 命令必填） |
| `--line <n>` | 行号，从 1 开始 |
| `--column <n>` | 列号，从 1 开始 |
| `--namespace <ns>` | 命名空间筛选（find-usings 使用，可与 type 单独或组合使用） |
| `--type <type>` | 类型名（find-usings 可选） |
| `--depth <n>` | 递归深度（method-call-graph、type-hierarchy） |
| `--format <format>` | 符号/引用/类型/调用图/find-usings 支持 json（默认）、text；两个字段分析命令支持 text（默认）、json、markdown |
| `-o, --output <file>` | 仅 unused-fields、never-assigned-fields 支持；其他命令需要保存结果时用 shell 重定向 |

## 命令格式

### LSP 命令（基于位置）

```bash
roslyn-tool <command> <project> --file <file> --line <n> --column <n> [--format json|text]
```

### find-usings 命令

```bash
roslyn-tool find-usings <project> [--namespace <namespace>] [--type <type>] [--format json|text]
```

### 代码分析命令

```bash
roslyn-tool <command> <project> [--format json|text|markdown] [-o <file>]
```

## 快速使用示例

以下坐标指向 ServiceDefaults 的 `Extensions` 类型；执行前先检查文件，后续源码变化时更新坐标。

```powershell
$roslynTool = (Resolve-Path '.\.agents\skills\csharp-lsp\scripts\roslyn-tool.exe').Path
$serviceDefaultsProject = (Resolve-Path '.\src\backend\Trelix.ServiceDefaults\Trelix.ServiceDefaults.csproj').Path
$solutionPath = (Resolve-Path '.\Trelix.slnx').Path
$sourceFile = (Resolve-Path '.\src\backend\Trelix.ServiceDefaults\Extensions.cs').Path

rg -n 'class Extensions' $sourceFile
& $roslynTool symbol-info $serviceDefaultsProject --file $sourceFile --line 16 --column 21 --format json
& $roslynTool find-references $solutionPath --file $sourceFile --line 16 --column 21 --format json
& $roslynTool unused-fields $serviceDefaultsProject --format json
```

## 详细文档

按需读取，避免一次性加载过多内容：

- **命令速查表**：[references/commands/index.md](references/commands/index.md)
- **使用示例**：[references/examples.md](references/examples.md)

单个命令详细说明（按需读取）：
- [symbol-info](references/commands/symbol-info.md)
- [find-references](references/commands/find-references.md)
- [go-to-definition](references/commands/go-to-definition.md)
- [find-derived-types](references/commands/find-derived-types.md)
- [find-implementations](references/commands/find-implementations.md)
- [method-call-graph](references/commands/method-call-graph.md)
- [type-hierarchy](references/commands/type-hierarchy.md)
- [find-usings](references/commands/find-usings.md)
- [unused-fields](references/commands/unused-fields.md)
- [never-assigned-fields](references/commands/never-assigned-fields.md)

## 注意事项

1. **项目路径**：必须提供有效的 `.csproj`、`.sln` 或 `.slnx` 文件路径
2. **行号列号**：从 1 开始计数（与 IDE 一致）
3. **输出格式**：需要结构化结果时显式使用 `--format json`；字段分析默认 text。
4. **性能**：项目加载可能较慢，选择与问题匹配的项目范围，不假定独立 CLI 进程之间会复用工作区。
5. **结果边界**：调用图仅描述静态分析与指定深度可见的关系；未使用字段也可能被反射、序列化或生成代码访问，删除前结合实际业务核实。
6. **参数差异**：优先以本机工具的 `<command> --help` 与实测行为为准；不要给符号查询传入字段分析专有选项。
