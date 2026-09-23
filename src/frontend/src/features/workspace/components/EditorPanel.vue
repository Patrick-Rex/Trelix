<script setup lang="ts">
import { computed, defineAsyncComponent } from 'vue'
import type { ConfigFile, JsonObject } from '@/shared/api/contracts'
import type { DocumentFormat } from '../utils/configDocument'
import JsonTreeEditor from './JsonTreeEditor.vue'

const MonacoCodeEditor = defineAsyncComponent(() => import('./MonacoCodeEditor.vue'))

const props = defineProps<{
  file: ConfigFile | null
  text: string
  format: DocumentFormat
  value: JsonObject
  dirty: boolean
  valid: boolean
  validationPending: boolean
  validationError: string | null
  loadingFile: boolean
  draftRevision: number
  saving: boolean
  publishing: boolean
  canPublish: boolean
  conflictDetected: boolean
  errorMessage: string | null
}>()
const emit = defineEmits<{
  updateText: [text: string]
  updateTree: [value: JsonObject]
  treeValidationChange: [message: string | null]
  changeFormat: [format: DocumentFormat]
  save: []
  publish: []
  showHistory: []
  renameFile: []
  deleteFile: []
  reload: []
}>()

const editorLanguage = computed(() => props.format === 'yaml' ? 'yaml' : 'json')
</script>

<template>
  <section class="editor-workbench" aria-label="配置编辑器">
    <header class="editor-header">
      <div class="file-heading">
        <span class="file-icon" aria-hidden="true">{ }</span>
        <div class="file-title-group">
          <h2>{{ props.file?.name ?? '选择配置文件' }}</h2>
          <span v-if="props.file" class="file-meta">草稿修订 {{ props.draftRevision }}<template v-if="props.file.currentReleaseVersion"> · 当前发布 v{{ props.file.currentReleaseVersion }}</template></span>
          <span v-else class="file-meta">从左侧选择项目、环境和文件</span>
        </div>
        <span v-if="props.file" class="save-state" :class="{ modified: props.dirty }" role="status">
          <i aria-hidden="true" />{{ props.dirty ? '有未保存更改' : '已保存' }}
        </span>
      </div>
      <div class="toolbar-actions">
        <slot name="connection-action" />
        <button class="button button-secondary" type="button" :disabled="!props.file || props.loadingFile" @click="emit('showHistory')">发布历史</button>
        <button class="button button-secondary" type="button" :disabled="!props.file || props.loadingFile" @click="emit('renameFile')">重命名</button>
        <button class="button button-quiet button-danger" type="button" :disabled="!props.file || props.loadingFile" @click="emit('deleteFile')">删除</button>
        <button class="button button-secondary" type="button" :disabled="!props.file || props.loadingFile || !props.dirty || props.saving" @click="emit('save')">
          {{ props.saving ? '正在保存…' : '保存草稿' }}
        </button>
        <button class="button button-primary" type="button" :disabled="!props.canPublish || props.loadingFile" @click="emit('publish')">
          {{ props.publishing ? '正在发布…' : '发布' }}
        </button>
      </div>
    </header>

    <div v-if="props.file" class="editor-tabs" role="tablist" aria-label="编辑表示">
      <button v-for="format in (['json', 'yaml', 'tree'] as const)" :key="format" class="format-tab" :class="{ active: props.format === format }" type="button" role="tab" :aria-selected="props.format === format" :disabled="props.loadingFile" @click="emit('changeFormat', format)">
        {{ format === 'json' ? 'JSON' : format === 'yaml' ? 'YAML' : 'Tree' }}
      </button>
      <span class="format-hint">同一份配置数据</span>
    </div>

    <div v-if="props.errorMessage" class="editor-alert" role="alert">
      <span>{{ props.errorMessage }}</span>
      <button v-if="props.conflictDetected" class="button button-quiet" type="button" @click="emit('reload')">放弃本地修改并重新读取</button>
    </div>
    <div v-if="props.validationError" class="validation-alert" role="alert">{{ props.validationError }}</div>
    <div v-else-if="props.validationPending" class="validation-pending" role="status">正在校验当前输入…</div>

    <div v-if="props.file" class="editor-body">
      <div class="code-view" v-show="props.format !== 'tree'">
        <Suspense>
          <MonacoCodeEditor :key="props.file.id" :value="props.text" :format="editorLanguage" @update="emit('updateText', $event)" />
          <template #fallback><div class="editor-loading" role="status">正在加载编辑器…</div></template>
        </Suspense>
      </div>
      <JsonTreeEditor v-if="props.format === 'tree'" :key="props.file.id" :value="props.value" @update="emit('updateTree', $event)" @validation-change="emit('treeValidationChange', $event)" />
      <div v-if="props.loadingFile" class="loading-overlay" role="status">正在读取配置草稿…</div>
    </div>
    <div v-else class="editor-empty">
      <div class="empty-mark" aria-hidden="true">{ }</div>
      <h3>从一份配置开始</h3>
      <p>选择现有配置文件，或在左侧新建一个配置文件。</p>
    </div>

    <footer class="editor-statusbar">
      <span>{{ props.file ? `${props.file.name} · ${props.format.toUpperCase()}` : '未选择配置文件' }}</span>
      <span>{{ props.valid ? '格式有效' : '请检查输入' }}</span>
    </footer>
  </section>
</template>

<style scoped>
.editor-workbench { display: flex; min-width: 0; min-height: 520px; flex: 1; flex-direction: column; overflow: hidden; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-surface); }
.editor-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; min-height: 70px; border-bottom: 1px solid var(--color-border); padding: 12px 18px; }
.file-heading { display: flex; min-width: 0; align-items: center; gap: 11px; }
.file-icon { display: grid; width: 32px; height: 32px; flex: 0 0 32px; place-items: center; border-radius: 8px; color: #9c783f; background: #f7f1e7; font-family: ui-monospace, monospace; font-size: 10px; }
.file-title-group { display: grid; min-width: 0; gap: 4px; }
.file-title-group h2 { overflow: hidden; margin: 0; text-overflow: ellipsis; white-space: nowrap; font-size: 14px; }
.file-meta { color: var(--color-muted); font-size: 10px; }
.save-state { display: inline-flex; align-items: center; gap: 6px; margin-left: 10px; color: #43805d; white-space: nowrap; font-size: 10px; }
.save-state i { width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
.save-state.modified { color: #a36d26; }
.toolbar-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 5px; }
.toolbar-actions .button { min-height: 32px; padding: 6px 9px; font-size: 11px; }
.editor-tabs { display: flex; align-items: center; gap: 2px; border-bottom: 1px solid var(--color-border); padding: 0 16px; }
.format-tab { position: relative; min-height: 40px; border: 0; padding: 0 13px; color: var(--color-muted); background: transparent; font: inherit; font-size: 12px; cursor: pointer; }
.format-tab.active { color: var(--el-color-primary); font-weight: 650; }
.format-tab.active::after { position: absolute; right: 10px; bottom: -1px; left: 10px; height: 2px; background: var(--el-color-primary); content: ''; }
.format-hint { margin-left: auto; color: var(--color-muted); font-size: 10px; }
.editor-alert, .validation-alert, .validation-pending { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 8px 16px; font-size: 11px; }
.editor-alert { color: #7b2e25; background: #fff4f1; }
.validation-alert { display: block; border-bottom: 1px solid #f3d3ce; color: #9f3125; background: #fff9f8; }
.validation-pending { justify-content: flex-start; color: #8a6525; background: #fffbf1; }
.editor-body { position: relative; display: flex; min-height: 300px; flex: 1; overflow: hidden; }
.loading-overlay { position: absolute; inset: 0; z-index: 2; display: grid; place-items: center; color: var(--color-muted); background: #ffffffd9; font-size: 12px; }
.code-view { display: flex; width: 100%; min-height: 300px; flex: 1; }
.code-view :deep(.monaco-container) { flex: 1; }
.editor-loading { display: grid; width: 100%; min-height: 300px; place-items: center; color: var(--color-muted); }
.editor-empty { display: grid; flex: 1; align-content: center; justify-items: center; padding: 40px 18px; text-align: center; }
.empty-mark { display: grid; width: 64px; height: 64px; place-items: center; border-radius: 18px; color: var(--el-color-primary); background: var(--el-color-primary-light-9); font-family: ui-monospace, monospace; font-size: 18px; }
.editor-empty h3 { margin: 18px 0 8px; font-size: 16px; }
.editor-empty p { margin: 0; color: var(--color-muted); font-size: 12px; }
.editor-statusbar { display: flex; justify-content: space-between; gap: 12px; border-top: 1px solid var(--color-border); padding: 8px 13px; color: var(--color-muted); font-size: 10px; }

@media (max-width: 1000px) {
  .editor-header { align-items: flex-start; flex-direction: column; }
  .toolbar-actions { justify-content: flex-start; }
}
@media (max-width: 560px) {
  .toolbar-actions { display: grid; width: 100%; grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .save-state { margin-left: 2px; }
}
</style>
