import { parseDocument, stringify } from 'yaml'
import type { JsonObject, JsonValue } from '@/shared/api/contracts'

/** 配置编辑器使用的文本表示。 */
export type DocumentFormat = 'json' | 'yaml' | 'tree'

/** 配置正文校验结果。 */
export interface DocumentValidation {
  /** 解析成功时的 JSON 根对象。 */
  value?: JsonObject
  /** 可安全显示的错误说明。 */
  error?: string
}

/**
 * 校验配置文档，并返回共享 JSON 数据或不含正文的错误。
 * @param text 编辑器当前文本。
 * @param format 文本使用 JSON 或 YAML 表示。
 * @returns JSON 数据或格式校验错误。
 */
export function parseConfigDocument(text: string, format: Exclude<DocumentFormat, 'tree'>): DocumentValidation {
  if (new TextEncoder().encode(text).byteLength > 1024 * 1024) return { error: '配置正文不能超过 1 MiB。' }
  try {
    const value: unknown = format === 'json' ? parseJsonWithDuplicateChecks(text) : parseYaml(text)
    return validateJsonObject(value)
  } catch (error) {
    if (error instanceof DocumentSyntaxError) return { error: error.message }
    return { error: format === 'json' ? jsonSyntaxMessage(error, text) : 'YAML 无法转换为 JSON，请检查语法、锚点与别名。' }
  }
}

/**
 * 校验 Tree/YAML 转换得到的原生值是否能保存为服务端接受的 JSON 对象。
 * @param value 本次操作接收的值。
 * @returns 校验成功的 JSON 对象或安全错误说明。
 */
export function validateJsonObject(value: unknown): DocumentValidation {
  if (!isJsonObject(value)) return { error: '配置根节点必须是对象。' }
  const issue = validateJsonValue(value, 0, new WeakSet<object>())
  if (issue) return { error: issue }
  try {
    const serialized = JSON.stringify(value)
    if (new TextEncoder().encode(serialized).byteLength > 1024 * 1024) return { error: '配置正文不能超过 1 MiB。' }
  } catch {
    return { error: '配置中存在无法序列化的值。' }
  }
  return { value }
}

/**
 * 按编辑表示生成稳定缩进的文本。
 * @param value 已验证的 JSON 根对象。
 * @param format 目标文本表示。
 * @returns JSON 或 YAML 正文。
 */
export function stringifyConfigDocument(value: JsonObject, format: Exclude<DocumentFormat, 'tree'>): string {
  return format === 'json' ? JSON.stringify(value, null, 2) : stringify(value, { version: '1.2', lineWidth: 0 }).trimEnd()
}

/**
 * 判断值是否为非空 JSON 根对象。
 * @param value 本次操作接收的值。
 * @returns 值是否是普通 JSON 根对象。
 */
export function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value) && Object.getPrototypeOf(value) === Object.prototype
}

/**
 * 把 JSON 值转换成紧凑形式，便于比较格式切换前后的实际配置。
 * @param value JSON 值。
 * @returns 稳定的 JSON 文本。
 */
export function canonicalJson(value: JsonValue): string {
  if (value === null || typeof value !== 'object') return JSON.stringify(value)
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(',')}]`
  const object = value as JsonObject
  return `{${Object.keys(object).sort().map((key) => `${JSON.stringify(key)}:${canonicalJson(object[key]!)}`).join(',')}}`
}

/** 表示已检查到可展示配置语法提示的内部异常。 */
class DocumentSyntaxError extends Error {}

/**
 * 按 JSON 语法递归扫描对象属性，拒绝服务端禁止的重复名和冒号。
 * @param text 当前待处理的完整文本。
 * @returns 语法和属性名检查后的 JSON 值。
 */
function parseJsonWithDuplicateChecks(text: string): unknown {
  const value: unknown = JSON.parse(text)
  const index = { current: 0 }
  scanJsonValue(text, index, 0)
  skipWhitespace(text, index)
  if (index.current !== text.length) throw new DocumentSyntaxError('JSON 格式错误。')
  return value
}

/**
 * 递归扫描一个 JSON 值，并在每个对象内执行服务端同等的属性名检查。
 * @param text 当前待处理的完整文本。
 * @param index 扫描位置，处理后会原地推进。
 * @param depth 当前节点的嵌套深度。
 */
function scanJsonValue(text: string, index: { current: number }, depth: number): void {
  skipWhitespace(text, index)
  if (depth > 64) throw new DocumentSyntaxError('配置嵌套深度不能超过 64 层。')
  const current = text[index.current]
  if (current === '{') {
    index.current++
    skipWhitespace(text, index)
    const keys = new Set<string>()
    if (text[index.current] === '}') { index.current++; return }
    while (index.current < text.length) {
      skipWhitespace(text, index)
      const key = scanJsonString(text, index)
      if (key.includes(':') || keys.has(key.toLowerCase())) {
        throw new DocumentSyntaxError('对象属性名不能包含冒号，也不能重复（不区分大小写）。')
      }
      keys.add(key.toLowerCase())
      skipWhitespace(text, index)
      index.current++
      scanJsonValue(text, index, depth + 1)
      skipWhitespace(text, index)
      if (text[index.current] === '}') { index.current++; return }
      index.current++
    }
  } else if (current === '[') {
    index.current++
    skipWhitespace(text, index)
    if (text[index.current] === ']') { index.current++; return }
    while (index.current < text.length) {
      scanJsonValue(text, index, depth + 1)
      skipWhitespace(text, index)
      if (text[index.current] === ']') { index.current++; return }
      index.current++
    }
  } else if (current === '"') {
    scanJsonString(text, index)
  } else {
    while (index.current < text.length && !',]} \t\r\n'.includes(text[index.current]!)) index.current++
  }
}

/**
 * 读取 JSON 字符串并返回解码后的 Unicode 文本。
 * @param text 当前待处理的完整文本。
 * @param index 扫描位置，处理后会原地推进。
 * @returns 解码后的 JSON 字符串。
 */
function scanJsonString(text: string, index: { current: number }): string {
  const start = index.current
  index.current++
  while (index.current < text.length) {
    const character = text[index.current++]
    if (character === '\\') index.current++
    else if (character === '"') break
  }
  return JSON.parse(text.slice(start, index.current)) as string
}

/**
 * 跳过 JSON 语法允许的空白。
 * @param text 当前待处理的完整文本。
 * @param index 扫描位置，处理后会原地推进。
 */
function skipWhitespace(text: string, index: { current: number }): void {
  let character = text[index.current]
  while (character !== undefined && ' \t\r\n'.includes(character)) {
    index.current++
    character = text[index.current]
  }
}

/**
 * 将 YAML 文档解析为原生值；拒绝解析器报告的问题。
 * @param text 当前待处理的完整文本。
 * @returns YAML 转换得到的原生值。
 */
function parseYaml(text: string): unknown {
  const document = parseDocument(text, { version: '1.2', schema: 'core', uniqueKeys: true })
  if (document.errors.length > 0) {
    const position = document.errors[0]?.linePos?.[0]
    const suffix = position ? `（第 ${position.line} 行，第 ${position.col} 列）` : ''
    throw new DocumentSyntaxError(`YAML 格式错误${suffix}。`)
  }
  return document.toJS({ maxAliasCount: 100 }) as unknown
}

/**
 * 校验 JSON 可表示值、属性名、嵌套深度和引用环。
 * @param value 本次操作接收的值。
 * @param depth 当前节点的嵌套深度。
 * @param seen 当前遍历路径的引用集合，用于识别循环。
 * @returns 发现的安全错误说明；合法时为 undefined。
 */
function validateJsonValue(value: unknown, depth: number, seen: WeakSet<object>): string | undefined {
  if (depth > 64) return '配置嵌套深度不能超过 64 层。'
  if (value === null || typeof value === 'string' || typeof value === 'boolean') return undefined
  if (typeof value === 'number') return Number.isFinite(value) ? undefined : '数值必须是有限的 JSON 数字。'
  if (typeof value !== 'object') return '配置只能包含 JSON 支持的数据类型。'
  if (seen.has(value)) return '配置中存在循环引用，不能保存为 JSON。'
  seen.add(value)
  if (Array.isArray(value)) {
    for (const item of value) {
      const issue = validateJsonValue(item, depth + 1, seen)
      if (issue) return issue
    }
  } else {
    const keys = new Set<string>()
    for (const [key, child] of Object.entries(value)) {
      const folded = key.toLowerCase()
      if (key.includes(':') || keys.has(folded)) return '对象属性名不能包含冒号，也不能重复（不区分大小写）。'
      keys.add(folded)
      const issue = validateJsonValue(child, depth + 1, seen)
      if (issue) return issue
    }
  }
  seen.delete(value)
  return undefined
}

/**
 * 生成不包含配置内容的 JSON 语法错误及行列位置。
 * @param error 需要分类或转换为提示的异常。
 * @param text 当前待处理的完整文本。
 * @returns 不包含正文的语法错误与可用行列位置。
 */
function jsonSyntaxMessage(error: unknown, text: string): string {
  const message = error instanceof Error ? error.message : ''
  const position = message.match(/position\s+(\d+)/i)?.[1]
  if (!position) return 'JSON 格式错误，请检查逗号、引号和括号。'
  const offset = Number(position)
  const before = text.slice(0, offset)
  const line = before.split('\n').length
  const column = offset - before.lastIndexOf('\n')
  return `JSON 格式错误（第 ${line} 行，第 ${column} 列）。`
}
