import { reactive } from 'vue'
import { http } from '@/shared/api/httpClient'

export interface Session {
  id: string
  nombre: string
  usuario: string
  rol: string
  permisos: string[]
  debeCambiarPassword: boolean
}

const state = reactive<{ user: Session | null; checked: boolean }>({ user: null, checked: false })

export const auth = {
  state,
  async check() {
    try { state.user = (await http.get('/auth/me')).data }
    catch { state.user = null }
    state.checked = true
    return state.user
  },
  async login(usuario: string, password: string) {
    state.user = (await http.post('/auth/login', { usuario, password })).data
    state.checked = true
  },
  async changePassword(passwordActual: string, passwordNueva: string, confirmacion: string) {
    state.user = (await http.post('/auth/cambiar-password',
      { passwordActual, passwordNueva, confirmacion })).data
  },
  async logout() {
    await http.post('/auth/logout')
    state.user = null
  },
  can(permission: string) {
    return !!state.user &&
      (state.user.rol === 'Administrador' || state.user.permisos.includes(permission))
  }
}
