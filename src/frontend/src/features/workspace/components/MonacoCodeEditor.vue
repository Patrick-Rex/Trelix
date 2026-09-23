<script setup lang="ts">
import { onBeforeUnmount, onMounted, shallowRef, watch } from 'vue'
import type * as Monaco from 'monaco-editor'
import EditorWorker from 'monaco-editor/editor/editor.worker.js?worker'
import JsonWorker from 'monaco-editor/language/json/json.worker.js?worker'

const props = defineProps<{ value: string; format: 'json' | 'yaml'; readOnly?: boolean }>()
const emit = defineEmits<{ update: [text: string] }>()
const container = shallowRef<HTMLDivElement | null>(null)
const loadError = shallowRef(false)

let editor: Monaco.editor.IStandaloneCodeEditor | undefined
let model: Monaco.editor.ITextModel | undefined
let monacoApi: typeof import('monaco-editor') | undefined
let disposed = false
let applyingExternalValue = false

/** 注册 Vite ESM worker 并创建由此组件独占的 Monaco 实例和 model。 */
onMounted(async () => {
  try {
    const monaco = await import('monaco-editor')
    if (disposed || !container.value) return
    monacoApi = monaco
    ;(self as typeof self & { MonacoEnvironment: { getWorker: (_moduleId: string, label: string) => Worker } }).MonacoEnvironment = {
      /**
       * 为当前语言创建由 Monaco 管理的 worker。
       * @param _moduleId Monaco 请求的模块标识。
       * @param label 当前语言服务名称。
       * @returns 通过 Vite 打包的后台 worker。
       */
      getWorker(_moduleId: string, label: string): Worker {
        return label === 'json' ? new JsonWorker() : new EditorWorker()
      },
    }
    registerYamlLanguage(monaco)
    model = monaco.editor.createModel(props.value, props.format, monaco.Uri.parse('inmemory://trelix/config'))
    editor = monaco.editor.create(container.value, {
      model,
      automaticLayout: true,
      ariaLabel: '配置正文编辑器',
      readOnly: props.readOnly ?? false,
      minimap: { enabled: false },
      scrollBeyondLastLine: false,
      tabSize: 2,
      insertSpaces: true,
      fontSize: 13,
      lineNumbersMinChars: 3,
      renderLineHighlight: 'line',
      wordWrap: 'on',
      padding: { top: 14, bottom: 14 },
    })
    editor.onDidChangeModelContent(() => {
      if (!applyingExternalValue && model) emit('update', model.getValue())
    })
  } catch {
    if (!disposed) loadError.value = true
  }
})

/** 在外部格式转换或 Tree 编辑后增量更新 model，避免每次重新创建撤销栈。 */
watch(() => props.value, (value) => {
  if (!model || model.getValue() === value) return
  applyingExternalValue = true
  editor?.pushUndoStop()
  const current = model.getValue()
  let prefix = 0
  while (prefix < current.length && prefix < value.length && current[prefix] === value[prefix]) prefix++
  let currentEnd = current.length
  let valueEnd = value.length
  while (currentEnd > prefix && valueEnd > prefix && current[currentEnd - 1] === value[valueEnd - 1]) {
    currentEnd--
    valueEnd--
  }
  editor?.executeEdits('trelix-synchronized-view', [{
    range: { startLineNumber: model.getPositionAt(prefix).lineNumber,
      startColumn: model.getPositionAt(prefix).column,
      endLineNumber: model.getPositionAt(currentEnd).lineNumber,
      endColumn: model.getPositionAt(currentEnd).column },
    text: value.slice(prefix, valueEnd),
    forceMoveMarkers: true,
  }])
  editor?.pushUndoStop()
  applyingExternalValue = false
})

/** 将已有 model 切换为适合 JSON 或 YAML 的语法模式。 */
watch(() => props.format, (format) => {
  if (model && monacoApi) monacoApi.editor.setModelLanguage(model, format)
})

/** 释放由包装组件创建的编辑器实例和 model。 */
onBeforeUnmount(() => {
  disposed = true
  editor?.dispose()
  model?.dispose()
  editor = undefined
  model = undefined
  monacoApi = undefined
})

/**
 * 注册轻量 YAML 词法高亮；解析和数据校验仍由共享文档逻辑执行。
 * @param monaco 已经加载的 Monaco 模块实例。
 */
function registerYamlLanguage(monaco: typeof import('monaco-editor')): void {
  if (monaco.languages.getLanguages().some((language) => language.id === 'yaml')) return
  monaco.languages.register({ id: 'yaml' })
  monaco.languages.setMonarchTokensProvider('yaml', {
    tokenizer: {
      root: [
        [/^\s*#.*$/, 'comment'],
        [/^\s*[\w.-]+(?=\s*:)/, 'key'],
        [/[&*][\w.-]+/, 'variable'],
        [/[{}[\],:>|?-]/, 'delimiter'],
        [/'[^']*'/, 'string'],
        [/"(?:[^"\\]|\\.)*"/, 'string'],
        [/\b(?:true|false|null|yes|no|on|off)\b/i, 'keyword'],
        [/-?\d+(?:\.\d+)?(?:e[+-]?\d+)?/i, 'number'],
      ],
    },
  })
}
</script>

<template>
  <div v-if="!loadError" ref="container" class="monaco-container" />
  <div v-else class="monaco-error" role="alert">编辑器加载失败，请刷新页面后重试。</div>
</template>

<style scoped>
.monaco-container { width: 100%; height: 100%; min-height: 280px; overflow: hidden; background: #fff; }
.monaco-error { display: grid; width: 100%; min-height: 280px; place-items: center; color: var(--color-danger); font-size: 12px; }
</style>
