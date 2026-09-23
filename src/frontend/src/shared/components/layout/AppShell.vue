<script setup lang="ts">
import { shallowRef } from 'vue'
import AppSidebar from './AppSidebar.vue'
import AppHeader from './AppHeader.vue'

type AppView = 'workspace' | 'tokens'

const props = defineProps<{ activeView: AppView; username: string }>()
const emit = defineEmits<{ navigate: [view: AppView]; logout: [] }>()
const sidebarExpanded = shallowRef(!window.matchMedia('(max-width: 720px)').matches)

/** 切换主导航展开状态，收起后保留侧栏内的展开入口。 */
function toggleSidebar(): void {
  sidebarExpanded.value = !sidebarExpanded.value
}

/**
 * 导航后在窄屏收起侧栏。
 * @param view 目标管理页面。
 */
function navigate(view: AppView): void {
  emit('navigate', view)
  if (window.matchMedia('(max-width: 720px)').matches) sidebarExpanded.value = false
}
</script>

<template>
  <div class="app-shell" :class="{ 'sidebar-collapsed': !sidebarExpanded }" @keydown.esc="sidebarExpanded = false">
    <a class="skip-link" href="#main-content">跳转到主内容</a>
    <AppSidebar :expanded="sidebarExpanded" :active-view="props.activeView" @navigate="navigate" @toggle="toggleSidebar" />
    <div class="app-body">
      <AppHeader :username="props.username" :title="props.activeView === 'workspace' ? '配置工作区' : '应用令牌'" @logout="emit('logout')" />
      <main id="main-content" class="main-content" tabindex="-1"><slot /></main>
    </div>
  </div>
</template>

<style scoped>
.app-shell { display: grid; grid-template-columns: 224px minmax(0, 1fr); min-height: 100dvh; }
.sidebar-collapsed { grid-template-columns: 64px minmax(0, 1fr); }
.app-body { display: flex; min-width: 0; min-height: 100dvh; flex-direction: column; }
.main-content { display: flex; flex: 1; min-width: 0; padding: 20px; }
.skip-link { position: fixed; top: -60px; left: 12px; z-index: 10; border-radius: 6px; padding: 10px 16px; color: white; background: var(--el-color-primary); }
.skip-link:focus { top: 12px; }

@media (max-width: 720px) {
  .app-shell, .sidebar-collapsed { grid-template-columns: 64px minmax(0, 1fr); }
  .app-body { grid-column: 2; }
  .app-shell :deep(.app-sidebar) { position: fixed; top: 0; bottom: 0; left: 0; z-index: 5; width: min(260px, calc(100vw - 48px)); box-shadow: 8px 0 24px #26383214; }
  .sidebar-collapsed :deep(.app-sidebar) { width: 64px; box-shadow: none; }
  .main-content { padding: 12px; }
}
</style>
