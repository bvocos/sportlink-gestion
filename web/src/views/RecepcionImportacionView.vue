<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/shared/components/AppButton.vue'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { notify } from '@/shared/uiFeedback'
import { addMissingColors, receptionLineErrors, receptionLineTotal, receptionTotal, type ReceptionRoll, type ReceptionVariant } from '@/presupuesto/recepcionImportacion'

type Product = { tipoCespedId: string; nombre: string; controlPorLotes: boolean; esImportado: boolean; colores: string[] }
type Deposit = { id: string; nombre: string; productos: Product[]; permiteRegistrarIngreso: boolean }
type Line = { tipoCespedId: string; cantidadM2: number; color: string; rollos: ReceptionRoll[]; variantes: ReceptionVariant[]; touched: boolean }

const route = useRoute(), router = useRouter()
const existingId = computed(() => route.params.id as string | undefined)
const deposits = ref<Deposit[]>([]), loading = ref(true), saving = ref(false), error = ref('')
const existing = ref<any>(null)
const attempted = ref(false)
const form = ref({ codigo: '', numeroDespacho: '', fecha: new Date().toISOString().slice(0, 10), depositoId: '', observaciones: '', lineas: [] as Line[] })
const selectedDeposit = computed(() => deposits.value.find(x => x.id === form.value.depositoId))
const products = computed(() => (selectedDeposit.value?.productos ?? []).filter(x => x.esImportado))
const total = computed(() => receptionTotal(form.value.lineas.map(line => ({ ...line, controlPorLotes: product(line)?.controlPorLotes ?? false }))))
const hasLocalErrors = computed(() => !form.value.depositoId || !form.value.codigo || form.value.lineas.length === 0 || form.value.lineas.some(line =>
  !line.tipoCespedId || receptionLineErrors({ ...line, controlPorLotes: product(line)?.controlPorLotes ?? false }).length > 0))
const number = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })

function rolls(): ReceptionRoll[] { return ['A', 'B', 'C'].map(posicion => ({ posicion: posicion as 'A' | 'B' | 'C', codigoBarra: '', cantidadM2: 0 })) }
function product(line: Line) { return products.value.find(x => x.tipoCespedId === line.tipoCespedId) }
function addLine(copy?: Line) { form.value.lineas.push({ tipoCespedId: copy?.tipoCespedId ?? '', cantidadM2: 0, color: '', rollos: rolls(), variantes: [], touched: false }) }
function productChanged(line: Line) { const value = product(line); line.color = value?.colores[0] ?? ''; line.cantidadM2 = 0; line.rollos = rolls(); line.variantes = value && !value.controlPorLotes && value.colores.length ? [{ color: value.colores[0], cantidadM2: 0 }] : []; line.touched = false }
function addVariant(line: Line) { line.variantes.push({ color: '', cantidadM2: 0 }) }
function addAllColors(line: Line) { line.variantes = addMissingColors(line.variantes, product(line)?.colores ?? []) }
function lineTotal(line: Line) { return receptionLineTotal({ ...line, controlPorLotes: product(line)?.controlPorLotes ?? false }) }
function depositChanged() { form.value.lineas = []; addLine() }

async function load() {
  loading.value = true
  try {
    deposits.value = ((await http.get('/stock')).data as Deposit[]).filter(x => x.permiteRegistrarIngreso)
    form.value.depositoId = deposits.value[0]?.id ?? ''
    if (existingId.value) {
      existing.value = (await http.get(`/importaciones/${existingId.value}`)).data
      Object.assign(form.value, { codigo: existing.value.codigo, numeroDespacho: existing.value.numeroDespacho ?? '', fecha: existing.value.fecha })
    }
    addLine()
  } catch (e) { error.value = apiErrorMessage(e, 'No se pudo preparar la recepción.') }
  finally { loading.value = false }
}

async function save() {
  attempted.value = true
  if (hasLocalErrors.value || saving.value) return
  saving.value = true; error.value = ''
  try {
    const payload = {
      importacionId: existingId.value ?? null,
      nueva: existingId.value ? null : { codigo: form.value.codigo, numeroDespacho: form.value.numeroDespacho || null, fecha: form.value.fecha, observaciones: null },
      depositoId: form.value.depositoId,
      observaciones: form.value.observaciones || null,
      lineas: form.value.lineas.map(line => ({ tipoCespedId: line.tipoCespedId,
        cantidadM2: product(line)?.controlPorLotes || (product(line)?.colores.length ?? 0) > 0 ? null : line.cantidadM2,
        color: product(line)?.controlPorLotes ? line.color || null : null,
        rollos: product(line)?.controlPorLotes ? line.rollos : null,
        variantes: !product(line)?.controlPorLotes && (product(line)?.colores.length ?? 0) > 0 ? line.variantes : null })),
    }
    const { data } = await http.post('/importaciones/recepcion', payload)
    notify(`Recepción ${data.codigo} registrada correctamente.`)
    await router.push({ path: '/importaciones', query: { detalle: data.importacionId } })
  } catch (e) { error.value = apiErrorMessage(e, 'No se pudo registrar la recepción.') }
  finally { saving.value = false }
}

watch(() => form.value.depositoId, () => { if (!loading.value) depositChanged() })
onMounted(load)
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center gap-2">
      <div><h2 class="m-0">{{ existingId ? 'Agregar mercadería' : 'Nueva recepción' }}</h2><p class="page-desc">Todos los ingresos se guardan juntos bajo el mismo código de importación.</p></div>
      <AppButton label="Volver" severity="secondary" @click="router.push('/importaciones')" />
    </div>
    <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>
    <Message v-if="loading" severity="info" :closable="false">Preparando recepción…</Message>
    <form v-else class="imp-form" @submit.prevent="save">
      <div class="imp-header-card">
      <div class="imp-header-grid">
        <div class="field"><label>Código de importación</label><InputText v-model="form.codigo" :disabled="!!existingId" maxlength="50" required /></div>
        <div class="field"><label>N° de despacho</label><InputText v-model="form.numeroDespacho" :disabled="!!existingId" maxlength="100" /></div>
        <div class="field"><label>Fecha</label><AppDatePicker v-model="form.fecha" :disabled="!!existingId" required /></div>
        <div class="field"><label>Depósito</label><Select v-model="form.depositoId" :options="deposits" option-label="nombre" option-value="id" placeholder="Seleccionar" /></div>
      </div>
      <div class="field imp-observations"><label>Observaciones de los ingresos</label><Textarea v-model="form.observaciones" maxlength="500" rows="3" /></div>
      </div>
      <Message v-if="selectedDeposit && !products.length" severity="warn" :closable="false">Este depósito no tiene productos importados activos. Marcá los productos correspondientes desde Productos.</Message>
      <div v-for="(line, index) in form.lineas" :key="index" class="imp-product-card">
        <div class="imp-card-title"><strong>Producto {{ index + 1 }}</strong><AppButton v-if="form.lineas.length > 1" text severity="danger" label="Quitar" @click="form.lineas.splice(index, 1)" /></div>
        <div class="imp-product-grid">
          <div class="field imp-product-select"><label>Producto importado</label><Select v-model="line.tipoCespedId" :options="products" option-label="nombre" option-value="tipoCespedId" filter @change="productChanged(line)" /></div>
          <div v-if="product(line) && !product(line)?.controlPorLotes && !product(line)?.colores.length" class="field imp-quantity"><label>Cantidad m²</label><InputNumber v-model="line.cantidadM2" :min="0" :max-fraction-digits="2" @update:model-value="line.touched = true" /></div>
          <div v-if="product(line)?.controlPorLotes" class="field imp-quantity"><label>Color</label><Select v-model="line.color" :options="product(line)?.colores" @change="line.touched = true" /></div>
        </div>
        <div v-if="product(line) && !product(line)?.controlPorLotes && product(line)?.colores.length" class="imp-variants">
          <div v-for="(variant, variantIndex) in line.variantes" :key="variantIndex" class="imp-variant-row"><Select v-model="variant.color" :options="product(line)?.colores" placeholder="Color" @change="line.touched = true" /><InputNumber v-model="variant.cantidadM2" placeholder="m²" :min="0" :max-fraction-digits="2" @update:model-value="line.touched = true" /><AppButton text severity="danger" icon="pi pi-trash" aria-label="Quitar color" @click="line.variantes.splice(variantIndex, 1)" /></div>
          <div class="imp-inline-actions"><AppButton type="button" label="Agregar color" severity="secondary" text @click="addVariant(line)" /><AppButton type="button" label="Cargar todos los colores" severity="secondary" text @click="addAllColors(line)" /></div>
        </div>
        <div v-if="product(line)?.controlPorLotes" class="imp-roll-grid">
          <div v-for="roll in line.rollos" :key="roll.posicion" class="imp-roll-card"><strong>Rollo {{ roll.posicion }}</strong><InputText v-model="roll.codigoBarra" :placeholder="`Código ${roll.posicion}`" @input="line.touched = true" /><InputNumber v-model="roll.cantidadM2" placeholder="m²" :min="0" :max-fraction-digits="2" @update:model-value="line.touched = true" /></div>
        </div>
        <div v-if="product(line)" class="imp-card-footer"><AppButton v-if="product(line)?.controlPorLotes" type="button" label="Agregar otro lote de este producto" severity="secondary" text @click="addLine(line)" /><strong>Subtotal: {{ number.format(lineTotal(line)) }} m²</strong></div>
        <small v-if="product(line) && (line.touched || attempted)" v-for="message in receptionLineErrors({ ...line, controlPorLotes: product(line)?.controlPorLotes ?? false })" :key="message" class="text-red-600 block">{{ message }}</small>
      </div>
      <AppButton type="button" label="Agregar producto" icon="pi pi-plus" severity="secondary" :disabled="form.lineas.length >= 100" @click="addLine()" />
      <div class="imp-sticky-footer"><strong>Total: {{ number.format(total) }} m²</strong><div class="imp-footer-actions"><AppButton type="button" label="Cancelar" severity="secondary" @click="router.push('/importaciones')" /><AppButton type="submit" label="Guardar recepción" :loading="saving" :disabled="hasLocalErrors || saving" /></div></div>
    </form>
  </section>
</template>
