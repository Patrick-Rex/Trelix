import { parseConfigDocument, stringifyConfigDocument } from './configDocument'
import type { JsonObject } from '@/shared/api/contracts'

/** 导出和复制可明确选择的配置内容来源。 */
export type ConfigurationSource = 'editing' | 'published'
/** 本地配置文件支持的表示格式。 */
export type ConfigurationFileFormat = 'json' | 'yaml'

/** 通过文件格式和业务结构校验的本地配置。 */
export interface ImportedConfiguration {
  /** 用于向管理员标识所选本地文件的名称。 */
  name: string
  /** 不含 UTF-8 BOM 的原始编辑文本。 */
  text: string
  /** 从文件扩展名确定的编辑表示。 */
  format: ConfigurationFileFormat
  /** 已校验的共享 JSON 数据。 */
  value: JsonObject
}

/**
 * 有界读取 UTF-8 JSON/YAML 文件，校验成功后才返回候选编辑内容。
 * @param file 管理员主动选择的单个本地文件。
 * @returns 可载入编辑器的配置，不执行保存或发布。
 * @throws {Error} 文件格式、编码、大小或配置数据无效时返回不含正文的提示。
 */
export async function readConfigurationFile(file: File): Promise<ImportedConfiguration> {
  const format = /\.json$/i.test(file.name) ? 'json' : /\.ya?ml$/i.test(file.name) ? 'yaml' : null
  if (!format) throw new Error('请选择 .json、.yaml 或 .yml 文件。')
  if (file.size > 1024 * 1024) throw new Error('配置正文不能超过 1 MiB。')
  let text: string
  try {
    text = new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer())
  } catch {
    throw new Error('无法读取文件，请使用 UTF-8 编码的配置文件。')
  }
  const result = parseConfigDocument(text, format)
  if (!result.value) throw new Error(result.error ?? '配置文件内容无效。')
  return { name: file.name, text, format, value: result.value }
}

/**
 * 将有效 JSON 转成所选下载表示，并保留原始 JSON 文本。
 * @param json 已明确选定来源的 JSON 正文。
 * @param format 下载文件格式。
 * @returns 带文件末尾换行的有效配置正文。
 * @throws {Error} 正文不满足配置校验时拒绝下载。
 */
export function exportConfigurationText(json: string, format: ConfigurationFileFormat): string {
  const result = parseConfigDocument(json, 'json')
  if (!result.value) throw new Error(result.error ?? '配置内容无效。')
  const text = format === 'json' ? json : stringifyConfigDocument(result.value, 'yaml')
  const exported = text.replace(/\r\n?/g, '\n').trimEnd() + '\n'
  if (new TextEncoder().encode(exported).byteLength > 1024 * 1024) throw new Error('导出正文超过 1 MiB，请调整配置或选择其他格式。')
  return exported
}

/**
 * 生成与下载表示一致且不含路径、控制字符的文件名。
 * @param name 当前配置资源名称。
 * @param format 下载文件格式。
 * @returns 适用于浏览器下载属性的文件名。
 */
export function configurationDownloadName(name: string, format: ConfigurationFileFormat): string {
  const base = name.replace(/\.(?:json|ya?ml)$/i, '').replace(/[<>:"/\\|?*\p{Cc}]/gu, '_')
    .replace(/[. ]+$/g, '').slice(0, 220) || 'configuration'
  return `${base}.${format}`
}

/**
 * 请求浏览器下载已选定内容，并释放由本次操作创建的对象地址。
 * @param name 安全的下载文件名。
 * @param text 已校验的配置正文。
 * @param format 下载文件格式。
 */
export function downloadConfiguration(name: string, text: string, format: ConfigurationFileFormat): void {
  const blob = new Blob([text], { type: format === 'json' ? 'application/json;charset=utf-8' : 'application/yaml;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  document.body.append(link)
  try { link.click() }
  finally { link.remove(); setTimeout(() => URL.revokeObjectURL(url), 0) }
}
