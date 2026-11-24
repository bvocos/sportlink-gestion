<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faCommentDots, faPen, faTrash } from '@/shared/icons'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { confirmAction, notify } from '@/shared/uiFeedback'
import GeografiaAutocomplete from '@/shared/components/GeografiaAutocomplete.vue'
import { formatCurrency as money, pluralize } from '@/shared/formatters'
import { useRoute } from 'vue-router'
import { isSearchFilterActive, searchQuery, useDebouncedSearch } from '@/shared/composables/useFilterTriggers'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS, tableFirst, type DataTablePageEvent } from '@/shared/tablePagination'
import { condicionIvaOptions, esCuitValido, esDniValido, normalizarDocumento, tipoDocumentoOptions } from '@/shared/fiscal'
import { isoDate } from '@/shared/dateFilters'

interface GeoOption { id: string; nombre: string }

const items = ref<any[]>([])
const showForm = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const saving = ref(false)
const buscar = ref('')
const page = ref(1)
const pageSize = ref(TABLE_ROWS)
const total = ref(0)
const first = computed(() => tableFirst(page.value, pageSize.value))
const loading = ref(false)
const loadError = ref('')
const provincias = ref<GeoOption[]>([])
const localidades = ref<GeoOption[]>([])
const geoAvailable = ref(false)
const geoMode = ref(false)
const geoLoading = ref(false)
const geoNotice = ref('')
const tipos = ['Particular', 'Club', 'Empresa', 'Constructor', 'Revendedor', 'Otro']
const route = useRoute()
const soloDeudores = ref(route.query.deuda === '1')
const deudas = ref<Record<string, any>>({})
const cuentaVisible = ref(false)
const cuenta = ref<any | null>(null)
const cuentaError = ref('')

const blank = () => ({
  nombre: '', apellido: '', telefono: '', correo: '', localidad: '', provincia: '',
  localidadId: '', provinciaId: '', tipo: 'Particular',
  fechaPrimerContacto: isoDate(), observaciones: '',
  razonSocial: '', tipoDocumento: null as string | null, numeroDocumento: '',
  condicionIva: null as string | null, domicilio: '', codigoPostal: '',
})
const form = ref(blank())

async function load(reset = false) {
  if (reset) page.value = 1
  loading.value = true
  loadError.value = ''
  try {
    const { data } = await http.get('/clientes', { params: { page: page.value, pageSize: pageSize.value, buscar: searchQuery(buscar.value) ?? undefined } })
    items.value = data.items
    total.value = Number(data.totalCount ?? data.total ?? data.items?.length ?? 0)
    if (!showForm.value) await loadDeudas()
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, 'No se pudieron cargar los clientes.')
  } finally {
    loading.value = false
  }
}

async function loadDeudas() {
  try {
    const { data } = await http.get('/clientes/deudores', { params: { page: 1, pageSize: 100, buscar: searchQuery(buscar.value) ?? undefined } })
    deudas.value = Object.fromEntries((data.items ?? []).map((x: any) => [x.id, x]))
    if (soloDeudores.value) { items.value = items.value.filter(x => deudas.value[x.id]); total.value = Number(data.total ?? items.value.length) }
  } catch { deudas.value = {} }
}
async function openAccount(client: any) {
  cuentaVisible.value = true; cuenta.value = null; cuentaError.value = ''
  try { cuenta.value = (await http.get(`/clientes/${client.id}/cuenta-corriente`)).data }
  catch (e) { cuentaError.value = apiErrorMessage(e, 'No se pudo cargar la cuenta corriente.') }
}
async function downloadAccount(ext: 'csv' | 'pdf') {
  if (!cuenta.value) return
  const response = await http.get(`/clientes/${cuenta.value.cliente.id}/cuenta-corriente.${ext}`, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data); const a = document.createElement('a'); a.href = url; a.download = `Estado-cuenta.${ext}`; a.click(); URL.revokeObjectURL(url)
}

function onPage(event: DataTablePageEvent) {
  page.value = event.page + 1
  pageSize.value = event.rows
  load()
}

async function loadProvincias() {
  try {
    provincias.value = (await http.get('/geografia/provincias')).data
    geoAvailable.value = true
  } catch {
    geoAvailable.value = false
    geoNotice.value = 'No se pudo consultar Georef. Podés cargar provincia y localidad manualmente.'
  }
}

async function loadLocalidades() {
  localidades.value = []
  if (!form.value.provinciaId) return
  geoLoading.value = true
  try {
    localidades.value = (await http.get('/geografia/localidades', { params: { provincia: form.value.provinciaId } })).data
  } catch {
    geoNotice.value = 'Georef no respondió. Se habilitó la carga manual para no bloquear el formulario.'
    geoMode.value = false
    form.value.provinciaId = ''
    form.value.localidadId = ''
  } finally {
    geoLoading.value = false
  }
}

function cancelForm() {
  showForm.value = false
  editingId.value = null
  form.value = blank()
  error.value = ''
  geoNotice.value = ''
  localidades.value = []
}

function create() {
  editingId.value = null
  form.value = blank()
  error.value = ''
  geoNotice.value = geoAvailable.value ? '' : 'Georef no está disponible. Usá la carga manual.'
  geoMode.value = geoAvailable.value
  localidades.value = []
  showForm.value = true
}

async function edit(c: any) {
  editingId.value = c.id
  form.value = {
    nombre: c.nombre, apellido: c.apellido, telefono: c.telefono, correo: c.correo ?? '',
    localidad: c.localidad, provincia: c.provincia, localidadId: c.localidadId ?? '',
    provinciaId: c.provinciaId ?? '', tipo: c.tipo, fechaPrimerContacto: c.fechaPrimerContacto,
    observaciones: c.observaciones ?? '', razonSocial: c.razonSocial ?? '',
    tipoDocumento: c.tipoDocumento ?? null, numeroDocumento: c.numeroDocumento ?? '',
    condicionIva: c.condicionIva ?? null, domicilio: c.domicilio ?? '', codigoPostal: c.codigoPostal ?? '',
  }
  error.value = ''
  geoMode.value = geoAvailable.value && !!c.provinciaId && !!c.localidadId
  geoNotice.value = !geoMode.value && geoAvailable.value
    ? 'Esta ubicación proviene de datos históricos. Podés conservarla o normalizarla con Georef.'
    : ''
  showForm.value = true
  if (geoMode.value) await loadLocalidades()
}

async function provinceChanged() {
  const option = provincias.value.find(x => x.id === form.value.provinciaId)
  form.value.provincia = option?.nombre ?? ''
  form.value.localidad = ''
  form.value.localidadId = ''
  await loadLocalidades()
}

function localitySelected(option: GeoOption) { form.value.localidad = option.nombre }

function enableOfficial() {
  geoMode.value = true
  geoNotice.value = ''
  form.value.provincia = ''
  form.value.localidad = ''
  form.value.provinciaId = ''
  form.value.localidadId = ''
  localidades.value = []
}

function useManual() {
  geoMode.value = false
  form.value.provinciaId = ''
  form.value.localidadId = ''
  geoNotice.value = 'Ubicación en modo manual: se guardarán los nombres sin códigos oficiales.'
}

function apiError(e: any) {
  const errors = e.response?.data?.errors
  if (errors) {
    const first = Object.values(errors).flat()[0]
    if (first) return String(first)
  }
  return apiErrorMessage(e, 'No se pudo guardar el cliente.')
}

async function save() {
  if (geoMode.value && (!form.value.provinciaId || !form.value.localidadId)) {
    error.value = 'Seleccioná una provincia y una localidad de las opciones oficiales.'
    return
  }
  const documentError = fiscalDocumentError()
  if (documentError) {
    error.value = documentError
    return
  }
  saving.value = true
  error.value = ''
  try {
    if (editingId.value) await http.put(`/clientes/${editingId.value}`, form.value)
    else await http.post('/clientes', form.value)
    cancelForm()
    await load()
  } catch (e: any) {
    error.value = apiError(e)
  } finally {
    saving.value = false
  }
}

function fiscalDocumentError() {
  const digits = normalizarDocumento(form.value.numeroDocumento)
  if (['ResponsableInscripto', 'Monotributista', 'MonotributistaSocial', 'Exento'].includes(form.value.condicionIva ?? '') && form.value.tipoDocumento !== 'Cuit')
    return 'Para esta condición de IVA se necesita el CUIT del cliente.'
  if (digits && !form.value.tipoDocumento) return 'Indicá el tipo de documento.'
  if (form.value.tipoDocumento && !digits) return 'Indicá el número de documento.'
  if (digits && (form.value.tipoDocumento === 'Cuit' || form.value.tipoDocumento === 'Cuil') && !esCuitValido(digits))
    return `El ${form.value.tipoDocumento === 'Cuit' ? 'CUIT' : 'CUIL'} ingresado no es válido. Revisá los dígitos.`
  if (digits && form.value.tipoDocumento === 'Dni' && !esDniValido(digits))
    return 'El DNI ingresado no es válido. Debe tener 7 u 8 dígitos.'
  return ''
}

async function remove(c: any) {
  if (!await confirmAction({ title: 'Eliminar cliente', message: `¿Querés eliminar a ${c.nombreCompleto}?`, confirmText: 'Eliminar', danger: true })) return
  try {
    if (editingId.value === c.id) cancelForm()
    await http.delete(`/clientes/${c.id}`)
    await load()
  } catch (e: any) {
    notify(apiErrorMessage(e, 'No se pudo eliminar el cliente.'))
  }
}

function whatsappUrl(phone: string) {
  let digits = String(phone ?? '').replace(/\D/g, '')
  if (digits.startsWith('00')) digits = digits.slice(2)
  if (digits.startsWith('54')) return `https://wa.me/${digits}`
  digits = digits.replace(/^0/, '')
  return `https://wa.me/549${digits}`
}

onMounted(async () => { await Promise.all([load(), loadProvincias()]) })
useDebouncedSearch(buscar, () => load(true))
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-2">
      <p class="page-desc text-color-secondary m-0">
        {{ showForm
          ? (editingId ? 'Editar cliente' : 'Nuevo cliente')
          : 'Personas y organizaciones con las que trabajás.' }}
      </p>
      <AppButton
        v-if="!showForm"
        label="Nuevo cliente"
        icon="pi pi-plus"
        size="small"
        @click="create"
      />
    </div>

    <Panel v-if="!showForm" class="filter-panel">
      <div class="grid formgrid p-fluid filter-form">
        <div class="field col-12 md:col-10">
          <label for="buscar">Buscar cliente</label>
          <InputText id="buscar" v-model="buscar" type="search" placeholder="Nombre, razón social o documento" />
          <small v-if="buscar.trim() && !isSearchFilterActive(buscar)" class="text-color-secondary">Escribí al menos 3 caracteres</small>
        </div>
        <div class="field col-12 md:col-2 filter-actions flex align-items-end">
          <AppButton type="button" label="Limpiar" icon="pi pi-filter-slash" severity="secondary" size="small" @click="buscar = ''; load(true)" />
        </div>
        <div class="field col-12">
          <label class="flex align-items-center gap-2"><input v-model="soloDeudores" type="checkbox" @change="load(true)"> Con deuda</label>
        </div>
      </div>
    </Panel>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load()" />
    </Message>

    <template v-else-if="!showForm">
      <div class="table-panel">
        <p class="page-desc text-color-secondary m-0 mb-2 px-1">
          {{ pluralize(total, 'cliente registrado', 'clientes registrados') }}
        </p>
        <DataTable
          :value="items"
          :loading="loading"
          lazy
          paginator
          :rows="pageSize"
          :total-records="total"
          :first="first"
          :rows-per-page-options="TABLE_ROWS_OPTIONS"
          striped-rows
          @page="onPage"
        >
          <template #empty>
            <div class="text-center py-5">
              <p class="font-bold mb-2">{{ searchQuery(buscar) ? 'Ningún resultado' : 'Todavía no hay clientes' }}</p>
              <p class="text-color-secondary mb-3">
                {{ searchQuery(buscar) ? 'Probá ajustar la búsqueda por nombre o apellido.' : 'Cargá el primero para empezar a vender y presupuestar.' }}
              </p>
              <AppButton v-if="!searchQuery(buscar)" label="Nuevo cliente" icon="pi pi-plus" size="small" @click="create" />
            </div>
          </template>
          <Column header="Nombre">
            <template #body="{ data }"><b>{{ data.nombreCompleto }}</b></template>
          </Column>
          <Column header="Tipo">
            <template #body="{ data }"><Tag :value="data.tipo" severity="success" /></template>
          </Column>
          <Column header="Documento">
            <template #body="{ data }">{{ data.numeroDocumentoFormateado ?? '' }}</template>
          </Column>
          <Column header="Contacto" body-class="cell-wrap">
            <template #body="{ data }">
              <div class="flex align-items-center gap-2">
                <span>{{ data.telefono }}</span>
                <a
                  v-if="data.telefono"
                  class="whatsapp-link"
                  :href="whatsappUrl(data.telefono)"
                  target="_blank"
                  rel="noopener noreferrer"
                  :aria-label="`Abrir WhatsApp de ${data.nombreCompleto}`"
                >
                  <AppIcon :icon="faCommentDots" />
                </a>
              </div>
              <small class="text-color-secondary">{{ data.correo }}</small>
            </template>
          </Column>
          <Column header="Ubicación" body-class="cell-wrap">
            <template #body="{ data }">
              {{ data.localidad }}, {{ data.provincia }}<br>
              <Tag
                :value="data.localidadId ? 'Ubicación oficial' : 'Dato histórico/manual'"
                :severity="data.localidadId ? 'success' : 'warn'"
                class="mt-1"
              />
            </template>
          </Column>
          <Column header="Deuda" body-class="cell-num">
            <template #body="{ data }"><span v-if="deudas[data.id]" :class="{ 'text-orange-600 font-bold': deudas[data.id].deudaVencidaUsd > 0 }">{{ money(deudas[data.id].deudaTotalUsd) }}</span><span v-else>—</span></template>
          </Column>
          <Column header="" body-class="cell-actions">
            <template #body="{ data }">
              <div class="flex gap-1">
                <AppButton text rounded severity="secondary" icon="pi pi-wallet" aria-label="Cuenta corriente" tooltip="Cuenta corriente" @click="openAccount(data)" />
                <AppButton text rounded severity="secondary" aria-label="Editar cliente" @click="edit(data)">
                  <AppIcon :icon="faPen" />
                </AppButton>
                <AppButton text rounded severity="danger" aria-label="Eliminar cliente" @click="remove(data)">
                  <AppIcon :icon="faTrash" />
                </AppButton>
              </div>
            </template>
          </Column>
        </DataTable>
      </div>
    </template>

    <article v-else class="card inline-form-panel inline-form-panel-wide">
      <form @submit.prevent="save">
        <Message v-if="error" severity="error" class="inline-form-error" :closable="false">{{ error }}</Message>
        <Message v-if="geoNotice" severity="info" class="mb-3" :closable="false">{{ geoNotice }}</Message>

        <div class="p-fluid cliente-form-pairs">
          <div class="field">
            <label for="cliente-nombre">Nombre</label>
            <InputText id="cliente-nombre" v-model="form.nombre" size="small" maxlength="100" required />
          </div>
          <div class="field">
            <label for="cliente-apellido">Apellido</label>
            <InputText id="cliente-apellido" v-model="form.apellido" size="small" maxlength="100" required />
          </div>
          <div class="field">
            <label for="cliente-telefono">Teléfono</label>
            <InputText id="cliente-telefono" v-model="form.telefono" type="tel" size="small" maxlength="30" required />
          </div>
          <div class="field">
            <label for="cliente-correo">Correo electrónico</label>
            <InputText id="cliente-correo" v-model="form.correo" type="email" size="small" maxlength="200" />
          </div>

          <template v-if="geoMode">
            <div class="cliente-form-banner"><Tag value="Ubicación oficial de Argentina" severity="success" /></div>
            <div class="field">
              <label>Provincia</label>
              <Select
                v-model="form.provinciaId"
                :options="provincias"
                option-label="nombre"
                option-value="id"
                placeholder="Seleccionar provincia"
                size="small"
                required
                @change="provinceChanged"
              />
            </div>
            <div class="field">
              <label>Localidad</label>
              <GeografiaAutocomplete
                v-model="form.localidadId"
                :options="localidades"
                :disabled="!form.provinciaId || geoLoading"
                :placeholder="geoLoading ? 'Cargando localidades…' : 'Buscar localidad'"
                @select="localitySelected"
              />
            </div>
            <div class="cliente-form-action">
              <AppButton type="button" label="Cargar manualmente (sin normalizar)" severity="secondary" size="small" @click="useManual" />
            </div>
          </template>

          <template v-else>
            <div class="cliente-form-banner"><Tag value="Modo manual: la ubicación se guardará sin códigos oficiales." severity="warn" /></div>
            <div class="field">
              <label>Provincia</label>
              <InputText v-model="form.provincia" maxlength="100" size="small" required />
            </div>
            <div class="field">
              <label>Localidad</label>
              <InputText v-model="form.localidad" maxlength="100" size="small" required />
            </div>
            <div v-if="geoAvailable" class="cliente-form-action">
              <AppButton type="button" label="Usar ubicaciones oficiales de Argentina" severity="secondary" size="small" @click="enableOfficial" />
            </div>
          </template>

          <div class="field">
            <label>Tipo</label>
            <Select v-model="form.tipo" :options="tipos" size="small" />
          </div>
          <div class="field">
            <label>Primer contacto</label>
            <AppDatePicker v-model="form.fechaPrimerContacto" required />
          </div>
          <div class="cliente-form-banner"><Tag value="Datos fiscales (opcional)" severity="info" /></div>
          <div class="field">
            <label>Razón social</label>
            <InputText v-model="form.razonSocial" maxlength="200" size="small" />
          </div>
          <div class="field">
            <label>Tipo de documento</label>
            <Select v-model="form.tipoDocumento" :options="tipoDocumentoOptions" option-label="label" option-value="value" show-clear size="small" />
          </div>
          <div class="field">
            <label>Número de documento</label>
            <InputText v-model="form.numeroDocumento" maxlength="13" size="small" inputmode="numeric" />
            <small v-if="fiscalDocumentError()" class="p-error">{{ fiscalDocumentError() }}</small>
          </div>
          <div class="field">
            <label>Condición frente al IVA</label>
            <Select v-model="form.condicionIva" :options="condicionIvaOptions" option-label="label" option-value="value" show-clear size="small" />
          </div>
          <div class="field">
            <label>Domicilio</label>
            <InputText v-model="form.domicilio" maxlength="250" size="small" />
          </div>
          <div class="field">
            <label>Código postal</label>
            <InputText v-model="form.codigoPostal" maxlength="20" size="small" />
          </div>
          <div class="field field-full">
            <label>Observaciones</label>
            <Textarea v-model="form.observaciones" maxlength="1000" rows="3" auto-resize />
          </div>
        </div>

        <div class="inline-form-footer">
          <small class="inline-form-note text-color-secondary">El cliente queda disponible para ventas y presupuestos.</small>
          <div class="flex gap-2">
            <AppButton type="button" label="Cancelar" severity="secondary" size="small" @click="cancelForm" />
            <AppButton type="submit" :label="saving ? 'Guardando…' : (editingId ? 'Guardar cambios' : 'Guardar cliente')" :loading="saving" size="small" />
          </div>
        </div>
      </form>
    </article>

    <Dialog v-model:visible="cuentaVisible" modal :header="`Cuenta corriente${cuenta?.cliente?.nombre ? ` — ${cuenta.cliente.nombre}` : ''}`" :style="{ width: 'min(1050px, 96vw)' }">
      <Message v-if="cuentaError" severity="error" :closable="false">{{ cuentaError }}</Message>
      <div v-else-if="cuenta" class="flex flex-column gap-3">
        <div class="summary-metrics">
          <article class="card metric"><small>Deuda total</small><strong>{{ money(cuenta.resumen.deudaTotalUsd) }}</strong><em v-if="cuenta.resumen.deudaTotalArs">≈ ARS {{ Number(cuenta.resumen.deudaTotalArs).toLocaleString('es-AR') }}</em></article>
          <article class="card metric"><small>Saldo de ventas</small><strong>{{ money(cuenta.resumen.saldoVentasUsd) }}</strong></article>
          <article class="card metric"><small>Cuotas pendientes</small><strong>{{ money(cuenta.resumen.cuotasPendientesUsd) }}</strong></article>
          <article class="card metric"><small>Vencido</small><strong class="text-orange-600">{{ money(cuenta.resumen.cuotasVencidasUsd) }}</strong></article>
        </div>
        <div class="flex justify-content-end gap-2"><AppButton label="CSV" icon="pi pi-download" severity="secondary" @click="downloadAccount('csv')" /><AppButton label="Estado de cuenta PDF" icon="pi pi-file-pdf" @click="downloadAccount('pdf')" /></div>
        <section><h4>Ventas con saldo</h4><div class="table-panel"><DataTable :value="cuenta.ventasConSaldo" striped-rows><Column field="fechaVenta" header="Fecha" /><Column field="producto" header="Producto" /><Column header="Total"><template #body="{ data }">{{ money(data.total) }}</template></Column><Column header="Cobrado"><template #body="{ data }">{{ money(data.cobrado) }}</template></Column><Column header="Saldo"><template #body="{ data }"><b>{{ money(data.saldo) }}</b></template></Column></DataTable></div></section>
        <section><h4>Cuotas pendientes</h4><div class="table-panel"><DataTable :value="cuenta.cuotasPendientes" striped-rows><Column field="numero" header="Cuota" /><Column field="fechaVencimiento" header="Vencimiento" /><Column header="Pendiente"><template #body="{ data }"><b>{{ money(data.pendiente) }}</b></template></Column><Column field="estado" header="Estado" /></DataTable></div></section>
        <section><h4>Movimientos recientes</h4><div class="table-panel"><DataTable :value="cuenta.movimientos" striped-rows><Column field="fecha" header="Fecha" /><Column field="concepto" header="Concepto" /><Column header="Importe USD"><template #body="{ data }">{{ money(data.importeUsd) }}</template></Column></DataTable></div></section>
      </div>
    </Dialog>
  </section>
</template>
