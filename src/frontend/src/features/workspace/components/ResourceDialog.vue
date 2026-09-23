<script setup lang="ts">
import { shallowRef, watch } from 'vue'
import { ElInput } from 'element-plus'
import ModalDialog from '@/shared/components/ModalDialog.vue'

/** 资源表单支持的资源层级。 */
type ResourceKind = 'project' | 'environment' | 'file'

const props = defineProps<{
  open: boolean
  kind: ResourceKind
  mode: 'create' | 'rename'
  initialKey?: string
  initialDisplayName?: string
  initialName?: string
  saving?: boolean
  error?: string | null
}>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  save: [value: { key?: string; displayName?: string; name?: string }]
}>()

const key = shallowRef('')
const displayName = shallowRef('')
const name = shallowRef('')
const validationError = shallowRef('')
const title = shallowRef('')

/** 每次打开时按资源类型重置表单和初始值。 */
watch(() => props.open, (open) => {
  if (!open) return
  key.value = props.initialKey ?? ''
  displayName.value = props.initialDisplayName ?? ''
  name.value = props.initialName ?? ''
  validationError.value = ''
  title.value = dialogTitle(props.kind, props.mode)
}, { flush: 'post' })

/** 通过表单提交所选资源名称。 */
function submit(): void {
  if (props.saving) return
  validationError.value = ''
  if (props.kind === 'file') {
    if (!name.value.trim()) { validationError.value = '文件名不能为空或仅包含空白。'; return }
    emit('save', { name: name.value })
    return
  }
  if (!key.value.trim() || !displayName.value.trim()) {
    validationError.value = '标识和显示名称都不能为空或仅包含空白。'
    return
  }
  if (key.value.length > 128 || displayName.value.length > 200) {
    validationError.value = '标识最多 128 个字符，显示名称最多 200 个字符。'
    return
  }
  emit('save', { key: key.value, displayName: displayName.value })
}

/**
 * 按资源种类和操作生成对话框标题。
 * @param kind 资源类型。
 * @param mode 资源创建或重命名模式。
 * @returns 对应资源操作的中文标题。
 */
function dialogTitle(kind: ResourceKind, mode: 'create' | 'rename'): string {
  const label = kind === 'project' ? '项目' : kind === 'environment' ? '环境' : '配置文件'
  return mode === 'create' ? `新建${label}` : `重命名${label}`
}

</script>

<template>
  <ModalDialog :open="open" :title="title" labelled-by="resource-dialog-title" @update:open="emit('update:open', $event)">
    <form class="resource-form" @submit.prevent="submit">
      <template v-if="kind === 'file'">
        <label for="resource-file-name">文件名</label>
        <ElInput id="resource-file-name" v-model="name" required maxlength="256" autofocus />
      </template>
      <template v-else>
        <label for="resource-key">业务标识</label>
        <ElInput id="resource-key" v-model="key" required maxlength="128" autofocus />
        <label for="resource-display-name">显示名称</label>
        <ElInput id="resource-display-name" v-model="displayName" required maxlength="200" />
      </template>
      <p v-if="validationError" class="form-error" role="alert">{{ validationError }}</p>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <footer class="dialog-actions">
        <button class="button button-secondary" type="button" @click="emit('update:open', false)">取消</button>
        <button class="button button-primary" type="submit" :disabled="saving">{{ saving ? '保存中…' : '保存' }}</button>
      </footer>
    </form>
  </ModalDialog>
</template>

<style scoped>
.resource-form { display: grid; gap: 10px; }
.resource-form label { margin-top: 5px; font-size: 12px; font-weight: 600; }
.form-error { margin: 0; color: var(--color-danger); font-size: 12px; }
.dialog-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 15px; }
</style>
