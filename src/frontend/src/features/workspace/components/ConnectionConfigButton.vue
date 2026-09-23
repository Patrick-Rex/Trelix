<script setup lang="ts">
import { computed, shallowRef, watch } from 'vue'
import { ElInput } from 'element-plus'
import ModalDialog from '@/shared/components/ModalDialog.vue'

const props = defineProps<{
  projectKey?: string
  environmentKey?: string
  fileName?: string
  published: boolean
  disabled: boolean
}>()

const open = shallowRef(false)
const serverUrl = shallowRef(window.location.origin)
const copying = shallowRef(false)
const copied = shallowRef(false)
const copyError = shallowRef('')
const hasSelection = computed(() => props.projectKey !== undefined && props.environmentKey !== undefined && props.fileName !== undefined)
const normalizedServerUrl = computed(() => {
  try {
    const url = new URL(serverUrl.value.trim())
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) return null
    return url.href.replace(/\/+$/, '')
  } catch {
    return null
  }
})
const connectionJson = computed(() => JSON.stringify({
  Trelix: {
    ServerUrl: normalizedServerUrl.value ?? serverUrl.value.trim(),
    ProjectKey: props.projectKey,
    EnvironmentKey: props.environmentKey,
    FileName: props.fileName,
    AccessToken: '<APPLICATION_TOKEN>',
  },
}, null, 2))

watch(connectionJson, () => { copied.value = false; copyError.value = '' })

/**
 * 一次点击复制当前文件的连接配置，同时打开预览供核对或调整服务地址。
 * @returns 复制请求处理完成时兑现；失败时保留可手动复制的预览。
 */
async function openAndCopy(): Promise<void> {
  if (props.disabled || !hasSelection.value || copying.value) return
  open.value = true
  await copyConfiguration()
}

/**
 * 将完整 Trelix 连接节写入剪贴板，成功提示仅对应本次复制的内容。
 * @returns 复制完成时兑现；权限或环境限制以不含配置正文的消息反馈。
 */
async function copyConfiguration(): Promise<void> {
  if (props.disabled || !hasSelection.value || !normalizedServerUrl.value || copying.value) return
  const content = connectionJson.value
  copying.value = true
  copied.value = false
  copyError.value = ''
  let timeout: ReturnType<typeof setTimeout> | undefined
  try {
    if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable')
    await Promise.race([
      navigator.clipboard.writeText(content),
      new Promise<never>((_, reject) => {
        timeout = setTimeout(() => reject(new Error('Clipboard timed out')), 5000)
      }),
    ])
    if (open.value && content === connectionJson.value) copied.value = true
  } catch {
    if (open.value && content === connectionJson.value) copyError.value = '无法访问剪贴板，请选中下方完整配置后手动复制。'
  } finally {
    clearTimeout(timeout)
    copying.value = false
  }
}
</script>

<template>
  <button class="button button-secondary connection-trigger" type="button" :disabled="props.disabled || !hasSelection || copying" @click="openAndCopy">复制连接配置</button>
  <ModalDialog v-model:open="open" title="连接配置" labelled-by="connection-config-title">
    <div class="connection-form">
      <label for="connection-server-url">Trelix 服务地址</label>
      <ElInput id="connection-server-url" v-model="serverUrl" type="url" aria-describedby="connection-address-help" />
      <p id="connection-address-help" class="connection-hint">默认使用当前站点地址。开发或跨主机接入时，请改为业务应用可访问的 Trelix 服务地址，再复制配置。</p>
      <p v-if="!normalizedServerUrl" class="connection-error" role="alert">请输入完整的 HTTP 或 HTTPS 地址，不包含凭证、查询参数或片段。</p>
      <p v-if="!props.published" class="connection-warning" role="status">此文件尚未发布，业务应用需在首次发布后才能读取配置。</p>
      <label for="connection-config-preview">appsettings.json 配置片段</label>
      <ElInput id="connection-config-preview" :model-value="connectionJson" type="textarea" :rows="10" readonly spellcheck="false" />
      <p class="connection-hint">将 Trelix 节合并到业务应用的本地配置。AccessToken 为占位符，请通过 User Secrets 或环境变量 Trelix__AccessToken 注入应用令牌。</p>
      <p v-if="copied" class="connection-success" role="status">已复制连接配置，应用令牌使用占位符。</p>
      <p v-if="copyError" class="connection-error" role="alert">{{ copyError }}</p>
      <footer class="connection-actions">
        <button class="button button-secondary" type="button" @click="open = false">关闭</button>
        <button class="button button-primary" type="button" :disabled="props.disabled || !hasSelection || !normalizedServerUrl || copying" @click="copyConfiguration">{{ copying ? '复制中…' : '复制配置' }}</button>
      </footer>
    </div>
  </ModalDialog>
</template>

<style scoped>
.connection-trigger { min-height: 32px; padding: 6px 9px; font-size: 11px; }
.connection-form { display: grid; gap: 10px; }
.connection-form label { font-size: 12px; font-weight: 600; }
.connection-form :deep(textarea) { font-family: ui-monospace, monospace; font-size: 12px; }
.connection-hint, .connection-error, .connection-warning, .connection-success { margin: 0; font-size: 12px; line-height: 1.6; }
.connection-hint { color: var(--color-muted); }
.connection-error { color: var(--color-danger); }
.connection-warning { color: #8a6525; }
.connection-success { color: var(--el-color-primary); }
.connection-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 6px; }
</style>
