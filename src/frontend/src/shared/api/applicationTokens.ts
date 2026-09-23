import type { ApplicationToken, Environment, IssuedApplicationToken, PageResponse, Project } from './contracts'
import { jsonBody, request } from './http'
import { listProjects, listEnvironments } from './workspace'

/**
 * 读取一页应用令牌元数据。
 * @param pageNumber 从 1 开始的页码。
 * @param signal 可选的请求取消信号。
 * @returns 当前页及分页边界信息。
 */
export async function listApplicationTokens(pageNumber = 1, signal?: AbortSignal): Promise<PageResponse<ApplicationToken>> {
  return request(`/api/admin/application-tokens?page=${pageNumber}&pageSize=100`, { signal })
}

/**
 * 创建指定名称、期限和项目环境范围的只读令牌。
 * @param name 待使用的资源名称。
 * @param expiresAt 令牌的未来到期时间，使用 ISO 8601 表示。
 * @param scopes 应用令牌允许读取的项目与环境范围。
 * @returns 管理 API 返回的操作结果。
 */
export function createApplicationToken(name: string, expiresAt: string, scopes: Array<{ projectId: string; environmentId: string }>): Promise<IssuedApplicationToken> {
  return request('/api/admin/application-tokens', {
    method: 'POST', body: jsonBody({ name, expiresAt, scopes }),
  })
}

/**
 * 撤销应用令牌。
 * @param id 目标资源的内部标识。
 * @returns 异步操作完成时兑现。
 */
export function revokeApplicationToken(id: string): Promise<void> {
  return request(`/api/admin/application-tokens/${id}/revoke`, { method: 'POST' })
}

/**
 * 轮换令牌凭证并保留原名称和授权范围。
 * @param id 目标资源的内部标识。
 * @param expiresAt 令牌的未来到期时间，使用 ISO 8601 表示。
 * @returns 管理 API 返回的操作结果。
 */
export function rotateApplicationToken(id: string, expiresAt: string): Promise<IssuedApplicationToken> {
  return request(`/api/admin/application-tokens/${id}/rotate`, { method: 'POST', body: jsonBody({ expiresAt }) })
}

/**
 * 读取令牌授权选择器使用的项目和环境资源。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export async function listTokenScopeResources(signal?: AbortSignal): Promise<Array<{ project: Project; environments: Environment[] }>> {
  const projects = await listProjects(signal)
  const resources: Array<{ project: Project; environments: Environment[] }> = []
  for (const project of projects) resources.push({ project, environments: await listEnvironments(project.id, signal) })
  return resources
}
