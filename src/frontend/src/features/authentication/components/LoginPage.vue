<script setup lang="ts">
import { shallowRef } from 'vue'
import { ElInput } from 'element-plus'
import type { AdminSession } from '@/shared/api/contracts'
import * as authenticationApi from '@/shared/api/authentication'

defineProps<{ sessionExpired: boolean }>()
const emit = defineEmits<{ loggedIn: [session: AdminSession] }>()
const username = shallowRef('')
const password = shallowRef('')
const submitting = shallowRef(false)
const errorMessage = shallowRef('')

/**
 * 使用内置管理员凭证建立 Cookie 会话。
 * @returns 异步操作完成时兑现。
 */
async function submitLogin(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  errorMessage.value = ''
  try {
    emit('loggedIn', await authenticationApi.login(username.value, password.value))
    password.value = ''
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '登录失败，请重试。'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <section class="login-card" aria-labelledby="login-title">
      <div class="login-brand"><span class="brand-mark" aria-hidden="true">T</span><span>Trelix 配置中心</span></div>
      <h1 id="login-title">管理员登录</h1>
      <p class="login-description">登录后管理项目配置、发布版本和应用令牌。</p>
      <p v-if="sessionExpired" class="session-expired" role="status">管理员会话已失效，请重新登录。</p>
      <form class="login-form" @submit.prevent="submitLogin">
        <label for="username">管理员账号</label>
        <ElInput id="username" v-model="username" name="username" autocomplete="username" required maxlength="128" />
        <label for="password">密码</label>
        <ElInput id="password" v-model="password" name="password" type="password" autocomplete="current-password" required maxlength="1024" show-password />
        <p v-if="errorMessage" class="form-error" role="alert">{{ errorMessage }}</p>
        <button class="button button-primary login-submit" type="submit" :disabled="submitting">
          {{ submitting ? '正在登录…' : '登录' }}
        </button>
      </form>
      <p class="login-note">管理员账号由部署初始化配置创建。</p>
    </section>
  </main>
</template>

<style scoped>
.login-page {
  display: grid;
  min-height: 100dvh;
  place-items: center;
  padding: 24px;
}

.login-card {
  width: min(100%, 420px);
  border: 1px solid var(--color-border);
  border-radius: 14px;
  padding: 36px;
  background: var(--color-surface);
  box-shadow: 0 18px 50px #2638320e;
}

.login-brand {
  display: flex;
  align-items: center;
  gap: 12px;
  font-weight: 650;
}

.brand-mark {
  display: grid;
  width: 34px;
  height: 34px;
  place-items: center;
  border-radius: 9px;
  color: white;
  background: var(--el-color-primary);
  font-size: 22px;
}

h1 { margin: 30px 0 8px; font-size: 22px; }
.login-description, .login-note { color: var(--color-muted); font-size: 13px; line-height: 1.7; }
.login-description { margin: 0; }
.login-form { display: grid; gap: 9px; margin-top: 25px; }
.login-form label { margin-top: 8px; font-size: 13px; font-weight: 600; }
.login-form input { width: 100%; }
.login-submit { width: 100%; margin-top: 12px; }
.login-note { margin: 18px 0 0; font-size: 11px; }
.form-error { margin: 2px 0; color: var(--color-danger); font-size: 12px; }
.session-expired { margin: 14px 0 -8px; border-radius: 6px; padding: 9px 10px; color: #8a6525; background: #fffbf1; font-size: 12px; }

@media (max-width: 480px) {
  .login-card { padding: 26px 22px; }
}
</style>
