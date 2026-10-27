<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
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
const showForm = ref(false)
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
function cancelForm() { showForm.value = false; error.value = ''; errors.value = {} }
function openCreate() { releaseLogoPreview(); logoFile.value = null; logoError.value = ''; editingId.value = null; form.value = blank(); errors.value = {}; error.value = ''; showForm.value = true }
async function openEdit(x: any) {
  releaseLogoPreview(); logoFile.value = null; logoError.value = ''
  editingId.value = x.id
  form.value = { ...blank(), ...x, inicioActividades: x.inicioActividades || '', nombreDeposito: '' }
  errors.value = {}; error.value = ''; showForm.value = true
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
    showForm.value = false; await load(); branchContext.notifyChanged()
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
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-2">
      <p class="page-desc text-color-secondary m-0">
        {{ showForm
          ? (editingId ? 'Editando sucursal' : 'Nueva sucursal')
          : 'Datos operativos y fiscales de cada sede.' }}
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
        label="Nueva sucursal"
        icon="pi pi-plus"
        size="small"
        @click="openCreate"
      />
    </div>

    <!-- Advertencia fiscal — solo visible en el listado -->
    <Message v-if="!showForm" severity="info" :closable="false" class="mb-3">
      El CUIT y el punto de venta identifican la numeración fiscal de cada sucursal. Los datos vacíos no aparecerán en el recibo.
    </Message>

    <!-- ── Formulario inline ────────────────────────────────────────── -->
    <article v-if="showForm" class="card inline-form-panel inline-form-panel-wide">
      <form @submit.prevent="save">
        <Message v-if="error" severity="error" class="inline-form-error" :closable="false">{{ error }}</Message>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>1. Identificación</span>
          </header>
          <div class="grid formgrid p-fluid inline-form-grid">
            <div class="field col-12 md:col-6">
              <label>Nombre *</label>
              <InputText v-model="form.nombre" />
              <small v-if="errors.nombre" class="field-error">{{ errors.nombre }}</small>
            </div>
            <div v-if="!editingId" class="field col-12 md:col-6">
              <label>Nombre del depósito inicial</label>
              <InputText v-model="form.nombreDeposito" placeholder="Si queda vacío, usa el nombre de la sucursal" />
            </div>
          </div>
        </section>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>2. Datos fiscales</span>
          </header>
          <div class="grid formgrid p-fluid inline-form-grid">
            <div class="field col-12 md:col-6">
              <label>Razón social</label>
              <InputText v-model="form.razonSocial" />
            </div>
            <div class="field col-12 md:col-6">
              <label>CUIT</label>
              <InputText v-model="form.cuit" placeholder="20-12345678-6" />
              <small v-if="errors.cuit" class="field-error">{{ errors.cuit }}</small>
            </div>
            <div class="field col-12 md:col-6">
              <label>Condición de IVA</label>
              <Select v-model="form.condicionIva" :options="condiciones" show-clear />
            </div>
            <div class="field col-12 md:col-6">
              <label>Punto de venta ARCA</label>
              <InputNumber v-model="form.puntoVentaAfip" :min="1" :max="99999" :use-grouping="false" />
              <small v-if="errors.puntoVentaAfip" class="field-error">{{ errors.puntoVentaAfip }}</small>
            </div>
            <div class="field col-12 md:col-6">
              <label>Inicio de actividades</label>
              <input v-model="form.inicioActividades" class="native-input" type="date">
            </div>
            <div class="field col-12 md:col-6">
              <label>Ingresos brutos</label>
              <InputText v-model="form.ingresosBrutos" />
            </div>
          </div>
        </section>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>3. Contacto y ubicación</span>
          </header>
          <div class="grid formgrid p-fluid inline-form-grid">
            <div class="field col-12 md:col-6">
              <label>Domicilio fiscal</label>
              <InputText v-model="form.domicilioFiscal" />
            </div>
            <div class="field col-12 md:col-6">
              <label>Localidad</label>
              <InputText v-model="form.localidad" />
            </div>
            <div class="field col-12 md:col-6">
              <label>Teléfono</label>
              <InputText v-model="form.telefono" />
            </div>
            <div class="field col-12 md:col-6">
              <label>Email</label>
              <InputText v-model="form.email" type="email" />
            </div>
          </div>
        </section>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>4. Logo y estado</span>
          </header>
          <div class="logo-section">
            <div class="logo-preview-box">
              <img v-if="logoPreview" :src="logoPreview" alt="Logo de la sucursal">
              <span v-else>{{ form.razonSocial || form.nombre || 'Razón social' }}</span>
            </div>
            <div class="logo-controls">
              <small>PNG o JPG, máximo 1 MB. Vista previa del encabezado del recibo.</small>
              <input type="file" accept="image/png,image/jpeg" @change="chooseLogo">
              <small v-if="logoError" class="field-error">{{ logoError }}</small>
              <AppButton v-if="logoPreview" label="Quitar logo" severity="danger" text size="small" @click="removeLogo" />
            </div>
          </div>
          <div class="toggle-activo mt-3">
            <ToggleSwitch v-model="form.activo" input-id="sucursal-activo" />
            <label for="sucursal-activo">Sucursal activa</label>
          </div>
        </section>

        <div class="inline-form-footer">
          <small class="inline-form-note text-color-secondary">Los campos marcados con * son obligatorios.</small>
          <div class="flex gap-2">
            <AppButton type="button" label="Cancelar" severity="secondary" size="small" @click="cancelForm" />
            <AppButton type="submit" label="Guardar sucursal" :loading="saving" size="small" />
          </div>
        </div>
      </form>
    </article>

    <!-- ── Tabla ────────────────────────────────────────────────────── -->
    <DataTable v-else :value="items" :loading="loading" paginator :rows="10" striped-rows>
      <Column field="nombre" header="Sucursal" />
      <Column field="depositoPropioNombre" header="Depósito propio" />
      <Column field="cuit" header="CUIT">
        <template #body="{ data }">{{ data.cuit || '—' }}</template>
      </Column>
      <Column field="puntoVentaAfip" header="Punto de venta">
        <template #body="{ data }">{{ data.puntoVentaAfip || '—' }}</template>
      </Column>
      <Column field="usuariosAsignados" header="Usuarios" />
      <Column header="Estado">
        <template #body="{ data }">
          <Tag v-if="isHistorical(data)" severity="info" value="Histórica" />
          <Tag v-else :severity="data.activo ? 'success' : 'secondary'" :value="data.activo ? 'Activa' : 'Inactiva'" />
        </template>
      </Column>
      <Column header="Configuración">
        <template #body="{ data }">
          <Tag v-if="incomplete(data)" severity="warn" value="Fiscal incompleta" />
          <Tag v-else severity="success" value="Completa" />
        </template>
      </Column>
      <Column header="Acciones">
        <template #body="{ data }">
          <div v-if="!isHistorical(data)" class="actions">
            <AppButton
              icon="pi pi-pencil"
              text
              rounded
              size="small"
              tooltip="Editar sucursal"
              aria-label="Editar sucursal"
              @click="openEdit(data)"
            />
            <ToggleSwitch
              :model-value="data.activo"
              v-tooltip.top="data.activo ? 'Desactivar sucursal' : 'Activar sucursal'"
              class="sucursal-toggle"
              @change="toggle(data)"
            />
            <AppButton
              icon="pi pi-trash"
              text
              rounded
              size="small"
              severity="danger"
              tooltip="Eliminar sucursal"
              aria-label="Eliminar sucursal"
              @click="remove(data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>
  </section>
</template>

<style scoped>
.inline-form-grid {
  gap: 0;
}

.native-input {
  padding: .7rem;
  border: 1px solid var(--surface-border);
  border-radius: 6px;
  width: 100%;
}

.field-error {
  color: #c62828;
}

.logo-section {
  display: flex;
  gap: 1rem;
  align-items: flex-start;
  flex-wrap: wrap;
}

.logo-preview-box {
  width: 220px;
  height: 90px;
  border: 1px solid var(--surface-border);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 10px;
  font-size: 1.1rem;
  font-weight: 700;
  flex-shrink: 0;
}

.logo-preview-box img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}

.logo-controls {
  display: flex;
  flex-direction: column;
  gap: .5rem;
}

.toggle-activo {
  display: flex;
  align-items: center;
  gap: .75rem;
  font-weight: 600;
}

.actions {
  display: flex;
  align-items: center;
  gap: .5rem;
}

.sucursal-toggle {
  transform: scale(0.85);
}
</style>
