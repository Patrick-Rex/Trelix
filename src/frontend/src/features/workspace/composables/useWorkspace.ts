import { computed, onMounted, onUnmounted, readonly, shallowRef } from 'vue'
import type { ConfigFile, Environment, JsonObject, Project, Release } from '@/shared/api/contracts'
import { ApiError } from '@/shared/api/http'
import { confirmAction } from '@/shared/components/confirmAction'
import * as api from '@/shared/api/workspace'
import { canonicalJson, parseConfigDocument, stringifyConfigDocument, validateJsonObject } from '../utils/configDocument'
import type { DocumentFormat } from '../utils/configDocument'

const initialJson = '{\n}'

/**
 * 管理资源树、编辑缓冲区与配置发布操作。
 * @returns 只读视图状态及资源、编辑、发布操作。
 */
export function useWorkspace() {
  const projects = shallowRef<Project[]>([])
  const environments = shallowRef<Environment[]>([])
  const files = shallowRef<ConfigFile[]>([])
  const selectedProjectId = shallowRef<string | null>(null)
  const selectedEnvironmentId = shallowRef<string | null>(null)
  const selectedFileId = shallowRef<string | null>(null)
  const documentText = shallowRef(initialJson)
  const documentFormat = shallowRef<DocumentFormat>('json')
  const codeFormat = shallowRef<'json' | 'yaml'>('json')
  const documentValue = shallowRef<JsonObject>({})
  const savedValue = shallowRef<JsonObject | null>(null)
  const savedViewText = shallowRef(initialJson)
  const validationError = shallowRef<string | null>(null)
  const treeInputError = shallowRef<string | null>(null)
  const validationPending = shallowRef(false)
  const draftRevision = shallowRef(0)
  const releases = shallowRef<Release[]>([])
  const loading = shallowRef(false)
  const loadingFile = shallowRef(false)
  const saving = shallowRef(false)
  const publishing = shallowRef(false)
  const errorMessage = shallowRef<string | null>(null)
  const conflictDetected = shallowRef(false)

  const currentProject = computed(() => projects.value.find((item) => item.id === selectedProjectId.value) ?? null)
  const currentEnvironment = computed(() => environments.value.find((item) => item.id === selectedEnvironmentId.value) ?? null)
  const currentFile = computed(() => files.value.find((item) => item.id === selectedFileId.value) ?? null)
  const isDirty = computed(() => {
    if (!currentFile.value || loadingFile.value) return false
    if (validationPending.value || validationError.value) {
      if (documentFormat.value === 'tree' && validationError.value) return true
      return documentText.value !== savedViewText.value
    }
    if (!savedValue.value || draftRevision.value === 0) return true
    return canonicalJson(documentValue.value) !== canonicalJson(savedValue.value)
  })
  const isValid = computed(() => !validationPending.value && !validationError.value)
  const canPublish = computed(() => !!currentFile.value && draftRevision.value > 0 && !publishing.value && !saving.value)

  let parseTimer: ReturnType<typeof setTimeout> | undefined
  let projectController: AbortController | undefined
  let environmentController: AbortController | undefined
  let fileController: AbortController | undefined
  let historyController: AbortController | undefined
  let fileRequestId = 0

  /** 工作区挂载时读取项目资源。 */
  onMounted(() => { void loadProjects() })
  /** 工作区卸载时取消待处理读取和防抖任务。 */
  onUnmounted(() => {
    clearTimeout(parseTimer)
    projectController?.abort()
    environmentController?.abort()
    fileController?.abort()
    historyController?.abort()
  })

  /**
   * 读取项目并在可用时进入第一个项目、环境和文件。
   * @returns 异步操作完成时兑现。
   */
  async function loadProjects(): Promise<void> {
    projectController?.abort()
    const controller = new AbortController()
    projectController = controller
    loading.value = true
    errorMessage.value = null
    try {
      const result = await api.listProjects(controller.signal)
      if (controller.signal.aborted) return
      projects.value = result
      const next = projects.value.find((item) => item.id === selectedProjectId.value) ?? projects.value[0] ?? null
      selectedProjectId.value = next?.id ?? null
      if (next) await loadEnvironments(next.id, true)
      else clearSelection()
    } catch (error) {
      if (!isAbort(error)) errorMessage.value = errorText(error)
    } finally {
      loading.value = false
    }
  }

  /**
   * 切换项目并载入其资源；取消时保留当前编辑区。
   * @param projectId 项目的内部标识。
   * @returns 异步操作完成时兑现。
   */
  async function selectProject(projectId: string): Promise<void> {
    if (projectId === selectedProjectId.value || !(await confirmDiscardIfNeeded())) return
    selectedProjectId.value = projectId
    selectedEnvironmentId.value = null
    environments.value = []
    files.value = []
    clearDocument()
    await loadEnvironments(projectId, true)
  }

  /**
   * 切换环境并载入它的配置文件。
   * @param environmentId 环境的内部标识。
   * @returns 异步操作完成时兑现。
   */
  async function selectEnvironment(environmentId: string): Promise<void> {
    if (environmentId === selectedEnvironmentId.value || !(await confirmDiscardIfNeeded())) return
    selectedEnvironmentId.value = environmentId
    files.value = []
    clearDocument()
    await loadFiles(true)
  }

  /**
   * 打开一份配置草稿，忽略已取消或过期的文件响应。
   * @param fileId 配置文件的内部标识。
   * @returns 异步操作完成时兑现。
   */
  async function selectFile(fileId: string): Promise<void> {
    if (fileId === selectedFileId.value || !(await confirmDiscardIfNeeded())) return
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    if (!projectId || !environmentId) return
    clearDocument()
    selectedFileId.value = fileId
    await loadDraft(projectId, environmentId, fileId, true)
  }

  /**
   * 从管理 API 载入项目环境列表。
   * @param projectId 项目的内部标识。
   * @param selectFirst 当前选择不可用时是否自动选择首项。
   * @returns 异步操作完成时兑现。
   */
  async function loadEnvironments(projectId: string, selectFirst: boolean): Promise<void> {
    environmentController?.abort()
    const controller = new AbortController()
    environmentController = controller
    loading.value = true
    errorMessage.value = null
    try {
      const result = await api.listEnvironments(projectId, controller.signal)
      if (controller.signal.aborted || projectId !== selectedProjectId.value) return
      environments.value = result
      const next = environments.value.find((item) => item.id === selectedEnvironmentId.value)
        ?? (selectFirst ? environments.value[0] : null)
      selectedEnvironmentId.value = next?.id ?? null
      if (next) await loadFiles(selectFirst)
      else { files.value = []; clearDocument() }
    } catch (error) {
      if (!isAbort(error)) errorMessage.value = errorText(error)
    } finally {
      loading.value = false
    }
  }

  /**
   * 载入环境中的文件，并保留仍存在的当前选择。
   * @param selectFirst 当前选择不可用时是否自动选择首项。
   * @returns 异步操作完成时兑现。
   */
  async function loadFiles(selectFirst: boolean): Promise<void> {
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    if (!projectId || !environmentId) { files.value = []; clearDocument(); return }
    environmentController?.abort()
    const controller = new AbortController()
    environmentController = controller
    loading.value = true
    errorMessage.value = null
    try {
      const result = await api.listFiles(projectId, environmentId, controller.signal)
      if (controller.signal.aborted || projectId !== selectedProjectId.value || environmentId !== selectedEnvironmentId.value) return
      files.value = result
      const previousFileId = selectedFileId.value
      const next = files.value.find((item) => item.id === previousFileId)
        ?? (selectFirst ? files.value[0] : null)
      selectedFileId.value = next?.id ?? null
      if (next) await loadDraft(projectId, environmentId, next.id, next.id !== previousFileId)
      else clearDocument()
    } catch (error) {
      if (!isAbort(error)) errorMessage.value = errorText(error)
    } finally {
      loading.value = false
    }
  }

  /**
   * 读取草稿快照并重置编辑基线。
   * @param projectId 项目的内部标识。
   * @param environmentId 环境的内部标识。
   * @param fileId 配置文件的内部标识。
   * @param clearSelectionOnFailure 读取失败时是否清除当前文件选择。
   * @returns 异步操作完成时兑现。
   */
  async function loadDraft(projectId: string, environmentId: string, fileId: string, clearSelectionOnFailure = false): Promise<void> {
    fileController?.abort()
    fileController = new AbortController()
    const requestId = ++fileRequestId
    loadingFile.value = true
    errorMessage.value = null
    try {
      const draft = await api.getDraft(projectId, environmentId, fileId, fileController.signal)
      if (requestId !== fileRequestId || fileId !== selectedFileId.value) return
      files.value = files.value.map((item) => item.id === fileId ? draft.file : item)
      const json = draft.json ?? initialJson
      const parsed = parseConfigDocument(json, 'json')
      const value = parsed.value ?? {}
      documentFormat.value = 'json'
      codeFormat.value = 'json'
      documentText.value = json
      documentValue.value = value
      savedValue.value = draft.json === null ? null : value
      savedViewText.value = json
      validationError.value = parsed.error ?? null
      treeInputError.value = null
      validationPending.value = false
      draftRevision.value = draft.file.draftRevision
      conflictDetected.value = false
      await loadHistory(projectId, environmentId, fileId)
    } catch (error) {
      if (!isAbort(error) && requestId === fileRequestId) {
        errorMessage.value = errorText(error)
        if (clearSelectionOnFailure) {
          selectedFileId.value = null
          clearDocument()
        }
      }
    } finally {
      if (requestId === fileRequestId) loadingFile.value = false
    }
  }

  /**
   * 输入文本后防抖校验；无效内容仍保留在编辑缓冲区。
   * @param text 当前待处理的完整文本。
   */
  function updateDocumentText(text: string): void {
    documentText.value = text
    validationPending.value = true
    validationError.value = null
    clearTimeout(parseTimer)
    parseTimer = setTimeout(() => { validateDocument() }, 250)
  }

  /**
   * 立即校验当前编辑文本并同步最后一次有效 JSON 数据。
   * @returns 当前输入的有效对象；输入无效时为 null。
   */
  function validateDocument(): JsonObject | null {
    clearTimeout(parseTimer)
    validationPending.value = false
    if (documentFormat.value === 'tree') {
      if (treeInputError.value) { validationError.value = treeInputError.value; return null }
      const result = validateJsonObject(documentValue.value)
      validationError.value = result.error ?? null
      return result.value ?? null
    }
    const result = parseConfigDocument(documentText.value, documentFormat.value)
    validationError.value = result.error ?? null
    if (!result.value) return null
    documentValue.value = result.value
    return result.value
  }

  /**
   * 在验证成功后切换 JSON、YAML 或 Tree 表示。
   * @param format 目标文档表示。
   */
  function changeFormat(format: DocumentFormat): void {
    if (format === documentFormat.value) return
    const value = validateDocument()
    if (!value) return
    if (documentFormat.value !== 'tree') codeFormat.value = documentFormat.value
    if (format !== 'tree') {
      codeFormat.value = format
      documentText.value = stringifyConfigDocument(value, format)
      savedViewText.value = savedValue.value
        ? stringifyConfigDocument(savedValue.value, format)
        : initialJson
    }
    documentFormat.value = format
    validationError.value = null
    validationPending.value = false
  }

  /**
   * 由 Tree 操作更新共享 JSON 数据和代码视图文本。
   * @param value 本次操作接收的值。
   */
  function updateTreeValue(value: JsonObject): void {
    documentValue.value = value
    documentText.value = stringifyConfigDocument(value, codeFormat.value)
    validationError.value = treeInputError.value ?? validateJsonObject(value).error ?? null
    validationPending.value = false
  }

  /**
   * 将 Tree 输入缓冲的错误纳入所有文档操作的同步校验。
   * @param message 尚未生效的输入错误，全部输入有效时为 null。
   */
  function updateTreeValidation(message: string | null): void {
    treeInputError.value = message
    if (documentFormat.value === 'tree') validationError.value = message ?? validateJsonObject(documentValue.value).error ?? null
  }

  /**
   * 校验并把当前编辑内容保存为一个新草稿修订。
   * @returns 异步操作完成时兑现。
   */
  async function save(): Promise<void> {
    const file = currentFile.value
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    if (!file || !projectId || !environmentId || saving.value) return
    const value = validateDocument()
    if (!value) return
    const sourceFormat = documentFormat.value
    const sourceText = documentText.value
    const json = sourceFormat === 'json' ? sourceText : JSON.stringify(value, null, 2)
    const generation = fileRequestId
    saving.value = true
    conflictDetected.value = false
    errorMessage.value = null
    try {
      const draft = await api.saveDraft(projectId, environmentId, file, json)
      if (generation !== fileRequestId || currentFile.value?.id !== file.id) return
      files.value = files.value.map((item) => item.id === file.id ? draft.file : item)
      draftRevision.value = draft.file.draftRevision
      savedValue.value = structuredClone(value)
      savedViewText.value = sourceFormat === 'tree'
        ? stringifyConfigDocument(value, codeFormat.value)
        : sourceText
      if (!isDirty.value) documentValue.value = value
      await loadHistory(projectId, environmentId, file.id)
    } catch (error) {
      handleMutationError(error)
    } finally {
      saving.value = false
    }
  }

  /**
   * 保存必要的编辑内容后发布刚确认的草稿修订。
   * @returns 异步操作完成时兑现。
   */
  async function publish(): Promise<void> {
    const file = currentFile.value
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    if (!file || !projectId || !environmentId || publishing.value) return
    if (!validateDocument()) return
    publishing.value = true
    errorMessage.value = null
    conflictDetected.value = false
    try {
      if (isDirty.value) await save()
      if (conflictDetected.value || errorMessage.value || draftRevision.value < 1) return
      const latestFile = currentFile.value
      if (!latestFile || latestFile.id !== file.id || selectedProjectId.value !== projectId || selectedEnvironmentId.value !== environmentId) return
      const result = await api.publishDraft(projectId, environmentId, latestFile, draftRevision.value)
      if (currentFile.value?.id === latestFile.id) {
        files.value = files.value.map((item) => item.id === latestFile.id ? result.file : item)
        await loadHistory(projectId, environmentId, latestFile.id)
      }
    } catch (error) {
      handleMutationError(error)
    } finally {
      publishing.value = false
    }
  }

  /**
   * 载入发布历史列表。
   * @param projectId 项目的内部标识。
   * @param environmentId 环境的内部标识。
   * @param fileId 配置文件的内部标识。
   * @returns 异步操作完成时兑现。
   */
  async function loadHistory(projectId: string, environmentId: string, fileId: string): Promise<void> {
    historyController?.abort()
    historyController = new AbortController()
    try {
      const result = await api.listReleases(projectId, environmentId, fileId, historyController.signal)
      if (fileId === selectedFileId.value) releases.value = result
    } catch (error) {
      if (!isAbort(error)) errorMessage.value = errorText(error)
    }
  }

  /**
   * 为历史面板提供选定发布版本的只读正文。
   * @param version 所选发布版本号。
   * @param signal 可选的请求取消信号。
   */
  async function readRelease(version: number, signal?: AbortSignal) {
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    const fileId = selectedFileId.value
    if (!projectId || !environmentId || !fileId) return null
    try { return await api.getRelease(projectId, environmentId, fileId, version, signal) }
    catch (error) {
      if (isAbort(error)) return null
      errorMessage.value = errorText(error)
      return null
    }
  }

  /**
   * 以历史内容生成新发布版本，同时保留当前草稿及本地输入。
   * @param version 所选发布版本号。
   * @returns 异步操作完成时兑现。
   */
  async function rollbackTo(version: number): Promise<void> {
    const file = currentFile.value
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    if (!file || !projectId || !environmentId) return
    errorMessage.value = null
    try {
      const result = await api.rollback(projectId, environmentId, file, version)
      if (currentFile.value?.id !== file.id || selectedProjectId.value !== projectId || selectedEnvironmentId.value !== environmentId) return
      files.value = files.value.map((item) => item.id === file.id ? result.file : item)
      await loadHistory(projectId, environmentId, file.id)
    } catch (error) {
      handleMutationError(error)
    }
  }

  /**
   * 新建项目并切换到新资源。
   * @param body 待提交的资源字段。
   * @returns 资源写入成功时为 true；取消或失败时为 false。
   */
  async function addProject(body: { key: string; displayName: string }): Promise<boolean> {
    errorMessage.value = null
    if (!(await confirmDiscardIfNeeded())) return false
    try {
      const project = await api.createProject(body)
      projects.value = [...projects.value, project]
      selectedProjectId.value = project.id
      selectedEnvironmentId.value = null
      environments.value = []
      files.value = []
      clearDocument()
      await loadEnvironments(project.id, true)
      return true
    } catch (error) { errorMessage.value = errorText(error); return false }
  }

  /**
   * 修改项目标识或显示名。
   * @param body 待提交的资源字段。
   * @returns 资源写入成功时为 true；取消或失败时为 false。
   */
  async function updateProject(body: { key: string; displayName: string }): Promise<boolean> {
    errorMessage.value = null
    const project = currentProject.value
    if (!project) return false
    try {
      const updated = await api.renameProject(project, body)
      projects.value = projects.value.map((item) => item.id === project.id ? updated : item)
      return true
    } catch (error) { handleMutationError(error); return false }
  }

  /**
   * 删除空项目并载入其他可用资源。
   * @returns 异步操作完成时兑现。
   */
  async function removeProject(): Promise<void> {
    const project = currentProject.value
    if (!project || !(await confirmAction(`确定删除项目“${project.displayName}”吗？`))) return
    try { await api.deleteProject(project); selectedProjectId.value = null; await loadProjects() }
    catch (error) { handleMutationError(error) }
  }

  /**
   * 在当前项目中创建环境并切换到它。
   * @param body 待提交的资源字段。
   * @returns 资源写入成功时为 true；取消或失败时为 false。
   */
  async function addEnvironment(body: { key: string; displayName: string }): Promise<boolean> {
    errorMessage.value = null
    const project = currentProject.value
    if (!project || !(await confirmDiscardIfNeeded())) return false
    try {
      const environment = await api.createEnvironment(project.id, body)
      environments.value = [...environments.value, environment]
      selectedEnvironmentId.value = environment.id
      files.value = []
      clearDocument()
      return true
    } catch (error) { errorMessage.value = errorText(error); return false }
  }

  /**
   * 修改当前环境标识或显示名。
   * @param body 待提交的资源字段。
   * @returns 资源写入成功时为 true；取消或失败时为 false。
   */
  async function updateEnvironment(body: { key: string; displayName: string }): Promise<boolean> {
    errorMessage.value = null
    const environment = currentEnvironment.value
    if (!environment) return false
    try {
      const updated = await api.renameEnvironment(environment, body)
      environments.value = environments.value.map((item) => item.id === environment.id ? updated : item)
      return true
    } catch (error) { handleMutationError(error); return false }
  }

  /**
   * 删除空环境并清理当前文件选择。
   * @returns 异步操作完成时兑现。
   */
  async function removeEnvironment(): Promise<void> {
    const environment = currentEnvironment.value
    if (!environment || !(await confirmAction(`确定删除环境“${environment.displayName}”吗？`))) return
    try {
      await api.deleteEnvironment(environment)
      environments.value = environments.value.filter((item) => item.id !== environment.id)
      selectedEnvironmentId.value = null
      files.value = []
      selectedFileId.value = null
      clearDocument()
    } catch (error) { handleMutationError(error) }
  }

  /**
   * 在当前环境创建配置文件并载入空 JSON 草稿。
   * @param name 待使用的资源名称。
   * @returns 资源写入成功时为 true；取消或失败时为 false。
   */
  async function addFile(name: string): Promise<boolean> {
    errorMessage.value = null
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    if (!projectId || !environmentId || !(await confirmDiscardIfNeeded())) return false
    try {
      const file = await api.createFile(projectId, environmentId, name)
      files.value = [...files.value, file]
      clearDocument()
      selectedFileId.value = file.id
      await loadDraft(projectId, environmentId, file.id, true)
      return true
    } catch (error) { errorMessage.value = errorText(error); return false }
  }

  /**
   * 修改当前文件名并更新资源树。
   * @param name 待使用的资源名称。
   * @returns 资源写入成功时为 true；取消或失败时为 false。
   */
  async function updateFileName(name: string): Promise<boolean> {
    errorMessage.value = null
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    const file = currentFile.value
    if (!projectId || !environmentId || !file) return false
    try {
      const updated = await api.renameFile(projectId, environmentId, file, name)
      files.value = files.value.map((item) => item.id === file.id ? updated : item)
      return true
    } catch (error) { handleMutationError(error); return false }
  }

  /**
   * 删除当前文件及其草稿和历史，并选择下一份文件。
   * @returns 异步操作完成时兑现。
   */
  async function removeFile(): Promise<void> {
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    const file = currentFile.value
    if (!projectId || !environmentId || !file) return
    if (!(await confirmAction(`删除“${file.name}”会同时删除草稿和全部发布历史${isDirty.value ? '，并放弃本地未保存内容' : ''}，确定继续吗？`))) return
    try {
      await api.deleteFile(projectId, environmentId, file)
      files.value = files.value.filter((item) => item.id !== file.id)
      if (selectedFileId.value !== file.id) return
      const next = files.value[0] ?? null
      selectedFileId.value = next?.id ?? null
      if (next) await loadDraft(projectId, environmentId, next.id)
      else clearDocument()
    } catch (error) { handleMutationError(error) }
  }

  /**
   * 刷新当前文件；若有本地修改，需要管理员明确放弃后再读。
   * @returns 异步操作完成时兑现。
   */
  async function reloadCurrentDraft(): Promise<void> {
    const file = currentFile.value
    const projectId = selectedProjectId.value
    const environmentId = selectedEnvironmentId.value
    if (!file || !projectId || !environmentId) return
    if (isDirty.value && !(await confirmAction('重新读取会丢弃当前本地修改，确定继续吗？'))) return
    await loadDraft(projectId, environmentId, file.id)
  }

  /**
   * 确认离开时是否丢弃当前未保存编辑。
   * @returns 可安全离开或用户已确认放弃时为 true。
   */
  async function confirmDiscardIfNeeded(): Promise<boolean> {
    return !isDirty.value || await confirmAction('当前配置有未保存修改。切换后将放弃这些本地内容，确定继续吗？')
  }

  /** 清理编辑器、历史与文件选择状态。 */
  function clearDocument(): void {
    clearTimeout(parseTimer)
    treeInputError.value = null
    fileController?.abort()
    historyController?.abort()
    fileRequestId++
    loadingFile.value = false
    selectedFileId.value = null
    releases.value = []
    draftRevision.value = 0
    savedValue.value = null
    documentValue.value = {}
    documentText.value = initialJson
    savedViewText.value = initialJson
    documentFormat.value = 'json'
    codeFormat.value = 'json'
    validationError.value = null
    validationPending.value = false
    conflictDetected.value = false
  }

  /** 显示没有数据文件时的工作区默认内容。 */
  function clearSelection(): void {
    selectedEnvironmentId.value = null
    selectedFileId.value = null
    environments.value = []
    files.value = []
    clearDocument()
  }

  /**
   * 将并发冲突与常规服务错误映射到不会覆盖编辑内容的提示。
   * @param error 需要分类或转换为提示的异常。
   */
  function handleMutationError(error: unknown): void {
    if (error instanceof ApiError && error.status === 409) conflictDetected.value = true
    errorMessage.value = errorText(error)
  }

  return {
    projects: readonly(projects), environments: readonly(environments), files: readonly(files),
    selectedProjectId: readonly(selectedProjectId), selectedEnvironmentId: readonly(selectedEnvironmentId),
    selectedFileId: readonly(selectedFileId), currentProject, currentEnvironment, currentFile,
    documentText: readonly(documentText), documentFormat: readonly(documentFormat), documentValue: readonly(documentValue),
    validationError: readonly(validationError), validationPending: readonly(validationPending), isValid, isDirty,
    draftRevision: readonly(draftRevision), releases: readonly(releases), loading: readonly(loading),
    loadingFile: readonly(loadingFile), saving: readonly(saving), publishing: readonly(publishing),
    canPublish, errorMessage: readonly(errorMessage), conflictDetected: readonly(conflictDetected),
    loadProjects, selectProject, selectEnvironment, selectFile, updateDocumentText, changeFormat,
    updateTreeValue, updateTreeValidation, validateDocument, save, publish, readRelease, rollbackTo, addProject,
    updateProject, removeProject, addEnvironment, updateEnvironment, removeEnvironment, addFile,
    updateFileName, removeFile, reloadCurrentDraft, confirmDiscardIfNeeded,
  }
}

/**
 * 判断被取消的网络请求并避免在 UI 展示过期请求错误。
 * @param error 需要分类或转换为提示的异常。
 * @returns 异常是否表示请求已取消。
 */
function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

/**
 * 将网络和服务错误转换为安全的工作区提示。
 * @param error 需要分类或转换为提示的异常。
 * @returns 可向管理员显示的错误说明。
 */
function errorText(error: unknown): string {
  return error instanceof Error ? error.message : '操作失败，请稍后重试。'
}
