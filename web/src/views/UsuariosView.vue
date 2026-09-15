<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Password from 'primevue/password'
import Select from 'primevue/select'
import Checkbox from 'primevue/checkbox'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faPen, faTrash } from '@/shared/icons'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { confirmAction, notify } from '@/shared/uiFeedback'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS } from '@/shared/tablePagination'

const permissionModules = [
  { key: 'dashboard', label: 'Inicio', actions: [['ver', 'Ver']] },
  { key: 'ventas', label: 'Ventas', actions: [['ver', 'Ver'], ['crear', 'Crear'], ['editar', 'Editar'], ['eliminar', 'Eliminar']] },
  { key: 'entregas', label: 'Próximas entregas', actions: [['ver', 'Ver']] },
  { key: 'presupuestos', label: 'Presupuestos', actions: [['ver', 'Ver'], ['crear', 'Crear'], ['editar', 'Editar'], ['eliminar', 'Eliminar']] },
  { key: 'clientes', label: 'Clientes', actions: [['ver', 'Ver'], ['crear', 'Crear'], ['editar', 'Editar'], ['eliminar', 'Eliminar']] },
  { key: 'cuotas', label: 'Cuotas', actions: [['ver', 'Ver'], ['editar', 'Editar'], ['registrarPago', 'Registrar pago'], ['anularPago', 'Anular pago']] },
  { key: 'caja', label: 'Caja', actions: [['ver', 'Ver'], ['crear', 'Crear'], ['editar', 'Editar']] },
  { key: 'gastos', label: 'Gastos', actions: [['ver', 'Ver'], ['crear', 'Crear'], ['editar', 'Editar'], ['eliminar', 'Eliminar']] },
  { key: 'rentabilidad', label: 'Rentabilidad', actions: [['ver', 'Ver']] },
  { key: 'stock', label: 'Stock', actions: [['ver', 'Ver'], ['crear', 'Crear']] },
  { key: 'administracion', label: 'Administración de productos', actions: [['ver', 'Ver'], ['crear', 'Crear'], ['editar', 'Editar']] },
] as const

const permissionColumns = [
  ['ver', 'Ver'],
  ['crear', 'Crear'],
  ['editar', 'Editar'],
  ['eliminar', 'Eliminar'],
  ['registrarPago', 'Registrar pago'],
  ['anularPago', 'Anular pago'],
] as const

type PermissionForm = Record<string, Record<string, boolean> | boolean>
const roles = ['Usuario', 'Administrador']

const supports = (module: typeof permissionModules[number], action: string) =>
  module.actions.some(([key]) => key === action)

const emptyPermissions = () =>
  Object.fromEntries(
    permissionModules.map(module => [
      module.key,
      Object.fromEntries(module.actions.map(([action]) => [action, false])),
    ]),
  ) as Record<string, Record<string, boolean>>

function normalizePermissions(value: unknown): PermissionForm {
  const normalized: PermissionForm = emptyPermissions()
  normalized['ventas.verMontos'] = false
  normalized['cuotas.verMontos'] = false

  if (Array.isArray(value)) {
    for (const module of permissionModules.filter(x => value.includes(x.key))) {
      normalized[module.key] = Object.fromEntries(module.actions.map(([action]) => [action, true]))
      if (module.key === 'ventas' || module.key === 'cuotas') normalized[`${module.key}.verMontos`] = true
    }
    return normalized
  }

  if (!value || typeof value !== 'object') return normalized

  const source = value as Record<string, unknown>
  for (const module of permissionModules) {
    const actions = source[module.key]
    if (!actions || typeof actions !== 'object' || Array.isArray(actions)) continue
    for (const [action] of module.actions)
      (normalized[module.key] as Record<string, boolean>)[action] =
        (actions as Record<string, unknown>)[action] === true
  }
  normalized['ventas.verMontos'] = source['ventas.verMontos'] === true
  normalized['cuotas.verMontos'] = source['cuotas.verMontos'] === true
  return normalized
}

function modulosCount(permisos: unknown) {
  if (!permisos || typeof permisos !== 'object' || Array.isArray(permisos)) return 0
  return Object.entries(permisos as Record<string, unknown>)
    .filter(([key]) => !key.endsWith('.verMontos'))
    .filter(([, value]) =>
      typeof value === 'object' &&
      value !== null &&
      (value as Record<string, boolean>).ver === true,
    )
    .length
}

function permissionValue(moduleKey: string, action: string) {
  const actions = form.value.permisos[moduleKey]
  if (!actions || typeof actions !== 'object' || Array.isArray(actions)) return false
  return (actions as Record<string, boolean>)[action] === true
}

function setPermission(moduleKey: string, action: string, value: boolean) {
  const actions = form.value.permisos[moduleKey]
  if (!actions || typeof actions !== 'object' || Array.isArray(actions)) return
  ;(actions as Record<string, boolean>)[action] = value
}

function montosPermission(moduleKey: 'ventas' | 'cuotas') {
  return form.value.permisos[`${moduleKey}.verMontos`] === true
}

function setMontosPermission(moduleKey: 'ventas' | 'cuotas', value: boolean) {
  form.value.permisos[`${moduleKey}.verMontos`] = value
}

function toggleModuleAccess(moduleKey: string, enabled: boolean) {
  const module = permissionModules.find(x => x.key === moduleKey)
  if (!module) return
  setPermission(moduleKey, 'ver', enabled)
  if (!enabled) {
    for (const [action] of module.actions) {
      if (action !== 'ver') setPermission(moduleKey, action, false)
    }
    if (moduleKey === 'ventas' || moduleKey === 'cuotas') setMontosPermission(moduleKey, false)
  }
}

function hasModuleAccess(moduleKey: string) {
  return permissionValue(moduleKey, 'ver')
}

const items = ref<any[]>([])
const sucursales = ref<any[]>([])
const showForm = ref(false)
const editing = ref<string | null>(null)
const saving = ref(false)
const error = ref('')
const loading = ref(false)
const loadError = ref('')

const blank = () => ({
  nombre: '',
  nombreUsuario: '',
  password: '',
  repetirPassword: '',
  rol: 'Usuario',
  permisos: {
    ...emptyPermissions(),
    dashboard: { ver: true },
    'ventas.verMontos': false,
    'cuotas.verMontos': false,
  } as PermissionForm,
  activo: true,
  sucursalId: null as string | null,
})

const form = ref(blank())
const sucursalesActivas = computed(() => sucursales.value.filter(x => x.activo))

async function loadSucursales() {
  try {
    sucursales.value = (await http.get('/sucursales')).data.filter((x: any) => x.activo)
  } catch {
    sucursales.value = []
  }
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    items.value = (await http.get('/usuarios')).data
  } catch (e) {
    loadError.value = apiErrorMessage(e, 'No se pudieron cargar los usuarios.')
  } finally {
    loading.value = false
  }
}

function cancelForm() {
  showForm.value = false
  editing.value = null
  form.value = blank()
  error.value = ''
}

function create() {
  editing.value = null
  form.value = blank()
  error.value = ''
  showForm.value = true
}

function edit(x: any) {
  editing.value = x.id
  form.value = {
    nombre: x.nombre,
    nombreUsuario: x.nombreUsuario,
    password: '',
    repetirPassword: '',
    rol: x.rol,
    permisos: normalizePermissions(x.permisos),
    activo: x.activo,
    sucursalId: x.sucursalId || null,
  }
  error.value = ''
  showForm.value = true
}

async function save() {
  if (form.value.password !== form.value.repetirPassword) {
    error.value = 'Las contraseñas no coinciden.'
    return
  }
  if (form.value.rol !== 'Administrador' && !form.value.sucursalId) {
    error.value = 'Seleccioná una sucursal.'
    return
  }

  saving.value = true
  error.value = ''
  try {
    const { repetirPassword: _, ...payload } = form.value
    if (payload.rol === 'Administrador') payload.sucursalId = null
    if (editing.value) await http.put(`/usuarios/${editing.value}`, payload)
    else await http.post('/usuarios', payload)
    cancelForm()
    await load()
  } catch (e: any) {
    error.value = apiErrorMessage(e, 'No se pudo guardar el usuario.')
  } finally {
    saving.value = false
  }
}

async function remove(x: any) {
  if (!await confirmAction({ title: 'Eliminar usuario', message: `¿Querés eliminar el usuario ${x.nombreUsuario}?`, confirmText: 'Eliminar', danger: true })) return
  try { await http.delete(`/usuarios/${x.id}`); await load() }
  catch (e: any) { notify(apiErrorMessage(e, 'No se pudo eliminar el usuario.')) }
}

watch(() => form.value.rol, rol => {
  if (rol === 'Administrador') form.value.sucursalId = null
})

onMounted(async () => {
  await Promise.all([load(), loadSucursales()])
})
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-2">
      <p class="page-desc text-color-secondary m-0">
        {{ showForm
          ? (editing ? 'Editar usuario' : 'Nuevo usuario')
          : 'Personas habilitadas y módulos disponibles.' }}
      </p>
      <AppButton
        v-if="showForm"
        label="Volver al listado"
        icon="pi pi-arrow-left"
        severity="secondary"
        size="small"
        @click="cancelForm"
      />
      <AppButton
        v-else
        label="Nuevo usuario"
        icon="pi pi-plus"
        size="small"
        @click="create"
      />
    </div>

    <Message v-if="loadError && !showForm" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

    <div v-else-if="!showForm" class="table-panel">
      <DataTable
        :value="items"
        :loading="loading"
        paginator
        :rows="TABLE_ROWS"
        :rows-per-page-options="TABLE_ROWS_OPTIONS"
        striped-rows
      >
        <template #empty>
          <div class="text-center py-5 text-color-secondary">No hay usuarios registrados.</div>
        </template>
        <Column header="Nombre">
          <template #body="{ data }"><b>{{ data.nombre }}</b></template>
        </Column>
        <Column field="nombreUsuario" header="Usuario" />
        <Column field="rol" header="Rol" />
        <Column header="Sucursal">
          <template #body="{ data }">{{ data.sucursalNombre || '—' }}</template>
        </Column>
        <Column header="Accesos">
          <template #body="{ data }">
            {{ data.rol === 'Administrador' ? 'Todos' : `${modulosCount(data.permisos)} módulos` }}
          </template>
        </Column>
        <Column header="Estado">
          <template #body="{ data }">
            <Tag :value="data.activo ? 'Activo' : 'Inactivo'" :severity="data.activo ? 'success' : 'warn'" />
          </template>
        </Column>
        <Column header="" body-class="cell-actions">
          <template #body="{ data }">
            <div class="flex gap-1">
              <AppButton text rounded severity="secondary" aria-label="Editar usuario" @click="edit(data)">
                <AppIcon :icon="faPen" />
              </AppButton>
              <AppButton text rounded severity="danger" aria-label="Eliminar usuario" @click="remove(data)">
                <AppIcon :icon="faTrash" />
              </AppButton>
            </div>
          </template>
        </Column>
      </DataTable>
    </div>

    <article v-else class="card inline-form-panel inline-form-panel-wide">
      <form @submit.prevent="save">
        <Message v-if="error" severity="error" class="inline-form-error" :closable="false">{{ error }}</Message>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>1. Datos del usuario</span>
          </header>
          <div class="grid formgrid p-fluid inline-form-grid">
            <div class="field col-12 md:col-4">
              <label for="nombre">Nombre</label>
              <InputText id="nombre" v-model="form.nombre" size="small" required />
            </div>
            <div class="field col-12 md:col-4">
              <label>Rol</label>
              <Select v-model="form.rol" :options="roles" size="small" />
            </div>
            <div v-if="form.rol !== 'Administrador'" class="field col-12 md:col-4">
              <label>Sucursal</label>
              <Select
                v-model="form.sucursalId"
                :options="sucursalesActivas"
                option-label="nombre"
                option-value="id"
                placeholder="Seleccionar sucursal"
                size="small"
                required
              />
            </div>
            <div class="field col-12 md:col-4">
              <label for="nombreUsuario">Usuario</label>
              <InputText id="nombreUsuario" v-model="form.nombreUsuario" size="small" required />
            </div>
            <div class="field col-12 md:col-4">
              <label for="password">Contraseña {{ editing ? '(opcional)' : '' }}</label>
              <Password
                id="password"
                v-model="form.password"
                :feedback="false"
                toggle-mask
                autocomplete="new-password"
                :required="!editing"
                size="small"
                input-class="w-full"
              />
            </div>
            <div class="field col-12 md:col-4">
              <label for="repetirPassword">Repetir contraseña</label>
              <Password
                id="repetirPassword"
                v-model="form.repetirPassword"
                :feedback="false"
                toggle-mask
                autocomplete="new-password"
                :required="!editing || !!form.password"
                size="small"
                input-class="w-full"
              />
            </div>
          </div>
        </section>

        <section v-if="form.rol !== 'Administrador'" class="inline-form-block">
          <header class="inline-form-block-head">
            <span>2. Acceso a vistas</span>
          </header>
          <div class="usuario-perms">
            <div class="usuario-perms-grid">
              <div v-for="module in permissionModules" :key="module.key" class="usuario-perm-item">
                <Checkbox
                  :input-id="`mod-${module.key}`"
                  :model-value="hasModuleAccess(module.key)"
                  binary
                  @update:model-value="toggleModuleAccess(module.key, $event)"
                />
                <label :for="`mod-${module.key}`">{{ module.label }}</label>
              </div>
            </div>
          </div>
        </section>

        <section v-if="form.rol !== 'Administrador'" class="inline-form-block">
          <header class="inline-form-block-head">
            <span>3. Permisos detallados por acción</span>
          </header>
          <div class="usuario-perms usuario-perms-table">
            <div class="permission-grid-scroll">
              <table class="permission-grid">
                <thead class="permission-grid-head">
                  <tr>
                    <th scope="col">Módulo</th>
                    <th v-for="[, label] in permissionColumns" :key="label" scope="col">{{ label }}</th>
                    <th scope="col">Ver montos</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="module in permissionModules" :key="module.key">
                    <td><b>{{ module.label }}</b></td>
                    <td v-for="[action] in permissionColumns" :key="action">
                      <label v-if="supports(module, action)" class="permission-check" :aria-label="`${module.label}: ${action}`">
                        <Checkbox
                          :model-value="permissionValue(module.key, action)"
                          binary
                          @update:model-value="setPermission(module.key, action, $event)"
                        />
                      </label>
                      <span v-else class="permission-na">—</span>
                    </td>
                    <td>
                      <label v-if="module.key === 'ventas' || module.key === 'cuotas'" class="permission-check" :aria-label="`${module.label}: ver montos`">
                        <Checkbox
                          :model-value="montosPermission(module.key)"
                          binary
                          @update:model-value="setMontosPermission(module.key, $event)"
                        />
                      </label>
                      <span v-else class="permission-na">—</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>

        <div class="inline-form-footer">
          <div class="flex align-items-center gap-2">
            <ToggleSwitch v-model="form.activo" input-id="usuarioActivo" />
            <label for="usuarioActivo">Activo</label>
          </div>
          <div class="flex gap-2">
            <AppButton type="button" label="Cancelar" severity="secondary" size="small" @click="cancelForm" />
            <AppButton type="submit" :label="saving ? 'Guardando…' : 'Guardar'" :loading="saving" size="small" />
          </div>
        </div>
      </form>
    </article>
  </section>
</template>
