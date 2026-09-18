<script setup lang="ts">
import { shallowRef } from 'vue'
import AppSidebar from './AppSidebar.vue'
import AppHeader from './AppHeader.vue'

const sidebarExpanded = shallowRef(!window.matchMedia('(max-width: 720px)').matches)
</script>

<template>
  <div
    class="app-shell"
    :class="{ 'sidebar-collapsed': !sidebarExpanded }"
    @keydown.esc="sidebarExpanded = false"
  >
    <a class="skip-link" href="#main-content">跳转到主内容</a>
    <AppSidebar :expanded="sidebarExpanded" />
    <div class="app-body">
      <AppHeader
        :sidebar-expanded="sidebarExpanded"
        @toggle-sidebar="sidebarExpanded = !sidebarExpanded"
      />
      <main id="main-content" class="main-content" tabindex="-1">
        <slot />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-shell {
  display: grid;
  grid-template-columns: 248px minmax(0, 1fr);
  min-height: 100dvh;
}

.sidebar-collapsed {
  grid-template-columns: 64px minmax(0, 1fr);
}

.app-body {
  display: flex;
  min-width: 0;
  flex-direction: column;
}

.main-content {
  display: flex;
  flex: 1;
  min-width: 0;
  padding: 24px;
}

.skip-link {
  position: fixed;
  top: -60px;
  left: 12px;
  z-index: 10;
  border-radius: 6px;
  padding: 10px 16px;
  color: white;
  background: var(--el-color-primary);
}

.skip-link:focus {
  top: 12px;
}

@media (max-width: 720px) {
  .app-shell {
    grid-template-columns: minmax(0, 1fr);
  }

  .app-shell :deep(.app-sidebar) {
    position: fixed;
    top: 64px;
    bottom: 0;
    left: 0;
    z-index: 2;
    width: min(280px, calc(100vw - 48px));
    overflow-y: auto;
    box-shadow: 8px 0 24px #26383214;
  }

  .sidebar-collapsed :deep(.app-sidebar) {
    display: none;
  }

  .main-content {
    padding: 16px;
  }
}
</style>
