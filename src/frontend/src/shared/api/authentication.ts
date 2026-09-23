import type { AdminSession } from './contracts'
import { clearAntiforgeryToken, jsonBody, refreshAntiforgeryToken, request } from './http'

/**
 * 查询当前管理员会话。
 * @param signal 可选的请求取消信号。
 * @returns 管理 API 返回的操作结果。
 */
export function getSession(signal?: AbortSignal): Promise<AdminSession> {
  return request('/api/admin/auth/session', { signal })
}

/**
 * 使用内置管理员凭证登录并刷新会话绑定的防伪造令牌。
 * @param username 管理员账号名。
 * @param password 管理员密码。
 * @returns 新建立的管理员会话。
 */
export async function login(username: string, password: string): Promise<AdminSession> {
  const session = await request<AdminSession>('/api/admin/auth/login', {
    method: 'POST',
    body: jsonBody({ username, password }),
  })
  clearAntiforgeryToken()
  try { await refreshAntiforgeryToken() }
  catch { clearAntiforgeryToken() }
  return session
}

/**
 * 注销当前管理员会话。
 * @returns 异步操作完成时兑现。
 */
export async function logout(): Promise<void> {
  await request<void>('/api/admin/auth/logout', { method: 'POST' })
}
