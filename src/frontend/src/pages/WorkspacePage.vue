<script setup lang="ts">
import WorkspaceEmptyState from '@/features/workspace/components/WorkspaceEmptyState.vue'
</script>

<template>
  <section class="workspace" aria-labelledby="workspace-title">
    <div class="workspace-toolbar">
      <h2 id="workspace-title" class="workspace-title">配置文件</h2>
      <span class="file-state">未选择文件</span>
    </div>
    <WorkspaceEmptyState />
    <footer class="workspace-footer">
      <span>项目：未选择</span>
      <span>环境：未选择</span>
    </footer>
  </section>
</template>

<style scoped>
.workspace {
  display: flex;
  flex: 1;
  min-width: 0;
  flex-direction: column;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface);
}

.workspace-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  border-bottom: 1px solid var(--color-border);
  padding: 16px 20px;
}

.workspace-title {
  margin: 0;
  font-size: 13px;
  font-weight: 600;
}

.file-state,
.workspace-footer {
  color: var(--color-muted);
  font-size: 12px;
}

.workspace-footer {
  display: flex;
  flex-wrap: wrap;
  gap: 20px;
  border-top: 1px solid var(--color-border);
  padding: 10px 20px;
}
</style>
