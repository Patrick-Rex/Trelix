# Server 开发指引

先遵循 [后端公共指引](../AGENTS.md)。本文件仅维护 Server 的 HTTP、存储、认证和业务验证要求；业务规则以 [产品方案](../../../docs/product-plan.md) 为准。

## 结构与边界

- 采用轻量模块化单体；按 [目标项目结构](../../../docs/architecture.md#目标项目结构) 将认证、项目环境、文件草稿、发布、应用令牌与分发放入各自 `Features` 目录。`Program.cs` 只负责宿主装配，公共构建与依赖约定继承后端公共指引。
- 使用 Controllers 与内置 `AddOpenApi` / `MapOpenApi`；管理 API 和应用 API 的路由、认证策略及契约分别维护。HTTP 错误使用 Problem Details，通用异常处理机制放 Core 的 `Middleware`，业务错误标识与映射在 Server 定义并装配，不返回原始异常或敏感配置。
- SQLite + EF Core 10 的 DbContext、实体映射及迁移放入 `Persistence`；认证技术实现与通知等待管理放入 `Infrastructure`。目录划分不增加部署服务或多层转发类。
- Server 引用 Core 的通用技术能力和 ServiceDefaults 的宿主默认配置，AppHost 负责编排；业务逻辑、数据访问、认证授权与发布通知留在 Server。具体归属见 [职责边界](../../../docs/architecture.md#通用技术与业务基础设施边界)。

## 技能入口

- API、DTO、HTTP 语义、OpenAPI 与异常处理：读 [dotnet-webapi](../../../.agents/skills/dotnet-webapi/SKILL.md)。
- 慢 EF 查询、N+1 与查询分配：读 [optimizing-ef-core-queries](../../../.agents/skills/optimizing-ef-core-queries/SKILL.md)。它是查询优化技能，不是数据库建模规范。
- 可观测性修改同时遵循 [ServiceDefaults 指引](../Trelix.ServiceDefaults/AGENTS.md)。

## API 与业务代码

- Controller 处理 HTTP 边界，复杂业务逻辑由用例服务承担；用例服务可直接使用 DbContext 并控制事务，不再包装通用仓储。按业务能力组织代码，接口在存在替换、测试隔离或稳定契约需求时引入，不机械地为每个类复制接口；模块之间通过明确服务方法协作，不调用其他模块的 Controller。
- 请求/响应 DTO 与持久化实体分离；采用明确类型、正确状态码与可识别错误，新增端点同步维护 OpenAPI 元数据和 `.http` 调用样例。

## SQLite 与配置数据

- 采用 EF Core 10 的 SQLite provider；实体与迁移按实际 SQLite 能力验证，不套用 SQL Server 的 schema、sequence 或数据库生成 rowversion 示例。
- 查询优先投影所需字段；只读实体查询按需使用 `AsNoTracking`，避免 N+1 和无界结果集。
- SQLite 的并发写入和类型操作有限制；时间、版本字段的持久化映射与索引应可在真实 SQLite 上排序和比较，不能从 API 的 DateTimeOffset 类型直接推断存储方案。
- 配置按 Project → Env → ConfigFile 组织，正文统一保存 JSON；JSON/YAML/Tree 视图转换由管理界面承担，服务端只接收并校验 JSON。
- 保存仅更新草稿，应用仅读取已发布版本；回滚生成新发布版本。遵循产品方案中的版本和事务规则，保存、发布、回滚均实施并发保护，不能最后写入覆盖。
- 管理 API 与应用读取/监听 API 分离；只有发布事务提交成功后才发送变更通知，保存草稿不通知应用。
- 数据库路径、持久化目录和备份恢复遵循 [生产部署](../../../docs/deployment.md)；首版为 Linux Docker 单容器，Server 统一提供静态资源与 API，SQLite 挂载持久化。本地数据库文件、日志和真实配置不应进入源码管理。
- 配置解析与校验在服务端执行；只处理数据，不执行配置内容中的代码。

## 认证与授权

- 以 [产品方案](../../../docs/product-plan.md#首版认证与访问控制) 为规则来源；首版认证在 Server 内实现，不引入外部 IAM、OIDC/SSO 或 OAuth 令牌签发服务。
- 内置管理员使用安全密码哈希校验与 ASP.NET Core Cookie 认证；初始化凭证只在没有管理员时创建首个账号。管理写操作执行防伪造校验，登录 Cookie 使用 HttpOnly，生产环境使用 HTTPS 和 Secure Cookie。
- 首版只有一个内置管理员，管理全部项目与环境，不扩展用户、角色或 RBAC。
- 管理 API 与应用读取/监听 API 使用明确分离的认证方案和授权策略；应用只读令牌不能成为管理接口的身份凭证。
- 应用令牌使用高强度随机字符串及服务端校验摘要，通过 Bearer 请求头传递；按令牌有效期、撤销状态和项目/环境授权校验，不把 Bearer 等同于 JWT 验证。
- 读取和长轮询均校验权限，仅返回授权范围内的已发布配置；不能将应用令牌用于读取草稿或执行管理写操作。
- 长轮询遵循 [监听流程](../../../docs/architecture.md#单实例长轮询)：先核对持久化发布身份，注册等待后再次核对，等待结束重新验证授权与发布身份，等待期间不持有数据库事务。
- API 返回 401/403 表达认证失败或授权不足，不把登录页面或 SSO 跳转作为配置 API 响应。密码、令牌、Cookie 和完整认证请求头不得进入日志、错误响应或遥测。

## 验证

- 从项目根目录构建：`dotnet build .\src\backend\Trelix.Server\Trelix.Server.csproj --nologo -v:q -clp:ErrorsOnly`，只读取 error 与退出码。
- 前后端联合验证与构建边界见 [本地开发](../../../docs/local-development.md#构建与验证)。
- 天气示例接口已移除；当前 `.http` 样例使用 ServiceDefaults 在 Development 环境提供的 `/health` 与 `/alive` 验证服务启动。
- API 改动验证正确与错误路径；涉及存储时验证实际 SQLite 行为，涉及契约时验证序列化与返回结构。
- 认证改动验证登录、退出、会话失效、管理写操作防伪造校验，以及应用令牌过期、撤销、越权和管理接口隔离；读取与长轮询均覆盖。
- 配置业务验证覆盖草稿隔离、并发冲突、发布事务与回滚生成新版本；完整场景见 [质量与验收](../../../docs/quality.md)。
