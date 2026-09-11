import { onBeforeUnmount, type Ref, watch, type WatchSource } from 'vue'

export const SEARCH_MIN_LENGTH = 3
export const SEARCH_DEBOUNCE_MS = 350

/** Devuelve el texto de búsqueda listo para la API, o `null` si aún no alcanza el mínimo. */
export function searchQuery(value: string, minLength = SEARCH_MIN_LENGTH) {
  const trimmed = value.trim()
  if (!trimmed) return undefined
  if (trimmed.length < minLength) return null
  return trimmed
}

export function isSearchFilterActive(value: string, minLength = SEARCH_MIN_LENGTH) {
  const trimmed = value.trim()
  return trimmed.length === 0 || trimmed.length >= minLength
}

export function useDebouncedSearch(
  search: Ref<string>,
  onApply: () => void,
  minLength = SEARCH_MIN_LENGTH,
  debounceMs = SEARCH_DEBOUNCE_MS,
) {
  let timer: ReturnType<typeof setTimeout> | undefined

  watch(search, (value) => {
    if (timer) clearTimeout(timer)
    const trimmed = value.trim()
    if (trimmed.length > 0 && trimmed.length < minLength) return
    timer = setTimeout(onApply, debounceMs)
  })

  onBeforeUnmount(() => {
    if (timer) clearTimeout(timer)
  })
}

export function useImmediateFilters(sources: WatchSource | WatchSource[], onApply: () => void) {
  watch(sources, onApply, { deep: true })
}
