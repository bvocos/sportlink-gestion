<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Pencil, Trash2 } from 'lucide-vue-next'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { confirmAction, notify } from '@/shared/uiFeedback'

const permissions = [
  ['dashboard', 'Inicio'], ['ventas', 'Ventas'], ['entregas', 'Próximas entregas'],
  ['clientes', 'Clientes'], ['cuotas', 'Cuotas'], ['caja', 'Caja'],
  ['rentabilidad', 'Rentabilidad'], ['administracion', 'Administración de productos']
]
const items = ref<any[]>([])
const show = ref(false)
const editing = ref<string | null>(null)
const error = ref('')
const blank = () => ({ nombre: '', nombreUsuario: '', password: '', repetirPassword: '', rol: 'Usuario', permisos: ['dashboard'], activo: true })
const form = ref(blank())

async function load() { items.value = (await http.get('/usuarios')).data }
function create() { editing.value = null; form.value = blank(); error.value = ''; show.value = true }
function edit(x: any) {
  editing.value = x.id
  form.value = { nombre: x.nombre, nombreUsuario: x.nombreUsuario, password: '', repetirPassword: '', rol: x.rol, permisos: [...x.permisos], activo: x.activo }
  error.value = ''
  show.value = true
}
async function save() {
  if (form.value.password !== form.value.repetirPassword) {
    error.value = 'Las contraseñas no coinciden.'
    return
  }
  try {
    const { repetirPassword: _, ...payload } = form.value
    if (editing.value) await http.put(`/usuarios/${editing.value}`, payload)
    else await http.post('/usuarios', payload)
    show.value = false
    await load()
  } catch (e: any) {
    error.value = apiErrorMessage(e, 'No se pudo guardar el usuario.')
  }
}
async function remove(x: any) {
  if (!await confirmAction({title:'Eliminar usuario',message:`¿Querés eliminar el usuario ${x.nombreUsuario}?`,confirmText:'Eliminar',danger:true})) return
  try { await http.delete(`/usuarios/${x.id}`); await load() }
  catch (e: any) { notify(apiErrorMessage(e, 'No se pudo eliminar el usuario.')) }
}
onMounted(load)
</script>

<template>
  <section class="page">
    <div class="page-title"><div><h2>Usuarios</h2><p>Personas habilitadas y módulos disponibles.</p></div><button class="btn" @click="create">+ Nuevo usuario</button></div>
    <div class="panel"><table><thead><tr><th>Nombre</th><th>Usuario</th><th>Rol</th><th>Accesos</th><th>Estado</th><th></th></tr></thead><tbody><tr v-for="x in items" :key="x.id"><td><b>{{x.nombre}}</b></td><td>{{x.nombreUsuario}}</td><td>{{x.rol}}</td><td>{{x.rol==='Administrador'?'Todos':x.permisos.length+' módulos'}}</td><td><span class="badge" :class="{warn:!x.activo}">{{x.activo?'Activo':'Inactivo'}}</span></td><td><div class="row-actions"><button class="icon-btn" @click="edit(x)"><Pencil/></button><button class="icon-btn danger" @click="remove(x)"><Trash2/></button></div></td></tr></tbody></table></div>
    <div v-if="show" class="modal-bg"><form class="modal" @submit.prevent="save"><h3>{{editing?'Editar':'Crear'}} usuario</h3><p v-if="error" class="error">{{error}}</p><div class="form-grid"><div class="field"><label>Nombre</label><input v-model="form.nombre" required></div><div class="field"><label>Usuario</label><input v-model="form.nombreUsuario" required></div><div class="field"><label>Contraseña {{editing?'(vacía para conservar)':''}}</label><input v-model="form.password" type="password" :required="!editing" minlength="8" autocomplete="new-password"></div><div class="field"><label>Repetir contraseña</label><input v-model="form.repetirPassword" type="password" :required="!editing||!!form.password" minlength="8" autocomplete="new-password"></div><div class="field"><label>Rol</label><select v-model="form.rol"><option>Usuario</option><option>Administrador</option></select></div></div><fieldset v-if="form.rol!=='Administrador'" class="permissions"><legend>Vistas disponibles</legend><label v-for="[key,label] in permissions" :key="key"><input v-model="form.permisos" type="checkbox" :value="key"> {{label}}</label></fieldset><label class="check"><input v-model="form.activo" type="checkbox"> Usuario activo</label><div class="actions"><button type="button" class="btn secondary" @click="show=false">Cancelar</button><button class="btn">Guardar usuario</button></div></form></div>
  </section>
</template>
