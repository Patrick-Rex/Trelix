<script setup lang="ts">
import { ElIcon } from 'element-plus'
import { Document } from '@element-plus/icons-vue'
</script>

<template>
  <div class="empty-state">
    <span class="document-icon" aria-hidden="true"><ElIcon :size="36"><Document /></ElIcon></span>
    <h3 class="empty-title">从一份配置开始</h3>
    <p class="empty-description">在这里集中管理项目配置，让每一次变更清晰可控。</p>
    <p class="empty-hint">项目与文件管理尚未接入，当前展示工作区布局。</p>
    <div class="workflow" aria-label="配置工作流程">
      <span>选择文件</span><span aria-hidden="true">→</span>
      <span>编辑草稿</span><span aria-hidden="true">→</span>
      <span>发布配置</span>
    </div>
  </div>
</template>

<style scoped>
.empty-state {
  display: flex;
  flex: 1;
  min-height: 350px;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
  text-align: center;
}

.document-icon {
  display: grid;
  width: 76px;
  height: 76px;
  place-items: center;
  border: 1px solid var(--el-color-primary-light-8);
  border-radius: 22px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.empty-title {
  margin: 24px 0 10px;
  font-size: 21px;
  font-weight: 600;
}

.empty-description {
  margin: 0;
  color: var(--color-muted);
  line-height: 1.8;
}

.empty-hint {
  margin: 8px 0 0;
  color: var(--color-muted);
  font-size: 12px;
  line-height: 1.8;
}

.workflow {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 12px;
  margin-top: 32px;
  color: var(--color-muted);
  font-size: 12px;
}
</style>
