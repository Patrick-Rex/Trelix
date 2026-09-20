---
name: csharp-lsp
description: >-
  使用随仓库提供的 Roslyn CLI 按名称查找 C# 符号，分析引用、定义、接口实现、类型层级、方法调用图和可疑字段，
  用于理解代码及重构前的影响分析。提供语义查询，不执行重命名或修改源代码；
  普通文件路径与文本搜索使用 rg，不用于 Vue/TypeScript 分析。
---

# C# LSP 技能

提供基于 Roslyn 的 C# 代码分析能力，支持符号信息查询、引用查找、类型层级分析等功能。
通过随仓库提供的 CLI 执行，不会自动注册为编辑器 LSP 服务或 Codex MCP 工具。

## Trelix 使用约定

- 从包含 `Trelix.slnx` 的项目根目录运行；不要因 Git 顶层位于父目录而扫描其他项目。
- 单项目分析优先传入相关 `.csproj`；跨项目引用分析使用 `Trelix.slnx`，并明确查询覆盖范围。
- 只知道名称时用 `find-symbols` 获取声明位置；已知文件时用 `rg -n` 或读取源码核实当前行列。位置必须落在目标标识符上，编辑后重新查询。
- 项目和源码参数优先使用 `Resolve-Path` 得到的绝对路径，避免工作目录影响文件定位。
- 与 `csharp-refactoring` 配合时，本技能负责语义查询和影响分析；实际编辑及行为验证遵循重构技能与项目指引。
- 工具失败或工作区加载不完整时说明限制；搜索不到符号或引用不能直接推断“没有使用”。可先检查项目路径、SDK、依赖还原和加载错误。
- `find-references` 的结果可能包含 `isDefinition: true` 的定义项，`count` 也包含它；统计调用/使用数量时排除定义项。
- 若确需编译，遵循项目“只读取 error 与退出码”的要求；语义分析输出照常读取，不能当作编译或测试通过的证据。

## 工具路径

本目录仅随 Git 跟踪预编译工具与技能文档，不包含工具工程、源码或按需构建脚本。当前产物为 Windows x64 的 `scripts/roslyn-tool.exe`，版本 `1.1.0`，大小 22,562,324 字节（约 21.52 MiB）；使用本机 .NET 10 运行时，进行项目分析还需要符合根目录 `global.json` 的 .NET SDK。被分析项目的依赖需先还原。

SHA-256：`138E86C12D956ED3C7B4BFB1909A69ECA51172A2F6796E584E2C4646FC598CEB`。源码、验证脚本和手动打包说明在独立 `csharp-lsp` 工程维护，更新时仅同步发布 EXE 与本技能文档。

Roslyn 与 MSBuild BuildHost 依赖已打入 EXE，工具启动无需从源码编译或恢复自己的 NuGet 包。首次运行会把内嵌依赖解包到运行时缓存（Windows 默认为 `%TEMP%/.net`），需有可写空间；缓存和 .NET SDK 的磁盘占用不计入 EXE 大小。

在项目根目录执行；从其他目录运行时改用实际技能目录的绝对路径。此 EXE 不适用于 Linux/macOS；其他平台需要从独立源码工程发布对应 RID 的产物，不能将 Windows EXE 改名后使用。缺少匹配工具或 SDK 时说明限制，不自动下载替代二进制。

```powershell
# Windows / PowerShell
$roslynTool = (Resolve-Path '.\.agents\skills\csharp-lsp\scripts\roslyn-tool.exe').Path
& $roslynTool --help
& $roslynTool find-symbols --help
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

### 查找命令（无需行列）

| 命令 | 说明 | 必要参数 |
|------|------|----------|
| `find-symbols` | 按简单名称搜索源码类型或成员，返回声明位置 | project, name；exact、kind、limit 可选 |
| `find-usings` | 查找使用某命名空间/类型的文件 | project；按需求指定 namespace 或 type，也可同时指定 |

### 代码分析命令

| 命令 | 说明 | 必要参数 |
|------|------|----------|
| `unused-fields` | 查找未使用的 private 字段 | project |
| `never-assigned-fields` | 查找未赋值的非 public 字段 | project |

## 通用参数

| 参数 | 说明 |
|------|------|
| `<project>` | 位置查询和 find-symbols 支持 `.csproj`、`.sln`、`.slnx`；字段分析仅 `.csproj`；find-usings 建议使用 `.csproj` |
| `--name <name>` | find-symbols 必填，按简单名称进行不区分大小写的包含匹配 |
| `--exact` / `--kind <kind>` / `--limit <n>` | find-symbols 专用：完整名称匹配（仍忽略大小写）、符号种类、输出上限（默认 100，最大 1000） |
| `--file <file>` | 文件路径（LSP 命令必填） |
| `--line <n>` | 行号，从 1 开始 |
| `--column <n>` | 列号，从 1 开始 |
| `--namespace <ns>` | 命名空间筛选（find-usings 使用，可与 type 单独或组合使用） |
| `--type <type>` | 类型名（find-usings 可选） |
| `--depth <n>` | 递归深度（method-call-graph、type-hierarchy） |
| `--format <format>` | 符号/引用/类型/调用图/find-usings 支持 json（默认）、text；两个字段分析命令支持 text（默认）、json、markdown |
| `-o, --output <file>` | 仅 unused-fields、never-assigned-fields 支持；其他命令需要保存结果时用 shell 重定向 |

## 命令格式

### find-symbols 命令

```bash
roslyn-tool find-symbols <project> --name <name> [--exact] [--kind all|type|method|property|field|event] [--limit 100] [--format json|text]
```

`.csproj` 仅搜索该项目的声明，不包含引用项目或元数据符号；解决方案搜索所有已加载 C# 项目。`total` 是匹配声明位置总数，`count` 是实际返回数量，`truncated` 标记输出截断；partial 类型的不同声明位置分别计数。`workspaceIncomplete: true` 表示加载存在诊断，结果不能视为完整。退出码：0 成功（允许无匹配）、1 参数或执行错误、2 工作区有诊断但返回部分结果、130 取消。这些约定仅适用于新命令；旧命令还需检查 stderr 和结果字段。

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

先按名称定位 ServiceDefaults 的 `Extensions` 类型，再把返回位置传入原有命令；不要硬编码可能随编辑失效的坐标。

```powershell
$roslynTool = (Resolve-Path '.\.agents\skills\csharp-lsp\scripts\roslyn-tool.exe').Path
$serviceDefaultsProject = (Resolve-Path '.\src\backend\Trelix.ServiceDefaults\Trelix.ServiceDefaults.csproj').Path
$solutionPath = (Resolve-Path '.\Trelix.slnx').Path
$json = & $roslynTool find-symbols $serviceDefaultsProject --name Extensions --exact --kind type --format json
if ($LASTEXITCODE -ne 0) { throw '符号查询失败或工作区加载不完整，请检查 stderr。' }
$result = $json | ConvertFrom-Json
if ($result.count -ne 1) { throw '需要根据项目和文件选择唯一声明。' }
$symbol = $result.symbols[0]
& $roslynTool symbol-info $serviceDefaultsProject --file $symbol.file --line $symbol.line --column $symbol.column --format json
& $roslynTool find-references $solutionPath --file $symbol.file --line $symbol.line --column $symbol.column --format json
& $roslynTool unused-fields $serviceDefaultsProject --format json
```

## 详细文档

按需读取，避免一次性加载过多内容：

- **命令速查表**：[references/commands/index.md](references/commands/index.md)
- **使用示例**：[references/examples.md](references/examples.md)

单个命令详细说明（按需读取）：

- [find-symbols](references/commands/find-symbols.md)
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

1. **项目路径**：支持范围见通用参数；`find-usings` 对解决方案仅分析第一个加载项目，不可当作全解决方案扫描，建议逐个 `.csproj` 查询。
2. **行号列号**：从 1 开始计数（与 IDE 一致）
3. **输出格式**：需要结构化结果时显式使用 `--format json`；字段分析默认 text。
4. **性能**：项目加载可能较慢，选择与问题匹配的项目范围，不假定独立 CLI 进程之间会复用工作区。
5. **结果边界**：调用图仅描述静态分析与指定深度可见的关系；未使用字段也可能被反射、序列化或生成代码访问，删除前结合实际业务核实。
6. **参数差异**：优先以本机工具的 `<command> --help` 与实测行为为准；不要给符号查询传入字段分析专有选项。
7. **加载诊断**：根 `Trelix.slnx` 查询可能报告不支持前端 `.esproj` 和 Aspire bundle 诊断；检查 stderr，必要时缩小到相关 C# 项目。语义查询成功不等于项目编译通过。
