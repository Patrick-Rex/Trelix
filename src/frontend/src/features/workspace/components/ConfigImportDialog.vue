<script setup lang="ts">
import { onBeforeUnmount, shallowRef, watch } from 'vue'
import ModalDialog from '@/shared/components/ModalDialog.vue'
import { readConfigurationFile } from '../utils/configTransfer'
import type { ImportedConfiguration } from '../utils/configTransfer'

const props = defineProps<{ open: boolean; applying: boolean; error: string | null }>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  import: [configuration: ImportedConfiguration]
}>()
const candidate = shallowRef<ImportedConfiguration | null>(null)
const reading = shallowRef(false)
const readError = shallowRef('')
let readId = 0

/** 打开或关闭时清理候选文件，并隔离尚未结束的本地文件读取。 */
watch(() => props.open, () => {
  readId++
  candidate.value = null
  reading.value = false
  readError.value = ''
})
/** 卸载后忽略尚未完成的本地读取。 */
onBeforeUnmount(() => { readId++ })

/**
 * 读取当前选中的配置文件；失败时保留原工作区正文。
 * @param event 原生文件选择事件。
 * @returns 文件校验结束时兑现。
 */
async function selectFile(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  const id = ++readId
  candidate.value = null
  readError.value = ''
  reading.value = true
  try {
    const configuration = await readConfigurationFile(file)
    if (id === readId && props.open) candidate.value = configuration
  } catch (error) {
    if (id === readId && props.open) readError.value = error instanceof Error ? error.message : '文件读取失败。'
  } finally {
    if (id === readId) reading.value = false
  }
}

/** 提交已通过校验的候选文件，由工作区处理未保存内容确认。 */
function submit(): void {
  if (candidate.value && !reading.value && !props.applying) emit('import', candidate.value)
}
</script>

<template>
  <ModalDialog :open="props.open" title="导入配置" labelled-by="configuration-import-title" @update:open="!props.applying && emit('update:open', $event)">
    <form class="transfer-form" @submit.prevent="submit">
      <p class="transfer-description">选择单个 UTF-8 JSON/YAML 文件（最多 1 MiB），校验后载入当前编辑区，再手动保存草稿和发布。</p>
      <label for="configuration-import-file">配置文件</label>
      <input id="configuration-import-file" type="file" accept=".json,.yaml,.yml" :disabled="reading || props.applying" @change="selectFile">
      <p v-if="reading" role="status">正在读取并校验文件…</p>
      <p v-if="candidate" class="transfer-description" role="status">{{ candidate.name }} · {{ candidate.format.toUpperCase() }} · 校验通过</p>
      <p v-if="candidate?.format === 'yaml'" class="transfer-description">YAML 注释、锚点名称和排版不会持久化。</p>
      <p v-if="readError || props.error" class="transfer-error" role="alert">{{ readError || props.error }}</p>
      <footer class="transfer-actions">
        <button class="button button-secondary" type="button" :disabled="props.applying" @click="emit('update:open', false)">取消</button>
        <button class="button button-primary" type="submit" :disabled="!candidate || reading || props.applying">{{ props.applying ? '正在载入…' : '载入编辑区' }}</button>
      </footer>
    </form>
  </ModalDialog>
</template>

<style scoped src="./configTransfer.css" />
