<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Pencil, Trash2 } from 'lucide-vue-next'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { confirmAction, notify } from '@/shared/uiFeedback'

const permissions = [
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
  { key: 'administracion', label: 'Administración de productos', actions: [['ver', 'Ver'], ['crear', 'Crear'], ['editar', 'Editar']] }
] as const
const permissionColumns = [['ver','Ver'],['crear','Crear'],['editar','Editar'],['eliminar','Eliminar'],['registrarPago','Registrar pago'],['anularPago','Anular pago']] as const
const supports = (module: typeof permissions[number], action: string) => module.actions.some(([key]) => key === action)
const emptyPermissions = () => Object.fromEntries(permissions.map(module => [module.key, Object.fromEntries(module.actions.map(([action]) => [action, false]))])) as Record<string, Record<string, boolean>>
function normalizePermissions(value: unknown) {
  const normalized: Record<string, Record<string, boolean> | boolean> = emptyPermissions()
  normalized['ventas.verMontos'] = false
  normalized['cuotas.verMontos'] = false
  if (Array.isArray(value)) {
    for (const module of permissions.filter(x => value.includes(x.key))) {
      normalized[module.key] = Object.fromEntries(module.actions.map(([action]) => [action, true]))
      if (module.key === 'ventas' || module.key === 'cuotas') normalized[`${module.key}.verMontos`] = true
    }
    return normalized
  }
  if (!value || typeof value !== 'object') return normalized
  const source = value as Record<string, unknown>
  for (const module of permissions) {
    const actions = source[module.key]
    if (!actions || typeof actions !== 'object' || Array.isArray(actions)) continue
    for (const [action] of module.actions)
      (normalized[module.key] as Record<string, boolean>)[action] = (actions as Record<string, unknown>)[action] === true
  }
  normalized['ventas.verMontos'] = source['ventas.verMontos'] === true
  normalized['cuotas.verMontos'] = source['cuotas.verMontos'] === true
  return normalized
}
const items = ref<any[]>([])
const sucursales = ref<any[]>([])
const show = ref(false)
const editing = ref<string | null>(null)
const error = ref('')
const blank = () => ({
  nombre: '', nombreUsuario: '', password: '', repetirPassword: '', rol: 'Usuario',
  permisos: { ...emptyPermissions(), dashboard: { ver: true }, 'ventas.verMontos': false, 'cuotas.verMontos': false } as Record<string, Record<string, boolean> | boolean>, activo: true, sucursalId: null as string | null
})
const form = ref(blank())
const sucursalesActivas = computed(() => sucursales.value.filter(x => x.activo))

async function load() {
  const [usersResponse, branchesResponse] = await Promise.all([
    http.get('/usuarios'),
    http.get('/sucursales')
  ])
  items.value = usersResponse.data
  sucursales.value = branchesResponse.data
}
function create() {
  editing.value = null
  form.value = blank()
  error.value = ''
  show.value = true
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
    sucursalId: x.sucursalId ?? null
  }
  error.value = ''
  show.value = true
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
  try {
    const { repetirPassword: _, ...payload } = form.value
    if (payload.rol === 'Administrador') payload.sucursalId = null
    if (editing.value) await http.put(`/usuarios/${editing.value}`, payload)
    else await http.post('/usuarios', payload)
    show.value = false
    await load()
  } catch (e: any) {
    error.value = apiErrorMessage(e, 'No se pudo guardar el usuario.')
  }
}
async function remove(x: any) {
  if (!await confirmAction({ title: 'Eliminar usuario', message: `¿Querés eliminar el usuario ${x.nombreUsuario}?`, confirmText: 'Eliminar', danger: true })) return
  try {
    await http.delete(`/usuarios/${x.id}`)
    await load()
  } catch (e: any) {
    notify(apiErrorMessage(e, 'No se pudo eliminar el usuario.'))
  }
}
watch(() => form.value.rol, rol => {
  if (rol === 'Administrador') form.value.sucursalId = null
})
onMounted(load)
</script>

<template>
  <section class="page">
    <div class="page-title">
      <div><h2>Usuarios</h2><p>Personas habilitadas y módulos disponibles.</p></div>
      <button class="btn" @click="create">+ Nuevo usuario</button>
    </div>
    <div class="panel">
      <table>
        <thead><tr><th>Nombre</th><th>Usuario</th><th>Rol</th><th>Sucursal</th><th>Accesos</th><th>Estado</th><th></th></tr></thead>
        <tbody>
          <tr v-for="x in items" :key="x.id">
            <td><b>{{ x.nombre }}</b></td><td>{{ x.nombreUsuario }}</td><td>{{ x.rol }}</td>
            <td>{{ x.sucursalNombre || '—' }}</td>
            <td>{{ x.rol === 'Administrador' ? 'Todos' : `${Object.entries(x.permisos).filter(([key, value]: any) => !key.endsWith('.verMontos') && value?.ver).length} módulos` }}</td>
            <td><span class="badge" :class="{ warn: !x.activo }">{{ x.activo ? 'Activo' : 'Inactivo' }}</span></td>
            <td><div class="row-actions"><button class="icon-btn" type="button" aria-label="Editar usuario" @click="edit(x)"><Pencil /></button><button class="icon-btn danger" type="button" aria-label="Eliminar usuario" @click="remove(x)"><Trash2 /></button></div></td>
          </tr>
        </tbody>
      </table>
    </div>
    <div v-if="show" class="modal-bg">
      <form class="modal" @submit.prevent="save">
        <h3>{{ editing ? 'Editar' : 'Crear' }} usuario</h3>
        <p v-if="error" class="error">{{ error }}</p>
        <div class="form-grid">
          <div class="field"><label>Nombre</label><input v-model="form.nombre" required></div>
          <div class="field"><label>Usuario</label><input v-model="form.nombreUsuario" required></div>
          <div class="field"><label>Contraseña {{ editing ? '(vacía para conservar)' : '' }}</label><input v-model="form.password" type="password" :required="!editing" minlength="8" autocomplete="new-password"></div>
          <div class="field"><label>Repetir contraseña</label><input v-model="form.repetirPassword" type="password" :required="!editing || !!form.password" minlength="8" autocomplete="new-password"></div>
          <div class="field"><label>Rol</label><select v-model="form.rol"><option>Usuario</option><option>Administrador</option></select></div>
          <div class="field">
            <label>Sucursal</label>
            <select v-model="form.sucursalId" :disabled="form.rol === 'Administrador'" :required="form.rol !== 'Administrador'">
              <option :value="null">{{ form.rol === 'Administrador' ? 'No corresponde' : 'Seleccionar sucursal' }}</option>
              <option v-for="sucursal in sucursalesActivas" :key="sucursal.id" :value="sucursal.id">{{ sucursal.nombre }}</option>
            </select>
            <small v-if="form.rol === 'Administrador'">Los administradores no pertenecen a una sucursal.</small>
          </div>
        </div>
        <fieldset v-if="form.rol !== 'Administrador'" class="permissions permission-matrix">
          <legend>Permisos por módulo y acción</legend>
          <div class="permission-grid-scroll"><table class="permission-grid"><thead><tr><th>Módulo</th><th v-for="[,label] in permissionColumns" :key="label">{{label}}</th><th>Ver montos</th></tr></thead><tbody><tr v-for="module in permissions" :key="module.key"><td><b>{{module.label}}</b></td><td v-for="[action] in permissionColumns" :key="action"><label v-if="supports(module,action)" :aria-label="`${module.label}: ${action}`"><input v-model="(form.permisos[module.key] as Record<string, boolean>)[action]" type="checkbox"></label><span v-else class="permission-na">—</span></td><td><label v-if="module.key === 'ventas' || module.key === 'cuotas'" :aria-label="`${module.label}: ver montos`"><input v-model="form.permisos[`${module.key}.verMontos`]" type="checkbox"></label><span v-else class="permission-na">—</span></td></tr></tbody></table></div>
        </fieldset>
        <label class="check"><input v-model="form.activo" type="checkbox"> Usuario activo</label>
        <div class="actions"><button type="button" class="btn secondary" @click="show = false">Cancelar</button><button class="btn">Guardar usuario</button></div>
      </form>
    </div>
  </section>
</template>
