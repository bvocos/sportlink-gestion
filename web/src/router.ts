import { createRouter, createWebHistory } from 'vue-router'
import { auth } from './auth'

const permissionRoutes: Record<string, string> = {
  dashboard: '/',
  ventas: '/ventas',
  entregas: '/entregas',
  clientes: '/clientes',
  cuotas: '/cuotas',
  caja: '/caja',
  rentabilidad: '/rentabilidad',
  administracion: '/admin'
}

const routes = [
  { path: '/login', name: 'login', component: () => import('./views/LoginView.vue'), meta: { public: true } },
  { path: '/cambiar-password', name: 'cambiar-password', component: () => import('./views/ChangePasswordView.vue'), meta: { passwordChange: true } },
  { path: '/sin-acceso', name: 'sin-acceso', component: () => import('./views/NoAccessView.vue') },
  { path: '/', component: () => import('./views/DashboardView.vue'), meta: { permission: 'dashboard' } },
  { path: '/ventas', component: () => import('./views/VentasView.vue'), meta: { permission: 'ventas' } },
  { path: '/entregas', component: () => import('./views/EntregasView.vue'), meta: { permission: 'entregas' } },
  { path: '/clientes', component: () => import('./views/ClientesView.vue'), meta: { permission: 'clientes' } },
  { path: '/cuotas', component: () => import('./views/CuotasView.vue'), meta: { permission: 'cuotas' } },
  { path: '/caja', component: () => import('./views/CajaView.vue'), meta: { permission: 'caja' } },
  { path: '/rentabilidad', component: () => import('./views/RentabilidadView.vue'), meta: { permission: 'rentabilidad' } },
  { path: '/admin', component: () => import('./views/AdminView.vue'), meta: { permission: 'administracion' } },
  { path: '/usuarios', component: () => import('./views/UsuariosView.vue'), meta: { admin: true } },
  { path: '/auditoria', component: () => import('./views/AuditoriaView.vue'), meta: { admin: true } }
]

function landingPage() {
  const user = auth.state.user
  if (!user) return '/login'
  if (user.debeCambiarPassword) return '/cambiar-password'
  if (user.rol === 'Administrador') return '/'
  const permission = Object.keys(permissionRoutes).find(value => user.permisos.includes(value))
  return permission ? permissionRoutes[permission] : '/sin-acceso'
}

const router = createRouter({ history: createWebHistory(), routes })
router.beforeEach(async to => {
  if (!auth.state.checked) await auth.check()

  if (to.meta.public)
    return auth.state.user ? landingPage() : true
  if (!auth.state.user)
    return { name: 'login' }
  if (auth.state.user.debeCambiarPassword && !to.meta.passwordChange)
    return { name: 'cambiar-password' }
  if (to.meta.passwordChange)
    return auth.state.user.debeCambiarPassword ? true : landingPage()
  if (to.meta.admin && auth.state.user.rol !== 'Administrador')
    return landingPage()

  const permission = to.meta.permission as string | undefined
  if (permission && !auth.can(permission))
    return landingPage()
  return true
})

export default router
