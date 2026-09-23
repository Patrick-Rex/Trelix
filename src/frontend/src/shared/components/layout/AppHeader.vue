<script setup lang="ts">
defineProps<{ username: string; title: string }>()
defineEmits<{ logout: [] }>()
</script>

<template>
  <header class="app-header">
    <span class="header-product">Trelix 配置中心</span>
    <span class="header-divider" aria-hidden="true">/</span>
    <h1 class="header-title">{{ title }}</h1>
    <div class="header-account">
      <span class="account-name">{{ username }}</span>
      <button class="button button-quiet" type="button" @click="$emit('logout')">退出登录</button>
    </div>
  </header>
</template>

<style scoped>
.app-header {
  display: flex;
  align-items: center;
  gap: 14px;
  min-height: 64px;
  border-bottom: 1px solid var(--color-border);
  padding: 12px 24px;
  background: var(--color-surface);
}

.header-product, .header-divider { color: var(--color-muted); font-size: 13px; }
.header-title { margin: 0; font-size: 14px; font-weight: 600; }
.header-account { display: flex; align-items: center; gap: 12px; margin-left: auto; }
.account-name { color: var(--color-muted); font-size: 12px; }

@media (max-width: 600px) {
  .app-header { gap: 8px; padding-inline: 12px; }
  .header-product, .header-divider { display: none; }
  .account-name { display: none; }
}
</style>
