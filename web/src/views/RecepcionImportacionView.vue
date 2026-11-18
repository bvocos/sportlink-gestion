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
import { receptionLineErrors, receptionTotal, type ReceptionRoll } from '@/presupuesto/recepcionImportacion'

type Product = { tipoCespedId: string; nombre: string; controlPorLotes: boolean; esImportado: boolean; colores: string[] }
type Deposit = { id: string; nombre: string; productos: Product[]; permiteRegistrarIngreso: boolean }
type Line = { tipoCespedId: string; cantidadM2: number; color: string; rollos: ReceptionRoll[] }

const route = useRoute(), router = useRouter()
const existingId = computed(() => route.params.id as string | undefined)
const deposits = ref<Deposit[]>([]), loading = ref(true), saving = ref(false), error = ref('')
const existing = ref<any>(null)
const form = ref({ codigo: '', numeroDespacho: '', fecha: new Date().toISOString().slice(0, 10), depositoId: '', observaciones: '', lineas: [] as Line[] })
const selectedDeposit = computed(() => deposits.value.find(x => x.id === form.value.depositoId))
const products = computed(() => (selectedDeposit.value?.productos ?? []).filter(x => x.esImportado))
const total = computed(() => receptionTotal(form.value.lineas.map(line => ({ ...line, controlPorLotes: product(line)?.controlPorLotes ?? false }))))
const hasLocalErrors = computed(() => !form.value.depositoId || !form.value.codigo || form.value.lineas.length === 0 || form.value.lineas.some(line =>
  !line.tipoCespedId || receptionLineErrors({ ...line, controlPorLotes: product(line)?.controlPorLotes ?? false }).length > 0))
const number = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })

function rolls(): ReceptionRoll[] { return ['A', 'B', 'C'].map(posicion => ({ posicion: posicion as 'A' | 'B' | 'C', codigoBarra: '', cantidadM2: 0 })) }
function product(line: Line) { return products.value.find(x => x.tipoCespedId === line.tipoCespedId) }
function addLine() { form.value.lineas.push({ tipoCespedId: '', cantidadM2: 0, color: '', rollos: rolls() }) }
function productChanged(line: Line) { const value = product(line); line.color = value?.colores[0] ?? ''; line.cantidadM2 = 0; line.rollos = rolls() }
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
  if (hasLocalErrors.value || saving.value) return
  saving.value = true; error.value = ''
  try {
    const payload = {
      importacionId: existingId.value ?? null,
      nueva: existingId.value ? null : { codigo: form.value.codigo, numeroDespacho: form.value.numeroDespacho || null, fecha: form.value.fecha, observaciones: null },
      depositoId: form.value.depositoId,
      observaciones: form.value.observaciones || null,
      lineas: form.value.lineas.map(line => ({ tipoCespedId: line.tipoCespedId,
        cantidadM2: product(line)?.controlPorLotes ? null : line.cantidadM2,
        color: product(line)?.controlPorLotes ? line.color || null : null,
        rollos: product(line)?.controlPorLotes ? line.rollos : null })),
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
    <form v-else class="p-fluid" @submit.prevent="save">
      <div class="grid">
        <div class="field col-12 md:col-3"><label>Código de importación</label><InputText v-model="form.codigo" :disabled="!!existingId" maxlength="50" required /></div>
        <div class="field col-12 md:col-3"><label>N° de despacho</label><InputText v-model="form.numeroDespacho" :disabled="!!existingId" maxlength="100" /></div>
        <div class="field col-12 md:col-3"><label>Fecha</label><AppDatePicker v-model="form.fecha" :disabled="!!existingId" required /></div>
        <div class="field col-12 md:col-3"><label>Depósito</label><Select v-model="form.depositoId" :options="deposits" option-label="nombre" option-value="id" placeholder="Seleccionar" /></div>
      </div>
      <Message v-if="selectedDeposit && !products.length" severity="warn" :closable="false">Este depósito no tiene productos importados activos. Marcá los productos correspondientes desde Productos.</Message>
      <div v-for="(line, index) in form.lineas" :key="index" class="surface-card border-1 surface-border border-round p-3 mb-3">
        <div class="flex justify-content-between align-items-center mb-2"><strong>Línea {{ index + 1 }}</strong><AppButton v-if="form.lineas.length > 1" text severity="danger" label="Quitar" @click="form.lineas.splice(index, 1)" /></div>
        <div class="grid">
          <div class="field col-12 md:col-5"><label>Producto importado</label><Select v-model="line.tipoCespedId" :options="products" option-label="nombre" option-value="tipoCespedId" filter @change="productChanged(line)" /></div>
          <div v-if="product(line) && !product(line)?.controlPorLotes" class="field col-12 md:col-3"><label>Cantidad m²</label><InputNumber v-model="line.cantidadM2" :min="0" :max-fraction-digits="2" /></div>
          <div v-if="product(line)?.controlPorLotes" class="field col-12 md:col-3"><label>Color</label><Select v-model="line.color" :options="product(line)?.colores" /></div>
        </div>
        <div v-if="product(line)?.controlPorLotes" class="grid">
          <div v-for="roll in line.rollos" :key="roll.posicion" class="col-12 md:col-4"><strong>Rollo {{ roll.posicion }}</strong><InputText v-model="roll.codigoBarra" :placeholder="`Código ${roll.posicion}`" /><InputNumber v-model="roll.cantidadM2" placeholder="m²" :min="0" :max-fraction-digits="2" /></div>
        </div>
        <small v-for="message in receptionLineErrors({ ...line, controlPorLotes: product(line)?.controlPorLotes ?? false })" :key="message" class="text-red-600 block">{{ message }}</small>
      </div>
      <AppButton type="button" label="Agregar línea" icon="pi pi-plus" severity="secondary" :disabled="form.lineas.length >= 100" @click="addLine" />
      <div class="field mt-3"><label>Observaciones de los ingresos</label><Textarea v-model="form.observaciones" maxlength="500" rows="3" /></div>
      <div class="flex justify-content-between align-items-center mt-3"><strong>Total: {{ number.format(total) }} m²</strong><AppButton type="submit" label="Guardar recepción" :loading="saving" :disabled="hasLocalErrors || saving" /></div>
    </form>
  </section>
</template>
