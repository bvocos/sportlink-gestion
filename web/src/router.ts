import { createRouter, createWebHistory } from 'vue-router'
import { auth } from './auth'

const permissionRoutes: Record<string, string> = {
  dashboard: '/',
  ventas: '/ventas',
  presupuestos: '/presupuestos',
  entregas: '/entregas',
  clientes: '/clientes',
  cuotas: '/cuotas',
  caja: '/caja',
  gastos: '/gastos',
  rentabilidad: '/rentabilidad',
  administracion: '/admin'
}

const routes = [
  { path: '/login', name: 'login', component: () => import('./views/LoginView.vue'), meta: { public: true, title: 'Ingresar' } },
  { path: '/cambiar-password', name: 'cambiar-password', component: () => import('./views/ChangePasswordView.vue'), meta: { passwordChange: true, title: 'Cambiar contraseña' } },
  { path: '/sin-acceso', name: 'sin-acceso', component: () => import('./views/NoAccessView.vue'), meta: { title: 'Sin acceso' } },
  { path: '/', component: () => import('./views/DashboardView.vue'), meta: { permission: 'dashboard', title: 'Inicio' } },
  { path: '/ventas', component: () => import('./views/VentasView.vue'), meta: { permission: 'ventas', title: 'Ventas' } },
  { path: '/ventas/nueva', component: () => import('./views/NuevaVentaView.vue'), meta: { permission: 'ventas', title: 'Nueva venta' } },
  { path: '/ventas/:id/editar', component: () => import('./views/NuevaVentaView.vue'), meta: { permission: 'ventas', title: 'Editar venta' } },
  { path: '/presupuestos', component: () => import('./views/PresupuestosView.vue'), meta: { permission: 'presupuestos', title: 'Presupuestos' } },
  { path: '/presupuestos/nuevo', component: () => import('./views/NuevoPresupuestoView.vue'), meta: { permission: 'presupuestos', title: 'Nuevo presupuesto' } },
  { path: '/presupuestos/:id/editar', component: () => import('./views/NuevoPresupuestoView.vue'), meta: { permission: 'presupuestos', title: 'Editar presupuesto' } },
  { path: '/entregas', component: () => import('./views/EntregasView.vue'), meta: { permission: 'entregas', title: 'Entregas' } },
  { path: '/clientes', component: () => import('./views/ClientesView.vue'), meta: { permission: 'clientes', title: 'Clientes' } },
  { path: '/cuotas', component: () => import('./views/CuotasView.vue'), meta: { permission: 'cuotas', title: 'Cuotas' } },
  { path: '/caja', component: () => import('./views/CajaView.vue'), meta: { permission: 'caja', title: 'Caja' } },
  { path: '/gastos', component: () => import('./views/GastosView.vue'), meta: { permission: 'gastos', title: 'Gastos' } },
  { path: '/rentabilidad', component: () => import('./views/RentabilidadView.vue'), meta: { permission: 'rentabilidad', title: 'Rentabilidad' } },
  { path: '/admin', component: () => import('./views/AdminView.vue'), meta: { permission: 'administracion', title: 'Productos' } },
  { path: '/usuarios', component: () => import('./views/UsuariosView.vue'), meta: { admin: true, title: 'Usuarios' } },
  { path: '/auditoria', component: () => import('./views/AuditoriaView.vue'), meta: { admin: true, title: 'Auditoría' } }
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

router.afterEach(to => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · Sportlink by Empire` : 'Sportlink by Empire'
})

export default router
