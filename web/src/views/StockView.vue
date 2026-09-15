<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import { auth } from '@/auth'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { pluralize } from '@/shared/formatters'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS, tableFirst, type DataTablePageEvent } from '@/shared/tablePagination'

type StockProduct = { tipoCespedId: string; nombre: string; stockActualM2: number; controlPorLotes: boolean; colores: string[] }
type StockDeposito = { id: string; nombre: string; productos: StockProduct[]; permiteRegistrarIngreso: boolean }
type RolloForm = { posicion: 'A' | 'B' | 'C'; codigoBarra: string; cantidadM2: number }

const depositos = ref<StockDeposito[]>([])
const movimientos = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const showForm = ref(false)
const saving = ref(false)
const saveError = ref('')
const page = ref(1)
const pageSize = ref(TABLE_ROWS)
const total = ref(0)
const first = computed(() => tableFirst(page.value, pageSize.value))
const filters = ref({ depositoId: '', tipoCespedId: '', desde: '', hasta: '' })
const form = ref({ depositoId: '', tipoCespedId: '', color: '', cantidadM2: 0, observaciones: '', rollos: newRolls() })
const expandedLots = ref<Record<string, boolean>>({})
const lotsByProduct = ref<Record<string, any[]>>({})
const lotsLoading = ref<Record<string, boolean>>({})
const barcode = ref('')
const barcodeResult = ref<any>(null)
const barcodeError = ref('')
const barcodeLoading = ref(false)
const barcodeInputs = ref<Record<string, HTMLInputElement | null>>({})

const isAdmin = computed(() => auth.state.user?.rol === 'Administrador')
const depositosIngreso = computed(() => depositos.value.filter(x => x.permiteRegistrarIngreso))
const productosIngreso = computed(() => depositos.value.find(x => x.id === form.value.depositoId)?.productos ?? [])
const selectedProduct = computed(() => productosIngreso.value.find(x => x.tipoCespedId === form.value.tipoCespedId))
const depositoFilterOptions = computed(() => [{ id: '', nombre: 'Todos los depósitos' }, ...depositos.value])
const productoFilterOptions = computed(() => [{ tipoCespedId: '', nombre: 'Todos los productos' }, ...productosFiltro.value])
const productosFiltro = computed(() => {
  if (filters.value.depositoId) return depositos.value.find(x => x.id === filters.value.depositoId)?.productos ?? []
  const unique = new Map<string, StockProduct>()
  depositos.value.flatMap(x => x.productos).forEach(product => unique.set(product.tipoCespedId, product))
  return [...unique.values()].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'))
})
const totalStockM2 = computed(() =>
  depositos.value.reduce((sum, deposito) =>
    sum + deposito.productos.reduce((acc, producto) => acc + Number(producto.stockActualM2 ?? 0), 0), 0),
)

const number = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })

function newRolls(): RolloForm[] {
  return ['A', 'B', 'C'].map(posicion => ({ posicion: posicion as RolloForm['posicion'], codigoBarra: '', cantidadM2: 0 }))
}
function meters(value: number) { return `${number.format(Number(value ?? 0))} m²` }
function lotKey(depositoId: string, tipoCespedId: string) { return `${depositoId}:${tipoCespedId}` }
function setBarcodeInput(position: string, element: any) { barcodeInputs.value[position] = element as HTMLInputElement | null }
function focusNext(position: string) {
  const next = position === 'A' ? 'B' : position === 'B' ? 'C' : ''
  if (next) nextTick(() => barcodeInputs.value[next]?.focus())
}

async function loadStock() {
  depositos.value = (await http.get('/stock')).data
  if (!depositosIngreso.value.some(x => x.id === form.value.depositoId)) {
    form.value.depositoId = depositosIngreso.value[0]?.id ?? ''
  }
  ensureIngresoProduct()
}

function ensureIngresoProduct() {
  if (!productosIngreso.value.some(x => x.tipoCespedId === form.value.tipoCespedId)) {
    form.value.tipoCespedId = productosIngreso.value[0]?.tipoCespedId ?? ''
  }
  form.value.color = selectedProduct.value?.colores[0] ?? ''
  form.value.rollos = newRolls()
}

function ensureFilterProduct() {
  if (!productosFiltro.value.some(x => x.tipoCespedId === filters.value.tipoCespedId)) {
    filters.value.tipoCespedId = ''
  }
}

async function loadMovimientos(reset = false) {
  if (reset) page.value = 1
  const { data } = await http.get('/stock/movimientos', {
    params: {
      depositoId: filters.value.depositoId || undefined,
      tipoCespedId: filters.value.tipoCespedId || undefined,
      desde: filters.value.desde || undefined,
      hasta: filters.value.hasta || undefined,
      page: page.value,
      pageSize: pageSize.value,
    },
  })
  movimientos.value = data.items
  total.value = data.total
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    await Promise.all([loadStock(), loadMovimientos()])
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, 'No se pudo cargar el stock.')
  } finally {
    loading.value = false
  }
}

function cancelForm() {
  showForm.value = false
  saveError.value = ''
}

function openIngreso() {
  form.value = {
    depositoId: depositosIngreso.value[0]?.id ?? '',
    tipoCespedId: '',
    color: '',
    cantidadM2: 0,
    observaciones: '',
    rollos: newRolls(),
  }
  ensureIngresoProduct()
  saveError.value = ''
  showForm.value = true
}

async function saveIngreso() {
  saving.value = true
  saveError.value = ''
  try {
    const controlled = selectedProduct.value?.controlPorLotes
    await http.post(controlled ? '/stock/lotes' : '/stock/ingresos', controlled
      ? {
        depositoId: form.value.depositoId,
        tipoCespedId: form.value.tipoCespedId,
        color: form.value.color || null,
        rollos: form.value.rollos,
        observaciones: form.value.observaciones,
      }
      : {
        depositoId: form.value.depositoId,
        tipoCespedId: form.value.tipoCespedId,
        cantidadM2: form.value.cantidadM2,
        observaciones: form.value.observaciones,
      })
    cancelForm()
    expandedLots.value = {}
    lotsByProduct.value = {}
    await Promise.all([loadStock(), loadMovimientos(true)])
  } catch (e: any) {
    saveError.value = apiErrorMessage(e, 'No se pudo registrar el ingreso de stock.')
  } finally {
    saving.value = false
  }
}

async function toggleLots(depositoId: string, tipoCespedId: string) {
  const key = lotKey(depositoId, tipoCespedId)
  expandedLots.value[key] = !expandedLots.value[key]
  if (!expandedLots.value[key] || lotsByProduct.value[key]) return
  lotsLoading.value[key] = true
  try {
    lotsByProduct.value[key] = (await http.get('/stock/lotes', {
      params: { depositoId, tipoCespedId, estado: 'Disponible' },
    })).data
  } catch {
    lotsByProduct.value[key] = []
  } finally {
    lotsLoading.value[key] = false
  }
}

async function searchBarcode() {
  barcodeError.value = ''
  barcodeResult.value = null
  barcodeLoading.value = true
  try {
    barcodeResult.value = (await http.get('/stock/lotes/buscar-por-codigo', {
      params: { codigoBarra: barcode.value.trim() },
    })).data
  } catch (e: any) {
    barcodeError.value = apiErrorMessage(e, 'No se encontró el código ingresado.')
  } finally {
    barcodeLoading.value = false
  }
}

async function applyFilters() {
  loading.value = true
  loadError.value = ''
  try {
    await loadMovimientos(true)
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, 'No se pudieron cargar los movimientos.')
  } finally {
    loading.value = false
  }
}

async function clearFilters() {
  filters.value = { depositoId: '', tipoCespedId: '', desde: '', hasta: '' }
  await applyFilters()
}

function onPage(event: DataTablePageEvent) {
  page.value = event.page + 1
  pageSize.value = event.rows
  loadMovimientos()
}

function movementSeverity(tipo: string) {
  if (tipo === 'Ajuste') return 'warn'
  if (tipo === 'SalidaPorVenta') return 'danger'
  return 'secondary'
}

onMounted(load)
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-2">
      <p class="page-desc text-color-secondary m-0">
        {{ showForm
          ? 'Registrar ingreso de stock'
          : 'Disponibilidad de césped y trazabilidad de los depósitos habilitados.' }}
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
        v-else-if="auth.can('stock', 'crear')"
        label="Registrar ingreso"
        icon="pi pi-plus"
        size="small"
        :disabled="!depositosIngreso.length"
        @click="openIngreso"
      />
    </div>

    <Message v-if="loadError && !showForm" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

    <article v-else-if="showForm" class="card inline-form-panel inline-form-panel-wide">
      <form @submit.prevent="saveIngreso">
        <Message v-if="saveError" severity="error" class="inline-form-error" :closable="false">{{ saveError }}</Message>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>1. Origen del ingreso</span>
          </header>
          <div class="grid formgrid p-fluid inline-form-grid">
            <div class="field col-12 md:col-6">
              <label>Depósito</label>
              <Select
                v-model="form.depositoId"
                :options="depositosIngreso"
                option-label="nombre"
                option-value="id"
                :disabled="!isAdmin"
                required
                size="small"
                @change="ensureIngresoProduct"
              />
              <small v-if="!isAdmin" class="text-color-secondary">Sólo podés ingresar stock en el depósito de tu sucursal.</small>
            </div>
            <div class="field col-12 md:col-6">
              <label>Producto</label>
              <Select
                v-model="form.tipoCespedId"
                :options="productosIngreso"
                option-label="nombre"
                option-value="tipoCespedId"
                placeholder="Seleccionar producto"
                required
                size="small"
                @change="ensureIngresoProduct"
              />
            </div>
          </div>
        </section>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>2. Detalle del ingreso</span>
          </header>
          <template v-if="selectedProduct?.controlPorLotes">
            <Message severity="info" class="mb-3" :closable="false">
              Este producto se controla por lotes: registrá exactamente los rollos A, B y C.
            </Message>
            <div v-if="selectedProduct.colores.length" class="field col-12 md:col-4">
              <label>Color</label>
              <Select v-model="form.color" :options="selectedProduct.colores" required size="small" />
            </div>
            <div class="lot-roll-grid">
              <fieldset v-for="rollo in form.rollos" :key="rollo.posicion">
                <legend>Rollo {{ rollo.posicion }}</legend>
                <div class="field">
                  <label>Código de barra</label>
                  <InputText
                    :ref="element => setBarcodeInput(rollo.posicion, element)"
                    v-model.trim="rollo.codigoBarra"
                    maxlength="150"
                    required
                    autocomplete="off"
                    size="small"
                    @keydown.enter.prevent="focusNext(rollo.posicion)"
                    @blur="rollo.codigoBarra && focusNext(rollo.posicion)"
                  />
                </div>
                <div class="field">
                  <label>Cantidad (m²)</label>
                  <InputNumber v-model="rollo.cantidadM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" required />
                </div>
              </fieldset>
            </div>
            <strong class="lot-total">Total del lote: {{ meters(form.rollos.reduce((sum, rollo) => sum + Number(rollo.cantidadM2 || 0), 0)) }}</strong>
          </template>
          <div v-else class="field col-12 md:col-4">
            <label>Cantidad (m²)</label>
            <InputNumber v-model="form.cantidadM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" required />
          </div>
          <div class="field col-12">
            <label>Observaciones</label>
            <Textarea v-model="form.observaciones" maxlength="500" rows="3" placeholder="Ej.: recepción de rollos del proveedor" auto-resize />
          </div>
        </section>

        <div class="inline-form-footer">
          <small class="inline-form-note text-color-secondary">El ingreso impacta stock y movimientos del depósito seleccionado.</small>
          <div class="flex gap-2">
            <AppButton type="button" label="Cancelar" severity="secondary" size="small" @click="cancelForm" />
            <AppButton type="submit" label="Registrar ingreso" :loading="saving" size="small" />
          </div>
        </div>
      </form>
    </article>

    <template v-else>
      <div v-if="loading && !depositos.length" class="text-center py-5 text-color-secondary">Cargando stock…</div>

      <div v-else-if="!depositos.length" class="text-center py-5 text-color-secondary">
        No hay depósitos habilitados para tu sucursal.
      </div>

      <template v-else>
        <div class="summary-metrics">
          <article class="card metric">
            <small>Stock total</small>
            <strong>{{ meters(totalStockM2) }}</strong>
            <em>{{ pluralize(depositos.length, 'depósito', 'depósitos') }} habilitados</em>
          </article>
        </div>

        <div class="stock-cards mb-3">
          <article v-for="deposito in depositos" :key="deposito.id" class="card stock-deposit-card">
            <h3 class="m-0 mb-2">{{ deposito.nombre }}</h3>
            <div class="stock-product-list">
              <div v-for="producto in deposito.productos" :key="producto.tipoCespedId" class="stock-product-row">
                <span>
                  {{ producto.nombre }}
                  <Tag v-if="producto.controlPorLotes" value="Por lotes" severity="info" class="ml-1" />
                </span>
                <b :class="{ negative: producto.stockActualM2 < 0 }">{{ meters(producto.stockActualM2) }}</b>
                <AppButton
                  v-if="producto.controlPorLotes"
                  class="stock-lot-link"
                  :label="expandedLots[lotKey(deposito.id, producto.tipoCespedId)] ? 'Ocultar lotes' : 'Ver lotes'"
                  text
                  size="small"
                  @click="toggleLots(deposito.id, producto.tipoCespedId)"
                />
                <div v-if="expandedLots[lotKey(deposito.id, producto.tipoCespedId)]" class="stock-lots">
                  <small v-if="lotsLoading[lotKey(deposito.id, producto.tipoCespedId)]">Cargando lotes…</small>
                  <div v-for="lote in lotsByProduct[lotKey(deposito.id, producto.tipoCespedId)]" :key="lote.id" class="stock-lot">
                    <b>{{ lote.color || 'Sin color' }} · {{ meters(lote.cantidadM2) }}</b>
                    <span v-for="rollo in lote.rollos" :key="rollo.id">{{ rollo.posicion }}: {{ rollo.codigoBarra }} ({{ meters(rollo.cantidadM2) }})</span>
                  </div>
                  <small v-if="!lotsLoading[lotKey(deposito.id, producto.tipoCespedId)] && !lotsByProduct[lotKey(deposito.id, producto.tipoCespedId)]?.length">
                    No hay lotes disponibles.
                  </small>
                </div>
              </div>
            </div>
          </article>
        </div>

        <Panel class="filter-panel mb-3">
          <div class="grid formgrid p-fluid filter-form">
            <div class="field col-12 md:col-8">
              <label for="barcode">Buscar trazabilidad por código de barra</label>
              <InputText id="barcode" v-model.trim="barcode" maxlength="150" placeholder="Escaneá o escribí el código" />
            </div>
            <div class="field col-12 md:col-4 flex align-items-end">
              <AppButton label="Buscar" :loading="barcodeLoading" class="w-full" @click="searchBarcode" />
            </div>
          </div>
        </Panel>

        <Message v-if="barcodeError" severity="error" class="mb-3" :closable="false">{{ barcodeError }}</Message>

        <Panel v-if="barcodeResult" class="mb-3">
          <template #header>
            <div class="flex justify-content-between align-items-center w-full gap-2 flex-wrap">
              <div>
                <span class="font-bold">Rollo {{ barcodeResult.rollo.codigoBarra }}</span>
                <small class="block text-color-secondary">{{ barcodeResult.lote.tipoCespedNombre }} · {{ barcodeResult.lote.depositoNombre }}</small>
              </div>
              <Tag
                :value="barcodeResult.lote.estado"
                :severity="barcodeResult.lote.estado === 'Disponible' ? 'success' : barcodeResult.lote.estado === 'Vendido' ? 'warn' : 'secondary'"
              />
            </div>
          </template>
          <div class="barcode-detail">
            <p class="mt-0"><b>Color:</b> {{ barcodeResult.lote.color || 'Sin color' }}</p>
            <p><b>Posición encontrada:</b> {{ barcodeResult.rollo.posicion }} · {{ meters(barcodeResult.rollo.cantidadM2) }}</p>
            <p><b>Lote completo:</b></p>
            <div class="roll-code-list">
              <span v-for="rollo in barcodeResult.lote.rollos" :key="rollo.id">
                {{ rollo.posicion }} · {{ rollo.codigoBarra }} · {{ meters(rollo.cantidadM2) }}
              </span>
            </div>
            <p v-if="barcodeResult.venta"><b>Venta:</b> {{ barcodeResult.venta.cliente }} · {{ barcodeResult.venta.fechaVenta }}</p>
          </div>
        </Panel>

        <Panel class="filter-panel mb-3">
          <form class="filter-form stock-movement-filters" @submit.prevent="applyFilters">
            <div class="stock-movement-filters-grid">
              <div class="field">
                <label for="stock-filter-deposito">Depósito</label>
                <Select
                  id="stock-filter-deposito"
                  v-model="filters.depositoId"
                  :options="depositoFilterOptions"
                  option-label="nombre"
                  option-value="id"
                  size="small"
                  @change="ensureFilterProduct"
                />
              </div>
              <div class="field">
                <label for="stock-filter-producto">Producto</label>
                <Select
                  id="stock-filter-producto"
                  v-model="filters.tipoCespedId"
                  :options="productoFilterOptions"
                  option-label="nombre"
                  option-value="tipoCespedId"
                  size="small"
                />
              </div>
              <div class="field">
                <label for="stock-filter-desde">Desde</label>
                <AppDatePicker id="stock-filter-desde" v-model="filters.desde" size="small" />
              </div>
              <div class="field">
                <label for="stock-filter-hasta">Hasta</label>
                <AppDatePicker id="stock-filter-hasta" v-model="filters.hasta" size="small" />
              </div>
              <div class="field stock-filter-actions">
                <label class="stock-filter-actions-label" aria-hidden="true">&nbsp;</label>
                <div class="flex gap-1 flex-nowrap">
                  <AppButton type="submit" icon="pi pi-check" aria-label="Aplicar filtros" size="small" />
                  <AppButton type="button" icon="pi pi-filter-slash" severity="secondary" aria-label="Restablecer filtros" size="small" @click.prevent="clearFilters" />
                </div>
              </div>
            </div>
          </form>
        </Panel>

        <div class="table-panel">
          <p class="page-desc text-color-secondary m-0 mb-2 px-1">
            {{ pluralize(total, 'movimiento registrado', 'movimientos registrados') }}
          </p>
          <DataTable
            :value="movimientos"
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
              <div class="text-center py-5 text-color-secondary">No hay movimientos para los filtros seleccionados.</div>
            </template>
            <Column header="Fecha">
              <template #body="{ data }">{{ new Date(data.fecha).toLocaleString('es-AR') }}</template>
            </Column>
            <Column header="Depósito">
              <template #body="{ data }"><b>{{ data.depositoNombre }}</b></template>
            </Column>
            <Column field="tipoCespedNombre" header="Producto" />
            <Column header="Tipo">
              <template #body="{ data }">
                <Tag :value="data.tipo" :severity="movementSeverity(data.tipo)" />
              </template>
            </Column>
            <Column field="usuario" header="Usuario" />
            <Column header="Observaciones" body-class="cell-wrap">
              <template #body="{ data }">{{ data.observaciones || '—' }}</template>
            </Column>
            <Column header="Cantidad" body-class="cell-num">
              <template #body="{ data }">
                <b :class="{ negative: data.cantidadConSigno < 0 }">
                  {{ data.cantidadConSigno > 0 ? '+ ' : '− ' }}{{ meters(Math.abs(data.cantidadConSigno)) }}
                </b>
              </template>
            </Column>
          </DataTable>
        </div>
      </template>
    </template>
  </section>
</template>
