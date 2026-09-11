<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/shared/components/AppButton.vue'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faCheck, faPen, faPlus, faTrash } from '@/shared/icons'
import { http } from '@/shared/api/httpClient'
import { formatCurrency as money } from '@/shared/formatters'
import ClienteAutocomplete from '@/shared/components/ClienteAutocomplete.vue'
import NuevoClienteModal from '@/shared/components/NuevoClienteModal.vue'
import { auth } from '@/auth'

const router = useRouter()
const route = useRoute()
const editingId = computed(() => typeof route.params.id === 'string' ? route.params.id : '')
const clientes = ref<any[]>([])
const maestros = ref<any>({ tiposCesped: [], alicuotasIva: [] })
const saving = ref(false)
const error = ref('')
const editingLine = ref<number | null>(null)
const showNewClient = ref(false)
const showProductEntry = ref(true)

const estados = ['Confirmada', 'Futura', 'Entregada', 'Cancelada']
const formasPago = ['Contado', 'Transferencia', 'Cheque', 'Cuotas', 'Otros']

const line = () => ({ tipoCespedId: '', color: '', cantidadM2: 1, precioCompraM2: 0, precioVentaM2: 0, total: 0 })
const draft = ref(line())
const form = ref({
  clienteId: '', fechaVenta: new Date().toISOString().slice(0, 10), lineas: [] as ReturnType<typeof line>[],
  montoEntrega: 0, formaPago: 'Contado', cantidadCuotas: null as number | null, estado: 'Confirmada',
  fechaEntregaEstimada: null as string | null, costoEnvio: 0, otrosCostos: 0, alicuotaIvaId: '', observaciones: '',
})

const totalProductos = computed(() => form.value.lineas.reduce((sum, x) => sum + Number(x.total || 0), 0))
const totalVenta = computed(() => totalProductos.value)

function product(row: any) { return maestros.value.tiposCesped.find((x: any) => x.id === row.tipoCespedId) }
function selectProduct(row: any) {
  const p = product(row)
  if (!p) return
  row.precioCompraM2 = p.costoM2
  row.precioVentaM2 = p.precioVentaM2
  row.color = p.colores?.length === 1 ? p.colores[0] : ''
  recalculate(row)
}
function recalculate(row: any) { row.total = Math.round(Number(row.cantidadM2 || 0) * Number(row.precioVentaM2 || 0) * 100) / 100 }
function saveLine() {
  if (!draft.value.tipoCespedId || draft.value.cantidadM2 <= 0 || draft.value.precioCompraM2 <= 0 || draft.value.precioVentaM2 <= 0 || draft.value.total <= 0) {
    error.value = 'Completá todos los datos del producto antes de agregarlo.'
    return
  }
  if (editingLine.value === null) form.value.lineas.push({ ...draft.value })
  else form.value.lineas[editingLine.value] = { ...draft.value }
  draft.value = line()
  editingLine.value = null
  showProductEntry.value = false
  error.value = ''
}
function startNewLine() { draft.value = line(); editingLine.value = null; showProductEntry.value = true; error.value = '' }
function cancelLine() { draft.value = line(); editingLine.value = null; showProductEntry.value = form.value.lineas.length === 0; error.value = '' }
function editLine(index: number) { draft.value = { ...form.value.lineas[index]! }; editingLine.value = index; showProductEntry.value = true; window.scrollTo({ top: 300, behavior: 'smooth' }) }
function removeLine(index: number) {
  form.value.lineas.splice(index, 1)
  if (editingLine.value === index) { draft.value = line(); editingLine.value = null }
  showProductEntry.value = form.value.lineas.length === 0
}
function productName(id: string) { return maestros.value.tiposCesped.find((x: any) => x.id === id)?.nombre ?? 'Producto' }
function clientCreated(cliente: any) { clientes.value.push(cliente); form.value.clienteId = cliente.id; showNewClient.value = false }

async function load() {
  const [filters, masters] = await Promise.all([http.get('/ventas/filtros'), http.get('/maestros')])
  clientes.value = filters.data.clientes
  maestros.value = masters.data
  form.value.alicuotaIvaId = masters.data.alicuotasIva?.[0]?.id ?? ''
  if (editingId.value) {
    const v = (await http.get(`/ventas/${editingId.value}`)).data
    form.value = {
      clienteId: v.clienteId, fechaVenta: v.fechaVenta,
      lineas: v.lineas?.length ? v.lineas : [{
        tipoCespedId: v.tipoCespedId, color: v.color ?? '', cantidadM2: v.cantidadM2,
        precioCompraM2: v.costoCompraUnitario, precioVentaM2: v.precioUnitario, total: v.precioTotal,
      }],
      montoEntrega: v.montoEntrega, formaPago: v.formaPago, cantidadCuotas: v.cantidadCuotas,
      estado: v.estado, fechaEntregaEstimada: v.fechaEntregaEstimada, costoEnvio: v.costoEnvio,
      otrosCostos: v.otrosCostos, alicuotaIvaId: v.alicuotaIvaId, observaciones: v.observaciones ?? '',
    }
    showProductEntry.value = false
  }
}
async function save() {
  error.value = ''
  if (!form.value.lineas.length) { error.value = 'Agregá al menos un producto a la venta.'; return }
  saving.value = true
  try {
    const first = form.value.lineas[0]!
    const payload = {
      ...form.value,
      tipoCespedId: first.tipoCespedId, color: first.color,
      cantidadM2: form.value.lineas.reduce((s, x) => s + Number(x.cantidadM2), 0),
      precioUnitario: totalProductos.value / form.value.lineas.reduce((s, x) => s + Number(x.cantidadM2), 0),
      precioTotal: totalProductos.value,
      costoCompraUnitario: form.value.lineas.reduce((s, x) => s + Number(x.precioCompraM2) * Number(x.cantidadM2), 0) / form.value.lineas.reduce((s, x) => s + Number(x.cantidadM2), 0),
    }
    editingId.value ? await http.put(`/ventas/${editingId.value}`, payload) : await http.post('/ventas', payload)
    await router.push('/ventas')
  } catch (e: any) {
    error.value = e.response?.data?.detail ?? Object.values(e.response?.data?.errors ?? {}).flat()[0] ?? 'Revisá los datos ingresados.'
  } finally {
    saving.value = false
  }
}
onMounted(load)
</script>

<template>
  <section class="page sale-create-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Seleccioná el cliente, agregá los productos y acordá el pago.</p>
      <RouterLink to="/ventas">
        <AppButton label="Volver a ventas" severity="secondary" />
      </RouterLink>
    </div>

    <form @submit.prevent="save">
      <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>

      <Panel class="sale-step mb-3">
        <template #header><span class="font-bold">1. Cliente</span></template>
        <div class="grid formgrid p-fluid sale-step-body">
          <div class="field col-12 md:col-8">
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
          <div class="field col-12 md:col-4">
            <label for="fechaVenta">Fecha de venta</label>
            <AppDatePicker id="fechaVenta" v-model="form.fechaVenta" required />
          </div>
        </div>
      </Panel>

      <Panel class="sale-step sale-detail-panel mb-3">
        <template #header>
          <div class="flex justify-content-between align-items-center flex-wrap gap-2 w-full">
            <div>
              <span class="font-bold">2. Detalle de productos</span>
              <small class="block text-color-secondary">
                {{ form.lineas.length ? 'Productos incluidos en esta venta.' : 'Cargá el producto de la venta.' }}
              </small>
            </div>
            <AppButton v-if="form.lineas.length && !showProductEntry" type="button" label="Agregar otro producto" severity="secondary" size="small" @click="startNewLine">
              <AppIcon :icon="faPlus" class="mr-1" />
            </AppButton>
          </div>
        </template>

        <div v-if="showProductEntry" class="sale-entry grid formgrid p-fluid mb-3">
          <div class="field col-12 md:col-4 sale-product-field">
            <label>Producto</label>
            <Select
              v-model="draft.tipoCespedId"
              :options="maestros.tiposCesped"
              option-label="nombre"
              option-value="id"
              placeholder="Seleccionar producto"
              @change="selectProduct(draft)"
            />
          </div>
          <div class="field col-12 md:col-2">
            <label>Color</label>
            <Select
              v-if="product(draft)?.colores?.length"
              v-model="draft.color"
              :options="product(draft).colores"
              placeholder="Seleccionar"
            />
            <InputText v-else model-value="Sin variantes" disabled />
          </div>
          <div class="field col-12 md:col-2">
            <label>Metros m²</label>
            <InputNumber v-model="draft.cantidadM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" @update:model-value="recalculate(draft)" />
          </div>
          <div class="field col-12 md:col-2">
            <label>Costo / m²</label>
            <InputNumber v-model="draft.precioCompraM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" />
          </div>
          <div class="field col-12 md:col-2">
            <label>Venta / m²</label>
            <InputNumber v-model="draft.precioVentaM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" @update:model-value="recalculate(draft)" />
          </div>
          <div class="field col-12 md:col-2 highlight-field">
            <label>Total</label>
            <InputNumber v-model="draft.total" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" />
            <small class="text-color-secondary">Editable · cálculo {{ money(Number(draft.cantidadM2) * Number(draft.precioVentaM2)) }}</small>
          </div>
          <div class="col-12 sale-line-actions flex justify-content-end gap-2">
            <AppButton v-if="form.lineas.length" type="button" label="Cancelar" severity="secondary" @click="cancelLine" />
            <AppButton
              type="button"
              :tooltip="editingLine !== null ? 'Actualizar producto' : 'Agregar producto'"
              @click="saveLine"
            >
              <AppIcon :icon="editingLine !== null ? faCheck : faPlus" class="mr-1" />
              {{ editingLine !== null ? 'Actualizar' : 'Agregar producto' }}
            </AppButton>
          </div>
        </div>

        <DataTable :value="form.lineas" striped-rows class="sale-detail-table">
          <template #empty>
            <div class="text-center py-4 text-color-secondary">Todavía no agregaste productos a la venta.</div>
          </template>
          <Column header="#" style="width: 3rem">
            <template #body="{ index }">{{ index + 1 }}</template>
          </Column>
          <Column header="Producto">
            <template #body="{ data: row }"><b>{{ productName(row.tipoCespedId) }}</b></template>
          </Column>
          <Column header="Color">
            <template #body="{ data: row }">{{ row.color || '—' }}</template>
          </Column>
          <Column header="Metros" body-class="text-right">
            <template #body="{ data: row }"><span class="num">{{ row.cantidadM2 }} m²</span></template>
          </Column>
          <Column header="Costo / m²" body-class="text-right">
            <template #body="{ data: row }"><span class="num">{{ money(row.precioCompraM2) }}</span></template>
          </Column>
          <Column header="Venta / m²" body-class="text-right">
            <template #body="{ data: row }"><span class="num">{{ money(row.precioVentaM2) }}</span></template>
          </Column>
          <Column header="Total" body-class="text-right">
            <template #body="{ data: row }"><b class="num">{{ money(row.total) }}</b></template>
          </Column>
          <Column header="" style="width: 7rem">
            <template #body="{ index }">
              <div class="flex gap-1">
                <AppButton text rounded severity="secondary" title="Editar línea" @click="editLine(index)">
                  <AppIcon :icon="faPen" />
                </AppButton>
                <AppButton text rounded severity="danger" title="Quitar línea" @click="removeLine(index)">
                  <AppIcon :icon="faTrash" />
                </AppButton>
              </div>
            </template>
          </Column>
        </DataTable>

        <div class="sale-products-total flex justify-content-between align-items-center mt-3">
          <span>Subtotal de productos</span>
          <strong>{{ money(totalProductos) }}</strong>
        </div>
      </Panel>

      <Panel class="sale-step mb-3">
        <template #header><span class="font-bold">3. Entrega y pago</span></template>
        <div class="grid formgrid p-fluid sale-step-body">
          <div class="field col-12 md:col-4">
            <label>Estado</label>
            <Select v-model="form.estado" :options="estados" />
          </div>
          <div v-if="form.estado === 'Futura'" class="field col-12 md:col-4">
            <label for="fechaEntrega">Entrega estimada</label>
            <AppDatePicker id="fechaEntrega" v-model="form.fechaEntregaEstimada" required />
          </div>
          <div class="field col-12 md:col-4">
            <label>Envío</label>
            <InputNumber v-model="form.costoEnvio" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" />
          </div>
          <div class="field col-12 md:col-4">
            <label>Otros costos</label>
            <InputNumber v-model="form.otrosCostos" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" />
          </div>
          <div class="field col-12 md:col-4">
            <label>IVA</label>
            <Select v-model="form.alicuotaIvaId" :options="maestros.alicuotasIva" option-label="nombre" option-value="id" required />
          </div>
          <div class="field col-12 md:col-4">
            <label>Forma de pago</label>
            <Select v-model="form.formaPago" :options="formasPago" />
          </div>
          <div v-if="form.formaPago === 'Cuotas'" class="field col-12 md:col-4">
            <label>Cuotas sobre el saldo</label>
            <InputNumber v-model="form.cantidadCuotas" :min="1" :max="60" required />
          </div>
          <div class="field col-12 md:col-4 highlight-field">
            <label>Entrega inicial</label>
            <InputNumber v-model="form.montoEntrega" :min="0.01" :max="totalProductos" :min-fraction-digits="2" :max-fraction-digits="2" required />
            <small class="text-color-secondary">Se registra como ingreso en Caja.</small>
          </div>
          <div class="field col-12">
            <label for="observaciones">Observaciones</label>
            <Textarea id="observaciones" v-model="form.observaciones" rows="3" auto-resize />
          </div>
        </div>
        <div class="sale-final-total mt-3">
          <span>Total final de la venta</span>
          <strong class="block text-2xl">{{ money(totalVenta) }}</strong>
          <small class="text-color-secondary">Los costos de envío y otros costos afectan rentabilidad, no el importe cobrado por productos.</small>
        </div>
      </Panel>

      <div class="flex justify-content-end gap-2">
        <RouterLink to="/ventas">
          <AppButton type="button" label="Cancelar" severity="secondary" />
        </RouterLink>
        <AppButton type="submit" :label="saving ? 'Guardando…' : editingId ? 'Guardar cambios' : 'Confirmar venta'" :loading="saving" />
      </div>
    </form>

    <NuevoClienteModal v-if="showNewClient" @close="showNewClient = false" @created="clientCreated" />
  </section>
</template>
