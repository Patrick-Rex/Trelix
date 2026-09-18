# ServiceDefaults 开发指引

先遵循 [后端公共指引](../AGENTS.md)。本文件仅维护公共运行能力的实现边界和验证要求；跨模块依赖见 [架构设计](../../../docs/architecture.md#模块职责与依赖方向)，信息边界验收见 [AC-10](../../../docs/quality.md#ac-10-可观测性与信息边界)。

- 当前 `Extensions.cs` 统一配置 OpenTelemetry、健康检查、服务发现和 HttpClient 弹性；维持宿主默认配置的职责，其他通用技术基础设施按 [职责边界](../../../docs/architecture.md#通用技术与业务基础设施边界) 归入 Core。
- 不引入 Server 业务类型、配置实体、DbContext 或管理界面依赖，避免循环引用。
- 修改追踪、指标、OTLP 或日志关联时读取 [configuring-opentelemetry-dotnet](../../../.agents/skills/configuring-opentelemetry-dotnet/SKILL.md)。
- 现有依赖和 `AddServiceDefaults()` 已完成公共注册，不能照搬技能示例在 Server 再添加一套 provider、instrumentation 或 exporter。
- 按环境配置决定 OTLP 导出，避免硬编码收集器地址。健康检查目前仅在 Development 映射，改变暴露范围需结合部署与认证设计。
- 遥测使用配置标识、操作结果和耗时等信息，不采集配置正文、凭证或敏感标签。避免按配置值生成高基数指标。
- 调整 HttpClient 重试时评估写操作的幂等性与取消传播，避免重复发布或重复变更。
- 配置长轮询的超时、重试与取消必须匹配监听协议；不能直接套用短请求超时，也不能为客户端 SDK 引入对 Server 业务代码的依赖。
- 改动后从根目录执行 `dotnet build .\src\backend\Trelix.ServiceDefaults\Trelix.ServiceDefaults.csproj --nologo -v:q -clp:ErrorsOnly`，只读取 error 与退出码；涉及公共注册时额外验证 Server 启动。
