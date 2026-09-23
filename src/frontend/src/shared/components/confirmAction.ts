import { ElMessageBox } from 'element-plus'

/**
 * 使用可键盘访问的页面对话框确认有副作用的操作。
 * @param message 操作影响与确认说明，按纯文本呈现。
 * @returns 用户确认时为 true，取消或关闭时为 false。
 */
export async function confirmAction(message: string): Promise<boolean> {
  try {
    await ElMessageBox.confirm(message, '确认操作', {
      confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning',
      closeOnClickModal: false, distinguishCancelAndClose: true,
    })
    return true
  } catch {
    return false
  }
}
