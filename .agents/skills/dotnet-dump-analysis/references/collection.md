# 采集与进阶诊断

仅在需要新采集或补充证据时读取。用户已授权的采集不重复确认；若只要求分析现有文件，先完成离线分析，再说明补采的原因和所需条件。

## 工具与现场信息

检查已有 `dotnet-dump --version` 和可用工具；需要安装时使用官方工具。不要为每次分析无条件更新已有版本。安装命令按实际需要选择，安装后记录版本：

```text
dotnet tool install --global dotnet-dump
dotnet tool update --global dotnet-dump
dotnet-dump --version
```

确认目标进程而非假定 PID 为 1；记录进程启动时间、采集时间、负载、实际运行时/架构、容器内外 PID 与内存限额。Trelix 有 Server、AppHost 等不同进程，不能混用快照。已有平台指标可复用，按需在 Linux 读取 `/proc/<pid>/status`、`smaps_rollup` 和对应 cgroup 指标，区分进程 RSS 与容器合计值，不读取整份进程环境变量。

## Linux 采集

优先 `dotnet-dump collect`；以下 Bash 示例中 PID 与路径均须替换为现场值，并预先保证输出目录存在、权限和容量合适：

```bash
target_pid=1234
dump_path=/var/tmp/trelix-dumps/server-t1.core
dotnet-dump collect --process-id "$target_pid" --type Full --output "$dump_path"
```

采集会暂停进程，Full/Heap 还可能因内存页调入增加压力，容器余量不足可导致 OOM。磁盘大小依 dump 类型、映射与实际采集页面而变，不能保证“1 GB RSS 等于 1 GB dump”。根据堆分析需要选择 Full/Heap，保留现场原始快照。[采集参数与影响](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump)

直接使用 createdump 时，从**目标进程实际加载的运行时目录**确定工具路径，自包含发布可能在应用目录；不要从机器所有 runtime 中排序挑最新版本。先查看该工具 `--help`。以下 Bash 变量中的版本是待替换占位符：

```bash
target_pid=1234
createdump_path=/usr/share/dotnet/shared/Microsoft.NETCore.App/REPLACE_WITH_TARGET_VERSION/createdump
dump_path=/var/tmp/trelix-dumps/server-t1.core
"$createdump_path" --full --name "$dump_path" "$target_pid"
```

createdump 的 `--full`/`-u` 表示完整转储，`--name`/`-f` 接收文件名；默认 `--withheap` 并不等于 Full。不要套用 dotnet-dump 的 `-o`。采集是进程快照操作，不应依赖其触发 GC；采集前后变化需用 GC 事件证实。[createdump 参数源码](https://github.com/dotnet/runtime/blob/main/src/coreclr/debug/createdump/createdumpmain.cpp)

Linux 失败时按错误检查用户身份、PID/文件系统命名空间、诊断端点与 `TMPDIR`、输出目录、ptrace/seccomp 限制。不要自动关闭安全策略、重启容器或修改内存限额来重试；需要改变部署条件时把具体阻碍交给用户。gcore 或系统 core 也可能可分析，但应检查堆页面是否完整，不能仅凭 ELF 魔数保证可用。[Linux 采集故障定位](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/faq-dumps)

每次使用不同文件名，成功后核实文件与采集退出码；不要覆盖唯一现场 dump。优先在预热后和异常增长时各采一份，持续增长时再补第三份，时间间隔由增长速度和影响决定，不强制固定两小时。记录期间是否重启、发布或负载变化。

## dotnet-gcdump：补充存活对象图

需要可视化堆对比且能接受影响时才选用；它会主动触发 Gen2 GC，可能长时间暂停，采集事件缓冲也占内存。它不是完整进程 dump，不能用 dotnet-dump 打开。数据丢失或采集未完成时不把报告当完整堆。

```text
dotnet tool install --global dotnet-gcdump
dotnet-gcdump collect --process-id 1234 --output server-t1.gcdump
```

可用 Visual Studio/PerfView 分析与对比。不能把它对 GC 的影响类推到 createdump。[dotnet-gcdump 文档](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump)

## dotnet-trace：区分 GC 与分配热点

先检查当前版本帮助与可用 profile。下面为 60 秒采样，输出写入诊断目录，按当前问题选择其中一种：

```text
dotnet tool install --global dotnet-trace
dotnet-trace collect --process-id 1234 --profile gc-collect --duration 00:00:01:00 --output gc-events.nettrace
dotnet-trace collect --process-id 1234 --profile gc-verbose --duration 00:00:01:00 --output gc-allocations.nettrace
```

`gc-collect` 用于观察 GC 事件，不能声称提供分配热点；`gc-verbose` 增加分配采样及开销，采样不等于所有分配。结合 Gen2 收集前后趋势与 dump 引用链定位问题，不能只凭分配频繁就断言泄漏。[dotnet-trace profile 文档](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
