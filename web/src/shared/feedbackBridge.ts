import type { ConfirmationOptions } from 'primevue/confirmationoptions'
import type { ToastMessageOptions } from 'primevue/toast'

type ConfirmHandler = (options: ConfirmationOptions) => void
type ToastHandler = (options: ToastMessageOptions) => void

let confirmHandler: ConfirmHandler | null = null
let toastHandler: ToastHandler | null = null

export function registerConfirmHandler(handler: ConfirmHandler) {
  confirmHandler = handler
}

export function registerToastHandler(handler: ToastHandler) {
  toastHandler = handler
}

export function showConfirm(options: ConfirmationOptions) {
  confirmHandler?.(options)
}

export function showToast(options: ToastMessageOptions) {
  toastHandler?.(options)
}
