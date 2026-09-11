<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faPen, faTrash } from '@/shared/icons'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { formatCurrency as money, pluralize } from '@/shared/formatters'
import { confirmAction, notify } from '@/shared/uiFeedback'
import { formatDateRangeLabel, monthRange } from '@/shared/dateFilters'
import { isSearchFilterActive, searchQuery, useDebouncedSearch, useImmediateFilters } from '@/shared/composables/useFilterTriggers'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS, tableFirst, type DataTablePageEvent } from '@/shared/tablePagination'

const items = ref<any[]>([])
const total = ref(0)
const totalImporte = ref(0)
const page = ref(1)
const pageSize = ref(TABLE_ROWS)
const first = computed(() => tableFirst(page.value, pageSize.value))
const loading = ref(false)
const loadError = ref('')
const filters = ref({ ...monthRange(), buscar: '' })
const filterPeriodLabel = computed(() => formatDateRangeLabel(filters.value.desde, filters.value.hasta))
const show = ref(false)
const editingId = ref<string | null>(null)
const saving = ref(false)
const error = ref('')

const blank = () => ({ fecha: new Date().toISOString().slice(0, 10), categoria: '', descripcion: '', importe: 0, observaciones: '' })
const form = ref(blank())

async function load(reset = false) {
  if (reset) page.value = 1
  loading.value = true
  loadError.value = ''
  try {
    const { data } = await http.get('/gastos', { params: { ...filters.value, buscar: searchQuery(filters.value.buscar) ?? undefined, page: page.value, pageSize: pageSize.value } })
    items.value = data.items
    total.value = data.total
    totalImporte.value = data.totalImporte
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, 'No se pudieron cargar los gastos.')
  } finally {
    loading.value = false
  }
}

function create() {
  editingId.value = null
  form.value = blank()
  error.value = ''
  show.value = true
}

function edit(x: any) {
  editingId.value = x.id
  form.value = { fecha: x.fecha, categoria: x.categoria, descripcion: x.descripcion, importe: x.importe, observaciones: x.observaciones ?? '' }
  error.value = ''
  show.value = true
}

async function save() {
  saving.value = true
  error.value = ''
  try {
    editingId.value ? await http.put(`/gastos/${editingId.value}`, form.value) : await http.post('/gastos', form.value)
    show.value = false
    await load()
  } catch (e: any) {
    error.value = apiErrorMessage(e, 'No se pudo guardar el gasto.')
  } finally {
    saving.value = false
  }
}

async function remove(x: any) {
  if (!await confirmAction({ title: 'Eliminar gasto', message: `¿Querés eliminar “${x.descripcion}” por ${money(x.importe)}?`, confirmText: 'Eliminar', danger: true })) return
  try {
    await http.delete(`/gastos/${x.id}`)
    await load()
  } catch (e: any) {
    notify(apiErrorMessage(e, 'No se pudo eliminar el gasto.'))
  }
}

function resetFilters() {
  filters.value = { ...monthRange(), buscar: '' }
  load(true)
}

function hasActiveFilters() {
  return !!searchQuery(filters.value.buscar)
}

function onPage(event: DataTablePageEvent) {
  page.value = event.page + 1
  pageSize.value = event.rows
  load()
}

onMounted(() => load())
useDebouncedSearch(computed({ get: () => filters.value.buscar, set: (v) => { filters.value.buscar = v } }), () => load(true))
useImmediateFilters([() => filters.value.desde, () => filters.value.hasta], () => load(true))
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Registro independiente de gastos generales.</p>
      <AppButton label="Nuevo gasto" icon="pi pi-plus" @click="create" />
    </div>

    <Panel class="filter-panel filter-panel-inline">
      <div class="grid formgrid p-fluid filter-form filter-form-inline">
        <div class="field col-12 xl:col-2">
          <label for="desde">Desde</label>
          <AppDatePicker id="desde" v-model="filters.desde" />
        </div>
        <div class="field col-12 xl:col-2">
          <label for="hasta">Hasta</label>
          <AppDatePicker id="hasta" v-model="filters.hasta" />
        </div>
        <div class="field col-12 xl:col-3">
          <label for="buscar">Buscar</label>
          <InputText id="buscar" v-model="filters.buscar" type="search" placeholder="Categoría, descripción u observación" />
          <small v-if="filters.buscar.trim() && !isSearchFilterActive(filters.buscar)" class="text-color-secondary">Escribí al menos 3 caracteres</small>
        </div>
        <div class="field col-12 xl:col-3 flex align-items-end">
          <p class="filter-period-hint m-0">
            Período: <strong>{{ filterPeriodLabel }}</strong>
          </p>
        </div>
        <div class="field col-12 xl:col-2 filter-actions flex align-items-end justify-content-end">
          <AppButton type="button" label="Restablecer" icon="pi pi-filter-slash" severity="secondary" @click="resetFilters" />
        </div>
      </div>
    </Panel>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load()" />
    </Message>

    <template v-else>
      <div class="summary-metrics">
        <article class="card metric">
          <small>Total del período</small>
          <strong>{{ money(totalImporte) }}</strong>
          <em>{{ pluralize(total, 'gasto registrado', 'gastos registrados') }}</em>
        </article>
      </div>

      <div class="table-panel">
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
              <p class="font-bold mb-2">{{ hasActiveFilters() ? 'Ningún resultado' : 'Todavía no hay gastos' }}</p>
              <p class="text-color-secondary mb-3">
                {{ hasActiveFilters() ? 'Probá ajustar fechas o la búsqueda.' : 'Registrá el primero para llevar el control del período.' }}
              </p>
              <AppButton v-if="!hasActiveFilters()" label="Nuevo gasto" icon="pi pi-plus" @click="create" />
            </div>
          </template>
          <Column header="Fecha">
            <template #body="{ data: x }">{{ new Date(`${x.fecha}T00:00:00`).toLocaleDateString('es-AR') }}</template>
          </Column>
          <Column header="Categoría">
            <template #body="{ data: x }"><Tag :value="x.categoria" /></template>
          </Column>
          <Column header="Descripción" body-class="cell-wrap">
            <template #body="{ data: x }"><b>{{ x.descripcion }}</b></template>
          </Column>
          <Column header="Observaciones" body-class="cell-wrap">
            <template #body="{ data: x }">{{ x.observaciones || '—' }}</template>
          </Column>
          <Column header="Importe" body-class="cell-num">
            <template #body="{ data: x }"><b>{{ money(x.importe) }}</b></template>
          </Column>
          <Column header="" body-class="cell-actions">
            <template #body="{ data: x }">
              <div class="flex gap-1">
                <AppButton text rounded severity="secondary" aria-label="Editar gasto" @click="edit(x)">
                  <AppIcon :icon="faPen" />
                </AppButton>
                <AppButton text rounded severity="danger" aria-label="Eliminar gasto" @click="remove(x)">
                  <AppIcon :icon="faTrash" />
                </AppButton>
              </div>
            </template>
          </Column>
        </DataTable>
      </div>
    </template>

    <Dialog
      v-model:visible="show"
      modal
      :header="`${editingId ? 'Editar' : 'Nuevo'} gasto`"
      :style="{ width: 'min(560px, 96vw)' }"
    >
      <form @submit.prevent="save">
        <p class="text-color-secondary mt-0">Este registro no modifica Caja, Rentabilidad ni otras vistas.</p>
        <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>

        <div class="grid formgrid p-fluid">
          <div class="field col-12 md:col-6">
            <label>Fecha</label>
            <AppDatePicker v-model="form.fecha" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>Categoría</label>
            <InputText v-model="form.categoria" maxlength="100" placeholder="Ej.: Servicios, insumos, alquiler" required />
          </div>
          <div class="field col-12">
            <label>Descripción</label>
            <InputText v-model="form.descripcion" maxlength="300" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>Importe</label>
            <InputNumber
              v-model="form.importe"
              :min="0.01"
              :min-fraction-digits="2"
              :max-fraction-digits="2"
              mode="currency"
              currency="ARS"
              locale="es-AR"
              required
            />
          </div>
          <div class="field col-12">
            <label>Observaciones</label>
            <Textarea v-model="form.observaciones" maxlength="1000" rows="3" auto-resize />
          </div>
        </div>

        <div class="flex justify-content-end gap-2 mt-4">
          <AppButton type="button" label="Cancelar" severity="secondary" @click="show = false" />
          <AppButton type="submit" :label="saving ? 'Guardando…' : 'Guardar gasto'" :loading="saving" />
        </div>
      </form>
    </Dialog>
  </section>
</template>
