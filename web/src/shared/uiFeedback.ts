import { showConfirm, showToast } from '@/shared/feedbackBridge'

type ConfirmOptions = {
  title: string
  message: string
  confirmText?: string
  danger?: boolean
}

export async function confirmAction(options: ConfirmOptions): Promise<boolean> {
  return new Promise((resolve) => {
    let settled = false
    const finish = (value: boolean) => {
      if (settled) return
      settled = true
      resolve(value)
    }

    showConfirm({
      message: options.message,
      header: options.title,
      icon: 'pi pi-exclamation-triangle',
      rejectLabel: 'Cancelar',
      acceptLabel: options.confirmText ?? 'Confirmar',
      rejectClass: 'p-button-secondary p-button-outlined',
      acceptClass: options.danger ? 'p-button-danger' : '',
      accept: () => finish(true),
      reject: () => finish(false),
      onHide: () => finish(false),
    })
  })
}

export function notify(message: string, type: 'error' | 'success' = 'error') {
  showToast({
    severity: type === 'success' ? 'success' : 'error',
    summary: type === 'success' ? 'Éxito' : 'Error',
    detail: message,
    life: 4000,
  })
}
