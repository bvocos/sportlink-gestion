<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Pencil, Trash2 } from 'lucide-vue-next'
import { http } from '@/shared/api/httpClient'

const permissions = [
  ['dashboard', 'Resumen'], ['ventas', 'Ventas'], ['entregas', 'Próximas entregas'],
  ['clientes', 'Clientes'], ['cuotas', 'Cuotas'], ['caja', 'Caja'],
  ['rentabilidad', 'Rentabilidad'], ['administracion', 'Administración de productos'],
  ['usuarios', 'Administración de usuarios']
]
const items = ref<any[]>([])
const show = ref(false)
const editing = ref<string | null>(null)
const error = ref('')
const blank = () => ({ nombre: '', nombreUsuario: '', password: '', rol: 'Usuario', permisos: ['dashboard'], activo: true })
const form = ref(blank())

async function load() { items.value = (await http.get('/usuarios')).data }
function create() { editing.value = null; form.value = blank(); error.value = ''; show.value = true }
function edit(x: any) {
  editing.value = x.id
  form.value = { nombre: x.nombre, nombreUsuario: x.nombreUsuario, password: '', rol: x.rol, permisos: [...x.permisos], activo: x.activo }
  error.value = ''
  show.value = true
}
async function save() {
  try {
    if (editing.value) await http.put(`/usuarios/${editing.value}`, form.value)
    else await http.post('/usuarios', form.value)
    show.value = false
    await load()
  } catch (e: any) {
    error.value = e.response?.data?.message ?? e.response?.data?.detail ?? 'No se pudo guardar.'
  }
}
async function remove(x: any) {
  if (!confirm(`¿Eliminar usuario ${x.nombreUsuario}?`)) return
  try { await http.delete(`/usuarios/${x.id}`); await load() }
  catch (e: any) { alert(e.response?.data?.message ?? 'No se pudo eliminar.') }
}
onMounted(load)
</script>

<template><section class="page"><div class="page-title"><div><h2>Usuarios</h2><p>Personas habilitadas y módulos disponibles.</p></div><button class="btn" @click="create">+ Nuevo usuario</button></div><div class="panel"><table><thead><tr><th>Nombre</th><th>Usuario</th><th>Rol</th><th>Accesos</th><th>Estado</th><th></th></tr></thead><tbody><tr v-for="x in items" :key="x.id"><td><b>{{x.nombre}}</b></td><td>{{x.nombreUsuario}}</td><td>{{x.rol}}</td><td>{{x.rol==='Administrador'?'Todos':x.permisos.length+' módulos'}}</td><td><span class="badge" :class="{warn:!x.activo}">{{x.activo?'Activo':'Inactivo'}}</span></td><td><div class="row-actions"><button class="icon-btn" @click="edit(x)"><Pencil/></button><button class="icon-btn danger" @click="remove(x)"><Trash2/></button></div></td></tr></tbody></table></div><div v-if="show" class="modal-bg"><form class="modal" @submit.prevent="save"><h3>{{editing?'Editar':'Crear'}} usuario</h3><p v-if="error" class="error">{{error}}</p><div class="form-grid"><div class="field"><label>Nombre</label><input v-model="form.nombre" required></div><div class="field"><label>Usuario</label><input v-model="form.nombreUsuario" required></div><div class="field"><label>Contraseña {{editing?'(vacía para conservar)':''}}</label><input v-model="form.password" type="password" :required="!editing" minlength="8"></div><div class="field"><label>Rol</label><select v-model="form.rol"><option>Usuario</option><option>Administrador</option></select></div></div><fieldset v-if="form.rol!=='Administrador'" class="permissions"><legend>Vistas disponibles</legend><label v-for="[key,label] in permissions" :key="key"><input v-model="form.permisos" type="checkbox" :value="key"> {{label}}</label></fieldset><label class="check"><input v-model="form.activo" type="checkbox"> Usuario activo</label><div class="actions"><button type="button" class="btn secondary" @click="show=false">Cancelar</button><button class="btn">Guardar usuario</button></div></form></div></section></template>
