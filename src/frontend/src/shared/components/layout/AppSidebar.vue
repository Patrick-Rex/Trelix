<script setup lang="ts">
import { Expand, Fold } from '@element-plus/icons-vue'

type AppView = 'workspace' | 'tokens'

defineProps<{ expanded: boolean; activeView: AppView }>()
defineEmits<{ navigate: [view: AppView]; toggle: [] }>()
</script>

<template>
  <aside id="app-sidebar" class="app-sidebar" :class="{ compact: !expanded }" aria-label="主导航">
    <a class="brand" href="#main-content" aria-label="Trelix 配置中心">
      <span class="brand-mark" aria-hidden="true">T</span>
      <span v-if="expanded" class="brand-name">Trelix<span class="brand-caption">配置中心</span></span>
    </a>
    <nav class="navigation" aria-label="管理功能">
      <button class="nav-item" :class="{ active: activeView === 'workspace' }" type="button" :title="expanded ? undefined : '配置工作区'" :aria-current="activeView === 'workspace' ? 'page' : undefined" @click="$emit('navigate', 'workspace')">
        <span class="nav-icon" aria-hidden="true">▤</span>
        <span v-if="expanded">配置工作区</span>
        <span v-else class="sr-only">配置工作区</span>
      </button>
      <button class="nav-item" :class="{ active: activeView === 'tokens' }" type="button" :title="expanded ? undefined : '应用令牌'" :aria-current="activeView === 'tokens' ? 'page' : undefined" @click="$emit('navigate', 'tokens')">
        <span class="nav-icon" aria-hidden="true">⌑</span>
        <span v-if="expanded">应用令牌</span>
        <span v-else class="sr-only">应用令牌</span>
      </button>
    </nav>
    <footer class="sidebar-footer">
      <p v-if="expanded" class="sidebar-note">轻量配置 · 有序发布</p>
      <button class="button button-quiet sidebar-toggle" type="button" :aria-label="expanded ? '收起侧边栏' : '展开侧边栏'" :title="expanded ? '收起侧边栏' : '展开侧边栏'" :aria-expanded="expanded" aria-controls="app-sidebar" @click="$emit('toggle')">
        <Fold v-if="expanded" aria-hidden="true" />
        <Expand v-else aria-hidden="true" />
      </button>
    </footer>
  </aside>
</template>

<style scoped>
.app-sidebar { position: sticky; top: 0; display: flex; min-height: 0; height: 100dvh; align-self: start; flex-direction: column; overflow: hidden; border-right: 1px solid var(--color-border); background: var(--color-surface); }
.brand { display: flex; flex-shrink: 0; align-items: center; gap: 12px; min-height: 80px; padding: 20px; color: var(--color-text); text-decoration: none; }
.brand-mark { display: grid; flex: 0 0 32px; height: 32px; place-items: center; border-radius: 9px; color: white; background: var(--el-color-primary); font-size: 23px; font-weight: 700; }
.brand-name { font-size: 20px; font-weight: 650; line-height: 1.25; letter-spacing: -0.5px; }
.brand-caption { display: block; margin-top: 4px; color: var(--color-muted); font-size: 11px; font-weight: 400; letter-spacing: 2px; }
.sidebar-footer { display: flex; flex-shrink: 0; align-items: center; justify-content: space-between; gap: 8px; margin-top: auto; border-top: 1px solid var(--color-border); padding: 10px 12px; }
.sidebar-toggle { width: 36px; min-height: 36px; flex: 0 0 36px; padding: 8px; }
.sidebar-toggle :deep(svg) { width: 18px; height: 18px; }
.navigation { display: grid; min-height: 0; align-content: start; gap: 6px; overflow-y: auto; padding: 12px; }
.nav-item { display: flex; align-items: center; gap: 12px; width: 100%; min-height: 42px; border: 0; border-radius: 7px; padding: 10px 12px; color: var(--color-text); background: transparent; font: inherit; text-align: left; cursor: pointer; }
.nav-item.active { color: var(--el-color-primary); background: var(--el-color-primary-light-9); font-weight: 600; }
.nav-icon { display: inline-grid; width: 20px; place-items: center; font-size: 18px; }
.sidebar-note { margin: 0; color: var(--color-muted); font-size: 10px; }
.compact .brand { justify-content: center; padding-inline: 12px; }
.compact .navigation { padding-inline: 8px; }
.compact .sidebar-footer { justify-content: center; }
.compact .nav-item { justify-content: center; padding-inline: 8px; }
</style>
