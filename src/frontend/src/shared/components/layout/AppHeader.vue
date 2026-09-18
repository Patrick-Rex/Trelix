<script setup lang="ts">
import { ElButton, ElIcon } from 'element-plus'
import { Expand, Fold } from '@element-plus/icons-vue'

defineProps<{ sidebarExpanded: boolean }>()
defineEmits<{ toggleSidebar: [] }>()
</script>

<template>
  <header class="app-header">
    <ElButton
      class="sidebar-toggle"
      text
      :aria-label="sidebarExpanded ? '收起侧边栏' : '展开侧边栏'"
      :title="sidebarExpanded ? '收起侧边栏' : '展开侧边栏'"
      :aria-expanded="sidebarExpanded"
      aria-controls="app-sidebar"
      @click="$emit('toggleSidebar')"
    >
      <ElIcon :size="19"><Fold v-if="sidebarExpanded" /><Expand v-else /></ElIcon>
    </ElButton>
    <span class="header-product">配置中心</span>
    <span class="header-divider" aria-hidden="true">/</span>
    <h1 class="header-title">配置工作区</h1>
  </header>
</template>

<style scoped>
.app-header {
  display: flex;
  align-items: center;
  gap: 16px;
  min-height: 64px;
  border-bottom: 1px solid var(--color-border);
  padding: 12px 24px;
  background: var(--color-surface);
}

.sidebar-toggle {
  padding: 8px;
}

.header-product,
.header-divider {
  color: var(--color-muted);
  font-size: 13px;
}

.header-title {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
}

@media (max-width: 720px) {
  .app-header {
    gap: 12px;
    padding-inline: 16px;
  }
}
</style>
