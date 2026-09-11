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
import AppIcon from '@/shared/components/AppIcon.vue'
import { faEye } from '@/shared/icons'
import { http } from '@/shared/api/httpClient'
import { pluralize } from '@/shared/formatters'
import { auditDefaultRange, formatDateRangeLabel } from '@/shared/dateFilters'
import { isSearchFilterActive, searchQuery, useDebouncedSearch, useImmediateFilters } from '@/shared/composables/useFilterTriggers'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS, tableFirst, type DataTablePageEvent } from '@/shared/tablePagination'

const items = ref<any[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(TABLE_ROWS)
const first = computed(() => tableFirst(page.value, pageSize.value))
const loading = ref(false)
const selected = ref<any | null>(null)
const showDetail = ref(false)
const loadError = ref('')

const modulos = ['', 'Ventas', 'Cuotas', 'Caja', 'Clientes', 'Usuarios', 'Administración']
const acciones = ['', 'Creación', 'Modificación', 'Eliminación']

const filters = ref({
  modulo: '',
  accion: '',
  buscar: '',
  ...auditDefaultRange(),
})
const filterPeriodLabel = computed(() => formatDateRangeLabel(filters.value.desde, filters.value.hasta))

const fieldLabels: Record<string, string> = {
  Id: 'Identificador',
  CreatedAt: 'Fecha de creación',
  UpdatedAt: 'Última modificación',
  Nombre: 'Nombre',
  Apellido: 'Apellido',
  NombreUsuario: 'Nombre de usuario',
  Rol: 'Rol',
  PermisosJson: 'Permisos',
  Activo: 'Activo',
  Telefono: 'Teléfono',
  Correo: 'Correo electrónico',
  Localidad: 'Localidad',
  Provincia: 'Provincia',
  Tipo: 'Tipo',
  FechaPrimerContacto: 'Primer contacto',
  Observaciones: 'Observaciones',
  ClienteId: 'Cliente',
  TipoCespedId: 'Tipo de césped',
  AlicuotaIvaId: 'Alícuota de IVA',
  FechaVenta: 'Fecha de venta',
  FechaEntregaEstimada: 'Entrega estimada',
  CantidadM2: 'Cantidad de m²',
  PrecioUnitario: 'Precio por m²',
  PrecioTotal: 'Precio total',
  MontoEntrega: 'Monto de entrega',
  CostoCompraUnitario: 'Costo por m²',
  CostoCompraTotal: 'Costo de compra',
  CostoEnvio: 'Costo de envío',
  OtrosCostos: 'Otros costos',
  Iva: 'IVA',
  GananciaBruta: 'Ganancia bruta',
  GananciaNeta: 'Ganancia neta',
  Margen: 'Margen',
  FormaPago: 'Forma de pago',
  CantidadCuotas: 'Cantidad de cuotas',
  Estado: 'Estado',
  VentaId: 'Venta',
  Numero: 'Número de cuota',
  FechaVencimiento: 'Vencimiento',
  FechaPago: 'Fecha de pago',
  ImportePactado: 'Importe pactado',
  ImportePagado: 'Importe pagado',
  MedioPago: 'Medio de pago',
  Fecha: 'Fecha',
  Monto: 'Monto',
  Concepto: 'Concepto',
  Usuario: 'Usuario',
  CuotaId: 'Cuota',
  Descripcion: 'Descripción',
  PrecioVentaM2: 'Precio de venta por m²',
  PrecioContadoM2: 'Precio contado por m²',
  PrecioFinanciadoM2: 'Precio financiado por m²',
  CostoM2: 'Costo por m²',
  Porcentaje: 'Porcentaje',
  Clave: 'Clave',
  ValorDecimal: 'Valor',
}

async function load(reset = false) {
  if (reset) page.value = 1
  loading.value = true
  loadError.value = ''
  try {
    const { data } = await http.get('/auditoria', {
      params: {
        ...filters.value,
        buscar: searchQuery(filters.value.buscar) ?? undefined,
        page: page.value,
        pageSize: pageSize.value,
      },
    })
    items.value = data.items
    total.value = data.total
  } catch {
    loadError.value = 'No se pudo cargar el registro de auditoría.'
  } finally {
    loading.value = false
  }
}
function clearFilters() {
  filters.value = { modulo: '', accion: '', buscar: '', ...auditDefaultRange() }
  load(true)
}
function onPage(event: DataTablePageEvent) {
  page.value = event.page + 1
  pageSize.value = event.rows
  load()
}

function details(item: any) {
  try {
    return Object.entries(JSON.parse(item.detalleJson)) as [string, any][]
  } catch {
    return []
  }
}
function detailRows(item: any) {
  return details(item).map(([field, change]) => ({ field, anterior: change.anterior, nuevo: change.nuevo }))
}
function openDetails(item: any) {
  selected.value = item
  showDetail.value = true
}
function closeDetails() {
  showDetail.value = false
  selected.value = null
}
function value(v: unknown) {
  if (v === null || v === undefined || v === '') return '—'
  if (typeof v === 'boolean') return v ? 'Sí' : 'No'
  return String(v)
}
function fieldLabel(field: string) {
  return fieldLabels[field] ?? field.replace(/([a-záéíóú])([A-ZÁÉÍÓÚ])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase())
}
function dateTime(v: string) {
  return new Intl.DateTimeFormat('es-AR', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(v))
}
onMounted(() => load())
useDebouncedSearch(computed({ get: () => filters.value.buscar, set: (v) => { filters.value.buscar = v } }), () => load(true))
useImmediateFilters([() => filters.value.modulo, () => filters.value.accion, () => filters.value.desde, () => filters.value.hasta], () => load(true))
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar mb-3">
      <p class="page-desc text-color-secondary m-0">Historial de las operaciones realizadas por los usuarios.</p>
    </div>

    <Panel class="filter-panel">
      <p class="filter-period-hint pt-2">
        Período activo: <strong>{{ filterPeriodLabel }}</strong>
      </p>
      <div class="grid formgrid p-fluid filter-form">
        <div class="field col-12 md:col-2">
          <label>Vista o módulo</label>
          <Select
            v-model="filters.modulo"
            :options="modulos.map(m => ({ label: m || 'Todos', value: m }))"
            option-label="label"
            option-value="value"
          />
        </div>
        <div class="field col-12 md:col-2">
          <label>Acción</label>
          <Select
            v-model="filters.accion"
            :options="acciones.map(a => ({ label: a || 'Todas', value: a }))"
            option-label="label"
            option-value="value"
          />
        </div>
        <div class="field col-12 md:col-2">
          <label for="desde">Desde</label>
          <AppDatePicker id="desde" v-model="filters.desde" />
        </div>
        <div class="field col-12 md:col-2">
          <label for="hasta">Hasta</label>
          <AppDatePicker id="hasta" v-model="filters.hasta" />
        </div>
        <div class="field col-12 md:col-3 audit-search">
          <label for="buscar">Usuario, venta o movimiento</label>
          <InputText id="buscar" v-model="filters.buscar" type="search" placeholder="Buscar usuario, ID o cambio" />
          <small v-if="filters.buscar.trim() && !isSearchFilterActive(filters.buscar)" class="text-color-secondary">Escribí al menos 3 caracteres</small>
        </div>
        <div class="field col-12 md:col-1 filter-actions flex align-items-end">
          <AppButton type="button" label="Limpiar" icon="pi pi-filter-slash" severity="secondary" class="w-full" @click="clearFilters" />
        </div>
      </div>
    </Panel>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load()" />
    </Message>

    <div v-else class="table-panel">
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
          <div class="text-center py-5 text-color-secondary">No hay registros para los filtros elegidos.</div>
        </template>
        <Column header="Fecha y hora">
          <template #body="{ data }">{{ dateTime(data.fechaHora) }}</template>
        </Column>
        <Column header="Usuario">
          <template #body="{ data }"><b>{{ data.usuario }}</b></template>
        </Column>
        <Column field="modulo" header="Vista" />
        <Column header="Acción">
          <template #body="{ data }">
            <Tag :value="data.accion" :severity="data.accion === 'Eliminación' ? 'warn' : 'info'" />
          </template>
        </Column>
        <Column header="Registro afectado" body-class="cell-wrap">
          <template #body="{ data }">
            {{ data.entidad }}
            <small class="entity-id block">{{ data.entidadId }}</small>
          </template>
        </Column>
        <Column header="Cambios" style="width: 4rem">
          <template #body="{ data }">
            <AppButton text rounded severity="secondary" title="Ver cambios" @click="openDetails(data)">
              <AppIcon :icon="faEye" />
            </AppButton>
          </template>
        </Column>
      </DataTable>
    </div>

    <Dialog
      v-model:visible="showDetail"
      modal
      header="Cambios realizados"
      :style="{ width: 'min(720px, 96vw)' }"
      @hide="closeDetails"
    >
      <template v-if="selected">
        <p class="mt-0">
          <b>{{ selected.usuario }}</b> · {{ dateTime(selected.fechaHora) }} · {{ selected.entidad }}
        </p>
        <DataTable :value="detailRows(selected)" striped-rows size="small">
          <Column header="Campo">
            <template #body="{ data: row }"><b>{{ fieldLabel(row.field) }}</b></template>
          </Column>
          <Column header="Valor anterior">
            <template #body="{ data: row }">{{ value(row.anterior) }}</template>
          </Column>
          <Column header="Valor nuevo">
            <template #body="{ data: row }">{{ value(row.nuevo) }}</template>
          </Column>
        </DataTable>
        <div class="flex justify-content-end mt-4">
          <AppButton label="Cerrar" severity="secondary" @click="closeDetails" />
        </div>
      </template>
    </Dialog>
  </section>
</template>
