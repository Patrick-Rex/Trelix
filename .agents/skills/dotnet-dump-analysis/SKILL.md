---
name: dotnet-dump-analysis
description: >-
  使用 dotnet-dump 和 SOS 分析 .NET 内存 dump、Linux coredump，排查内存持续增长、
  疑似内存泄漏、LOH/POH 占用与对象引用链；支持多时点堆统计对比及按需指导采集。
  用户要求 dump 分析、托管堆分析或 gcroot 追踪时使用；普通文本日志分析不使用此技能。
---

# .NET 内存 Dump 分析

目标是建立“增长的类型 → 保留对象的引用链 → 对应代码生命周期”的证据链，区分对象滞留、正常缓存/池化、未回收对象、碎片与托管堆外增长。用中文交付结论，无法证实的部分明确列为待验证。

## 入口与边界

- 有 dump：先只读检查用户指定文件，再执行下述分析。没有 dump 或需要补采：读取 [采集与进阶诊断](references/collection.md)。不要把离线分析请求扩展成对在线进程采集、强制 GC 或重启。
- 核实 dump 路径、采集时间/时区、目标进程与启动时间、应用版本、目标 OS/架构、实际 .NET 运行时、采集工具/类型；尽量从现有材料获取，缺失项标注未知。PID 相同不代表同一次进程运行。
- Trelix 基线是 .NET 10，但 dump 的实际运行时以证据为准。不引入参考手册中的 UFX 项目路径、Redis/Npgsql 依赖或业务假设。涉及源码时限制在 Trelix 项目边界。
- dump 可能包含配置正文、口令和令牌；保留在用户指定位置，不提交仓库或自行上传。默认输出类型、计数、大小、地址和引用关系；检查对象字段时仅提取必要信息，报告中脱敏，不批量输出字符串/数组内容。[数据敏感性说明](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dumps)

## 1. 检查环境与可分析性

先查看工具版本与帮助；缺少工具时按采集参考中的安装方法处理，不把工具未安装判为 dump 损坏。

```text
dotnet-dump --version
dotnet-dump analyze --help
```

Linux 上分析优先匹配目标架构和发行版。Windows 也支持分析 Linux dump；按官方兼容规则选择工具架构，不因 ELF 文件就断言必须换机器。加载失败时检查实际运行时、DAC、架构及 dump 完整性，必要时准备来自目标环境的匹配运行时文件/符号；不要拿本机最新运行时替代目标版本。[Linux dump 分析与跨平台要求](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-linux-dumps)

格式检查示例，按当前 shell 选择；文件头只是格式线索，不能证明 GC 堆数据完整：

```bash
file /path/to/coredump
```

```powershell
# 仅读取前 64 字节，避免将 GB 级 dump 整体读入内存；兼容 Windows PowerShell 5.1。
$dumpPath = (Resolve-Path 'C:\dumps\coredump').Path
$dumpStream = [System.IO.File]::OpenRead($dumpPath)
try {
    $header = New-Object byte[] 64
    $readCount = $dumpStream.Read($header, 0, $header.Length)
    if ($readCount -gt 0) { [System.BitConverter]::ToString($header, 0, $readCount) }
} finally { $dumpStream.Dispose() }
```

`7F-45-4C-46` 是 ELF，`4D-44-4D-50` 是 Windows minidump 标识。Linux 上 `dotnet-dump collect` 也产生 core dump，并非另一个“.NET 专用格式”。Mini/Triage 或缺页 dump 可能无法完成堆分析。

## 2. 先看摘要，再选择调查范围

下列单行命令适用于 Bash/PowerShell；替换路径。使用重复的 `-c` 顺序执行并显式退出，避免管道输入后停留在交互会话。

```text
dotnet-dump analyze "/path/to/coredump" -c "eeversion" -c "eeheap -gc" -c "exit"
dotnet-dump analyze "/path/to/coredump" -c "dumpheap -stat" -c "exit"
dotnet-dump analyze "/path/to/coredump" -c "dumpheap -stat -min 85000" -c "exit"
```

记录实际输出中的 GC heap 数量、SOH/LOH/POH 分布、allocated/committed/reserved（若提供）、主要类型 Count/TotalSize 和 Free。字段随运行时/SOS 版本变化，缺失值保留为未知。不要把 allocated 当作可达存活对象大小，也不要把 committed、RSS 和容器内存当成同一指标。全堆统计可能较慢，先执行摘要并根据堆规模缩小后续查询。

`-min 85000` 是大小过滤，不能命名为“仅 LOH 统计”。默认 LOH 阈值为 **85,000 字节**，可配置，且对象大小不等于数组元素数量。精确确认归属时使用 `gcwhere <对象地址>` 或从 `eeheap -gc` 的 LOH 区间限制 `dumpheap` 范围；可选代际参数先查当前 `help dumpheap`，不猜版本语法。[阈值配置](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector#large-object-heap-threshold)

如需保存命令输出，写到用户的诊断目录并检查退出码与 SOS 错误文本。进程退出码为 0 不能代替命令成功或堆遍历完整的判断。[dotnet-dump 命令参考](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump)

## 3. 聚焦增长类型与引用链

先根据统计选类型，不只盯大对象；大量小对象同样会滞留。以下是 SOS 会话内命令，尖括号需要替换为本份 dump 中的真实值，不直接粘贴占位符执行：

```text
dumpheap -stat -type System.Byte[]
dumpheap -type System.Byte[] -min 100000
dumpheap -mt <MethodTable> -min 100000
gcwhere <对象地址>
gcroot <对象地址>
```

`-type` 按类型名称片段筛选；从输出核实完整类型名，需要精确筛选时用本份 dump 的 MethodTable。`100000` 只是进一步缩小样本的大小阈值。对增长类型抽查不同大小/持有者的多个对象，避免只凭一个地址下结论。

`gcroot` 首次可能耗时；多个对象可在同一分析会话中查询以复用缓存。记录“根类型/线程 → 持有者类型与字段 → 集合/缓冲区 → 目标类型”。根据引用链按需执行 `dumpobj <持有者地址>`，只摘录生命周期相关字段；线程根可结合 `clrthreads`、`setthread`、`clrstack`，终结/固定相关问题可结合 `finalizequeue`、`gchandles`。使用前按需查看 `help <命令>`。[SOS 命令与引用分析](https://raw.githubusercontent.com/dotnet/diagnostics/main/src/SOS/Strike/sosdocsunix.txt)

找到根只证明快照时被引用，不证明业务上已无用途；查不到根也不能在 dump 不完整、命令失败或堆状态异常时声称“已排除泄漏”。Byte[]、String、集合 Entry[] 仅是调查线索，不能仅凭类型归因 Redis、HTTP 或数据库。

## 4. 多时点对比与结论

优先比较同一次进程运行、相近负载和相同采集方式的两份以上 dump；记录预热阶段、请求量/并发和 GC 情况。不同版本或负载必须注明可比性限制。按完整类型名及必要的模块身份对齐，**不能跨 dump 按对象地址或 MethodTable 地址对齐**。

输出对比表：类型、T1 数量/字节、T2 数量/字节、增量、持有者证据；Free 单列，不计入业务对象。数值统一字节或 MiB（1 MiB = 1,048,576 字节）。TotalSize 是对象本身大小之和，不是它们引用的完整对象图大小。

| 观察到的证据 | 可作出的判断与下一步 |
| --- | --- |
| 可比负载下，同类对象在多次 Gen2 GC 后仍增长，长期持有者持续保留业务上已过期的对象 | 支持托管对象泄漏；继续核实移除/退订/释放路径 |
| 类型统计增长，但不知道是否发生 GC、负载增大或仍在预热 | 只能称内存增长/疑似滞留；补时间线和存活证据 |
| committed 或 Free 增多，可达对象趋稳 | 考虑碎片、预留容量与池化；不能仅凭段数判泄漏 |
| RSS/容器内存增多，托管堆与类型统计稳定 | 调查 native 分配、线程栈、映射与容器统计口径；SOS 不能独自解释全部进程内存 |
| 采集或 GC 后内存下降 | 不能排除部分泄漏，也不能证明 createdump 触发了 GC |

`dumpheap` 通常还包含尚未回收的不可达对象；单次堆统计不是完整的存活性证明。没有第二份 dump 时仍完成当前类型和根分析，但明确无法证明增长趋势。泄漏判断需要结合对象用途和释放时机。[官方内存泄漏分析示例](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-memory-leak)

不要把再次 createdump 或向业务添加 `GC.Collect` 作为默认验证/修复。GC 事件与分配热点的补充采样见采集参考；诊断不能通过强制 GC 掩盖引用保留问题。

## 5. 关联代码并交付

依据持有者类型，用 `rg` 定位静态集合、单例缓存、事件订阅、计时器、后台任务、队列、池化或 Dispose 路径；需要 C# 语义查询时按项目索引使用 csharp-lsp。核对 dump 对应的构建版本，避免把当前源码猜成现场代码。修复建议应针对证实的生命周期问题；业务上何时应释放若未定义，交给用户讨论。

交付包括：结论与可信度、dump/环境及可比性、关键统计与引用链、已执行命令和失败限制、代码证据及最小下一步。明确区分已证实事实、推断与缺失证据；没有实际 dump 时仅交付操作方案，不编造分析结果。

## 来源

依据用户提供的《.NET 内存 Dump 分析操作手册》整理为项目本地技能；命令与限制于 2026-09-16 对照文中链接的 Microsoft Learn 和 dotnet 官方源码核实。执行时以现场工具帮助为准。此技能不附带诊断二进制，也不声明工具已安装。
