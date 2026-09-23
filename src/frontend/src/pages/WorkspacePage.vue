<script setup lang="ts">
import { computed, onBeforeUnmount, shallowRef, watch } from 'vue'
import EditorPanel from '@/features/workspace/components/EditorPanel.vue'
import ConnectionConfigButton from '@/features/workspace/components/ConnectionConfigButton.vue'
import HistoryDialog from '@/features/workspace/components/HistoryDialog.vue'
import ResourceDialog from '@/features/workspace/components/ResourceDialog.vue'
import WorkspaceResourceTree from '@/features/workspace/components/WorkspaceResourceTree.vue'
import { useWorkspace } from '@/features/workspace/composables/useWorkspace'
import type { ReleaseDetail } from '@/shared/api/contracts'
import type { DocumentFormat } from '@/features/workspace/utils/configDocument'

/** 资源编辑对话框支持的资源层级。 */
type ResourceKind = 'project' | 'environment' | 'file'
/** 资源编辑对话框的操作模式。 */
type ResourceMode = 'create' | 'rename'

const workspace = useWorkspace()
const emit = defineEmits<{ dirtyChange: [hasChanges: boolean] }>()
const activeResourceDialog = shallowRef<{ kind: ResourceKind; mode: ResourceMode } | null>(null)
const resourceSaving = shallowRef(false)
const resourceError = shallowRef<string | null>(null)
const historyOpen = shallowRef(false)
const historyPreview = shallowRef<ReleaseDetail | null>(null)
const historyPreviewLoading = shallowRef(false)
const historyRequestId = shallowRef(0)
let historyPreviewController: AbortController | undefined

/** 页面卸载时取消历史正文读取并清理上级未保存提示。 */
onBeforeUnmount(() => { historyPreviewController?.abort(); emit('dirtyChange', false) })

watch(workspace.isDirty, (dirty) => emit('dirtyChange', dirty), { immediate: true })
watch(workspace.selectedFileId, () => {
  historyPreviewController?.abort()
  historyRequestId.value++
  historyPreview.value = null
})
watch(historyOpen, (open) => {
  if (!open) {
    historyPreviewController?.abort()
    historyRequestId.value++
    historyPreviewLoading.value = false
  }
})

const resourceDialogOpen = computed(() => activeResourceDialog.value !== null)
const resourceInitial = computed(() => {
  if (activeResourceDialog.value?.mode !== 'rename') return {}
  const kind = activeResourceDialog.value?.kind
  if (kind === 'project') return { key: workspace.currentProject.value?.key, displayName: workspace.currentProject.value?.displayName }
  if (kind === 'environment') return { key: workspace.currentEnvironment.value?.key, displayName: workspace.currentEnvironment.value?.displayName }
  return { name: workspace.currentFile.value?.name }
})

/**
 * 打开对应类型的新建资源对话框。
 * @param kind 资源类型。
 */
function openCreate(kind: ResourceKind): void {
  resourceError.value = null
  activeResourceDialog.value = { kind, mode: 'create' }
}

/**
 * 打开当前项目或环境的重命名对话框。
 * @param kind 资源类型。
 */
function openRename(kind: 'project' | 'environment'): void {
  resourceError.value = null
  activeResourceDialog.value = { kind, mode: 'rename' }
}

/** 打开当前文件的重命名表单。 */
function openFileRename(): void {
  resourceError.value = null
  activeResourceDialog.value = { kind: 'file', mode: 'rename' }
}

/**
 * 根据当前对话框类型创建、重命名项目环境或文件。
 * @param value 本次操作接收的值。
 * @returns 异步操作完成时兑现。
 */
async function saveResource(value: { key?: string; displayName?: string; name?: string }): Promise<void> {
  const dialog = activeResourceDialog.value
  if (!dialog || resourceSaving.value) return
  resourceSaving.value = true
  resourceError.value = null
  try {
    let saved = false
    if (dialog.kind === 'file' && value.name !== undefined) {
      saved = dialog.mode === 'create'
        ? await workspace.addFile(value.name)
        : await workspace.updateFileName(value.name)
    } else if (dialog.kind !== 'file') {
      const resource = { key: value.key ?? '', displayName: value.displayName ?? '' }
      if (dialog.kind === 'project') {
        saved = dialog.mode === 'create'
          ? await workspace.addProject(resource)
          : await workspace.updateProject(resource)
      } else {
        saved = dialog.mode === 'create'
          ? await workspace.addEnvironment(resource)
          : await workspace.updateEnvironment(resource)
      }
    }
    if (activeResourceDialog.value !== dialog) return
    if (saved) activeResourceDialog.value = null
    else resourceError.value = workspace.errorMessage.value
  } finally {
    resourceSaving.value = false
  }
}

/**
 * 根据当前选择删除空项目或环境。
 * @param kind 资源类型。
 * @returns 异步操作完成时兑现。
 */
async function deleteResource(kind: 'project' | 'environment'): Promise<void> {
  if (kind === 'project') await workspace.removeProject()
  else await workspace.removeEnvironment()
}

/**
 * 预览所选版本正文，并忽略切换前发出的较慢响应。
 * @param version 所选发布版本号。
 * @returns 异步操作完成时兑现。
 */
async function previewRelease(version: number): Promise<void> {
  historyPreviewController?.abort()
  historyPreviewController = new AbortController()
  const requestId = ++historyRequestId.value
  historyPreviewLoading.value = true
  historyPreview.value = null
  const result = await workspace.readRelease(version, historyPreviewController.signal)
  if (requestId === historyRequestId.value) {
    historyPreview.value = result
    historyPreviewLoading.value = false
  }
}

/** 按相同的编辑内容格式化状态进入历史面板。 */
function openHistory(): void {
  historyRequestId.value++
  historyPreview.value = null
  historyOpen.value = true
}

/**
 * 在保持当前编辑缓冲区的情况下回滚所选发布版本。
 * @param version 所选发布版本号。
 * @returns 异步操作完成时兑现。
 */
async function rollbackRelease(version: number): Promise<void> {
  await workspace.rollbackTo(version)
}

/**
 * 将代码、Tree 和历史交互转发给共享文档状态。
 * @param format 目标文档表示。
 */
function changeFormat(format: DocumentFormat): void {
  workspace.changeFormat(format)
}
</script>

<template>
  <section class="workspace-page" aria-label="配置工作区">
    <WorkspaceResourceTree
      :projects="workspace.projects.value"
      :environments="workspace.environments.value"
      :files="workspace.files.value"
      :selected-project-id="workspace.selectedProjectId.value"
      :selected-environment-id="workspace.selectedEnvironmentId.value"
      :selected-file-id="workspace.selectedFileId.value"
      :loading="workspace.loading.value"
      @select-project="workspace.selectProject"
      @select-environment="workspace.selectEnvironment"
      @select-file="workspace.selectFile"
      @create="openCreate"
      @rename="openRename"
      @delete="deleteResource"
    />
    <div class="workspace-main">
      <div class="workspace-context" aria-label="当前资源位置">
        <span>{{ workspace.currentProject.value?.displayName ?? '未选择项目' }}</span><span aria-hidden="true">/</span>
        <span>{{ workspace.currentEnvironment.value?.displayName ?? '未选择环境' }}</span><span aria-hidden="true">/</span>
        <strong>{{ workspace.currentFile.value?.name ?? '未选择配置文件' }}</strong>
      </div>
      <EditorPanel
        :file="workspace.currentFile.value"
        :text="workspace.documentText.value"
        :format="workspace.documentFormat.value"
        :value="workspace.documentValue.value"
        :dirty="workspace.isDirty.value"
        :valid="workspace.isValid.value"
        :validation-pending="workspace.validationPending.value"
        :validation-error="workspace.validationError.value"
        :loading-file="workspace.loadingFile.value"
        :draft-revision="workspace.draftRevision.value"
        :saving="workspace.saving.value"
        :publishing="workspace.publishing.value"
        :can-publish="workspace.canPublish.value"
        :conflict-detected="workspace.conflictDetected.value"
        :error-message="workspace.errorMessage.value"
        @update-text="workspace.updateDocumentText"
        @update-tree="workspace.updateTreeValue"
        @tree-validation-change="workspace.updateTreeValidation"
        @change-format="changeFormat"
        @save="workspace.save"
        @publish="workspace.publish"
        @show-history="openHistory"
        @rename-file="openFileRename"
        @delete-file="workspace.removeFile"
        @reload="workspace.reloadCurrentDraft"
      >
        <template #connection-action>
          <ConnectionConfigButton
            :project-key="workspace.currentProject.value?.key"
            :environment-key="workspace.currentEnvironment.value?.key"
            :file-name="workspace.currentFile.value?.name"
            :published="workspace.currentFile.value?.currentReleaseVersion != null"
            :disabled="workspace.loading.value || workspace.loadingFile.value"
          />
        </template>
      </EditorPanel>
    </div>

    <ResourceDialog
      :open="resourceDialogOpen"
      :kind="activeResourceDialog?.kind ?? 'project'"
      :mode="activeResourceDialog?.mode ?? 'create'"
      :initial-key="resourceInitial.key"
      :initial-display-name="resourceInitial.displayName"
      :initial-name="resourceInitial.name"
      :saving="resourceSaving"
      :error="resourceError"
      @update:open="!$event && (activeResourceDialog = null)"
      @save="saveResource"
    />
    <HistoryDialog
      v-model:open="historyOpen"
      :releases="workspace.releases.value"
      :current-version="workspace.currentFile.value?.currentReleaseVersion ?? null"
      :preview="historyPreview"
      :loading-preview="historyPreviewLoading"
      @select-version="previewRelease"
      @rollback="rollbackRelease"
    />
  </section>
</template>

<style scoped>
.workspace-page { display: grid; min-width: 0; min-height: calc(100dvh - 104px); flex: 1; grid-template-columns: 224px minmax(0, 1fr); border: 1px solid var(--color-border); border-radius: 10px; overflow: hidden; background: var(--color-surface); }
.workspace-main { display: flex; min-width: 0; min-height: 0; flex-direction: column; padding: 0 16px 16px; }
.workspace-context { display: flex; min-height: 42px; align-items: center; gap: 9px; overflow: hidden; color: var(--color-muted); white-space: nowrap; font-size: 10px; }
.workspace-context strong { overflow: hidden; color: var(--color-text); text-overflow: ellipsis; font-weight: 600; }

@media (max-width: 900px) {
  .workspace-page { grid-template-columns: minmax(0, 1fr); }
  .workspace-main { padding: 0 10px 10px; }
}
@media (max-width: 600px) { .workspace-context { gap: 5px; font-size: 9px; } }
</style>
