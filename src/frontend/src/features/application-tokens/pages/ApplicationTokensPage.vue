<script setup lang="ts">
import { computed, onMounted, onUnmounted, shallowRef } from 'vue'
import type { ApplicationToken, IssuedApplicationToken, TokenScope } from '@/shared/api/contracts'
import ModalDialog from '@/shared/components/ModalDialog.vue'
import { confirmAction } from '@/shared/components/confirmAction'
import * as tokenApi from '@/shared/api/applicationTokens'

/** 令牌授权选择器中的项目及其所属环境。 */
interface ScopeResource {
  /** 项目名称与稳定业务标识。 */
  project: { id: string; key: string; displayName: string }
  /** 此项目可授权的环境列表。 */
  environments: Array<{ id: string; key: string; displayName: string }>
}

const tokens = shallowRef<ApplicationToken[]>([])
const scopeResources = shallowRef<ScopeResource[]>([])
const selectedScopes = shallowRef<TokenScope[]>([])
const loading = shallowRef(false)
const loadingMore = shallowRef(false)
const page = shallowRef(1)
const hasMore = shallowRef(false)
const errorMessage = shallowRef('')
const createOpen = shallowRef(false)
const tokenName = shallowRef('')
const tokenExpiry = shallowRef('')
const formError = shallowRef('')
const rotateOpen = shallowRef(false)
const rotatingToken = shallowRef<ApplicationToken | null>(null)
const rotationExpiry = shallowRef('')
const issuedToken = shallowRef<IssuedApplicationToken | null>(null)
const copying = shallowRef(false)
const mutating = shallowRef(false)
const requestController = new AbortController()

const activeCount = computed(() => tokens.value.filter((token) => !token.revokedAt && Date.parse(token.expiresAt) > Date.now()).length)

/**
 * 首次载入令牌元数据及创建表单需要的授权资源。
 * @returns 异步操作完成时兑现。
 */
async function loadPage(): Promise<void> {
  loading.value = true
  errorMessage.value = ''
  try {
    const [tokenPage, resources] = await Promise.all([
      tokenApi.listApplicationTokens(1, requestController.signal),
      tokenApi.listTokenScopeResources(requestController.signal),
    ])
    tokens.value = tokenPage.items
    page.value = tokenPage.page
    hasMore.value = tokenPage.items.length === tokenPage.pageSize
    scopeResources.value = resources
  } catch (error) {
    if (!requestController.signal.aborted) errorMessage.value = errorText(error)
  } finally {
    loading.value = false
  }
}

/**
 * 读取下一页令牌元数据。
 * @returns 异步操作完成时兑现。
 */
async function loadMore(): Promise<void> {
  if (!hasMore.value || loadingMore.value) return
  loadingMore.value = true
  try {
    const next = await tokenApi.listApplicationTokens(page.value + 1, requestController.signal)
    tokens.value = [...tokens.value, ...next.items]
    page.value = next.page
    hasMore.value = next.items.length === next.pageSize
  } catch (error) {
    if (!requestController.signal.aborted) errorMessage.value = errorText(error)
  } finally {
    loadingMore.value = false
  }
}

/**
 * 显示或隐藏一个项目环境授权范围。
 * @param scope 令牌授权的项目与环境范围。
 * @param checked 范围是否被选中。
 */
function toggleScope(scope: TokenScope, checked: boolean): void {
  const key = scopeKey(scope)
  selectedScopes.value = checked
    ? selectedScopes.value.some((item) => scopeKey(item) === key) ? selectedScopes.value : [...selectedScopes.value, scope]
    : selectedScopes.value.filter((item) => scopeKey(item) !== key)
}

/**
 * 为创建请求校验必填范围、名称和未来有效期。
 * @returns 异步操作完成时兑现。
 */
async function createToken(): Promise<void> {
  if (mutating.value) return
  formError.value = ''
  if (!tokenName.value.trim() || tokenName.value.length > 200) { formError.value = '名称不能为空，且最多 200 个字符。'; return }
  if (selectedScopes.value.length === 0) { formError.value = '至少选择一个项目与环境授权范围。'; return }
  const expiration = Date.parse(tokenExpiry.value)
  if (!Number.isFinite(expiration) || expiration <= Date.now()) { formError.value = '有效期必须晚于当前时间。'; return }
  mutating.value = true
  try {
    issuedToken.value = await tokenApi.createApplicationToken(tokenName.value, new Date(expiration).toISOString(), selectedScopes.value)
    createOpen.value = false
    tokenName.value = ''
    tokenExpiry.value = ''
    selectedScopes.value = []
    await loadPage()
  } catch (error) { formError.value = errorText(error) }
  finally { mutating.value = false }
}

/**
 * 打开令牌轮换表单并记录目标令牌。
 * @param token 待处理的应用令牌元数据。
 */
function openRotate(token: ApplicationToken): void {
  rotatingToken.value = token
  rotationExpiry.value = ''
  formError.value = ''
  rotateOpen.value = true
}

/**
 * 确认新令牌有效期并原子轮换旧凭证。
 * @returns 异步操作完成时兑现。
 */
async function rotateToken(): Promise<void> {
  if (mutating.value) return
  const token = rotatingToken.value
  const expiration = Date.parse(rotationExpiry.value)
  if (!token) return
  if (!Number.isFinite(expiration) || expiration <= Date.now()) { formError.value = '有效期必须晚于当前时间。'; return }
  mutating.value = true
  try {
    issuedToken.value = await tokenApi.rotateApplicationToken(token.id, new Date(expiration).toISOString())
    rotateOpen.value = false
    rotatingToken.value = null
    await loadPage()
  } catch (error) { formError.value = errorText(error) }
  finally { mutating.value = false }
}

/**
 * 撤销指定凭证并刷新显示状态。
 * @param token 待处理的应用令牌元数据。
 * @returns 异步操作完成时兑现。
 */
async function revokeToken(token: ApplicationToken): Promise<void> {
  if (mutating.value) return
  if (token.revokedAt || !(await confirmAction(`确定立即撤销“${token.name}”吗？使用此令牌的应用将无法继续读取配置。`))) return
  if (mutating.value) return
  mutating.value = true
  try {
    await tokenApi.revokeApplicationToken(token.id)
    tokens.value = tokens.value.map((item) => item.id === token.id ? { ...item, revokedAt: new Date().toISOString() } : item)
  } catch (error) { errorMessage.value = errorText(error) }
  finally { mutating.value = false }
}

/**
 * 将刚签发且仅显示一次的凭证复制到剪贴板。
 * @returns 异步操作完成时兑现。
 */
async function copySecret(): Promise<void> {
  if (!issuedToken.value) return
  copying.value = true
  try {
    await navigator.clipboard.writeText(issuedToken.value.secret)
  } catch {
    errorMessage.value = '无法访问剪贴板，请手动选择并复制令牌原文。'
  } finally {
    copying.value = false
  }
}

/**
 * 关闭一次性令牌窗口时立即清除凭证原文。
 * @param open 对话框的目标打开状态。
 */
function closeIssuedToken(open: boolean): void {
  if (!open) issuedToken.value = null
}

/**
 * 格式化令牌生命周期时间。
 * @param value 本次操作接收的值。
 * @returns 按本地时区格式化的时间文字。
 */
function formatDate(value: string): string {
  return new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

/**
 * 显示令牌已撤销、已过期或仍有效的状态。
 * @param token 待处理的应用令牌元数据。
 * @returns 有效、已撤销或已过期状态。
 */
function tokenState(token: ApplicationToken): string {
  if (token.revokedAt) return '已撤销'
  return Date.parse(token.expiresAt) <= Date.now() ? '已过期' : '有效'
}

/**
 * 解析项目环境授权标识，便于比较选择状态。
 * @param scope 令牌授权的项目与环境范围。
 * @returns 用于比较授权范围的稳定标识。
 */
function scopeKey(scope: TokenScope): string {
  return `${scope.projectId}:${scope.environmentId}`
}

/**
 * 将授权范围转成管理员可读的项目与环境名称。
 * @param scope 令牌授权的项目与环境范围。
 * @returns 项目与环境的显示名称。
 */
function scopeLabel(scope: TokenScope): string {
  for (const resource of scopeResources.value) {
    if (resource.project.id !== scope.projectId) continue
    const environment = resource.environments.find((item) => item.id === scope.environmentId)
    return environment ? `${resource.project.displayName} / ${environment.displayName}` : `${resource.project.displayName} / 环境已删除`
  }
  return '项目已删除'
}

/**
 * 生成用于复选框稳定标识的授权范围 DOM id。
 * @param scope 令牌授权的项目与环境范围。
 * @returns 授权范围复选框的 DOM 标识。
 */
function scopeInputId(scope: TokenScope): string {
  return `scope-${scope.projectId}-${scope.environmentId}`
}

/**
 * 将 API 或网络失败转换为可读说明。
 * @param error 需要分类或转换为提示的异常。
 * @returns 可向管理员显示的错误说明。
 */
function errorText(error: unknown): string {
  return error instanceof Error ? error.message : '操作失败，请稍后重试。'
}

/** 页面挂载后读取令牌和范围数据。 */
onMounted(() => { void loadPage() })
/** 页面卸载时取消未完成的只读请求。 */
onUnmounted(() => requestController.abort())
</script>

<template>
  <section class="token-page" aria-labelledby="tokens-heading">
    <header class="page-heading">
      <div><span class="eyebrow">ACCESS CONTROL</span><h2 id="tokens-heading">应用令牌</h2><p>控制业务应用可读取的项目和环境。令牌原文只在创建或轮换后显示一次。</p></div>
      <button class="button button-primary" type="button" @click="createOpen = true">＋ 创建应用令牌</button>
    </header>
    <div class="token-summary"><span><strong>{{ tokens.length }}</strong> 个令牌记录</span><span><strong>{{ activeCount }}</strong> 个当前有效</span></div>
    <p v-if="errorMessage" class="page-alert" role="alert">{{ errorMessage }}</p>
    <div v-if="loading" class="token-loading" role="status">正在载入应用令牌…</div>
    <div v-else-if="tokens.length === 0" class="token-empty"><span class="empty-key" aria-hidden="true">⌑</span><h3>还没有应用令牌</h3><p>创建令牌后，业务应用可以读取授权范围内已发布的配置。</p></div>
    <div v-else class="token-list">
      <article v-for="token in tokens" :key="token.id" class="token-card">
        <div class="token-main">
          <div class="token-title-line"><h3>{{ token.name }}</h3><span class="token-status" :class="{ inactive: tokenState(token) !== '有效' }">{{ tokenState(token) }}</span></div>
          <p class="token-dates">创建于 {{ formatDate(token.createdAt) }} <span aria-hidden="true">·</span> 到期于 {{ formatDate(token.expiresAt) }}<template v-if="token.revokedAt"> <span aria-hidden="true">·</span> 撤销于 {{ formatDate(token.revokedAt) }}</template></p>
          <div class="scope-list" aria-label="授权范围">
            <span v-for="scope in token.scopes" :key="scopeKey(scope)" class="scope-chip">{{ scopeLabel(scope) }}</span>
          </div>
        </div>
        <div class="token-actions">
          <button class="button button-secondary" type="button" :disabled="!!token.revokedAt" @click="openRotate(token)">轮换</button>
          <button class="button button-quiet button-danger" type="button" :disabled="!!token.revokedAt" @click="revokeToken(token)">撤销</button>
        </div>
      </article>
      <button v-if="hasMore" class="button button-secondary load-more" type="button" :disabled="loadingMore" @click="loadMore">{{ loadingMore ? '正在读取…' : '加载更多' }}</button>
    </div>
  </section>

  <ModalDialog v-model:open="createOpen" title="创建应用令牌" labelled-by="create-token-title">
    <form class="token-form" @submit.prevent="createToken">
      <label for="token-name">令牌名称</label><input id="token-name" v-model="tokenName" maxlength="200" required>
      <label for="token-expiry">有效期至</label><input id="token-expiry" v-model="tokenExpiry" type="datetime-local" required>
      <fieldset class="scope-options"><legend>授权项目与环境</legend>
        <p v-if="scopeResources.length === 0" class="scope-empty">还没有可授权的环境，请先创建项目和环境。</p>
        <section v-for="resource in scopeResources" :key="resource.project.id" class="scope-project">
          <h3>{{ resource.project.displayName }} <small>{{ resource.project.key }}</small></h3>
          <label v-for="environment in resource.environments" :key="environment.id" class="scope-option" :for="scopeInputId({ projectId: resource.project.id, environmentId: environment.id })">
            <input :id="scopeInputId({ projectId: resource.project.id, environmentId: environment.id })" type="checkbox" :checked="selectedScopes.some((scope) => scope.projectId === resource.project.id && scope.environmentId === environment.id)" @change="toggleScope({ projectId: resource.project.id, environmentId: environment.id }, ($event.target as HTMLInputElement).checked)">
            <span>{{ environment.displayName }} <small>{{ environment.key }}</small></span>
          </label>
          <p v-if="resource.environments.length === 0" class="scope-empty">该项目还没有环境。</p>
        </section>
      </fieldset>
      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
      <footer class="dialog-actions"><button class="button button-secondary" type="button" @click="createOpen = false">取消</button><button class="button button-primary" type="submit">创建令牌</button></footer>
    </form>
  </ModalDialog>

  <ModalDialog v-model:open="rotateOpen" title="轮换应用令牌" labelled-by="rotate-token-title">
    <form class="token-form" @submit.prevent="rotateToken">
      <p class="modal-description">“{{ rotatingToken?.name }}”的授权范围会原样保留。旧凭证将在新令牌创建时立即失效。</p>
      <label for="rotation-expiry">新令牌有效期至</label><input id="rotation-expiry" v-model="rotationExpiry" type="datetime-local" required>
      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
      <footer class="dialog-actions"><button class="button button-secondary" type="button" @click="rotateOpen = false">取消</button><button class="button button-primary" type="submit">确认轮换</button></footer>
    </form>
  </ModalDialog>

  <ModalDialog :open="issuedToken !== null" title="请立即复制新令牌" labelled-by="issued-token-title" @update:open="closeIssuedToken">
    <div v-if="issuedToken" class="issued-token-panel">
      <p>此凭证原文只显示这一次。关闭窗口后，服务端无法再次取回它。</p>
      <label for="issued-token-value">应用令牌</label>
      <textarea id="issued-token-value" readonly rows="4" :value="issuedToken.secret" @focus="($event.target as HTMLTextAreaElement).select()" />
      <footer class="dialog-actions"><button class="button button-secondary" type="button" @click="closeIssuedToken(false)">已保存并关闭</button><button class="button button-primary" type="button" :disabled="copying" @click="copySecret">{{ copying ? '正在复制…' : '复制令牌' }}</button></footer>
    </div>
  </ModalDialog>
</template>

<style scoped>
.token-page { width: min(100%, 1100px); margin: 0 auto; }
.page-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; padding: 8px 0 20px; }
.eyebrow { color: var(--color-muted); font-size: 9px; font-weight: 700; letter-spacing: 1.6px; }
.page-heading h2 { margin: 6px 0; font-size: 22px; }
.page-heading p, .token-dates { margin: 0; color: var(--color-muted); font-size: 12px; line-height: 1.7; }
.token-summary { display: flex; gap: 24px; border-top: 1px solid var(--color-border); border-bottom: 1px solid var(--color-border); padding: 12px 2px; color: var(--color-muted); font-size: 11px; }
.token-summary strong { color: var(--color-text); font-size: 13px; }
.page-alert { border: 1px solid #f2d1cd; border-radius: 7px; padding: 10px 12px; color: #9f3125; background: #fff8f7; font-size: 12px; }
.token-loading, .token-empty { display: grid; min-height: 300px; place-content: center; justify-items: center; gap: 8px; color: var(--color-muted); text-align: center; }
.empty-key { display: grid; width: 58px; height: 58px; place-items: center; border-radius: 17px; color: var(--el-color-primary); background: var(--el-color-primary-light-9); font-size: 28px; }
.token-empty h3 { margin: 8px 0 0; color: var(--color-text); font-size: 15px; }
.token-empty p { margin: 0; font-size: 12px; }
.token-list { display: grid; gap: 10px; padding-top: 12px; }
.token-card { display: flex; justify-content: space-between; gap: 18px; border: 1px solid var(--color-border); border-radius: 9px; padding: 15px 16px; background: var(--color-surface); }
.token-main { min-width: 0; }
.token-title-line { display: flex; align-items: center; gap: 10px; }
.token-title-line h3 { margin: 0; overflow-wrap: anywhere; font-size: 14px; }
.token-status { border-radius: 10px; padding: 3px 8px; color: #33754d; background: #edf6ef; font-size: 10px; }
.token-status.inactive { color: #8d5631; background: #f8f0e9; }
.token-dates { margin-top: 7px; font-size: 10px; }
.scope-list { display: flex; flex-wrap: wrap; gap: 5px; margin-top: 10px; }
.scope-chip { border: 1px solid #e7eee9; border-radius: 5px; padding: 4px 7px; color: #496957; background: #f7faf8; font-size: 10px; }
.token-actions { display: flex; flex: 0 0 auto; align-items: flex-start; gap: 5px; }
.load-more { justify-self: center; margin: 8px 0 24px; }
.token-form { display: grid; gap: 9px; }
.token-form > label, .issued-token-panel > label { margin-top: 4px; font-size: 12px; font-weight: 600; }
.scope-options { max-height: 270px; overflow: auto; margin: 8px 0 0; border: 1px solid var(--color-border); border-radius: 7px; padding: 10px 12px; }
.scope-options legend { padding-inline: 5px; font-size: 12px; font-weight: 600; }
.scope-project + .scope-project { margin-top: 12px; border-top: 1px solid var(--color-border); padding-top: 10px; }
.scope-project h3 { margin: 0 0 7px; font-size: 12px; }
.scope-project small { color: var(--color-muted); font-size: 10px; font-weight: 400; }
.scope-option { display: flex; align-items: center; gap: 8px; padding: 4px 0; font-size: 11px; cursor: pointer; }
.scope-option input { min-height: auto; }
.scope-empty, .modal-description { margin: 4px 0; color: var(--color-muted); font-size: 11px; line-height: 1.7; }
.form-error { margin: 0; color: var(--color-danger); font-size: 12px; }
.dialog-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 15px; }
.issued-token-panel { display: grid; gap: 10px; }
.issued-token-panel p { margin: 0; color: var(--color-muted); font-size: 12px; line-height: 1.7; }
.issued-token-panel textarea { width: 100%; resize: vertical; color: #263832; font-family: ui-monospace, Consolas, monospace; font-size: 11px; overflow-wrap: anywhere; }

@media (max-width: 650px) {
  .page-heading { flex-direction: column; }
  .token-card { flex-direction: column; }
  .token-actions { justify-content: flex-end; }
}
</style>
