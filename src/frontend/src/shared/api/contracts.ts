/** 管理员登录后端创建的 Cookie 会话。 */
export interface AdminSession {
  /** 当前管理员账号名。 */
  username: string
  /** 会话失效时间。 */
  expiresAt: string
}

/** 带并发基准的项目资源。 */
export interface Project {
  /** 稳定项目标识。 */
  id: string
  /** 项目业务标识。 */
  key: string
  /** 项目显示名。 */
  displayName: string
  /** 后续写入使用的并发基准。 */
  concurrencyStamp: string
}

/** 项目所属环境。 */
export interface Environment {
  /** 稳定环境标识。 */
  id: string
  /** 所属项目标识。 */
  projectId: string
  /** 环境业务标识。 */
  key: string
  /** 环境显示名。 */
  displayName: string
  /** 后续写入使用的并发基准。 */
  concurrencyStamp: string
}

/** 配置文件元数据。 */
export interface ConfigFile {
  /** 稳定文件标识。 */
  id: string
  /** 所属环境标识。 */
  environmentId: string
  /** 环境内文件名。 */
  name: string
  /** 已保存草稿修订号。 */
  draftRevision: number
  /** 当前已发布版本；未发布时为空。 */
  currentReleaseVersion: number | null
  /** 后续写操作的并发基准。 */
  concurrencyStamp: string
}

/** 分页管理 API 响应。 */
export interface PageResponse<T> {
  /** 当前页项目。 */
  items: T[]
  /** 从 1 开始的页码。 */
  page: number
  /** 当前页大小。 */
  pageSize: number
}

/** 已保存草稿及原始 JSON 正文。 */
export interface DraftResponse {
  /** 同一快照读取的文件元数据。 */
  file: ConfigFile
  /** 未保存过草稿时为空。 */
  json: string | null
}

/** 发布历史项目。 */
export interface Release {
  /** 所属文件标识。 */
  configFileId: string
  /** 文件内单调递增的发布版本。 */
  version: number
  /** 产生正文的草稿修订。 */
  draftRevision: number
  /** 回滚来源版本；普通发布时为空。 */
  sourceVersion: number | null
  /** UTC 发布时间。 */
  publishedAt: string
}

/** 发布历史的完整正文快照。 */
export interface ReleaseDetail {
  /** 不可变发布元数据。 */
  release: Release
  /** 历史 JSON 正文。 */
  json: string
}

/** 保存、发布或回滚后的文件和版本。 */
export interface PublicationResponse {
  /** 更新后的文件并发基准。 */
  file: ConfigFile
  /** 新生成的发布版本。 */
  release: Release
}

/** 应用令牌的项目/环境授权范围。 */
export interface TokenScope {
  /** 获准项目。 */
  projectId: string
  /** 获准环境。 */
  environmentId: string
}

/** 应用只读令牌元数据，不含凭证原文。 */
export interface ApplicationToken {
  /** 稳定令牌标识。 */
  id: string
  /** 管理员设置的名称。 */
  name: string
  /** 创建时间。 */
  createdAt: string
  /** 失效时间。 */
  expiresAt: string
  /** 撤销时间；尚未撤销时为空。 */
  revokedAt: string | null
  /** 令牌获准访问的项目/环境组合。 */
  scopes: TokenScope[]
}

/** 创建或轮换后仅返回一次的应用凭证。 */
export interface IssuedApplicationToken {
  /** 新令牌的可管理信息。 */
  token: ApplicationToken
  /** 只在本次响应中可见的令牌原文。 */
  secret: string
}

/** JSON 编辑器可表示的数据值。 */
export type JsonValue = null | boolean | number | string | JsonValue[] | JsonObject

/** JSON 对象。 */
export interface JsonObject {
  [key: string]: JsonValue
}
