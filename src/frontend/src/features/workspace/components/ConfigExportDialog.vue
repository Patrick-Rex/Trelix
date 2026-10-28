<script setup lang="ts">
import { shallowRef, watch } from 'vue'
import ModalDialog from '@/shared/components/ModalDialog.vue'
import type { ConfigurationFileFormat, ConfigurationSource } from '../utils/configTransfer'

const props = defineProps<{ open: boolean; published: boolean; busy: boolean; error: string | null }>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  export: [source: ConfigurationSource, format: ConfigurationFileFormat]
}>()
const source = shallowRef<ConfigurationSource>('editing')
const format = shallowRef<ConfigurationFileFormat>('json')

/** 每次进入导出表单时明确重置内容来源和文件格式。 */
watch(() => props.open, (open) => {
  if (open) { source.value = 'editing'; format.value = 'json' }
})

/** 提交明确选择的来源及格式，下载由工作区动作执行。 */
function submit(): void {
  if (!props.busy) emit('export', source.value, format.value)
}
</script>

<template>
  <ModalDialog :open="props.open" title="导出配置" labelled-by="configuration-export-title" @update:open="!props.busy && emit('update:open', $event)">
    <form class="transfer-form" @submit.prevent="submit">
      <label for="configuration-export-source">内容来源</label>
      <select id="configuration-export-source" v-model="source" :disabled="props.busy">
        <option value="editing">当前编辑内容（包含未保存修改）</option>
        <option value="published" :disabled="!props.published">当前已发布版本</option>
      </select>
      <label for="configuration-export-format">文件格式</label>
      <select id="configuration-export-format" v-model="format" :disabled="props.busy"><option value="json">JSON</option><option value="yaml">YAML</option></select>
      <p class="transfer-description">导出会校验所选内容，不保存草稿、不发布配置。</p>
      <p v-if="!props.published" class="transfer-description">此文件尚未发布，可导出当前编辑内容。</p>
      <p v-if="props.error" class="transfer-error" role="alert">{{ props.error }}</p>
      <footer class="transfer-actions">
        <button class="button button-secondary" type="button" :disabled="props.busy" @click="emit('update:open', false)">取消</button>
        <button class="button button-primary" type="submit" :disabled="props.busy">{{ props.busy ? '正在准备…' : '下载配置' }}</button>
      </footer>
    </form>
  </ModalDialog>
</template>

<style scoped src="./configTransfer.css" />
