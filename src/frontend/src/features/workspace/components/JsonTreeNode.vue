<script setup lang="ts">
import { computed, onBeforeUnmount, shallowRef, watch } from 'vue'
import type { JsonValue } from '@/shared/api/contracts'

/** Tree 容器节点中的一个直接子项。 */
interface Entry {
  /** 不随数组下标移动的组件身份。 */
  id: string | symbol
  /** 对象属性名或数组下标。 */
  key: string | number
  /** 当前子节点值。 */
  value: JsonValue
}

const props = withDefaults(defineProps<{
  label: string
  node: JsonValue
  path: Array<string | number>
  keyEditable?: boolean
  root?: boolean
}>(), { keyEditable: false, root: false })
const emit = defineEmits<{
  updateValue: [path: Array<string | number>, value: JsonValue]
  removeValue: [path: Array<string | number>]
  renameKey: [path: Array<string | number>, key: string, field: symbol]
  validationChange: [field: symbol, message: string | null]
}>()

const keyField = Symbol('key')
const valueField = Symbol('value')
const arrayIds = shallowRef<symbol[]>([])
/** 为新增数组项分配身份，已有项的身份由删除动作同步移动。 */
watch(() => Array.isArray(props.node) ? props.node.length : 0, (length) => {
  arrayIds.value = Array.from({ length }, (_, index) => arrayIds.value[index] ?? Symbol('item'))
}, { immediate: true })

const keyText = shallowRef(props.label)
const numberText = shallowRef(String(props.node))
const entries = computed<Entry[]>(() => Array.isArray(props.node)
  ? props.node.map((value, key) => ({ id: arrayIds.value[key]!, key, value }))
  : isObject(props.node) ? Object.entries(props.node).map(([key, value]) => ({ id: key, key, value })) : [])
const valueType = computed(() => Array.isArray(props.node) ? 'array'
  : props.node === null ? 'null'
    : typeof props.node === 'object' ? 'object' : typeof props.node)

/** 父级改名后同步编辑器中显示的属性名。 */
watch(() => props.label, (label) => { keyText.value = label })
/** 外部替换数字时同步输入缓冲区。 */
watch(() => props.node, (value) => { if (typeof value === 'number') numberText.value = String(value) })

/** 在节点移除或类型改变后释放其输入校验状态。 */
onBeforeUnmount(() => {
  emit('validationChange', keyField, null)
  emit('validationChange', valueField, null)
})

/** 将尚未确认的属性名编辑标记为待校验。 */
function markKeyPending(): void {
  emit('validationChange', keyField, keyText.value === props.label ? null : '请确认正在修改的属性名。')
}

/**
 * 根据类型选择将该节点替换为可编辑的 JSON 默认值。
 * @param event 触发本次编辑的 DOM 事件。
 */
function changeType(event: Event): void {
  const type = (event.target as HTMLSelectElement).value
  const next: JsonValue = type === 'object' ? {}
    : type === 'array' ? []
      : type === 'string' ? ''
        : type === 'number' ? 0
          : type === 'boolean' ? false : null
  emit('validationChange', valueField, null)
  emit('updateValue', props.path, next)
}

/**
 * 用于接收字符串、数字及布尔编辑框的数据变化。
 * @param event 触发本次编辑的 DOM 事件。
 */
function changePrimitive(event: Event): void {
  const target = event.target as HTMLInputElement
  let next: JsonValue
  if (typeof props.node === 'number') {
    numberText.value = target.value
    const valid = /^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$/.test(target.value) && Number.isFinite(Number(target.value))
    emit('validationChange', valueField, valid ? null : '请输入完整且有限的 JSON 数字。')
    if (!valid) return
    next = Number(target.value)
  }
  else if (typeof props.node === 'boolean') next = target.checked
  else next = target.value
  emit('updateValue', props.path, next)
}

/** 提交修改过的对象键名。 */
function commitKey(): void {
  emit('renameKey', props.path, keyText.value, keyField)
}

/**
 * 删除直属数组项时同步移除其身份，保留后续项的输入和校验状态。
 * @param childPath 待删除节点的完整路径；后代节点请求继续向上传递。
 */
function removeChild(childPath: Array<string | number>): void {
  const index = childPath.at(-1)
  if (Array.isArray(props.node) && childPath.length === props.path.length + 1 && typeof index === 'number') {
    arrayIds.value = arrayIds.value.filter((_, itemIndex) => itemIndex !== index)
  }
  emit('removeValue', childPath)
}

/** 请求父级在当前对象中追加一个不冲突的键。 */
function addObjectEntry(): void {
  const current = new Set(entries.value.map((entry) => String(entry.key).toLowerCase()))
  let key = 'newField'
  let index = 2
  while (current.has(key.toLowerCase())) key = `newField${index++}`
  emit('updateValue', [...props.path, key], null)
}

/** 请求父级在当前数组末尾追加空值。 */
function addArrayEntry(): void {
  emit('updateValue', [...props.path, entries.value.length], null)
}

/**
 * 判断节点值是否为普通 JSON 对象。
 * @param value 本次操作接收的值。
 * @returns 值是否是非空且非数组的对象。
 */
function isObject(value: JsonValue): value is Record<string, JsonValue> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
</script>

<template>
  <div class="tree-node" :class="{ root }">
    <div class="node-row">
      <input v-if="keyEditable" v-model="keyText" class="key-input" :aria-label="`属性名：${label}`" @input="markKeyPending" @change="commitKey">
      <span v-else class="node-label">{{ label }}</span>
      <select v-if="!root" :value="valueType" class="type-select" :aria-label="`${label} 的数据类型`" @change="changeType">
        <option value="object">对象</option><option value="array">数组</option><option value="string">字符串</option>
        <option value="number">数字</option><option value="boolean">布尔</option><option value="null">空值</option>
      </select>
      <template v-if="typeof node === 'string'">
        <input class="value-input" :value="node" :aria-label="`${label} 的值`" @input="changePrimitive">
      </template>
      <template v-else-if="typeof node === 'number'">
        <input class="value-input" inputmode="decimal" :value="numberText" :aria-label="`${label} 的值`" @input="changePrimitive">
      </template>
      <label v-else-if="typeof node === 'boolean'" class="boolean-input">
        <input type="checkbox" :checked="node" :aria-label="`${label} 的值`" @change="changePrimitive"> {{ node ? '是' : '否' }}
      </label>
      <span v-else-if="node === null" class="null-value">null</span>
      <button v-if="!root" class="button button-quiet remove-node" type="button" :aria-label="`${keyEditable ? '删除属性' : '删除数组项'} ${label}`" @click="emit('removeValue', path)">删除</button>
    </div>
    <div v-if="typeof node === 'object' && node !== null" class="children">
      <div v-if="entries.length === 0" class="empty-children">{{ Array.isArray(node) ? '空数组' : '空对象' }}</div>
      <JsonTreeNode
        v-for="entry in entries"
        :key="entry.id"
        :label="String(entry.key)"
        :node="entry.value"
        :path="[...path, entry.key]"
        :key-editable="!Array.isArray(node)"
        @update-value="(childPath, value) => emit('updateValue', childPath, value)"
        @remove-value="removeChild"
        @rename-key="(childPath, key, field) => emit('renameKey', childPath, key, field)"
        @validation-change="(field, message) => emit('validationChange', field, message)"
      />
      <div v-if="Array.isArray(node)" class="tree-actions"><button class="button button-quiet" type="button" @click="addArrayEntry">+ 添加数组项</button></div>
      <div v-else class="tree-actions"><button class="button button-quiet" type="button" @click="addObjectEntry">+ 添加属性</button></div>
    </div>
  </div>
</template>

<style scoped>
.tree-node { min-width: 0; }
.node-row { display: flex; align-items: center; gap: 8px; min-height: 38px; }
.node-label { min-width: 92px; color: var(--color-text); font-family: ui-monospace, Consolas, monospace; font-size: 12px; }
.key-input { width: min(170px, 24vw); font-family: ui-monospace, Consolas, monospace; }
.type-select { width: 92px; padding-inline: 8px; font-size: 12px; }
.value-input { flex: 1; min-width: 90px; }
.boolean-input { display: flex; align-items: center; gap: 5px; min-width: 52px; color: var(--color-muted); font-size: 12px; }
.null-value { min-width: 52px; color: var(--color-muted); font-family: ui-monospace, Consolas, monospace; }
.remove-node { margin-left: auto; font-size: 11px; }
.children { margin-left: 14px; border-left: 1px solid var(--color-border); padding-left: 14px; }
.empty-children { padding: 8px 0; color: var(--color-muted); font-size: 12px; }
.tree-actions { padding: 4px 0 8px; }
.root > .node-row { padding-bottom: 8px; }

@media (max-width: 600px) {
  .node-row { flex-wrap: wrap; padding-block: 4px; }
  .value-input { flex-basis: 100%; }
  .children { margin-left: 5px; padding-left: 8px; }
}
</style>
