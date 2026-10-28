import { existsSync, mkdirSync, readFileSync } from 'node:fs'
import { homedir } from 'node:os'
import { join } from 'node:path'
import { env } from 'node:process'
import { spawnSync } from 'node:child_process'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import type { ConfigEnv, ServerOptions, UserConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * 仅为开发服务器准备 HTTPS 证书与后端代理，构建和预览不导出证书。
 * @returns Vite 开发服务器的显式配置。
 * @throws 无法创建开发证书时终止启动。
 */
function developmentServer(): ServerOptions {
  const baseFolder = env.APPDATA
    ? join(env.APPDATA, 'ASP.NET', 'https')
    : join(homedir(), '.aspnet', 'https')
  const certFilePath = join(baseFolder, 'trelix.client.pem')
  const keyFilePath = join(baseFolder, 'trelix.client.key')
  if (!existsSync(baseFolder)) mkdirSync(baseFolder, { recursive: true })
  if (!existsSync(certFilePath) || !existsSync(keyFilePath)) {
    const result = spawnSync('dotnet', [
      'dev-certs', 'https', '--export-path', certFilePath, '--format', 'Pem', '--no-password',
    ], { stdio: 'inherit' })
    if (result.status !== 0) throw new Error('Could not create certificate.')
  }
  return {
    proxy: {
      '^/(api|health|alive)(?:[/?]|$)': {
        target: env['services__trelix-server__https__0'] ?? 'https://localhost:7112',
        secure: false,
      },
    },
    port: Number.parseInt(env.DEV_SERVER_PORT || '58302', 10),
    https: { key: readFileSync(keyFilePath), cert: readFileSync(certFilePath) },
  }
}

/**
 * 按运行模式组合配置，将开发环境副作用限制在开发服务器启动阶段。
 * @param environment Vite 当前命令及预览模式。
 * @returns 浏览器 ESM 构建和开发服务器共用的配置。
 */
export default defineConfig((environment: ConfigEnv): UserConfig => ({
  plugins: [vue()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  build: { target: 'es2022' },
  worker: { format: 'es' },
  server: environment.command === 'serve' && !environment.isPreview ? developmentServer() : undefined,
}))
