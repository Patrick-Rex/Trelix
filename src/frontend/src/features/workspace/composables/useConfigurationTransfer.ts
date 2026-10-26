import { computed, onBeforeUnmount, shallowRef, watch } from 'vue'
import { ApiError } from '@/shared/api/http'
import * as api from '@/shared/api/workspace'
import type { useWorkspace } from './useWorkspace'
import { configurationDownloadName, downloadConfiguration, exportConfigurationText } from '../utils/configTransfer'
import type { ConfigurationFileFormat, ConfigurationSource, ImportedConfiguration } from '../utils/configTransfer'

/** 配置传输依赖的文档动作及当前项目环境快照。 */
type TransferWorkspace = Pick<ReturnType<typeof useWorkspace>,
  'currentFile' | 'currentProject' | 'currentEnvironment' | 'environments' | 'loading' | 'loadingFile' |
  'saving' | 'publishing' | 'importDocument' | 'readConfigurationJson'>

/**
 * 管理单文件导入、导出、跨环境复制及过期操作隔离。
 * @param workspace 提供文档校验和资源选择的工作区。
 * @returns 对话框状态、操作反馈及显式传输动作。
 */
export function useConfigurationTransfer(workspace: TransferWorkspace) {
  const importOpen = shallowRef(false)
  const exportOpen = shallowRef(false)
  const copyOpen = shallowRef(false)
  const busy = shallowRef(false)
  const error = shallowRef<string | null>(null)
  const message = shallowRef('')
  const disabled = computed(() => !workspace.currentFile.value || workspace.loading.value || workspace.loadingFile.value
    || workspace.saving.value || workspace.publishing.value || busy.value)
  let generation = 0
  let controller: AbortController | undefined

  /** 资源切换后关闭旧表单并取消只读请求，避免操作结果覆盖新的文件。 */
  watch(() => [workspace.currentProject.value?.id, workspace.currentEnvironment.value?.id, workspace.currentFile.value?.id], reset)
  /** 卸载时清理只读请求并隔离已经发送的写请求结果。 */
  onBeforeUnmount(reset)

  /** 取消过期读取并清理当前文件的传输交互。 */
  function reset(): void {
    generation++
    controller?.abort()
    importOpen.value = false
    exportOpen.value = false
    copyOpen.value = false
    busy.value = false
    error.value = null
    message.value = ''
  }

  /**
   * 打开选定的传输表单，保留工作区编辑内容。
   * @param kind 本次传输操作。
   */
  function open(kind: 'import' | 'export' | 'copy'): void {
    if (disabled.value) return
    error.value = null
    message.value = ''
    importOpen.value = kind === 'import'
    exportOpen.value = kind === 'export'
    copyOpen.value = kind === 'copy'
  }

  /**
   * 在确认后将校验成功的文件载入当前编辑区，不执行持久化。
   * @param configuration 通过读取校验的本地文件。
   * @returns 载入或取消后兑现。
   */
  async function importConfiguration(configuration: ImportedConfiguration): Promise<void> {
    if (disabled.value) return
    const id = generation
    busy.value = true
    error.value = null
    try {
      const loaded = await workspace.importDocument(configuration)
      if (id !== generation) return
      if (loaded) {
        importOpen.value = false
        message.value = `已载入“${configuration.name}”，请保存草稿后按需发布。`
      }
    } catch (failure) {
      if (id === generation) error.value = failureText(failure)
    } finally {
      if (id === generation) busy.value = false
    }
  }

  /**
   * 校验明确选择的内容并请求浏览器下载，保持源文件状态。
   * @param source 当前编辑内容或当前已发布快照。
   * @param format 下载文件格式。
   * @returns 下载发起或失败后兑现。
   */
  async function exportConfiguration(source: ConfigurationSource, format: ConfigurationFileFormat): Promise<void> {
    const file = workspace.currentFile.value
    if (!file || disabled.value) return
    const id = generation
    busy.value = true
    error.value = null
    controller = new AbortController()
    try {
      const json = await workspace.readConfigurationJson(source, controller.signal)
      if (id !== generation) return
      const name = configurationDownloadName(file.name, format)
      downloadConfiguration(name, exportConfigurationText(json, format), format)
      exportOpen.value = false
      message.value = `已请求下载“${name}”。`
    } catch (failure) {
      if (id === generation) error.value = failureText(failure)
    } finally {
      if (id === generation) busy.value = false
    }
  }

  /**
   * 在同项目其他环境原子创建新文件及草稿，冲突时保留表单供改名。
   * @param source 配置内容来源。
   * @param environmentId 目标环境标识。
   * @param name 目标环境内的新文件名。
   * @returns 新建草稿成功或失败后兑现。
   */
  async function copyConfiguration(source: ConfigurationSource, environmentId: string, name: string): Promise<void> {
    const project = workspace.currentProject.value
    const target = workspace.environments.value.find((environment) => environment.id === environmentId)
    if (!project || disabled.value) return
    error.value = null
    if (!target || target.projectId !== project.id || target.id === workspace.currentEnvironment.value?.id) {
      error.value = '请选择当前项目中的其他环境。'
      return
    }
    if (!name.trim() || name.length > 256) { error.value = '文件名不能为空或仅含空白，且最多 256 个字符。'; return }
    const id = generation
    busy.value = true
    controller = new AbortController()
    try {
      const json = await workspace.readConfigurationJson(source, controller.signal)
      if (id !== generation) return
      await api.createFile(project.id, target.id, name, json)
      if (id !== generation) return
      copyOpen.value = false
      message.value = `已在“${target.displayName}”创建“${name}”草稿，请切换到目标环境检查后发布。`
    } catch (failure) {
      if (id === generation) error.value = failure instanceof ApiError && failure.code === 'duplicate_resource'
        ? '目标环境已有同名文件，请修改目标文件名后重试。' : failureText(failure)
    } finally {
      if (id === generation) busy.value = false
    }
  }

  return { importOpen, exportOpen, copyOpen, busy, error, message, disabled, open, importConfiguration, exportConfiguration, copyConfiguration }
}

/**
 * 将传输错误转换为不含配置正文的操作提示。
 * @param failure 文件、校验或 HTTP 操作产生的错误。
 * @returns 可向管理员展示的错误说明。
 */
function failureText(failure: unknown): string {
  return failure instanceof Error ? failure.message : '配置操作失败，请重试。'
}
