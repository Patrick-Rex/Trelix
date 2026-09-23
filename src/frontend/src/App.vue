<script setup lang="ts">
import { onMounted, onUnmounted, shallowRef } from 'vue'
import { confirmAction } from '@/shared/components/confirmAction'
import AppShell from '@/shared/components/layout/AppShell.vue'
import LoginPage from '@/features/authentication/components/LoginPage.vue'
import ApplicationTokensPage from '@/features/application-tokens/pages/ApplicationTokensPage.vue'
import WorkspacePage from '@/pages/WorkspacePage.vue'
import type { AdminSession } from '@/shared/api/contracts'
import { ApiError, clearAntiforgeryToken } from '@/shared/api/http'
import * as authenticationApi from '@/shared/api/authentication'

/** 管理员界面可切换的功能页。 */
type AppView = 'workspace' | 'tokens'
/** 会话恢复和认证过程中需要展示的入口状态。 */
type SessionState = 'checking' | 'signed-out' | 'signed-in' | 'unavailable'

const session = shallowRef<AdminSession | null>(null)
const sessionState = shallowRef<SessionState>('checking')
const activeView = shallowRef<AppView>('workspace')
const workspaceHasChanges = shallowRef(false)
const sessionExpired = shallowRef(false)
const startupError = shallowRef('')

/** 处理任一管理 API 返回的失效会话并清除本地防伪造状态。 */
function handleSessionExpired(): void {
  session.value = null
  sessionState.value = 'signed-out'
  sessionExpired.value = true
  clearAntiforgeryToken()
}

/**
 * 恢复浏览器中由服务端管理的当前 Cookie 会话。
 * @returns 异步操作完成时兑现。
 */
async function restoreSession(): Promise<void> {
  sessionState.value = 'checking'
  startupError.value = ''
  try {
    session.value = await authenticationApi.getSession()
    sessionState.value = 'signed-in'
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      session.value = null
      sessionState.value = 'signed-out'
      return
    }
    startupError.value = error instanceof Error ? error.message : '无法连接配置服务。'
    sessionState.value = 'unavailable'
  }
}

/**
 * 在登录成功后显示管理员工作区。
 * @param value 本次操作接收的值。
 */
function handleLoggedIn(value: AdminSession): void {
  session.value = value
  sessionState.value = 'signed-in'
  sessionExpired.value = false
  activeView.value = 'workspace'
}

/**
 * 注销服务端会话并显示登录页。
 * @returns 异步操作完成时兑现。
 */
async function handleLogout(): Promise<void> {
  const prompt = workspaceHasChanges.value
    ? '工作区有未保存内容。退出会放弃本地修改，确定退出吗？'
    : '确定退出当前管理员会话吗？'
  if (!(await confirmAction(prompt))) return
  try {
    await authenticationApi.logout()
    clearAntiforgeryToken()
    session.value = null
    sessionState.value = 'signed-out'
    sessionExpired.value = false
  } catch (error) {
    startupError.value = error instanceof Error ? error.message : '退出失败，请重试。'
  }
}

/**
 * 在离开编辑工作区前保留管理员未保存的编辑缓冲区。
 * @param view 目标管理页面。
 * @returns 异步操作完成时兑现。
 */
async function handleNavigate(view: AppView): Promise<void> {
  if (activeView.value === 'workspace' && view !== 'workspace' && workspaceHasChanges.value
    && !(await confirmAction('工作区有未保存内容。切换到应用令牌页面会放弃本地修改，确定继续吗？'))) return
  activeView.value = view
}

/** 注册会话失效监听器并开始检查认证状态。 */
onMounted(() => {
  window.addEventListener('trelix:session-expired', handleSessionExpired)
  void restoreSession()
})
/** 卸载应用入口时清理全局会话监听。 */
onUnmounted(() => window.removeEventListener('trelix:session-expired', handleSessionExpired))
</script>

<template>
  <div v-if="sessionState === 'checking'" class="app-loading" role="status">正在检查管理员会话…</div>
  <section v-if="sessionState === 'unavailable'" class="app-unavailable" role="alert">
    <h1>暂时无法连接配置服务</h1>
    <p>{{ startupError }}</p>
    <button class="button button-primary" type="button" @click="restoreSession">重新连接</button>
  </section>
  <AppShell
    v-if="session || sessionExpired"
    v-show="sessionState === 'signed-in'"
    :active-view="activeView"
    :username="session?.username ?? ''"
    @navigate="handleNavigate"
    @logout="handleLogout"
  >
    <WorkspacePage v-if="activeView === 'workspace'" @dirty-change="workspaceHasChanges = $event" />
    <ApplicationTokensPage v-else />
  </AppShell>
  <LoginPage v-if="sessionState === 'signed-out'" :session-expired="sessionExpired" @logged-in="handleLoggedIn" />
</template>

<style scoped>
.app-loading,
.app-unavailable {
  display: grid;
  min-height: 100dvh;
  place-content: center;
  justify-items: center;
  gap: 12px;
  padding: 24px;
  text-align: center;
}

.app-unavailable h1,
.app-unavailable p {
  margin: 0;
}

.app-unavailable p {
  color: var(--color-muted);
}
</style>
