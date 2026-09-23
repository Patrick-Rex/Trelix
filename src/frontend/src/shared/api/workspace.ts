import type {
  ConfigFile,
  DraftResponse,
  Environment,
  Project,
  PublicationResponse,
  Release,
  ReleaseDetail,
} from './contracts'
import { jsonBody, request, requestAllPages } from './http'

/**
 * 逐页读取全部项目资源。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export async function listProjects(signal?: AbortSignal): Promise<Project[]> {
  return requestAllPages<Project>('/api/admin/projects', signal)
}

/**
 * 创建项目及其稳定业务标识。
 * @param body 待提交的资源字段。
 * @returns 管理 API 返回的操作结果。
 */
export function createProject(body: { key: string; displayName: string }): Promise<Project> {
  return request('/api/admin/projects', { method: 'POST', body: jsonBody(body) })
}

/**
 * 重命名项目并携带其并发基准。
 * @param project 当前项目及其并发基准。
 * @param body 待提交的资源字段。
 * @returns 管理 API 返回的操作结果。
 */
export function renameProject(project: Project, body: { key: string; displayName: string }): Promise<Project> {
  return request(`/api/admin/projects/${project.id}`, {
    method: 'PUT', body: jsonBody({ ...body, concurrencyStamp: project.concurrencyStamp }),
  })
}

/**
 * 删除空项目。
 * @param project 当前项目及其并发基准。
 * @returns 异步操作完成时兑现。
 */
export function deleteProject(project: Project): Promise<void> {
  return request(`/api/admin/projects/${project.id}?concurrencyStamp=${encodeURIComponent(project.concurrencyStamp)}`, { method: 'DELETE' })
}

/**
 * 读取指定项目的环境。
 * @param projectId 项目的内部标识。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export async function listEnvironments(projectId: string, signal?: AbortSignal): Promise<Environment[]> {
  return requestAllPages<Environment>(`/api/admin/projects/${projectId}/environments`, signal)
}

/**
 * 创建项目环境。
 * @param projectId 项目的内部标识。
 * @param body 待提交的资源字段。
 * @returns 管理 API 返回的操作结果。
 */
export function createEnvironment(projectId: string, body: { key: string; displayName: string }): Promise<Environment> {
  return request(`/api/admin/projects/${projectId}/environments`, { method: 'POST', body: jsonBody(body) })
}

/**
 * 重命名环境并携带其并发基准。
 * @param environment 当前环境及其并发基准。
 * @param body 待提交的资源字段。
 * @returns 管理 API 返回的操作结果。
 */
export function renameEnvironment(environment: Environment, body: { key: string; displayName: string }): Promise<Environment> {
  return request(`/api/admin/projects/${environment.projectId}/environments/${environment.id}`, {
    method: 'PUT', body: jsonBody({ ...body, concurrencyStamp: environment.concurrencyStamp }),
  })
}

/**
 * 删除没有文件或令牌授权的环境。
 * @param environment 当前环境及其并发基准。
 * @returns 异步操作完成时兑现。
 */
export function deleteEnvironment(environment: Environment): Promise<void> {
  return request(`/api/admin/projects/${environment.projectId}/environments/${environment.id}?concurrencyStamp=${encodeURIComponent(environment.concurrencyStamp)}`, { method: 'DELETE' })
}

/**
 * 读取环境中的配置文件元数据。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export async function listFiles(projectId: string, environmentId: string, signal?: AbortSignal): Promise<ConfigFile[]> {
  return requestAllPages<ConfigFile>(`/api/admin/projects/${projectId}/environments/${environmentId}/files`, signal)
}

/**
 * 创建空配置文件。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param name 待使用的资源名称。
 * @returns 管理 API 返回的操作结果。
 */
export function createFile(projectId: string, environmentId: string, name: string): Promise<ConfigFile> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files`, {
    method: 'POST', body: jsonBody({ name }),
  })
}

/**
 * 读取草稿正文和文件并发基准。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param fileId 配置文件的内部标识。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export function getDraft(projectId: string, environmentId: string, fileId: string, signal?: AbortSignal): Promise<DraftResponse> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${fileId}`, { signal })
}

/**
 * 重命名配置文件。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param file 当前文件及其并发基准。
 * @param name 待使用的资源名称。
 * @returns 管理 API 返回的操作结果。
 */
export function renameFile(projectId: string, environmentId: string, file: ConfigFile, name: string): Promise<ConfigFile> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${file.id}`, {
    method: 'PUT', body: jsonBody({ name, concurrencyStamp: file.concurrencyStamp }),
  })
}

/**
 * 删除配置文件及其草稿和完整发布历史。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param file 当前文件及其并发基准。
 * @returns 异步操作完成时兑现。
 */
export function deleteFile(projectId: string, environmentId: string, file: ConfigFile): Promise<void> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${file.id}?concurrencyStamp=${encodeURIComponent(file.concurrencyStamp)}`, { method: 'DELETE' })
}

/**
 * 保存新的草稿修订。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param file 当前文件及其并发基准。
 * @param json 已校验的 JSON 正文。
 * @returns 管理 API 返回的操作结果。
 */
export function saveDraft(projectId: string, environmentId: string, file: ConfigFile, json: string): Promise<DraftResponse> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${file.id}/draft`, {
    method: 'PUT', body: jsonBody({ json, concurrencyStamp: file.concurrencyStamp }),
  })
}

/**
 * 发布选定草稿修订。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param file 当前文件及其并发基准。
 * @param draftRevision 明确选定的草稿修订号。
 * @returns 管理 API 返回的操作结果。
 */
export function publishDraft(projectId: string, environmentId: string, file: ConfigFile, draftRevision: number): Promise<PublicationResponse> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${file.id}/releases`, {
    method: 'POST', body: jsonBody({ draftRevision, concurrencyStamp: file.concurrencyStamp }),
  })
}

/**
 * 读取配置文件的发布历史。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param fileId 配置文件的内部标识。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export async function listReleases(projectId: string, environmentId: string, fileId: string, signal?: AbortSignal): Promise<Release[]> {
  return requestAllPages<Release>(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${fileId}/releases`, signal)
}

/**
 * 读取指定历史版本正文。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param fileId 配置文件的内部标识。
 * @param version 所选发布版本号。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export function getRelease(projectId: string, environmentId: string, fileId: string, version: number, signal?: AbortSignal): Promise<ReleaseDetail> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${fileId}/releases/${version}`, { signal })
}

/**
 * 根据历史版本生成一个新的发布版本。
 * @param projectId 项目的内部标识。
 * @param environmentId 环境的内部标识。
 * @param file 当前文件及其并发基准。
 * @param sourceVersion 用于回滚的源发布版本号。
 * @returns 管理 API 返回的操作结果。
 */
export function rollback(projectId: string, environmentId: string, file: ConfigFile, sourceVersion: number): Promise<PublicationResponse> {
  return request(`/api/admin/projects/${projectId}/environments/${environmentId}/files/${file.id}/rollback`, {
    method: 'POST', body: jsonBody({ sourceVersion, concurrencyStamp: file.concurrencyStamp }),
  })
}
