import { defineConfig, globalIgnores } from 'eslint/config'
import globals from 'globals'
import js from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'
import pluginOxlint from 'eslint-plugin-oxlint'
import tseslint from 'typescript-eslint'

export default defineConfig([
  {
    name: 'app/files-to-lint',
    files: ['**/*.{vue,ts,mts}'],
  },

  globalIgnores(['**/dist/**', '**/dist-ssr/**', '**/coverage/**', '**/artifacts/**']),

  {
    files: ['src/**/*.{ts,vue}'],
    languageOptions: {
      globals: {
        ...globals.browser,
      },
    },
  },

  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...pluginVue.configs['flat/essential'],
  {
    files: ['**/*.vue'],
    languageOptions: {
      parserOptions: { parser: tseslint.parser },
    },
  },
  {
    files: ['*.config.{ts,mts}', 'scripts/**/*.{ts,mts}'],
    languageOptions: { globals: globals.node },
  },

  ...pluginOxlint.buildFromOxlintConfigFile('.oxlintrc.json'),
  {
    rules: {
      '@typescript-eslint/no-require-imports': 'error',
      'no-restricted-globals': ['error', 'require', 'module', 'exports', '__dirname', '__filename'],
      'no-restricted-syntax': [
        'error',
        { selector: 'TSImportEqualsDeclaration', message: '使用 ESM import 声明。' },
        { selector: 'TSExportAssignment', message: '使用 ESM export 声明。' },
      ],
    },
  },
])
