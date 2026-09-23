<script setup lang="ts">
import { shallowRef, toRaw } from 'vue'
import type { JsonObject, JsonValue } from '@/shared/api/contracts'
import JsonTreeNode from './JsonTreeNode.vue'

const props = defineProps<{ value: JsonObject }>()
const emit = defineEmits<{ update: [value: JsonObject]; validationChange: [message: string | null] }>()
const errorMessage = shallowRef<string | null>(null)
const inputErrors = new Map<symbol, string>()

/**
 * 汇总各输入框的错误，阻止保存尚未生效的输入。
 * @param field 随节点身份保留的输入框标识。
 * @param message 错误说明；null 表示该输入已有效。
 */
function setInputError(field: symbol, message: string | null): void {
  if (message) inputErrors.set(field, message)
  else inputErrors.delete(field)
  errorMessage.value = inputErrors.values().next().value ?? null
  emit('validationChange', errorMessage.value)
}

/**
 * 用不可变更新替换 Tree 节点并同步到共享文档状态。
 * @param path 从根对象到目标节点的属性名与数组下标序列。
 * @param value 本次操作接收的值。
 */
function updateValue(path: Array<string | number>, value: JsonValue): void {
  const root = structuredClone(toRaw(props.value))
  const parent = locateParent(root, path)
  const last = path.at(-1)
  if (!parent || last === undefined) return
  if (Array.isArray(parent) && typeof last === 'number') parent[last] = value
  else if (!Array.isArray(parent) && typeof last === 'string') Object.defineProperty(parent, last, { value, enumerable: true, configurable: true, writable: true })
  emit('update', root)
}

/**
 * 删除指定对象属性或数组项并同步完整根对象。
 * @param path 从根对象到目标节点的属性名与数组下标序列。
 */
function removeValue(path: Array<string | number>): void {
  const root = structuredClone(toRaw(props.value))
  const parent = locateParent(root, path)
  const last = path.at(-1)
  if (!parent || last === undefined) return
  if (Array.isArray(parent) && typeof last === 'number') parent.splice(last, 1)
  else if (!Array.isArray(parent) && typeof last === 'string') delete parent[last]
  emit('update', root)
}

/**
 * 重命名對象属性并阻止服务端不接受的重复或冒号属性名。
 * @param path 从根对象到目标节点的属性名与数组下标序列。
 * @param key 用户输入的属性名。
 * @param field 属性名输入框的稳定校验标识。
 */
function renameKey(path: Array<string | number>, key: string, field: symbol): void {
  const oldKey = path.at(-1)
  const parent = locateParent(props.value, path)
  if (!parent || Array.isArray(parent) || typeof oldKey !== 'string') return
  if (key.includes(':')) { setInputError(field, '属性名不能包含冒号。'); return }
  if (Object.keys(parent).some((item) => item !== oldKey && item.toLowerCase() === key.toLowerCase())) {
    setInputError(field, '对象属性名不能重复（不区分大小写）。')
    return
  }
  setInputError(field, null)
  if (key === oldKey) return
  const root = structuredClone(toRaw(props.value))
  const clonedParent = locateParent(root, path)
  if (!clonedParent || Array.isArray(clonedParent)) return
  Object.defineProperty(clonedParent, key, { value: clonedParent[oldKey], enumerable: true, configurable: true, writable: true })
  delete clonedParent[oldKey]
  emit('update', root)
}

/**
 * 在配置根对象中定位给定子节点的父容器。
 * @param root 待遍历的 JSON 根对象。
 * @param path 从根对象到目标节点的属性名与数组下标序列。
 * @returns 目标父容器；路径无效时为 null。
 */
function locateParent(root: JsonObject, path: Array<string | number>): JsonObject | JsonValue[] | null {
  let current: JsonValue = root
  for (const key of path.slice(0, -1)) {
    if (typeof current !== 'object' || current === null) return null
    const next: JsonValue | undefined = Array.isArray(current) && typeof key === 'number' ? current[key]
      : !Array.isArray(current) && typeof key === 'string' ? current[key] : undefined
    if (next === undefined) return null
    current = next
  }
  return typeof current === 'object' && current !== null ? current : null
}

</script>

<template>
  <section class="tree-editor" aria-label="配置树编辑器">
    <p class="tree-help">修改对象、数组和值。数据类型会与 JSON、YAML 编辑视图同步。</p>
    <p v-if="errorMessage" class="tree-error" role="alert">{{ errorMessage }}</p>
    <div class="tree-scroll">
      <JsonTreeNode label="配置对象" :node="value" :path="[]" root @update-value="updateValue" @remove-value="removeValue" @rename-key="renameKey" @validation-change="setInputError" />
    </div>
  </section>
</template>

<style scoped>
.tree-editor { display: flex; min-height: 280px; flex: 1; flex-direction: column; }
.tree-help { margin: 0; border-bottom: 1px solid var(--color-border); padding: 12px 16px; color: var(--color-muted); font-size: 12px; }
.tree-error { margin: 0; padding: 10px 16px; color: var(--color-danger); font-size: 12px; }
.tree-scroll { overflow: auto; padding: 14px 16px 20px; }
</style>
