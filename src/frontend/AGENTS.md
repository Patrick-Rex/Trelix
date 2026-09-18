# 前端开发指引

先遵循 [根指引](../../AGENTS.md)、[技术基线](../../docs/development-baseline.md) 与 [产品方案](../../docs/product-plan.md)。跨模块职责见 [架构设计](../../docs/architecture.md)，工作区验收见 [AC-07](../../docs/quality.md#ac-07-编辑工作区)。

## 结构与技术

- 按 [目标项目结构](../../docs/architecture.md#目标项目结构) 组织 `pages`、`features`、`shared` 和测试；入口与页面负责组合，业务状态和交互留在对应功能目录。
- 业务代码使用 TypeScript、Composition API、`<script setup lang="ts">`，页面使用 Element Plus，配置编辑使用 Monaco。
- 已接入 TypeScript、vue-tsc、tsconfig 和 ESLint TypeScript 支持；新增业务代码纳入类型检查，构建先检查类型再打包。
- 目标界面为 IDE 风格工作区：环境切换器、项目/文件资源树、编辑区及保存、发布、历史工具栏；保存草稿与发布是不同操作。
- 首版管理界面采用单个内置管理员 + Cookie 登录，包含登录、退出、会话失效提示和应用只读令牌管理，不实现多用户或 RBAC 界面。
- 沿用 Node.js 24、npm 与 `package-lock.json`，不另建其他包管理器的锁文件。`.esproj` 的 Visual Studio JavaScript SDK 版本在根目录 `global.json` 统一维护；根目录 C# 构建属性及 NuGet CPM 不应用于前端项目。

## 技能入口

- Vue 组件、响应式逻辑和 Vite 集成：读 [vue-best-practices](../../.agents/skills/vue-best-practices/SKILL.md)，并读其中要求的四份基础参考。
- Vue 异常与响应式故障：读 [vue-debug-guides](../../.agents/skills/vue-debug-guides/SKILL.md)。
- 可复用组合式函数：读 [create-adaptable-composable](../../.agents/skills/create-adaptable-composable/SKILL.md)，只按真实复用需求设计参数。
- 组件和端到端测试：读 [vue-testing-best-practices](../../.agents/skills/vue-testing-best-practices/SKILL.md)。
- 使用 Vue Router 或 Pinia 时读取对应技能，依赖接入遵循根指引；目录中的页面和共享状态职责不自动决定采用这些依赖。

## Vue 与界面

- 根组件和页面负责布局与组合，按职责拆分编辑区、表单和列表；状态与副作用复杂时提取 composable，避免把整项功能塞进 `App.vue`。
- 保持 props 向下、事件向上的数据流。派生状态用 computed；watch 处理副作用，并处理请求取消、乱序响应与卸载清理。
- 本地状态优先留在组件或 composable；存在跨页面共享需求时再讨论状态管理方案。
- 使用 Element Plus 已有能力实现表单、表格、弹窗等；业务规则留在业务代码中，不依赖组件内部实现。
- 编辑、保存失败、校验失败、加载中与空数据应有清楚的界面状态；错误提示不得泄露配置中的秘密。

## Monaco 集成

- 将 Monaco 封装为职责明确的组件/组合式函数，业务页面通过文本、语言、只读状态及变更事件交互。
- 编辑器、model 和第三方复杂实例不要放入深层响应式代理；按需求使用普通变量、`shallowRef` 或 `markRaw`。
- 在 DOM 可用时初始化，明确 model 的所有权；释放由本组件持有的编辑器、model 和事件订阅，共享 model 不重复释放。
- 配置切换时明确处理未保存内容、撤销栈和编辑位置，避免响应式同步反复 `setValue` 覆盖用户输入。
- 按当前 Monaco/Vite 官方文档接入 ESM 和所需语言 worker；优先延迟加载编辑器，验证开发环境及生产构建的 worker 路径。
- JSON/YAML/Tree 共用一份文档状态，按产品方案管理当前文本、有效 JSON 数据、校验结果和修订信息；通过统一动作更新，禁止视图互相循环监听。
- 保存、发布及格式切换前校验当前文本；无效输入和未完成的防抖不能导致上一份有效数据被误提交。YAML 只是视图，不承诺保存注释、锚点名称或原始排版。
- 编辑器提示不能替代服务端格式校验与权限检查。
- 验证输入、中文输入法、容器缩放、路由切换和卸载后的资源释放；真实 worker 和布局问题需要浏览器验证。

## API 与本地联调

- 管理员在登录页提交账号密码，由 Server 建立 HttpOnly Cookie 会话；管理请求按 Cookie 会话访问，写请求携带服务端要求的防伪造令牌。
- 应用只读令牌用于业务服务读取和监听配置，不作为 Web 管理界面的登录凭证；不将管理员密码、登录凭证或应用令牌持久化到浏览器本地存储。
- 明确处理管理 API 的 401 会话失效和 403 权限不足；首版不增加 OIDC/SSO 登录流程或前端 OAuth 令牌管理。
- 使用与后端约定的 DTO、Problem Details 和错误标识，按 [HTTP 边界](../../docs/architecture.md#http-与监听边界) 区分管理 API 与应用 API。
- 接入 API 时同步检查 Vite 代理路由、后端路由和 SPA fallback，API 错误不能返回管理界面的 HTML。
- 保持现有 HTTPS 开发证书和 `services__trelix-server__https__0` 服务地址配置。不得将本机端口和开发证书写成生产依赖。
- 生产构建产物由 Server 在 Linux Docker 单容器中统一提供，遵循 [生产部署](../../docs/deployment.md)；Vite 代理和开发服务器不构成生产部署能力。
- 统一启动、端口分配和联调步骤见 [本地开发](../../docs/local-development.md#统一启动与联调)，前端独立启动见 [前端 README](README.md#独立启动)。

## 命令与验证

在 `src/frontend` 目录运行：

- 安装锁定依赖：`npm ci`。
- 本地启动：`npm run dev`。
- 生产构建：`npm run build`；先执行类型检查再打包，按根指引只读取构建 error 与退出码。
- 独立类型检查：`npm run type-check`，覆盖 `src` 下的 TypeScript 与 Vue 文件。
- 无修改的 lint 检查：`npm exec -- oxlint .` 与 `npm exec -- eslint .`，必要时仅指定修改文件。
- `npm run lint` 的现有脚本带 `--fix`，会修改文件；不要当作只读检查运行。
- 当前没有 `test` 或 `test:e2e` 脚本；未接入的检查不能报告通过。应用壳检查侧栏展开/收起、键盘访问和窄屏布局；编辑功能接入后验证无效输入、格式切换、保存前立即输入、并发冲突及文件切换时的未保存内容。
