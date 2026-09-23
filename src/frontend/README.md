# Trelix Web 管理界面

`src/frontend` 是 Web 管理界面的工程根目录，负责单管理员登录、项目与环境选择、JSON/YAML/Tree 编辑、草稿保存、发布、历史回滚及应用令牌管理。业务实现采用 Vue 3、Vite、TypeScript、Element Plus 和 Monaco，按 [目标项目结构](../../docs/architecture.md#目标项目结构) 分为页面、功能模块和共享能力。

项目文件为 `trelix.client.esproj`，npm 包名为 `trelix.client`。业务应用使用的 .NET SDK 计划在 M5 以独立的 `src/sdk/Trelix.Extensions.Configuration` 项目交付，与本目录职责分开。

跨前后端的环境准备、统一启动和联调见 [本地开发](../../docs/local-development.md)。产品规则见 [产品方案](../../docs/product-plan.md)，技术选择见 [技术基线](../../docs/development-baseline.md)，前端实现约束见 [前端指引](AGENTS.md)，行为验收见 [AC-07](../../docs/quality.md#ac-07-编辑工作区)。完整入口见 [文档索引](../../docs/README.md)。

首版生产构建产物随 Server 在 Linux Docker 单容器统一交付，详见 [生产部署](../../docs/deployment.md)。当前开发服务器与生产构建脚本不表示已完成容器交付。

## 当前管理界面

界面提供 Cookie 管理员登录、会话失效提示、退出、项目/环境/文件资源树、草稿编辑、发布历史、回滚及应用令牌管理。资源写入使用 API 返回的并发基准；切换资源前提示未保存内容，过期请求不能替换当前文件。

项目标题旁集中放置项目新增、重命名和删除操作；环境标题旁集中放置环境的对应操作。`WORKSPACE` 小标题独立成行，项目标题与操作按钮垂直居中，项目与环境操作区的右边缘对齐。按钮有操作名称提示，重命名和删除针对当前选中资源，加载期间禁用操作；删除入口沿用空资源限制。新建资源的名称字段留空，重命名预填原值。主导航通过底部独立工具区的图标按钮收起或展开，页面滚动时保持可见；窄屏收起后保留图标栏和底部展开入口。

选中文件后，工具栏的“复制连接配置”直接复制 `appsettings.json` 的 `Trelix` 节并打开预览。服务地址默认使用当前站点地址，开发或跨主机接入时可改为业务应用可访问的地址后再次复制。应用令牌为占位符，真实值通过 User Secrets 或 `Trelix__AccessToken` 等外部配置注入；复制失败时可从预览手动复制，未发布文件提示先发布。字段约定见 [SDK 与宿主边界](../../docs/architecture.md#sdk-与宿主边界)，SDK 接入随 M5 交付。

`App.vue` 负责会话入口和页面切换；`pages/WorkspacePage.vue` 与 `features/workspace/composables/useWorkspace.ts` 组合资源与文档状态。Monaco、JSON/YAML/Tree 编辑器、历史面板和令牌页按功能拆分。管理请求由 `shared/api` 发送同源 Cookie 与防伪造令牌。未引入 Router 或 Pinia。

Monaco 和 YAML 转换通过 `monaco-editor` 与 `yaml` npm 依赖提供；Monaco 使用 Vite ESM worker、组件级 model 生命周期和增量文本同步。工作区状态管理当前文本、有效 JSON、校验状态、草稿修订和并发基准；无效输入不会提交上一份有效数据。

Server 已接入 SQLite / EF Core 和管理员认证，首次启动后端前须完成 [外部初始化配置](../../docs/local-development.md#server-存储与首次初始化)。M4 的当前实现范围和验证边界见 [M4 进度记录](../../docs/verification/m4.md)；浏览器认证后流程按 [AC-07](../../docs/quality.md#ac-07-编辑工作区) 验收。

## Recommended IDE Setup

[VS Code](https://code.visualstudio.com/) + [Vue (Official)](https://marketplace.visualstudio.com/items?itemName=Vue.volar) (and disable Vetur).

## Recommended Browser Setup

- Chromium-based browsers (Chrome, Edge, Brave, etc.):
  - [Vue.js devtools](https://chromewebstore.google.com/detail/vuejs-devtools/nhdogjmejiglipccpnnnanhbledajbpd)
  - [Turn on Custom Object Formatter in Chrome DevTools](http://bit.ly/object-formatters)
- Firefox:
  - [Vue.js devtools](https://addons.mozilla.org/en-US/firefox/addon/vue-js-devtools/)
  - [Turn on Custom Object Formatter in Firefox DevTools](https://fxdx.dev/firefox-devtools-custom-object-formatters/)

## Customize configuration

See [Vite Configuration Reference](https://vite.dev/config/).

## 安装依赖

以下命令在 `src/frontend` 目录执行。Node.js 与 npm 要求见 [本地开发](../../docs/local-development.md#环境准备)；`package.json` 与 `.nvmrc` 声明 Node.js 主版本 24，具体补丁版本由本地或 CI 环境选择。保留 npm 与现有锁文件。

```sh
npm ci
```

## 独立启动

统一启动前后端时遵循 [本地开发](../../docs/local-development.md#统一启动与联调)。仅需独立开发前端时，在本目录运行：

```sh
npm run dev
```

独立启动时从 Vite 控制台获取 HTTPS 地址，端口可通过 `DEV_SERVER_PORT` 覆盖；后端需要另行启动。`/api`、`/health`、`/alive` 代理优先读取 `services__trelix-server__https__0`，未设置时使用 `vite.config.js` 中的开发回退地址；后端默认地址见其 `Properties/launchSettings.json` 的 `https` Profile。健康检查由 Server 的 ServiceDefaults 在 Development 环境提供；管理界面通过同源 `/api/admin` 访问会话和管理资源。

## 构建与检查

生产构建：

```sh
npm run build
```

构建先执行 `vue-tsc --noEmit`，类型检查成功后才执行 Vite 打包；按根指引只读取构建 error 与退出码。也可独立运行 `npm run type-check`。当前没有 `test` 或 `test:e2e` 脚本，`.esproj` 不声明未安装的测试框架。

构建成功后预览 `dist`：

```sh
npm run preview
```

从 Vite 控制台获取预览地址；使用本地初始化的管理员凭证检查登录、资源树、JSON/YAML/Tree 切换、保存前立即输入、发布/历史/回滚、令牌创建/轮换/撤销、401/403 与窄屏布局。真实 worker 路径和 IME、光标、撤销及未保存切换按 AC-07 验收。此预览用于核对前端产物，不代表 Server 静态资源集成或生产容器已经交付。

无修改的 lint 检查：

```sh
npm exec -- oxlint .
npm exec -- eslint .
```

`npm run lint` 会依次执行带 `--fix` 的 Oxlint 和 ESLint，修改文件；只读验证不使用该脚本。
