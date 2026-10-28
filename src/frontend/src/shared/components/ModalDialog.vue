<script setup lang="ts">
import { ElDialog } from 'element-plus'

const props = defineProps<{ open: boolean; title: string; labelledBy: string }>()
const emit = defineEmits<{ 'update:open': [open: boolean] }>()
/**
 * 向父组件转发 Element Plus 对话框的打开状态。
 * @param open 对话框的目标打开状态。
 */
function updateOpen(open: boolean): void {
  emit('update:open', open)
}
</script>

<template>
  <ElDialog :model-value="props.open" :title="props.title" :aria-labelledby="props.labelledBy" :close-on-click-modal="false" width="min(520px, calc(100vw - 32px))" @update:model-value="updateOpen">
    <slot />
  </ElDialog>
</template>

<style scoped>
:global(.el-dialog) { max-height: 80dvh; overflow: auto; border: 1px solid var(--color-border); border-radius: var(--radius-dialog); color: var(--color-text); box-shadow: var(--shadow-dialog); }
:global(.el-dialog__header) { border-bottom: 1px solid var(--color-border); padding: 16px 20px; }
:global(.el-dialog__title) { font-size: var(--font-size-section); font-weight: 650; }
:global(.el-dialog__body) { padding: 20px; }
</style>
