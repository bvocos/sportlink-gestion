import { onMounted, onUnmounted, type Ref } from 'vue'

export function useEscapeKey(active: Ref<boolean> | (() => boolean), handler: () => void) {
  function onKey(event: KeyboardEvent) {
    const isActive = typeof active === 'function' ? active() : active.value
    if (event.key === 'Escape' && isActive) handler()
  }
  onMounted(() => window.addEventListener('keydown', onKey))
  onUnmounted(() => window.removeEventListener('keydown', onKey))
}
