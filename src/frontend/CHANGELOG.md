此文件解释 Visual Studio 如何创建项目。

以下工具用于生成此项目:
- create-vite

以下为生成此项目的步骤:
- 使用 create-vite: `npm init --yes vue@latest trelix.client -- --eslint ` 创建 vue 项目。
- 更新 `vite.config.js` 以设置代理和证书。
- 更新 `HelloWorld` 组件以提取并显示天气信息。
- 创建项目文件 (`trelix.client.esproj`)。
- 创建 `launch.json` 以启用调试。
- 创建 `tasks.json` 以启用调试。
- 向解决方案添加项目。
- 将代理终结点更新为后端服务器终结点。
- 写入此文件。
