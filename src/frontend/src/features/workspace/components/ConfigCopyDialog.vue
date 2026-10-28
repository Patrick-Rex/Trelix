<script setup lang="ts">
import { computed, shallowRef, watch } from 'vue'
import { ElInput } from 'element-plus'
import ModalDialog from '@/shared/components/ModalDialog.vue'
import type { Environment } from '@/shared/api/contracts'
import type { ConfigurationSource } from '../utils/configTransfer'

const props = defineProps<{
  open: boolean
  environments: readonly Environment[]
  currentEnvironmentId: string | null
  fileName: string
  published: boolean
  busy: boolean
  error: string | null
}>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  copy: [source: ConfigurationSource, environmentId: string, name: string]
}>()
const source = shallowRef<ConfigurationSource>('editing')
const environmentId = shallowRef('')
const name = shallowRef('')
const validationError = shallowRef('')
const targets = computed(() => props.environments.filter((environment) => environment.id !== props.currentEnvironmentId))

/** 打开复制表单时保留原文件名供明确修改，目标环境必须主动选择。 */
watch(() => props.open, (open) => {
  if (!open) return
  source.value = 'editing'
  environmentId.value = ''
  name.value = props.fileName
  validationError.value = ''
})

/** 校验目标环境和文件名后发出仅创建新文件的复制请求。 */
function submit(): void {
  if (props.busy) return
  validationError.value = ''
  if (!targets.value.some((environment) => environment.id === environmentId.value)) {
    validationError.value = '请选择当前项目中的其他环境。'
    return
  }
  if (!name.value.trim() || name.value.length > 256) {
    validationError.value = '文件名不能为空或仅含空白，且最多 256 个字符。'
    return
  }
  emit('copy', source.value, environmentId.value, name.value)
}
</script>

<template>
  <ModalDialog :open="props.open" title="复制到其他环境" labelled-by="configuration-copy-title" @update:open="!props.busy && emit('update:open', $event)">
    <form class="transfer-form" @submit.prevent="submit">
      <label for="configuration-copy-source">内容来源</label>
      <select id="configuration-copy-source" v-model="source" :disabled="props.busy"><option value="editing">当前编辑内容（包含未保存修改）</option><option value="published" :disabled="!props.published">当前已发布版本</option></select>
      <label for="configuration-copy-environment">目标环境</label>
      <select id="configuration-copy-environment" v-model="environmentId" :disabled="props.busy"><option value="" disabled>选择同项目其他环境</option><option v-for="environment in targets" :key="environment.id" :value="environment.id">{{ environment.displayName }} · {{ environment.key }}</option></select>
      <label for="configuration-copy-name">目标文件名</label>
      <ElInput id="configuration-copy-name" v-model="name" maxlength="256" :disabled="props.busy" required />
      <p class="transfer-description">只创建新文件并保存草稿；目标已有同名文件时需改名。源文件和目标环境的已发布配置保持不变。</p>
      <p v-if="targets.length === 0" class="transfer-description">请先在当前项目创建另一个环境。</p>
      <p v-if="validationError || props.error" class="transfer-error" role="alert">{{ validationError || props.error }}</p>
      <footer class="transfer-actions">
        <button class="button button-secondary" type="button" :disabled="props.busy" @click="emit('update:open', false)">取消</button>
        <button class="button button-primary" type="submit" :disabled="props.busy || targets.length === 0">{{ props.busy ? '正在复制…' : '创建目标草稿' }}</button>
      </footer>
    </form>
  </ModalDialog>
</template>

<style scoped src="./configTransfer.css" />
