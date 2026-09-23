import type { PageResponse } from './contracts'

/** 可安全展示的管理 API 错误。 */
export class ApiError extends Error {
  /** HTTP 状态码。 */
  readonly status: number
  /** 后端稳定错误标识。 */
  readonly code: string

  /**
   * 创建标准化 API 错误。
   * @param status HTTP 状态码。
   * @param code 后端稳定错误标识。
   * @param message 可向管理员展示的错误说明。
   */
  constructor(status: number, code: string, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/** 服务端返回的防伪造令牌和请求头名称。 */
interface AntiforgeryResponse {
  requestToken: string
  headerName: string
}

let antiforgery: AntiforgeryResponse | undefined

/** 清除浏览器内存中的防伪造令牌。 */
export function clearAntiforgeryToken(): void {
  antiforgery = undefined
}

/**
 * 登录成功后重新获取绑定管理员会话的防伪造令牌。
 * @returns 异步操作完成时兑现。
 */
export async function refreshAntiforgeryToken(): Promise<void> {
  await getAntiforgeryToken(true)
}

/**
 * 读取匿名或当前管理员身份绑定的防伪造令牌。
 * @param forceRefresh 是否忽略已有缓存并重新获取防伪造令牌。
 * @returns 当前身份绑定的防伪造令牌与请求头名称。
 */
async function getAntiforgeryToken(forceRefresh = false): Promise<AntiforgeryResponse> {
  if (antiforgery && !forceRefresh) return antiforgery
  let response: Response
  try {
    response = await fetch('/api/admin/auth/antiforgery', { credentials: 'same-origin' })
  } catch {
    throw new ApiError(0, 'network_error', '无法连接配置服务，请检查服务状态后重试。')
  }
  if (!response.ok) throw new ApiError(response.status, 'antiforgery_unavailable', '无法建立安全请求，请刷新页面后重试。')
  antiforgery = await response.json() as AntiforgeryResponse
  return antiforgery
}

/**
 * 发送同源管理 API 请求并解析类型化 JSON 响应。
 * @param path 管理 API 路径。
 * @param init Fetch 请求选项。
 * @returns 已解析的响应数据。
 * @throws {ApiError} 服务端返回非成功状态时抛出可展示的错误。
 */
export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase()
  const headers = new Headers(init.headers)
  if (init.body !== undefined && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  if (method !== 'GET' && method !== 'HEAD') {
    const token = await getAntiforgeryToken()
    headers.set(token.headerName, token.requestToken)
  }

  let response: Response
  try {
    response = await fetch(path, { ...init, method, headers, credentials: 'same-origin' })
  } catch (error) {
    if (init.signal?.aborted) throw error
    throw new ApiError(0, 'network_error', '无法连接配置服务，请检查服务状态后重试。')
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string; code?: string } | null
    const message = response.status === 401
      ? path.endsWith('/auth/login') ? '管理员账号或密码不正确。' : statusMessage(401)
      : response.status === 403 ? statusMessage(403) : problem?.title ?? statusMessage(response.status)
    const error = new ApiError(response.status, problem?.code ?? 'request_failed', message)
    if (response.status === 401 && !path.endsWith('/auth/login') && !path.endsWith('/auth/session')) {
      window.dispatchEvent(new Event('trelix:session-expired'))
    }
    throw error
  }
  if (response.status === 204) return undefined as T
  return await response.json() as T
}

/**
 * 为未包含业务提示的标准 HTTP 错误生成简短中文说明。
 * @param status HTTP 响应状态码。
 * @returns 对应状态码的中文提示。
 */
function statusMessage(status: number): string {
  if (status === 401) return '登录状态已失效，请重新登录。'
  if (status === 403) return '当前账号无权执行此操作。'
  if (status === 404) return '所选资源已不存在，请刷新资源列表。'
  if (status === 409) return '资源已被其他操作修改，请重新读取后核对。'
  if (status === 429) return '登录尝试过于频繁，请稍后再试。'
  return '请求失败，请稍后重试。'
}

/**
 * 用于将管理写请求正文序列化为 JSON。
 * @param value 本次操作接收的值。
 * @returns 用于 HTTP 请求体的 JSON 字符串。
 */
export function jsonBody(value: unknown): string {
  return JSON.stringify(value)
}

/**
 * 逐页读取资源，避免资源树或历史视图静默遗漏首批之外的条目。
 * @typeParam T 分页条目的类型。
 * @param path 不带查询参数的列表 API 路径。
 * @param signal 取消整个读取序列的信号。
 * @returns 各页条目的顺序集合。
 */
export async function requestAllPages<T>(path: string, signal?: AbortSignal): Promise<T[]> {
  const items: T[] = []
  for (let page = 1; page <= 1000000; page++) {
    const result = await request<PageResponse<T>>(`${path}?page=${page}&pageSize=100`, { signal })
    items.push(...result.items)
    if (result.items.length < result.pageSize) return items
  }
  throw new ApiError(0, 'pagination_limit', '资源数量超过可读取的分页范围。')
}
