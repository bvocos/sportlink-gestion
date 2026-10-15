<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { confirmAction, notify } from '@/shared/uiFeedback'
import { branchContext } from '@/branchContext'
import { normalizeCuit, readAndValidateSucursalLogoDimensions, validateSucursalForm, validateSucursalLogo } from '@/sucursalValidation'

const items = ref<any[]>([])
const loading = ref(false)
const dialog = ref(false)
const saving = ref(false)
const editingId = ref<string | null>(null)
const error = ref('')
const errors = ref<Record<string, string>>({})
const logoFile = ref<File | null>(null)
const logoPreview = ref('')
const logoError = ref('')
const condiciones = ['Responsable Inscripto', 'Monotributo', 'Exento']
const historicalSucursalId = '7b100000-0000-0000-0000-ffffffffffff'
const isHistorical = (x: any) => String(x.id).toLowerCase() === historicalSucursalId
const blank = () => ({ nombre: '', nombreDeposito: '', depositoPropioId: null as string | null,
  razonSocial: '', cuit: '', condicionIva: null as string | null, domicilioFiscal: '',
  localidad: '', telefono: '', email: '', inicioActividades: '', ingresosBrutos: '',
  puntoVentaAfip: null as number | null, activo: true, tieneLogo: false })
const form = ref(blank())

async function load() {
  loading.value = true
  try { items.value = (await http.get('/sucursales')).data }
  catch (e) { notify(apiErrorMessage(e, 'No se pudieron cargar las sucursales.'), 'error') }
  finally { loading.value = false }
}
function releaseLogoPreview() { if (logoPreview.value) URL.revokeObjectURL(logoPreview.value); logoPreview.value = '' }
function openCreate() { releaseLogoPreview(); logoFile.value = null; logoError.value = ''; editingId.value = null; form.value = blank(); errors.value = {}; error.value = ''; dialog.value = true }
async function openEdit(x: any) {
  releaseLogoPreview(); logoFile.value = null; logoError.value = ''
  editingId.value = x.id
  form.value = { ...blank(), ...x, inicioActividades: x.inicioActividades || '', nombreDeposito: '' }
  errors.value = {}; error.value = ''; dialog.value = true
  await loadStoredLogo(x.id, x.tieneLogo)
}
async function loadStoredLogo(id: string, tieneLogo: boolean) {
  releaseLogoPreview()
  if (!tieneLogo) return
  try {
    const response = await http.get('/sucursales/' + id + '/logo', { responseType: 'blob' })
    logoPreview.value = URL.createObjectURL(response.data)
  } catch { logoError.value = 'No se pudo cargar la vista previa del logo.' }
}
async function chooseLogo(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0] || null
  if (!file) return
  logoError.value = validateSucursalLogo(file)
  if (logoError.value) return
  try { logoError.value = await readAndValidateSucursalLogoDimensions(file) }
  catch { logoError.value = 'No se pudieron leer las dimensiones del logo.' }
  if (logoError.value) return
  releaseLogoPreview(); logoFile.value = file; logoPreview.value = URL.createObjectURL(file)
}
async function removeLogo() {
  if (logoFile.value) {
    logoFile.value = null
    logoError.value = ''
    await loadStoredLogo(editingId.value || '', form.value.tieneLogo)
    return
  }
  if (!editingId.value) { releaseLogoPreview(); logoFile.value = null; return }
  try {
    await http.delete('/sucursales/' + editingId.value + '/logo')
    releaseLogoPreview(); form.value.tieneLogo = false; await load()
  }
  catch (e) { logoError.value = apiErrorMessage(e, 'No se pudo quitar el logo.') }
}
function readBackendErrors(e: any) {
  const data = e?.response?.data
  if (data?.errors) for (const [key, value] of Object.entries(data.errors))
    errors.value[key] = Array.isArray(value) ? String(value[0]) : String(value)
  else error.value = data?.message || apiErrorMessage(e, 'No se pudo guardar la sucursal.')
}
async function save() {
  errors.value = validateSucursalForm(form.value)
  error.value = ''
  if (Object.keys(errors.value).length) return
  saving.value = true
  try {
    const payload = { ...form.value, cuit: form.value.cuit ? normalizeCuit(form.value.cuit) : null,
      inicioActividades: form.value.inicioActividades || null,
      nombreDeposito: editingId.value ? null : (form.value.nombreDeposito || form.value.nombre) }
    const wasCreating = !editingId.value
    const response = editingId.value
      ? await http.put('/sucursales/' + editingId.value, payload)
      : await http.post('/sucursales', payload)
    const id = editingId.value || response.data.id
    if (wasCreating) {
      editingId.value = id
      form.value.tieneLogo = false
      await load()
      branchContext.notifyChanged()
    }
    if (logoFile.value) {
      const data = new FormData(); data.append('file', logoFile.value)
      try { await http.put('/sucursales/' + id + '/logo', data) }
      catch (e) {
        logoError.value = apiErrorMessage(e, 'No se pudo guardar el logo.')
        return
      }
      logoFile.value = null
      form.value.tieneLogo = true
    }
    dialog.value = false; await load(); branchContext.notifyChanged()
    notify('Sucursal guardada correctamente.', 'success')
  } catch (e) { readBackendErrors(e) } finally { saving.value = false }
}
async function toggle(x: any) {
  try { await http.patch('/sucursales/' + x.id + '/estado', { activo: !x.activo }); await load(); branchContext.notifyChanged() }
  catch (e) { notify(apiErrorMessage(e, 'No se pudo cambiar el estado.'), 'error') }
}
async function remove(x: any) {
  if (!await confirmAction({ title: 'Eliminar sucursal', message: '¿Querés eliminar esta sucursal?', confirmText: 'Eliminar', danger: true })) return
  try { await http.delete('/sucursales/' + x.id); await load(); branchContext.notifyChanged(); notify('Sucursal eliminada.', 'success') }
  catch (e) { notify(apiErrorMessage(e, 'No se pudo eliminar. Desactivala si tiene datos asociados.'), 'error') }
}
const incomplete = (x: any) => !x.cuit || !x.puntoVentaAfip || !x.razonSocial
onMounted(load)
onBeforeUnmount(releaseLogoPreview)
</script>

<template>
  <section class="page">
    <div class="page-header">
      <div><h1>Sucursales</h1><p>Datos operativos y fiscales de cada sede.</p></div>
      <Button label="Nueva sucursal" icon="pi pi-plus" @click="openCreate" />
    </div>
    <Message severity="info" :closable="false">El CUIT y el punto de venta identifican la numeración fiscal de cada sucursal. Los datos vacíos no aparecerán en el recibo.</Message>
    <DataTable :value="items" :loading="loading" paginator :rows="10" striped-rows>
      <Column field="nombre" header="Sucursal" />
      <Column field="depositoPropioNombre" header="Depósito propio" />
      <Column field="cuit" header="CUIT"><template #body="{ data }">{{ data.cuit || '—' }}</template></Column>
      <Column field="puntoVentaAfip" header="Punto de venta"><template #body="{ data }">{{ data.puntoVentaAfip || '—' }}</template></Column>
      <Column field="usuariosAsignados" header="Usuarios" />
      <Column header="Estado"><template #body="{ data }"><Tag v-if="isHistorical(data)" severity="info" value="Histórica" /><Tag v-else :severity="data.activo ? 'success' : 'secondary'" :value="data.activo ? 'Activa' : 'Inactiva'" /></template></Column>
      <Column header="Configuración"><template #body="{ data }"><Tag v-if="incomplete(data)" severity="warn" value="Fiscal incompleta" /><Tag v-else severity="success" value="Completa" /></template></Column>
      <Column header="Acciones"><template #body="{ data }"><div v-if="!isHistorical(data)" class="actions">
        <Button icon="pi pi-pencil" text rounded aria-label="Editar" @click="openEdit(data)" />
        <Button icon="pi pi-power-off" text rounded :severity="data.activo ? 'warn' : 'success'" aria-label="Cambiar estado" @click="toggle(data)" />
        <Button icon="pi pi-trash" text rounded severity="danger" aria-label="Eliminar" @click="remove(data)" />
      </div></template></Column>
    </DataTable>

    <Dialog v-model:visible="dialog" modal :header="editingId ? 'Editar sucursal' : 'Nueva sucursal'" :style="{ width: '760px', maxWidth: '96vw' }">
      <Message v-if="error" severity="error">{{ error }}</Message>
      <div class="form-grid">
        <label>Nombre *<InputText v-model="form.nombre" /><small v-if="errors.nombre">{{ errors.nombre }}</small></label>
        <label v-if="!editingId">Nombre del depósito inicial<InputText v-model="form.nombreDeposito" placeholder="Si queda vacío, usa el nombre de la sucursal" /></label>
        <label>Razón social<InputText v-model="form.razonSocial" /></label>
        <label>CUIT<InputText v-model="form.cuit" placeholder="20-12345678-6" /><small v-if="errors.cuit">{{ errors.cuit }}</small></label>
        <label>Condición de IVA<Select v-model="form.condicionIva" :options="condiciones" show-clear /></label>
        <label>Punto de venta ARCA<InputNumber v-model="form.puntoVentaAfip" :min="1" :max="99999" :use-grouping="false" /><small v-if="errors.puntoVentaAfip">{{ errors.puntoVentaAfip }}</small></label>
        <label>Domicilio fiscal<InputText v-model="form.domicilioFiscal" /></label>
        <label>Localidad<InputText v-model="form.localidad" /></label>
        <label>Teléfono<InputText v-model="form.telefono" /></label>
        <label>Email<InputText v-model="form.email" type="email" /></label>
        <label>Inicio de actividades<input v-model="form.inicioActividades" class="native-input" type="date"></label>
        <label>Ingresos brutos<InputText v-model="form.ingresosBrutos" /></label>
        <div class="logo-field">
          <strong>Logo de la sucursal</strong>
          <div class="logo-preview"><img v-if="logoPreview" :src="logoPreview" alt="Logo de la sucursal"><span v-else>{{ form.razonSocial || form.nombre || 'Razón social' }}</span></div>
          <input type="file" accept="image/png,image/jpeg" @change="chooseLogo">
          <small>PNG o JPG, máximo 1 MB. Vista previa del encabezado del recibo.</small>
          <small v-if="logoError" class="field-error">{{ logoError }}</small>
          <Button v-if="logoPreview" label="Quitar logo" severity="danger" text @click="removeLogo" />
        </div>
        <label class="toggle"><ToggleSwitch v-model="form.activo" /> Sucursal activa</label>
      </div>
      <template #footer><Button label="Cancelar" severity="secondary" text @click="dialog = false" /><Button label="Guardar" :loading="saving" @click="save" /></template>
    </Dialog>
  </section>
</template>

<style scoped>
.page-header{display:flex;align-items:center;justify-content:space-between;gap:1rem;margin-bottom:1rem}.page-header h1{margin:0}.page-header p{margin:.25rem 0;color:var(--text-color-secondary)}
.form-grid{display:grid;grid-template-columns:1fr 1fr;gap:1rem;margin-top:1rem}.form-grid label{display:flex;flex-direction:column;gap:.4rem;font-weight:600}.form-grid label>*{width:100%}.form-grid small{color:#c62828}.toggle{flex-direction:row!important;align-items:center;grid-column:1/-1}.toggle>*{width:auto!important}.native-input{padding:.7rem;border:1px solid var(--surface-border);border-radius:6px}.actions{display:flex;gap:.25rem}@media(max-width:650px){.form-grid{grid-template-columns:1fr}}
.logo-field{grid-column:1/-1;display:flex;flex-direction:column;gap:.5rem}.logo-preview{height:90px;border:1px solid var(--surface-border);display:flex;align-items:center;justify-content:center;padding:10px;font-size:1.25rem;font-weight:700}.logo-preview img{max-width:100%;max-height:100%;object-fit:contain}.field-error{color:#c62828!important}
</style>
