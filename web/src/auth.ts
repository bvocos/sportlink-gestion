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
let checkPromise: Promise<Session | null> | null = null

const delay = (milliseconds: number) => new Promise(resolve => setTimeout(resolve, milliseconds))

async function waitForBackend() {
  let lastError: unknown
  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      await http.get('/health', { timeout: 30000 })
      return
    } catch (error: any) {
      // Una respuesta HTTP confirma que la API ya está disponible.
      if (error?.response) return
      lastError = error
      if (attempt < 2) await delay(1500)
    }
  }
  throw lastError
}

export const auth = {
  state,
  async check() {
    if (checkPromise) return checkPromise
    checkPromise = (async () => {
      try { state.user = (await http.get('/auth/me', { headers: { 'Cache-Control': 'no-cache' } })).data }
      catch { state.user = null }
      state.checked = true
      return state.user
    })()
    try { return await checkPromise }
    finally { checkPromise = null }
  },
  async login(usuario: string, password: string) {
    if (checkPromise) await checkPromise
    await waitForBackend()
    // Azure puede iniciar desde cero luego de un período sin uso. El login tiene
    // un margen mayor que las operaciones normales para tolerar ese arranque.
    await http.post('/auth/login', { usuario, password }, { timeout: 60000 })
    state.user = (await http.get('/auth/me', { headers: { 'Cache-Control': 'no-cache' }, timeout: 30000 })).data
    state.checked = true
    return state.user
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
