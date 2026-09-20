# Trelix Web 管理界面

`src/frontend` 是 Web 管理界面的工程根目录，负责单管理员登录、项目与环境选择、JSON/YAML/Tree 编辑、草稿保存、发布、历史回滚及应用令牌管理。业务实现采用 Vue 3、Vite、TypeScript、Element Plus 和 Monaco，按 [目标项目结构](../../docs/architecture.md#目标项目结构) 分为页面、功能模块和共享能力。

项目文件为 `trelix.client.esproj`，npm 包名为 `trelix.client`。业务应用使用的 .NET SDK 位于独立的 `src/sdk/Trelix.Extensions.Configuration` 项目，与本目录职责分开。

跨前后端的环境准备、统一启动和联调见 [本地开发](../../docs/local-development.md)。产品规则见 [产品方案](../../docs/product-plan.md)，技术选择见 [技术基线](../../docs/development-baseline.md)，前端实现约束见 [前端指引](AGENTS.md)，行为验收见 [AC-07](../../docs/quality.md#ac-07-编辑工作区)。完整入口见 [文档索引](../../docs/README.md)。

首版生产构建产物随 Server 在 Linux Docker 单容器统一交付，详见 [生产部署](../../docs/deployment.md)。当前开发服务器与生产构建脚本不表示已完成容器交付。

## 当前应用壳

默认首页为配置工作区：可收起的侧边栏、顶部栏、环境选择占位和主内容空状态；窄屏使用可展开侧栏，支持 Escape 收起及键盘跳转到主内容。Vue 欢迎页、示例图标与天气请求已移除。

`App.vue` 组合 `shared/components/layout` 中的布局和 `pages/WorkspacePage.vue`，工作区空状态位于 `features/workspace/components`。采用 TypeScript、Element Plus 与按需图标导入；组件样式由 `main.ts` 显式引入。

当前没有业务 API 调用。登录、项目与环境、应用令牌和编辑器尚未接入，相关入口以禁用或说明呈现；Monaco、SQLite / EF Core 随对应功能接入。未引入 Router 或 Pinia。开发运行与生产预览的应用壳、键盘和窄屏检查见 [M1 验收记录](../../docs/verification/m1.md)。

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

独立启动时从 Vite 控制台获取 HTTPS 地址，端口可通过 `DEV_SERVER_PORT` 覆盖；后端需要另行启动。当前 `/health`、`/alive` 代理优先读取 `services__trelix-server__https__0`，未设置时使用 `vite.config.js` 中的开发回退地址；后端默认地址见其 `Properties/launchSettings.json` 的 `https` Profile。这两个端点由 Server 的 ServiceDefaults 在 Development 环境提供；可直接访问前端同源地址验证代理，页面不会自动轮询。

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

从 Vite 控制台获取预览地址；检查应用壳、侧栏展开/收起、Tab 与 Enter 键盘访问、Escape 收起以及窄屏无横向溢出。此预览用于核对前端产物，不代表 Server 静态资源集成或生产容器已经交付。

无修改的 lint 检查：

```sh
npm exec -- oxlint .
npm exec -- eslint .
```

`npm run lint` 会依次执行带 `--fix` 的 Oxlint 和 ESLint，修改文件；只读验证不使用该脚本。
