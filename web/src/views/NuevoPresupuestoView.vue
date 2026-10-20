<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/shared/components/AppButton.vue'
import PresupuestoPdfPreview from '@/shared/components/PresupuestoPdfPreview.vue'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faPlus, faTrash } from '@/shared/icons'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import ClienteAutocomplete from '@/shared/components/ClienteAutocomplete.vue'
import NuevoClienteForm from '@/shared/components/NuevoClienteForm.vue'
import { auth } from '@/auth'
import { branchContext } from '@/branchContext'
import { notify } from '@/shared/uiFeedback'
import { isDownPaymentSuggestionEnabled, resolveSuggestedDownPayment } from '@/presupuestoEntrega'

const route = useRoute()
const router = useRouter()
const id = computed(() => String(route.params.id ?? ''))
const clientes = ref<any[]>([])
const productos = ref<any[]>([])
const depositos = ref<any[]>([])
const saving = ref(false)
const error = ref('')
const loading = ref(false)
const loadError = ref('')
const showNewClient = ref(false)
const previewUrl = ref('')
const previewTitle = ref('')
const previewLoading = ref(false)
const showPreview = ref(false)
const previewPanelRef = ref<HTMLElement | null>(null)
const presupuestoNumero = ref<number | null>(null)
const CASH_PAYMENT_TEXT = 'Pago de contado en dólar billete o pesos en efectivo a cotización del dólar blue.'
const suggestedDownPayment = ref(0)
const downPaymentManuallyModified = ref(false)
const activeBranchId = computed(() => branchContext.state.active?.id ?? auth.state.user?.sucursalId ?? null)
const showDownPaymentSuggestion = computed(() => isDownPaymentSuggestionEnabled(activeBranchId.value))
let suggestionTimer: ReturnType<typeof setTimeout> | undefined
let suggestionRequest = 0

const today = new Date()
function addDay(value: string) {
  const date = new Date(`${value}T00:00:00`)
  date.setDate(date.getDate() + 1)
  return date.toISOString().slice(0, 10)
}
const blankLine = () => ({ tipoCespedId: '', color: '', cantidadM2: 1, precioContadoM2: 0, precioFinanciadoM2: 0 })
const initialDate = today.toISOString().slice(0, 10)
const form = ref({
  clienteId: '', depositoId: '', fecha: initialDate, validezHasta: addDay(initialDate),
  descuentoContadoPorcentaje: 0, ivaContadoPorcentaje: 10.5, ivaFinanciadoPorcentaje: 21,
  entregaFinanciada: 0, observaciones: '', lineas: [blankLine()],
})

watch(() => form.value.fecha, (fecha) => { form.value.validezHasta = addDay(fecha) })
watch(() => form.value.depositoId, () => {
  for (const line of form.value.lineas) {
    if (line.tipoCespedId && !productosDisponibles.value.some((x: any) => x.id === line.tipoCespedId))
      Object.assign(line, blankLine())
  }
})

const subtotalContado = computed(() => form.value.lineas.reduce((s, l) => s + Number(l.cantidadM2 || 0) * Number(l.precioContadoM2 || 0), 0))
const subtotalFinanciado = computed(() => form.value.lineas.reduce((s, l) => s + Number(l.cantidadM2 || 0) * Number(l.precioFinanciadoM2 || 0), 0))
const contado = computed(() => subtotalContado.value * (1 - Number(form.value.descuentoContadoPorcentaje) / 100))
const financiado = computed(() => subtotalFinanciado.value)
const entregaInicial = computed(() => Number(form.value.entregaFinanciada))
const saldoFinanciado = computed(() => financiado.value - entregaInicial.value)
const usd = (v: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'USD' }).format(v)
const productosDisponibles = computed(() => productos.value.filter((x: any) =>
  (x.depositoIds?.length ? x.depositoIds : [x.depositoId]).includes(form.value.depositoId)))

function suggestionPayload() {
  const lineas = form.value.lineas
    .filter(l => l.tipoCespedId && Number(l.cantidadM2) > 0)
    .map(l => ({ tipoCespedId: l.tipoCespedId, cantidadM2: Number(l.cantidadM2) }))
  return lineas.length === form.value.lineas.length ? { lineas } : null
}

async function requestSuggestedDownPayment(requestId: number) {
  const payload = suggestionPayload()
  if (!payload || !showDownPaymentSuggestion.value) return
  try {
    const response = await http.post('/presupuestos/entrega-sugerida', payload)
    if (requestId !== suggestionRequest || !showDownPaymentSuggestion.value) return
    const decision = resolveSuggestedDownPayment(
      Number(form.value.entregaFinanciada),
      Number(response.data.entregaSugerida),
      financiado.value,
      downPaymentManuallyModified.value,
    )
    suggestedDownPayment.value = decision.suggested
    if (decision.apply) form.value.entregaFinanciada = decision.value
  } catch {
    // La sugerencia es una ayuda no bloqueante: se conserva el valor actual.
  }
}

function scheduleSuggestedDownPayment() {
  const requestId = ++suggestionRequest
  if (suggestionTimer) clearTimeout(suggestionTimer)
  if (!showDownPaymentSuggestion.value) {
    suggestedDownPayment.value = 0
    return
  }
  suggestionTimer = setTimeout(() => requestSuggestedDownPayment(requestId), 300)
}

function markDownPaymentAsManual() {
  downPaymentManuallyModified.value = true
}

function resetSuggestedDownPayment() {
  downPaymentManuallyModified.value = false
  if (suggestionTimer) clearTimeout(suggestionTimer)
  requestSuggestedDownPayment(++suggestionRequest)
}

watch(
  () => form.value.lineas.map(l => `${l.tipoCespedId}:${l.cantidadM2}:${l.precioFinanciadoM2}`).join('|'),
  scheduleSuggestedDownPayment,
)
watch(activeBranchId, scheduleSuggestedDownPayment)

function product(l: any) { return productosDisponibles.value.find(x => x.id === l.tipoCespedId) }
function selectProduct(l: any) {
  const p = product(l)
  if (p) { l.precioContadoM2 = p.precioContadoM2; l.precioFinanciadoM2 = p.precioFinanciadoM2; l.color = '' }
}
function colors(l: any) {
  const raw = product(l)?.coloresJson
  try { return JSON.parse(raw || '[]') } catch { return [] }
}
function addLine() { form.value.lineas.push(blankLine()) }
function removeLine(i: number) { if (form.value.lineas.length > 1) form.value.lineas.splice(i, 1) }
function clientCreated(cliente: any) { clientes.value.push(cliente); form.value.clienteId = cliente.id; showNewClient.value = false }

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const f = (await http.get('/presupuestos/filtros')).data
    clientes.value = f.clientes
    productos.value = f.productos
    depositos.value = f.depositos ?? []
    form.value.depositoId = depositos.value[0]?.id ?? ''
    if (id.value) {
      const p = (await http.get(`/presupuestos/${id.value}`)).data
      presupuestoNumero.value = p.numero
      downPaymentManuallyModified.value = true
      form.value = {
        clienteId: p.clienteId, depositoId: p.depositoId, fecha: p.fecha, validezHasta: addDay(p.fecha),
        descuentoContadoPorcentaje: p.descuentoContadoPorcentaje, ivaContadoPorcentaje: p.ivaContadoPorcentaje,
        ivaFinanciadoPorcentaje: p.ivaFinanciadoPorcentaje, entregaFinanciada: p.entregaFinanciada,
        observaciones: p.observaciones ?? '',
        lineas: p.lineas.map((l: any) => ({
          tipoCespedId: l.tipoCespedId, color: l.color ?? '', cantidadM2: l.cantidadM2,
          precioContadoM2: l.precioContadoM2, precioFinanciadoM2: l.precioFinanciadoM2,
        })),
      }
    }
  } catch (e) {
    loadError.value = apiErrorMessage(e, 'No se pudieron cargar los datos necesarios para el presupuesto.')
  } finally {
    loading.value = false
  }
}
function closePreview() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  previewTitle.value = ''
  previewLoading.value = false
  showPreview.value = false
}

async function previewPdf() {
  if (!id.value) return

  if (showPreview.value && !previewLoading.value) {
    closePreview()
    return
  }

  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  previewTitle.value = `Presupuesto #${String(presupuestoNumero.value ?? 0).padStart(5, '0')}`
  previewLoading.value = true
  showPreview.value = true

  await nextTick()
  previewPanelRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })

  try {
    const r = await http.get(`/presupuestos/${id.value}/pdf`, { responseType: 'blob' })
    previewUrl.value = URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }))
  } catch (e) {
    closePreview()
    notify(apiErrorMessage(e, 'No se pudo abrir la vista previa.'))
  } finally {
    previewLoading.value = false
  }
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (!form.value.lineas.every(l => l.tipoCespedId && l.cantidadM2 > 0 && l.precioContadoM2 > 0 && l.precioFinanciadoM2 > 0)) throw new Error('lines')
    if (entregaInicial.value > financiado.value) throw new Error('deposit')
    id.value ? await http.put(`/presupuestos/${id.value}`, form.value) : await http.post('/presupuestos', form.value)
    await router.push('/presupuestos')
  } catch (e: any) {
    const details = Object.values(e.response?.data?.errors ?? {}).flat().map(String)
    error.value = e.message === 'lines' ? 'Completá producto, metros y ambos precios en todas las líneas.'
      : e.message === 'deposit' ? 'La entrega inicial no puede superar el total financiado.'
      : details[0] ?? apiErrorMessage(e, 'Revisá los datos ingresados.')
  } finally {
    saving.value = false
  }
}
onMounted(load)
onBeforeUnmount(() => {
  closePreview()
  if (suggestionTimer) clearTimeout(suggestionTimer)
  suggestionRequest++
})
</script>

<template>
  <section class="page quote-create-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Completá pocos datos y generá una propuesta profesional en USD.</p>
      <RouterLink to="/presupuestos">
        <AppButton label="Volver" severity="secondary" />
      </RouterLink>
    </div>

    <Message v-if="loading" severity="info" class="mb-3" :closable="false">Cargando datos del presupuesto…</Message>
    <Message v-else-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

    <form v-else @submit.prevent="save">
      <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>

      <div class="field mb-3" style="max-width: 28rem">
        <label for="depositoPresupuesto">Depósito del presupuesto</label>
        <Select id="depositoPresupuesto" v-model="form.depositoId" :options="depositos" option-label="nombre"
          option-value="id" placeholder="Seleccionar depósito" class="w-full" required />
      </div>

      <Panel class="sale-step mb-3">
        <template #header><span class="font-bold">1. Cliente y vigencia</span></template>
        <article v-if="showNewClient" class="card inline-form-panel inline-form-panel-wide">
          <NuevoClienteForm
            hint="Se guardará y quedará seleccionado en este presupuesto."
            @cancel="showNewClient = false"
            @created="clientCreated"
          />
        </article>
        <div v-else class="grid formgrid p-fluid sale-step-body quote-header">
          <div class="field col-12 md:col-6">
            <label>Cliente</label>
            <div class="sale-client-picker flex gap-2">
              <ClienteAutocomplete v-model="form.clienteId" :clientes="clientes" class="flex-1" />
              <AppButton
                v-if="auth.can('clientes')"
                type="button"
                severity="secondary"
                size="small"
                tooltip="Nuevo cliente"
                aria-label="Nuevo cliente"
                @click="showNewClient = true"
              >
                <AppIcon :icon="faPlus" />
              </AppButton>
            </div>
          </div>
          <div class="field col-12 md:col-3">
            <label for="fecha">Fecha</label>
            <AppDatePicker id="fecha" v-model="form.fecha" required />
          </div>
          <div class="field col-12 md:col-3">
            <label>Validez</label>
            <div class="quote-validity-note text-color-secondary">24 horas desde la emisión</div>
          </div>
        </div>
      </Panel>

      <Panel class="sale-step mb-3">
        <template #header>
          <div class="flex justify-content-between align-items-center flex-wrap gap-2 w-full">
            <div>
              <span class="font-bold">2. Productos</span>
              <small class="block text-color-secondary">Los precios contado y financiado se cargan desde Productos y siguen siendo editables.</small>
            </div>
            <AppButton type="button" label="Agregar producto" severity="secondary" size="small" @click="addLine">
              <AppIcon :icon="faPlus" class="mr-1" />
            </AppButton>
          </div>
        </template>
        <div class="quote-lines">
          <div v-for="(l, i) in form.lineas" :key="i" class="quote-line grid formgrid p-fluid align-items-end mb-3">
            <div class="field col-12 md:col-3">
              <label>Producto</label>
              <Select
                v-model="l.tipoCespedId"
                :options="productosDisponibles"
                option-label="nombre"
                option-value="id"
                placeholder="Seleccionar producto"
                required
                @change="selectProduct(l)"
              />
            </div>
            <div class="field col-12 md:col-2">
              <label>Color</label>
              <Select
                v-if="colors(l).length"
                v-model="l.color"
                :options="[{ label: 'Sin especificar', value: '' }, ...colors(l).map((c: string) => ({ label: c, value: c }))]"
                option-label="label"
                option-value="value"
              />
              <InputText v-else model-value="Sin variantes" disabled />
            </div>
            <div class="field col-12 md:col-2">
              <label>Metros m²</label>
              <InputNumber v-model="l.cantidadM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" required />
            </div>
            <div class="field col-12 md:col-2">
              <label>Contado / m² (USD)</label>
              <InputNumber v-model="l.precioContadoM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" required />
            </div>
            <div class="field col-12 md:col-2">
              <label>Financiado / m² (USD)</label>
              <InputNumber v-model="l.precioFinanciadoM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" required />
            </div>
            <div class="field col-12 md:col-2 highlight-field">
              <label>Totales de línea</label>
              <strong class="block">Contado {{ usd(l.cantidadM2 * l.precioContadoM2) }}</strong>
              <small class="text-color-secondary">Financiado {{ usd(l.cantidadM2 * l.precioFinanciadoM2) }}</small>
            </div>
            <div class="col-12 md:col-1 flex justify-content-end">
              <AppButton type="button" text rounded severity="danger" title="Quitar producto" :disabled="form.lineas.length === 1" @click="removeLine(i)">
                <AppIcon :icon="faTrash" />
              </AppButton>
            </div>
          </div>
        </div>
        <div class="sale-products-total quote-subtotals flex flex-wrap gap-3 mt-3">
          <span>Subtotal contado <strong>{{ usd(subtotalContado) }}</strong></span>
          <span>Subtotal financiado <strong>{{ usd(subtotalFinanciado) }}</strong></span>
        </div>
      </Panel>

      <Panel class="sale-step mb-3">
        <template #header>
          <div>
            <span class="font-bold">3. Opciones para el cliente</span>
            <small class="block text-color-secondary">El PDF siempre incluirá contado y financiado.</small>
          </div>
        </template>
        <div class="quote-payment grid">
          <article class="quote-option col-12 md:col-6">
            <h4>Contado</h4>
            <p class="text-color-secondary">{{ CASH_PAYMENT_TEXT }}</p>
            <div class="grid formgrid p-fluid">
              <div class="field col-12 md:col-6">
                <label>Descuento (%)</label>
                <InputNumber v-model="form.descuentoContadoPorcentaje" :min="0" :max="100" :min-fraction-digits="2" :max-fraction-digits="2" />
              </div>
              <div class="field col-12 md:col-6">
                <label>IVA (%)</label>
                <InputNumber v-model="form.ivaContadoPorcentaje" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" />
              </div>
            </div>
            <strong class="text-xl">{{ usd(contado) }}</strong>
          </article>
          <article class="quote-option financed col-12 md:col-6">
            <h4>Financiado</h4>
            <div class="grid formgrid p-fluid">
              <div class="field col-12 md:col-6">
                <label>IVA (%)</label>
                <InputNumber v-model="form.ivaFinanciadoPorcentaje" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" />
              </div>
              <div class="field col-12 md:col-6">
                <label>Entrega inicial (USD)</label>
                <InputNumber v-model="form.entregaFinanciada" :min="0" :min-fraction-digits="2" :max-fraction-digits="2"
                  @update:model-value="markDownPaymentAsManual" />
                <small v-if="showDownPaymentSuggestion" class="text-color-secondary">El importe ingresado ya incluye IVA.</small>
                <small v-if="showDownPaymentSuggestion" class="text-color-secondary block">Cálculo automático: costo del producto × m² = {{ usd(suggestedDownPayment) }}</small>
                <div v-if="showDownPaymentSuggestion && downPaymentManuallyModified" class="flex align-items-center gap-2 mt-2">
                  <small class="text-color-secondary">Modificado manualmente</small>
                  <AppButton type="button" label="Restablecer cálculo" size="small" severity="secondary"
                    @click="resetSuggestedDownPayment" />
                </div>
              </div>
            </div>
            <strong class="text-xl">{{ usd(financiado) }}</strong>
            <small class="text-color-secondary block">Entrega inicial: {{ usd(entregaInicial) }} · Saldo a financiar: {{ usd(saldoFinanciado) }}</small>
          </article>
        </div>
        <div class="field quote-notes mt-3">
          <label for="observaciones">Observaciones y condiciones</label>
          <Textarea id="observaciones" v-model="form.observaciones" rows="4" placeholder="Flete, instalación, garantía u otras aclaraciones" auto-resize />
        </div>
      </Panel>

      <div class="flex justify-content-between align-items-center flex-wrap gap-2">
        <AppButton
          v-if="id"
          type="button"
          label="Vista previa PDF"
          icon="pi pi-eye"
          severity="secondary"
          :loading="previewLoading"
          @click="previewPdf"
        />
        <div v-else />
        <div class="flex gap-2">
          <RouterLink to="/presupuestos">
            <AppButton type="button" label="Cancelar" severity="secondary" />
          </RouterLink>
          <AppButton type="submit" :label="saving ? 'Guardando…' : 'Guardar presupuesto'" :loading="saving" />
        </div>
      </div>
    </form>

    <div v-if="showPreview" ref="previewPanelRef">
      <PresupuestoPdfPreview
        :title="previewTitle"
        :url="previewUrl"
        :loading="previewLoading"
        @close="closePreview"
      />
    </div>

  </section>
</template>
