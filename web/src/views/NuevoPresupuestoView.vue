<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/shared/components/AppButton.vue'
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
import NuevoClienteModal from '@/shared/components/NuevoClienteModal.vue'
import { auth } from '@/auth'

const route = useRoute()
const router = useRouter()
const id = computed(() => String(route.params.id ?? ''))
const clientes = ref<any[]>([])
const productos = ref<any[]>([])
const saving = ref(false)
const error = ref('')
const showNewClient = ref(false)

const today = new Date()
function addDay(value: string) {
  const date = new Date(`${value}T00:00:00`)
  date.setDate(date.getDate() + 1)
  return date.toISOString().slice(0, 10)
}
const blankLine = () => ({ tipoCespedId: '', color: '', cantidadM2: 1, precioContadoM2: 0, precioFinanciadoM2: 0 })
const initialDate = today.toISOString().slice(0, 10)
const form = ref({
  clienteId: '', fecha: initialDate, validezHasta: addDay(initialDate),
  descuentoContadoPorcentaje: 0, ivaContadoPorcentaje: 10.5, ivaFinanciadoPorcentaje: 21,
  entregaFinanciada: 0, observaciones: '', lineas: [blankLine()],
})

watch(() => form.value.fecha, (fecha) => { form.value.validezHasta = addDay(fecha) })

const subtotalContado = computed(() => form.value.lineas.reduce((s, l) => s + Number(l.cantidadM2 || 0) * Number(l.precioContadoM2 || 0), 0))
const subtotalFinanciado = computed(() => form.value.lineas.reduce((s, l) => s + Number(l.cantidadM2 || 0) * Number(l.precioFinanciadoM2 || 0), 0))
const contado = computed(() => subtotalContado.value * (1 - Number(form.value.descuentoContadoPorcentaje) / 100) * (1 + Number(form.value.ivaContadoPorcentaje) / 100))
const financiado = computed(() => subtotalFinanciado.value * (1 + Number(form.value.ivaFinanciadoPorcentaje) / 100))
const entregaConIva = computed(() => Number(form.value.entregaFinanciada) * 1.21)
const saldoFinanciado = computed(() => financiado.value - entregaConIva.value)
const usd = (v: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'USD' }).format(v)

function product(l: any) { return productos.value.find(x => x.id === l.tipoCespedId) }
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
  const f = (await http.get('/presupuestos/filtros')).data
  clientes.value = f.clientes
  productos.value = f.productos
  if (id.value) {
    const p = (await http.get(`/presupuestos/${id.value}`)).data
    form.value = {
      clienteId: p.clienteId, fecha: p.fecha, validezHasta: addDay(p.fecha),
      descuentoContadoPorcentaje: p.descuentoContadoPorcentaje, ivaContadoPorcentaje: p.ivaContadoPorcentaje,
      ivaFinanciadoPorcentaje: p.ivaFinanciadoPorcentaje, entregaFinanciada: p.entregaFinanciada,
      observaciones: p.observaciones ?? '',
      lineas: p.lineas.map((l: any) => ({
        tipoCespedId: l.tipoCespedId, color: l.color ?? '', cantidadM2: l.cantidadM2,
        precioContadoM2: l.precioContadoM2, precioFinanciadoM2: l.precioFinanciadoM2,
      })),
    }
  }
}
async function save() {
  error.value = ''
  saving.value = true
  try {
    if (!form.value.lineas.every(l => l.tipoCespedId && l.cantidadM2 > 0 && l.precioContadoM2 > 0 && l.precioFinanciadoM2 > 0)) throw new Error('lines')
    if (entregaConIva.value > financiado.value) throw new Error('deposit')
    id.value ? await http.put(`/presupuestos/${id.value}`, form.value) : await http.post('/presupuestos', form.value)
    await router.push('/presupuestos')
  } catch (e: any) {
    const details = Object.values(e.response?.data?.errors ?? {}).flat().map(String)
    error.value = e.message === 'lines' ? 'Completá producto, metros y ambos precios en todas las líneas.'
      : e.message === 'deposit' ? 'La entrega más IVA 21% no puede superar el total financiado.'
      : details[0] ?? apiErrorMessage(e, 'Revisá los datos ingresados.')
  } finally {
    saving.value = false
  }
}
onMounted(load)
</script>

<template>
  <section class="page quote-create-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Completá pocos datos y generá una propuesta profesional en USD.</p>
      <RouterLink to="/presupuestos">
        <AppButton label="Volver" severity="secondary" />
      </RouterLink>
    </div>

    <form @submit.prevent="save">
      <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>

      <Panel class="sale-step mb-3">
        <template #header><span class="font-bold">1. Cliente y vigencia</span></template>
        <div class="grid formgrid p-fluid sale-step-body quote-header">
          <div class="field col-12 md:col-6">
            <label>Cliente</label>
            <div class="sale-client-picker flex gap-2">
              <ClienteAutocomplete v-model="form.clienteId" :clientes="clientes" class="flex-1" />
              <AppButton
                v-if="auth.can('clientes')"
                type="button"
                severity="secondary"
                title="Agregar nuevo cliente"
                aria-label="Agregar nuevo cliente"
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
                :options="productos"
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
                <label>Entrega sin IVA (USD)</label>
                <InputNumber v-model="form.entregaFinanciada" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" />
                <small class="text-color-secondary">En el PDF figurará este monto + IVA 21%.</small>
              </div>
            </div>
            <strong class="text-xl">{{ usd(financiado) }}</strong>
            <small class="text-color-secondary block">Entrega con IVA 21%: {{ usd(entregaConIva) }} · Saldo a financiar: {{ usd(saldoFinanciado) }}</small>
          </article>
        </div>
        <div class="field quote-notes mt-3">
          <label for="observaciones">Observaciones y condiciones</label>
          <Textarea id="observaciones" v-model="form.observaciones" rows="4" placeholder="Flete, instalación, garantía u otras aclaraciones" auto-resize />
        </div>
      </Panel>

      <div class="flex justify-content-end gap-2">
        <RouterLink to="/presupuestos">
          <AppButton type="button" label="Cancelar" severity="secondary" />
        </RouterLink>
        <AppButton type="submit" :label="saving ? 'Guardando…' : 'Guardar presupuesto'" :loading="saving" />
      </div>
    </form>

    <NuevoClienteModal v-if="showNewClient" @close="showNewClient = false" @created="clientCreated" />
  </section>
</template>
