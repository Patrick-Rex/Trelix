<script setup lang="ts">
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import type { ConfigFile, Environment, Project } from '@/shared/api/contracts'

const props = defineProps<{
  projects: readonly Project[]
  environments: readonly Environment[]
  files: readonly ConfigFile[]
  selectedProjectId: string | null
  selectedEnvironmentId: string | null
  selectedFileId: string | null
  loading: boolean
}>()
const emit = defineEmits<{
  selectProject: [id: string]
  selectEnvironment: [id: string]
  selectFile: [id: string]
  create: [kind: 'project' | 'environment' | 'file']
  rename: [kind: 'project' | 'environment']
  delete: [kind: 'project' | 'environment']
}>()
</script>

<template>
  <aside class="resource-panel" aria-label="项目资源">
    <header class="panel-heading">
      <span class="eyebrow">WORKSPACE</span>
      <h2>项目</h2>
      <div class="section-actions" role="group" aria-label="项目操作">
        <button class="button button-quiet icon-button create-button" type="button" aria-label="新建项目" title="新建项目" :disabled="props.loading" @click="emit('create', 'project')"><Plus aria-hidden="true" /></button>
        <button v-if="props.selectedProjectId" class="button button-quiet icon-button" type="button" aria-label="重命名项目" title="重命名项目" :disabled="props.loading" @click="emit('rename', 'project')"><Edit aria-hidden="true" /></button>
        <button v-if="props.selectedProjectId && props.environments.length === 0" class="button button-quiet icon-button button-danger" type="button" aria-label="删除项目" title="删除空项目" :disabled="props.loading" @click="emit('delete', 'project')"><Delete aria-hidden="true" /></button>
      </div>
    </header>

    <div class="project-list" aria-label="项目列表">
      <button
        v-for="project in props.projects" :key="project.id"
        class="resource-row project-row" :class="{ selected: project.id === props.selectedProjectId }"
        type="button" :aria-current="project.id === props.selectedProjectId ? 'true' : undefined"
        @click="emit('selectProject', project.id)"
      >
        <span class="resource-glyph" aria-hidden="true">▰</span><span class="resource-name">{{ project.displayName }}</span>
        <span class="resource-key">{{ project.key }}</span>
      </button>
    </div>

    <section v-if="props.selectedProjectId" class="tree-section">
      <header class="section-heading">
        <h3>环境</h3>
        <div class="section-actions" role="group" aria-label="环境操作">
          <button class="button button-quiet icon-button create-button" type="button" aria-label="新建环境" title="新建环境" :disabled="props.loading" @click="emit('create', 'environment')"><Plus aria-hidden="true" /></button>
          <button v-if="props.selectedEnvironmentId" class="button button-quiet icon-button" type="button" aria-label="重命名环境" title="重命名环境" :disabled="props.loading" @click="emit('rename', 'environment')"><Edit aria-hidden="true" /></button>
          <button v-if="props.selectedEnvironmentId && props.files.length === 0" class="button button-quiet icon-button button-danger" type="button" aria-label="删除环境" title="删除环境" :disabled="props.loading" @click="emit('delete', 'environment')"><Delete aria-hidden="true" /></button>
        </div>
      </header>
      <label class="sr-only" for="environment-select">当前环境</label>
      <select id="environment-select" class="environment-select" :value="props.selectedEnvironmentId ?? ''" @change="emit('selectEnvironment', ($event.target as HTMLSelectElement).value)">
        <option value="" disabled>{{ props.environments.length ? '选择环境' : '尚无环境' }}</option>
        <option v-for="environment in props.environments" :key="environment.id" :value="environment.id">{{ environment.displayName }} · {{ environment.key }}</option>
      </select>
    </section>

    <section v-if="props.selectedEnvironmentId" class="tree-section files-section">
      <header class="section-heading">
        <h3>配置文件</h3>
        <button class="button button-quiet icon-button create-button" type="button" aria-label="新建配置文件" title="新建配置文件" :disabled="props.loading" @click="emit('create', 'file')"><Plus aria-hidden="true" /></button>
      </header>
      <p v-if="props.loading" class="resource-message" role="status">正在读取资源…</p>
      <p v-else-if="props.files.length === 0" class="resource-message">此环境还没有配置文件。</p>
      <ul v-else class="file-list">
        <li v-for="file in props.files" :key="file.id">
          <button class="resource-row file-row" :class="{ selected: file.id === props.selectedFileId }" type="button" :aria-current="file.id === props.selectedFileId ? 'true' : undefined" @click="emit('selectFile', file.id)">
            <span class="resource-glyph file-glyph" aria-hidden="true">{ }</span>
            <span class="resource-name">{{ file.name }}</span>
            <span v-if="file.currentReleaseVersion" class="version-badge">v{{ file.currentReleaseVersion }}</span>
          </button>
        </li>
      </ul>
    </section>

    <p v-if="!props.selectedProjectId && !props.loading" class="resource-message resource-empty">创建项目以开始管理配置。</p>
    <p v-if="props.loading && props.projects.length === 0" class="resource-message">正在读取项目…</p>
  </aside>
</template>

<style scoped>
.resource-panel { display: flex; min-width: 0; min-height: 0; flex-direction: column; border-right: 1px solid var(--color-border); background: #fbfcfb; }
.panel-heading { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: 6px 8px; padding: 18px 12px 14px; }
.eyebrow { grid-column: 1 / -1; color: var(--color-muted); font-size: 9px; font-weight: 700; letter-spacing: 1.6px; }
.panel-heading h2 { margin: 0; font-size: 14px; }
.project-list { display: grid; gap: 3px; max-height: 32%; overflow: auto; padding: 0 8px 12px; }
.resource-row { display: flex; align-items: center; gap: 8px; width: 100%; min-width: 0; border: 0; border-radius: 6px; padding: 8px 9px; color: var(--color-text); background: transparent; font: inherit; text-align: left; cursor: pointer; }
.resource-row:hover { background: #eef2ef; }
.resource-row.selected { color: var(--el-color-primary-dark-2); background: var(--el-color-primary-light-9); font-weight: 600; }
.resource-name { overflow: hidden; flex: 1; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; }
.resource-key { overflow: hidden; max-width: 86px; color: var(--color-muted); text-overflow: ellipsis; white-space: nowrap; font-size: 10px; }
.resource-glyph { color: #759080; font-size: 13px; }
.tree-section { border-top: 1px solid var(--color-border); padding: 13px 10px 14px; }
.section-heading { display: flex; align-items: center; justify-content: space-between; gap: 4px; padding: 0 2px 8px; }
.section-heading h3 { margin: 0; color: #637169; font-size: 11px; font-weight: 650; }
.section-actions { display: flex; flex-shrink: 0; align-items: center; gap: 1px; }
.icon-button { min-width: 28px; min-height: 28px; padding: 5px; color: var(--color-muted); }
.icon-button :deep(svg) { width: 14px; height: 14px; }
.create-button { color: var(--el-color-primary); }
.icon-button.button-danger { color: var(--color-danger); }
.environment-select { width: 100%; min-height: 34px; font-size: 11px; }
.files-section { flex: 1; overflow: auto; }
.file-list { display: grid; gap: 2px; margin: 0; padding: 0; list-style: none; }
.file-row { min-height: 34px; padding: 7px 8px; }
.file-glyph { color: #a7834d; font-family: ui-monospace, monospace; font-size: 10px; }
.version-badge { border-radius: 8px; padding: 2px 5px; color: var(--color-muted); background: #eef1ef; font-size: 9px; }
.resource-message { margin: 5px 3px; color: var(--color-muted); font-size: 11px; line-height: 1.7; }
.resource-empty { margin-top: 20px; padding: 0 16px; text-align: center; }

@media (max-width: 900px) { .resource-panel { border-right: 0; border-bottom: 1px solid var(--color-border); } }
</style>
