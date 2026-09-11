<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { AppIconClass } from '@/shared/icons'
import Tag from 'primevue/tag'
import ConfirmDialog from 'primevue/confirmdialog'
import Toast from 'primevue/toast'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import AppButton from '@/shared/components/AppButton.vue'
import {
  faBars,
  faCalendarDays,
  faCartShopping,
  faChartColumn,
  faChevronLeft,
  faClipboardList,
  faFileLines,
  faGear,
  faHouse,
  faReceipt,
  faRightFromBracket,
  faTruck,
  faUserGear,
  faUsers,
  faWallet,
  faXmark,
} from '@/shared/icons'
import AppIcon from '@/shared/components/AppIcon.vue'
import { auth } from './auth'
import { http } from './shared/api/httpClient'
import DolarBlueWidget from './shared/components/DolarBlueWidget.vue'
import { registerConfirmHandler, registerToastHandler } from '@/shared/feedbackBridge'

const confirm = useConfirm()
const toast = useToast()
registerConfirmHandler((options) => confirm.require(options))
registerToastHandler((options) => toast.add(options))

type NavItem = { to: string; label: string; icon: AppIconClass; permission?: string; admin?: boolean }
type NavGroup = { id: string; label: string; items: NavItem[] }

const open = ref(false)
const collapsed = ref(localStorage.getItem('sidebar-collapsed') === 'true')
const route = useRoute()
const router = useRouter()
const systemOnline = ref(navigator.onLine)

const allGroups: NavGroup[] = [
  {
    id: 'comercial',
    label: 'Comercial',
    items: [
      { to: '/', label: 'Inicio', icon: faHouse, permission: 'dashboard' },
      { to: '/ventas', label: 'Ventas', icon: faCartShopping, permission: 'ventas' },
      { to: '/presupuestos', label: 'Presupuestos', icon: faFileLines, permission: 'presupuestos' },
      { to: '/entregas', label: 'Entregas', icon: faTruck, permission: 'entregas' },
      { to: '/clientes', label: 'Clientes', icon: faUsers, permission: 'clientes' },
      { to: '/cuotas', label: 'Cuotas', icon: faCalendarDays, permission: 'cuotas' },
    ],
  },
  {
    id: 'finanzas',
    label: 'Finanzas',
    items: [
      { to: '/caja', label: 'Caja', icon: faWallet, permission: 'caja' },
      { to: '/gastos', label: 'Gastos', icon: faReceipt, permission: 'gastos' },
      { to: '/rentabilidad', label: 'Rentabilidad', icon: faChartColumn, permission: 'rentabilidad' },
    ],
  },
  {
    id: 'sistema',
    label: 'Sistema',
    items: [
      { to: '/admin', label: 'Productos', icon: faGear, permission: 'administracion' },
      { to: '/usuarios', label: 'Usuarios', icon: faUserGear, admin: true },
      { to: '/auditoria', label: 'Auditoría', icon: faClipboardList, admin: true },
    ],
  },
]

const groups = computed(() => allGroups
  .map(group => ({
    ...group,
    items: group.items.filter(item => item.admin
      ? auth.state.user?.rol === 'Administrador'
      : !item.permission || auth.can(item.permission)),
  }))
  .filter(group => group.items.length))

const pageTitle = computed(() => (route.meta.title as string | undefined) || 'Sportlink')
const initials = computed(() => {
  const parts = (auth.state.user?.nombre || '').trim().split(/\s+/).filter(Boolean)
  return parts.slice(0, 2).map(part => part[0]?.toUpperCase() ?? '').join('') || '?'
})

let connectivityTimer: number | undefined

async function checkSystem() {
  if (!navigator.onLine) { systemOnline.value = false; return }
  try {
    await http.get('/health', { timeout: 3000 })
    systemOnline.value = true
  } catch {
    systemOnline.value = false
  }
}

function setOffline() { systemOnline.value = false }

async function logout() {
  await auth.logout()
  router.push('/login')
}

function toggleSidebar() {
  collapsed.value = !collapsed.value
  localStorage.setItem('sidebar-collapsed', String(collapsed.value))
}

function closeMenu() { open.value = false }

function isActive(to: string) {
  if (to === '/') return route.path === '/'
  return route.path === to || route.path.startsWith(`${to}/`)
}

function onKey(event: KeyboardEvent) {
  if (event.key === 'Escape') closeMenu()
}

onMounted(() => {
  window.addEventListener('online', checkSystem)
  window.addEventListener('offline', setOffline)
  window.addEventListener('keydown', onKey)
  checkSystem()
  connectivityTimer = window.setInterval(checkSystem, 30000)
})

onBeforeUnmount(() => {
  window.removeEventListener('online', checkSystem)
  window.removeEventListener('offline', setOffline)
  window.removeEventListener('keydown', onKey)
  if (connectivityTimer) window.clearInterval(connectivityTimer)
})

watch(() => route.path, closeMenu)
</script>

<template>
  <ConfirmDialog />
  <Toast position="top-right" />
  <RouterView v-if="route.meta.public || route.meta.passwordChange" />
  <div v-else class="shell" :class="{ 'sidebar-collapsed': collapsed }">
    <div class="shell-overlay" :class="{ open }" @click="closeMenu" />
    <aside :class="{ open }">
      <div class="sidebar-head">
        <div class="brand">
          <img src="/brand/sportlink-logo.png" alt="Sportlink by Empire">
          <AppButton
            v-tooltip.top="'Cerrar menú'"
            class="mobile-close"
            text
            rounded
            aria-label="Cerrar menú"
            @click="closeMenu"
          >
            <AppIcon :icon="faXmark" />
          </AppButton>
        </div>
        <AppButton
          v-tooltip.right="collapsed ? 'Abrir menú' : 'Minimizar menú'"
          class="sidebar-toggle"
          text
          rounded
          :aria-label="collapsed ? 'Abrir menú' : 'Minimizar menú'"
          @click="toggleSidebar"
        >
          <AppIcon v-if="collapsed" :icon="faBars" />
          <AppIcon v-else :icon="faChevronLeft" />
        </AppButton>
      </div>
      <nav>
        <div v-for="group in groups" :key="group.id" class="nav-group">
          <span class="nav-label">{{ group.label }}</span>
          <RouterLink
            v-for="item in group.items"
            :key="item.to"
            :to="item.to"
            active-class=""
            exact-active-class=""
            :class="{ 'router-link-active': isActive(item.to) }"
            :title="collapsed ? item.label : undefined"
            @click="closeMenu"
          >
            <AppIcon :icon="item.icon" fixed-width />
            <span>{{ item.label }}</span>
          </RouterLink>
        </div>
      </nav>
      <div class="profile">
        <div class="user-chip" :title="auth.state.user?.nombre">
          <span class="user-avatar">{{ initials }}</span>
          <span class="user-meta">
            <b>{{ auth.state.user?.nombre }}</b>
            <small>{{ auth.state.user?.rol }}</small>
          </span>
        </div>
        <AppButton v-tooltip.top="'Salir'" class="logout" text aria-label="Salir" @click="logout">
          <AppIcon :icon="faRightFromBracket" />
          <span>Salir</span>
        </AppButton>
      </div>
    </aside>
    <main>
      <header>
        <AppButton v-tooltip.bottom="'Abrir menú'" class="menu" text rounded aria-label="Abrir menú" @click="open = true">
          <AppIcon :icon="faBars" />
        </AppButton>
        <div class="header-context">
          <small>Sportlink by Empire</small>
          <h1>{{ pageTitle }}</h1>
        </div>
        <div class="header-tools">
          <Tag
            :severity="systemOnline ? 'success' : 'danger'"
            :value="systemOnline ? 'Sistema disponible' : 'Sin conexión'"
            class="online-tag"
          />
        </div>
      </header>
      <RouterView />
    </main>
    <DolarBlueWidget />
  </div>
</template>

<style scoped>
.online-tag :deep(.p-tag) {
  font-weight: 700;
}
</style>
