<script setup lang="ts">
import { ElIcon, ElSelect } from 'element-plus'
import { Files, FolderOpened, Key } from '@element-plus/icons-vue'

defineProps<{ expanded: boolean }>()
</script>

<template>
  <aside id="app-sidebar" class="app-sidebar" :class="{ compact: !expanded }" aria-label="侧边栏">
    <a class="brand" href="#main-content" aria-label="Trelix 配置中心">
      <span class="brand-mark" aria-hidden="true">T</span>
      <span v-if="expanded" class="brand-name">Trelix<span class="brand-caption">配置中心</span></span>
    </a>

    <nav class="navigation" aria-label="主导航">
      <a class="nav-item active" href="#main-content" aria-current="page" title="配置工作区">
        <ElIcon :size="19"><Files /></ElIcon>
        <span v-if="expanded">配置工作区</span>
        <span v-else class="sr-only">配置工作区</span>
      </a>
      <button class="nav-item" type="button" disabled title="应用令牌管理尚未接入" aria-label="应用令牌管理尚未接入">
        <ElIcon :size="19"><Key /></ElIcon>
        <span v-if="expanded">应用令牌<span class="upcoming">待接入</span></span>
      </button>
    </nav>

    <section v-if="expanded" class="resources" aria-labelledby="resources-title">
      <h2 id="resources-title" class="section-title">项目与文件</h2>
      <label class="field-label" for="environment-select">当前环境</label>
      <ElSelect
        id="environment-select"
        class="environment-select"
        :model-value="undefined"
        placeholder="尚未选择环境"
        disabled
      />
      <div class="resource-empty">
        <ElIcon :size="25"><FolderOpened /></ElIcon>
        <p class="resource-title">尚未加载项目</p>
        <p class="resource-description">项目与环境接入后，<br>将在这里显示配置文件。</p>
      </div>
    </section>

    <p v-if="expanded" class="sidebar-note">轻量配置 · 有序发布</p>
  </aside>
</template>

<style scoped>
.app-sidebar {
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--color-border);
  background: var(--color-surface);
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 80px;
  padding: 20px;
  color: var(--color-text);
  text-decoration: none;
}

.brand-mark {
  display: grid;
  flex: 0 0 32px;
  height: 32px;
  place-items: center;
  border-radius: 9px;
  color: white;
  background: var(--el-color-primary);
  font-size: 23px;
  font-weight: 700;
}

.brand-name {
  font-size: 21px;
  font-weight: 650;
  line-height: 1.25;
  letter-spacing: -0.5px;
}

.brand-caption {
  display: block;
  margin-top: 4px;
  color: var(--color-muted);
  font-size: 11px;
  font-weight: 400;
  letter-spacing: 2px;
}

.navigation {
  display: grid;
  gap: 6px;
  padding: 12px;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  min-height: 42px;
  border: 0;
  border-radius: 7px;
  padding: 10px 12px;
  color: var(--color-text);
  background: transparent;
  font: inherit;
  text-align: left;
  text-decoration: none;
}

.nav-item.active {
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  font-weight: 600;
}

.nav-item:disabled {
  color: var(--color-muted);
  cursor: not-allowed;
}

.upcoming {
  margin-left: 14px;
  font-size: 11px;
}

.resources {
  margin-top: 16px;
  border-top: 1px solid var(--color-border);
  padding: 24px 20px;
}

.section-title {
  margin: 0 0 24px;
  font-size: 12px;
  font-weight: 600;
}

.field-label {
  display: block;
  margin-bottom: 8px;
  color: var(--color-muted);
  font-size: 12px;
}

.environment-select {
  width: 100%;
}

.resource-empty {
  padding: 36px 0;
  color: var(--color-muted);
  text-align: center;
}

.resource-title {
  margin: 12px 0 6px;
  font-size: 13px;
}

.resource-description {
  margin: 0;
  font-size: 12px;
  line-height: 1.9;
}

.sidebar-note {
  margin-top: auto;
  padding: 20px;
  color: var(--color-muted);
  font-size: 11px;
  text-align: center;
}

.compact .brand {
  padding: 20px 16px;
}

.compact .navigation {
  padding: 12px 8px;
}

.compact .nav-item {
  justify-content: center;
  padding: 10px;
}
</style>
