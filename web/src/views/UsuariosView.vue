<script setup lang="ts">
import { onMounted, ref } from 'vue'
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

const permissions = [
  ['presupuestos', 'Presupuestos'],
  ['dashboard', 'Inicio'], ['ventas', 'Ventas'], ['entregas', 'Próximas entregas'],
  ['clientes', 'Clientes'], ['cuotas', 'Cuotas'], ['caja', 'Caja'],
  ['gastos', 'Gastos'],
  ['rentabilidad', 'Rentabilidad'], ['administracion', 'Administración de productos'],
]
const roles = ['Usuario', 'Administrador']

const items = ref<any[]>([])
const showForm = ref(false)
const editing = ref<string | null>(null)
const saving = ref(false)
const error = ref('')
const blank = () => ({ nombre: '', nombreUsuario: '', password: '', repetirPassword: '', rol: 'Usuario', permisos: ['dashboard'], activo: true })
const form = ref(blank())

async function load() { items.value = (await http.get('/usuarios')).data }

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
    permisos: [...x.permisos],
    activo: x.activo,
  }
  error.value = ''
  showForm.value = true
}

async function save() {
  if (form.value.password !== form.value.repetirPassword) {
    error.value = 'Las contraseñas no coinciden.'
    return
  }

  saving.value = true
  error.value = ''
  try {
    const { repetirPassword: _, ...payload } = form.value
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

onMounted(load)
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
        v-if="!showForm"
        label="Nuevo usuario"
        icon="pi pi-plus"
        size="small"
        @click="create"
      />
    </div>

    <div v-if="!showForm" class="table-panel">
      <DataTable
        :value="items"
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
        <Column header="Accesos">
          <template #body="{ data }">{{ data.rol === 'Administrador' ? 'Todos' : `${data.permisos.length} módulos` }}</template>
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

    <article v-else class="card inline-form-panel">
      <form @submit.prevent="save">
        <Message v-if="error" severity="error" class="inline-form-error" :closable="false">{{ error }}</Message>

        <div class="grid formgrid p-fluid inline-form-grid">
          <div class="field col-12 md:col-4">
            <label for="nombre">Nombre</label>
            <InputText id="nombre" v-model="form.nombre" size="small" required />
          </div>
          <div class="field col-12 md:col-4">
            <label for="nombreUsuario">Usuario</label>
            <InputText id="nombreUsuario" v-model="form.nombreUsuario" size="small" required />
          </div>
          <div class="field col-12 md:col-4">
            <label>Rol</label>
            <Select v-model="form.rol" :options="roles" size="small" />
          </div>
          <div class="field col-12 md:col-6">
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
          <div class="field col-12 md:col-6">
            <label for="repetirPassword">Repetir</label>
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

        <section v-if="form.rol !== 'Administrador'" class="usuario-perms">
          <span class="usuario-perms-label">Vistas disponibles</span>
          <div class="usuario-perms-grid">
            <div v-for="[key, label] in permissions" :key="key" class="usuario-perm-item">
              <Checkbox v-model="form.permisos" :input-id="`perm-${key}`" :value="key" />
              <label :for="`perm-${key}`">{{ label }}</label>
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
