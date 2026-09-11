<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { formatCurrency as money, pluralize } from '@/shared/formatters'
import { downloadBlob, downloadCsv } from '@/shared/csv'
import { notify } from '@/shared/uiFeedback'
import { formatDateRangeLabel, monthRange } from '@/shared/dateFilters'
import { isSearchFilterActive, searchQuery, useDebouncedSearch, useImmediateFilters } from '@/shared/composables/useFilterTriggers'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS, tableFirst, type DataTablePageEvent } from '@/shared/tablePagination'

const items = ref<any[]>([])
const loading = ref(false)
const exporting = ref(false)
const loadError = ref('')
const defaultRange = monthRange()
const buscar = ref('')
const desde = ref(defaultRange.desde)
const hasta = ref(defaultRange.hasta)
const filterPeriodLabel = computed(() => formatDateRangeLabel(desde.value, hasta.value))
const estadoFinanciero = ref('')
const page = ref(1)
const pageSize = ref(TABLE_ROWS)
const first = computed(() => tableFirst(page.value, pageSize.value))
const total = ref(0)
const totales = ref({
  cantidadVentas: 0,
  facturacionTotal: 0,
  costoTotal: 0,
  gananciaNetaTotal: 0,
  margenPromedioPonderado: 0,
})

const estadosFinancieros = [
  { label: 'Todos los estados', value: '' },
  { label: 'Pendiente de cobro', value: 'Pendiente de cobro' },
  { label: 'Rentable', value: 'Rentable' },
  { label: 'Muy rentable', value: 'Muy rentable' },
  { label: 'En pérdida', value: 'En pérdida' },
]

function hasActiveFilters() {
  return !!(searchQuery(buscar.value) || estadoFinanciero.value)
}
function financialStatusSeverity(status: string) {
  return ({
    'En pérdida': 'danger',
    'Pendiente de cobro': 'warn',
    Rentable: 'success',
    'Muy rentable': 'info',
  } as Record<string, string>)[status] ?? 'secondary'
}
async function load(reset = false) {
  if (reset) page.value = 1
  loading.value = true
  loadError.value = ''
  try {
    const { data } = await http.get('/rentabilidad', {
      params: {
        buscar: searchQuery(buscar.value) ?? undefined,
        desde: desde.value || undefined,
        hasta: hasta.value || undefined,
        estadoFinanciero: estadoFinanciero.value || undefined,
        page: page.value,
        pageSize: pageSize.value,
      },
    })
    items.value = data.items
    totales.value = data.totales
    total.value = data.total
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, 'No se pudo cargar el reporte de rentabilidad.')
  } finally {
    loading.value = false
  }
}
function clearFilters() {
  buscar.value = ''
  const range = monthRange()
  desde.value = range.desde
  hasta.value = range.hasta
  estadoFinanciero.value = ''
  load(true)
}
function onPage(event: DataTablePageEvent) {
  page.value = event.page + 1
  pageSize.value = event.rows
  load()
}

function exportPage() {
  downloadCsv(
    `rentabilidad-pagina-${page.value}-${new Date().toISOString().slice(0, 10)}.csv`,
    [
      'ID venta', 'Fecha', 'Cliente', 'Venta', 'Costo operativo', 'IVA', 'Costo total',
      'Ganancia bruta', 'Ganancia neta', 'Margen %', 'Cobrado', 'Pendiente total',
      'Pendiente en cuotas', 'Estado',
    ],
    items.value.map((r) => [
      r.id, r.fechaVenta, r.cliente, r.precioTotal, r.costoOperativo, r.iva, r.costoTotal,
      r.gananciaBruta, r.gananciaNeta, (r.margen * 100).toFixed(2), r.totalCobrado,
      r.totalPendiente, r.saldoPendienteCuotas, r.estadoFinanciero,
    ]),
  )
}
async function exportAll() {
  exporting.value = true
  try {
    const response = await http.get('/rentabilidad/exportar', {
      params: {
        buscar: searchQuery(buscar.value) ?? undefined,
        desde: desde.value || undefined,
        hasta: hasta.value || undefined,
        estadoFinanciero: estadoFinanciero.value || undefined,
      },
      responseType: 'blob',
    })
    downloadBlob(response.data, `rentabilidad-completa-${new Date().toISOString().slice(0, 10)}.csv`)
  } catch (e: any) {
    notify(apiErrorMessage(e, 'No se pudo exportar el reporte completo.'))
  } finally {
    exporting.value = false
  }
}
onMounted(() => load())
useDebouncedSearch(buscar, () => load(true))
useImmediateFilters([desde, hasta, estadoFinanciero], () => load(true))
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Precio − costos operativos − IVA = ganancia neta.</p>
      <div class="flex gap-2 flex-wrap">
        <AppButton label="Exportar página" severity="secondary" :disabled="!items.length" @click="exportPage" />
        <AppButton :label="exporting ? 'Preparando…' : 'Exportar todo lo filtrado'" :disabled="!total || exporting" :loading="exporting" @click="exportAll" />
      </div>
    </div>

    <Panel class="filter-panel">
      <p class="filter-period-hint pt-2">
        Período activo: <strong>{{ filterPeriodLabel }}</strong>
      </p>
      <div class="grid formgrid p-fluid filter-form">
        <div class="field col-12 md:col-3">
          <label for="buscar">Cliente o ID de venta</label>
          <InputText id="buscar" v-model="buscar" type="search" placeholder="Nombre, apellido o GUID" />
          <small v-if="buscar.trim() && !isSearchFilterActive(buscar)" class="text-color-secondary">Escribí al menos 3 caracteres</small>
        </div>
        <div class="field col-12 md:col-2">
          <label for="desde">Desde</label>
          <AppDatePicker id="desde" v-model="desde" />
        </div>
        <div class="field col-12 md:col-2">
          <label for="hasta">Hasta</label>
          <AppDatePicker id="hasta" v-model="hasta" />
        </div>
        <div class="field col-12 md:col-3">
          <label>Estado financiero</label>
          <Select v-model="estadoFinanciero" :options="estadosFinancieros" option-label="label" option-value="value" />
        </div>
        <div class="field col-12 md:col-2 filter-actions flex align-items-end">
          <AppButton type="button" label="Limpiar" icon="pi pi-filter-slash" severity="secondary" @click="clearFilters" />
        </div>
      </div>
    </Panel>

    <div v-if="!loadError" class="summary-metrics">
      <Panel class="card metric">
        <small>Facturación</small>
        <strong class="block text-2xl">{{ money(totales.facturacionTotal) }}</strong>
        <em class="text-color-secondary">{{ pluralize(totales.cantidadVentas, 'venta', 'ventas') }}</em>
      </Panel>
      <Panel class="card metric">
        <small>Costo total</small>
        <strong class="block text-2xl">{{ money(totales.costoTotal) }}</strong>
        <em class="text-color-secondary">Compra, envío, otros e IVA</em>
      </Panel>
      <Panel class="card metric">
        <small>Ganancia neta</small>
        <strong class="block text-2xl" :class="{ negative: totales.gananciaNetaTotal < 0 }">{{ money(totales.gananciaNetaTotal) }}</strong>
        <em class="text-color-secondary">Sobre todo el resultado</em>
      </Panel>
      <Panel class="card metric">
        <small>Margen ponderado</small>
        <strong class="block text-2xl">{{ (totales.margenPromedioPonderado * 100).toFixed(2) }}%</strong>
        <em class="text-color-secondary">Ponderado por facturación</em>
      </Panel>
    </div>

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
          <div class="text-center py-5">
            <template v-if="hasActiveFilters()">
              <p class="font-bold mb-2">Ningún resultado con estos filtros</p>
              <p class="text-color-secondary">Probá ajustarlos.</p>
            </template>
            <template v-else>
              <p class="font-bold mb-2">Todavía no hay ventas para analizar.</p>
              <RouterLink to="/ventas/nueva">
                <AppButton label="Nueva venta" icon="pi pi-plus" />
              </RouterLink>
            </template>
          </div>
        </template>
        <Column field="fechaVenta" header="Fecha" />
        <Column header="Cliente" body-class="cell-wrap">
          <template #body="{ data }">
            <b>{{ data.cliente }}</b>
            <small class="muted block">{{ data.id }}</small>
          </template>
        </Column>
        <Column header="Venta" body-class="cell-num">
          <template #body="{ data }">{{ money(data.precioTotal) }}</template>
        </Column>
        <Column header="Costo operativo" body-class="cell-num">
          <template #body="{ data }">{{ money(data.costoOperativo) }}</template>
        </Column>
        <Column header="IVA" body-class="cell-num">
          <template #body="{ data }">{{ money(data.iva) }}</template>
        </Column>
        <Column header="Costo total" body-class="cell-num">
          <template #body="{ data }">{{ money(data.costoTotal) }}</template>
        </Column>
        <Column header="Ganancia bruta" body-class="cell-num">
          <template #body="{ data }">{{ money(data.gananciaBruta) }}</template>
        </Column>
        <Column header="Ganancia neta" body-class="cell-num">
          <template #body="{ data }">
            <b :class="{ negative: data.gananciaNeta < 0 }">{{ money(data.gananciaNeta) }}</b>
          </template>
        </Column>
        <Column header="Margen" body-class="cell-num">
          <template #body="{ data }">{{ (data.margen * 100).toFixed(2) }}%</template>
        </Column>
        <Column header="Pendiente" body-class="cell-wrap cell-num">
          <template #body="{ data }">
            <b>{{ money(data.totalPendiente) }}</b>
            <small
              v-if="data.formaPago === 'Cuotas'"
              class="text-color-secondary block"
              :class="{ negative: Math.abs(data.totalPendiente - data.saldoPendienteCuotas) > 0.01 }"
            >
              En cuotas: {{ money(data.saldoPendienteCuotas) }}
            </small>
          </template>
        </Column>
        <Column header="Estado">
          <template #body="{ data }">
            <Tag :value="data.estadoFinanciero" :severity="financialStatusSeverity(data.estadoFinanciero)" />
          </template>
        </Column>
      </DataTable>
    </div>
  </section>
</template>
