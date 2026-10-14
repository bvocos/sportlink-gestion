import { reactive } from 'vue'

const STORAGE_KEY = 'active-sucursal'
const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || 'null') as { id: string; nombre: string } | null

const state = reactive<{ active: { id: string; nombre: string } | null }>({ active: saved })

export const branchContext = {
  state,
  select(sucursal: { id: string; nombre: string }) {
    state.active = { id: sucursal.id, nombre: sucursal.nombre }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state.active))
  },
  clear() {
    state.active = null
    localStorage.removeItem(STORAGE_KEY)
  },
}

export function activeSucursalId() {
  return branchContext.state.active?.id ?? null
}
