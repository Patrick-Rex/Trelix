<script setup lang="ts">
import { shallowRef, watch } from 'vue'
import type { Release, ReleaseDetail } from '@/shared/api/contracts'
import ModalDialog from '@/shared/components/ModalDialog.vue'
import { confirmAction } from '@/shared/components/confirmAction'

const props = defineProps<{
  open: boolean
  releases: readonly Release[]
  currentVersion: number | null
  preview: ReleaseDetail | null
  loadingPreview: boolean
}>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  selectVersion: [version: number]
  rollback: [version: number]
}>()
const selectedVersion = shallowRef<number | null>(null)

/** 打开历史面板时默认载入最近一次发布快照。 */
watch(() => props.open, (open) => {
  if (!open) return
  const newest = props.releases[0]
  selectedVersion.value = newest?.version ?? null
  if (newest) emit('selectVersion', newest.version)
})

/**
 * 选择指定发布版本查看不可变正文。
 * @param version 所选发布版本号。
 */
function chooseVersion(version: number): void {
  selectedVersion.value = version
  emit('selectVersion', version)
}

/**
 * 确认以所选历史正文创建一个新的发布版本。
 * @returns 异步操作完成时兑现。
 */
async function rollback(): Promise<void> {
  if (selectedVersion.value === null) return
  const version = selectedVersion.value
  if (await confirmAction(`将以 v${version} 的内容创建新发布版本。当前草稿和本地编辑不会被修改。确定继续吗？`)) {
    emit('rollback', version)
  }
}

/**
 * 将时间格式化为本地可读的中文时间。
 * @param value 本次操作接收的值。
 * @returns 按本地时区格式化的时间文字。
 */
function formatDate(value: string): string {
  return new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

</script>

<template>
  <ModalDialog :open="open" title="发布历史" labelled-by="release-history-title" @update:open="emit('update:open', $event)">
    <div class="history-layout">
      <section class="version-list" aria-label="版本列表">
        <p v-if="props.releases.length === 0" class="history-empty">还没有发布记录。</p>
        <button v-for="release in props.releases" :key="release.version" class="version-item" :class="{ selected: selectedVersion === release.version }" type="button" @click="chooseVersion(release.version)">
          <span class="version-title">v{{ release.version }} <span v-if="release.version === props.currentVersion" class="current-tag">当前</span></span>
          <span class="version-date">{{ formatDate(release.publishedAt) }}</span>
          <span class="version-source">草稿修订 {{ release.draftRevision }}<template v-if="release.sourceVersion"> · 回滚自 v{{ release.sourceVersion }}</template></span>
        </button>
      </section>
      <section class="preview-panel" aria-label="历史配置预览">
        <div class="preview-heading"><h3>{{ props.preview ? `版本 v${props.preview.release.version}` : '选择一个版本' }}</h3><button class="button button-secondary" type="button" :disabled="selectedVersion === null" @click="rollback">回滚并发布</button></div>
        <p v-if="props.loadingPreview" class="preview-message" role="status">正在载入历史配置…</p>
        <p v-else-if="!props.preview" class="preview-message">选择版本以查看只读配置正文。</p>
        <pre v-else class="json-preview"><code>{{ JSON.stringify(JSON.parse(props.preview.json), null, 2) }}</code></pre>
      </section>
    </div>
  </ModalDialog>
</template>

<style scoped>
.history-layout { display: grid; min-height: 320px; grid-template-columns: minmax(190px, 0.8fr) minmax(0, 1.5fr); gap: 18px; }
.version-list { display: grid; align-content: start; gap: 5px; max-height: 58dvh; overflow: auto; }
.version-item { display: grid; gap: 4px; border: 1px solid transparent; border-radius: 7px; padding: 10px; color: var(--color-text); background: #f7f9f7; text-align: left; cursor: pointer; }
.version-item.selected { border-color: var(--el-color-primary-light-5); background: var(--el-color-primary-light-9); }
.version-title { font-size: 12px; font-weight: 650; }
.current-tag { margin-left: 5px; color: var(--el-color-primary); font-size: 10px; }
.version-date, .version-source { color: var(--color-muted); font-size: 10px; }
.preview-panel { display: flex; min-width: 0; flex-direction: column; border: 1px solid var(--color-border); border-radius: 8px; overflow: hidden; }
.preview-heading { display: flex; align-items: center; justify-content: space-between; gap: 8px; border-bottom: 1px solid var(--color-border); padding: 8px 10px; }
.preview-heading h3 { margin: 0; font-size: 12px; }
.preview-message, .history-empty { margin: auto; padding: 18px; color: var(--color-muted); font-size: 11px; text-align: center; }
.json-preview { flex: 1; max-height: 52dvh; overflow: auto; margin: 0; padding: 12px; color: #35443b; background: #fbfcfb; font-size: 10px; line-height: 1.6; tab-size: 2; }

@media (max-width: 650px) { .history-layout { grid-template-columns: 1fr; } .version-list { max-height: 180px; } }
</style>
