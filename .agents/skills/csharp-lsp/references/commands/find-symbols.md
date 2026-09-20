# find-symbols

按简单名称查找源码声明，解决“不知道符号在哪个文件、哪一行”的查询入口问题。不执行重命名，不搜索纯文本或元数据中的外部 API。

## 参数与范围

```text
roslyn-tool find-symbols <project> --name <name> [--exact] [--kind all|type|method|property|field|event] [--limit 100] [--format json|text]
```

- `<project>`：`.csproj` 只搜索指定项目的声明；`.sln` / `.slnx` 搜索所有已加载的 C# 项目。
- `--name`：必填且非空白，默认按简单名称包含匹配，忽略大小写；不是正则或完整限定名。
- `--exact`：完整简单名称匹配，仍忽略大小写。
- `--kind`：默认 `all`，可按类型、方法、属性、字段、事件筛选。局部变量、参数和隐式声明不在查询范围内。
- `--limit`：默认 100，允许 1–1000，只限制输出数量，不减少项目搜索范围。
- `--format`：默认 `json`，也可用 `text`；不支持 `-o`，需要保存时使用 shell 重定向。

## 示例

以下为命令简写，实际入口见 [SKILL.md](../../SKILL.md#工具路径)。

```bash
roslyn-tool find-symbols ./MyProject.csproj --name Service --kind type
roslyn-tool find-symbols ./MySolution.slnx --name ProcessOrder --exact --kind method --limit 50
```

输出包括 `command`、`project`、`name`、`exact`、`kind`、`limit`、`searchedProjects`、`workspaceIncomplete`、`found`、`total`、`count`、`truncated` 和 `symbols`。

每个 `symbols` 项有 `name`、`kind`、`displayName`、`project`、`file`、`line`、`column`；行列从 1 开始，可用于 `symbol-info`、`find-references` 等命令。多重声明（例如 partial 类型）、方法重载和不同项目中的同名符号分别保留；先根据项目与完整显示名选择目标，不盲目取第一项。

结果按显示名、项目、文件及位置稳定排序。`total` 为匹配声明位置数，`count` 为实际返回数，`truncated: true` 表示还有未返回的匹配；不是完整搜索失败。

## 退出码与结果边界

| 退出码 | 含义 |
| --- | --- |
| 0 | 查询完成；无匹配时 `found: false`、`symbols: []` |
| 1 | 无效参数、文件不存在或查询异常；诊断在 stderr |
| 2 | 工作区加载存在诊断；仍输出可用结果，`workspaceIncomplete: true` |
| 130 | 查询被取消 |

工作区诊断也可能是非致命警告，工具保守地将结果标记为不完整。`workspaceIncomplete: false` 只表示没有收到工作区加载诊断，不代表代码无编译错误或测试通过；空结果不能代替引用分析。此退出码约定不改变旧命令的行为。
