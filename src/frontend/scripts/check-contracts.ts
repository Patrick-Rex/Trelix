import { readdirSync, readFileSync } from 'node:fs'
import { extname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { compileStyle, parse } from 'vue/compiler-sfc'

const root = fileURLToPath(new URL('../', import.meta.url))
const excluded = new Set(['node_modules', 'dist', 'dist-ssr', 'coverage', 'artifacts', '.git'])
const themeFile = join(root, 'src/assets/theme.css')
const failures: string[] = []
const theme = readFileSync(themeFile, 'utf8')
const tokens = new Set([...theme.matchAll(/(--[\w-]+)\s*:/g)].map((match) => match[1]))

/**
 * 枚举前端维护的文件，跳过依赖、产物与临时验收目录。
 * @param directory 当前遍历目录。
 * @returns 需要执行契约检查的文件路径。
 */
function* files(directory: string): Generator<string> {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (excluded.has(entry.name) || entry.isSymbolicLink()) continue
    const path = join(directory, entry.name)
    if (entry.isDirectory()) yield* files(path)
    else yield path
  }
}

/**
 * 记录文件级诊断，不输出业务正文。
 * @param path 违反契约的文件。
 * @param message 可用于修复的规则说明。
 */
function fail(path: string, message: string): void {
  failures.push(`${relative(root, path)}: ${message}`)
}

/**
 * 检查可解析的 CSS 声明，集中维护视觉令牌，避免组件引入独立主题。
 * @param path 样式所属文件。
 * @param source CSS 样式正文。
 */
function checkStyle(path: string, source: string): void {
  const result = compileStyle({ source, filename: path, id: 'contract-check' })
  if (result.errors.length) fail(path, '样式无法解析。')
  result.rawResult?.root.walkDecls((declaration) => {
    const { prop, value } = declaration
    if (path === themeFile) return
    if (/^--(?:color|font|radius|shadow|layout|el)-/.test(prop)) {
      fail(path, `${prop} 必须在 theme.css 定义。`)
    }
    if (/#(?:[\da-f]{3,8})\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\(/i.test(value)) {
      fail(path, `${prop} 使用了原始颜色，请引用主题变量。`)
    }
    const withoutTokens = value.replace(/var\(--[\w-]+\)/g, '').trim()
    if (/^(?:color|background(?:-color)?|border(?:-[\w]+)?|outline(?:-color)?|fill|stroke)$/.test(prop)
      && /\b[a-z]+\b/i.test(withoutTokens.replace(/\b(?:transparent|currentColor|inherit|none|solid|dashed|dotted|px|rem|em|auto)\b/gi, ''))) {
      fail(path, `${prop} 必须使用主题变量或无色/继承值。`)
    }
    if (/^(?:font-size|font-family|border-radius|box-shadow)$/.test(prop)
      && !/^(?:var\(--[\w-]+\)|inherit|none|0|50%)$/.test(value)) {
      fail(path, `${prop} 必须引用主题变量。`)
    }
    for (const match of value.matchAll(/var\((--(?:color|font|radius|shadow|layout|el)-[\w-]+)\)/g)) {
      if (!tokens.has(match[1])) fail(path, `主题变量 ${match[1]} 未定义。`)
    }
  })
}

for (const path of files(root)) {
  const extension = extname(path)
  if (/^\.(?:[cm]?js|jsx|cts|tsx)$/.test(extension)) {
    fail(path, '手写模块使用 ESM TypeScript；工具确实不支持时先记录具体兼容边界。')
  }
  if (extension === '.css') checkStyle(path, readFileSync(path, 'utf8'))
  if (extension !== '.vue') continue
  const { descriptor, errors } = parse(readFileSync(path, 'utf8'), { filename: path })
  if (errors.length) fail(path, 'Vue 单文件组件无法解析。')
  for (const script of [descriptor.script, descriptor.scriptSetup]) {
    if (script && script.lang !== 'ts') fail(path, 'Vue script 必须声明 lang="ts"。')
  }
  for (const style of descriptor.styles) checkStyle(path, style.content)
}

if (JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).type !== 'module') {
  fail(join(root, 'package.json'), '包必须声明 type: module。')
}
if (failures.length) {
  console.error(failures.join('\n'))
  process.exitCode = 1
} else {
  console.log('前端 ESM、TypeScript 与主题契约检查通过。')
}
