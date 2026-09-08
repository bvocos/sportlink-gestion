<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { auth } from '@/auth'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { pluralize } from '@/shared/formatters'

type StockProduct = { tipoCespedId: string; nombre: string; stockActualM2: number; controlPorLotes: boolean; colores: string[] }
type StockDeposito = { id: string; nombre: string; productos: StockProduct[]; permiteRegistrarIngreso: boolean }
type RolloForm = { posicion: 'A' | 'B' | 'C'; codigoBarra: string; cantidadM2: number }

const depositos = ref<StockDeposito[]>([])
const movimientos = ref<any[]>([])
const loading = ref(false), loadError = ref(''), showIngreso = ref(false), saveError = ref('')
const page = ref(1), total = ref(0), totalPages = ref(0)
const filters = ref({ depositoId: '', tipoCespedId: '', desde: '', hasta: '' })
const form = ref({ depositoId: '', tipoCespedId: '', color: '', cantidadM2: 0, observaciones: '', rollos: newRolls() })
const expandedLots = ref<Record<string, boolean>>({})
const lotsByProduct = ref<Record<string, any[]>>({})
const lotsLoading = ref<Record<string, boolean>>({})
const barcode = ref(''), barcodeResult = ref<any>(null), barcodeError = ref(''), barcodeLoading = ref(false)
const barcodeInputs = ref<Record<string, HTMLInputElement | null>>({})
const isAdmin = computed(() => auth.state.user?.rol === 'Administrador')
const depositosIngreso = computed(() => depositos.value.filter(x => x.permiteRegistrarIngreso))
const productosIngreso = computed(() => depositos.value.find(x => x.id === form.value.depositoId)?.productos ?? [])
const selectedProduct = computed(() => productosIngreso.value.find(x => x.tipoCespedId === form.value.tipoCespedId))
const productosFiltro = computed(() => {
  if (filters.value.depositoId) return depositos.value.find(x => x.id === filters.value.depositoId)?.productos ?? []
  const unique = new Map<string, StockProduct>()
  depositos.value.flatMap(x => x.productos).forEach(product => unique.set(product.tipoCespedId, product))
  return [...unique.values()].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'))
})
const number = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })

function newRolls(): RolloForm[] { return ['A', 'B', 'C'].map(posicion => ({ posicion: posicion as RolloForm['posicion'], codigoBarra: '', cantidadM2: 0 })) }
function meters(value: number) { return `${number.format(Number(value ?? 0))} m²` }
function lotKey(depositoId: string, tipoCespedId: string) { return `${depositoId}:${tipoCespedId}` }
function setBarcodeInput(position: string, element: any) { barcodeInputs.value[position] = element as HTMLInputElement | null }
function focusNext(position: string) { const next = position === 'A' ? 'B' : position === 'B' ? 'C' : ''; if (next) nextTick(() => barcodeInputs.value[next]?.focus()) }

async function loadStock() {
  depositos.value = (await http.get('/stock')).data
  if (!depositosIngreso.value.some(x => x.id === form.value.depositoId)) form.value.depositoId = depositosIngreso.value[0]?.id ?? ''
  ensureIngresoProduct()
}
function ensureIngresoProduct() {
  if (!productosIngreso.value.some(x => x.tipoCespedId === form.value.tipoCespedId)) form.value.tipoCespedId = productosIngreso.value[0]?.tipoCespedId ?? ''
  form.value.color = selectedProduct.value?.colores[0] ?? ''
  form.value.rollos = newRolls()
}
function ensureFilterProduct() { if (!productosFiltro.value.some(x => x.tipoCespedId === filters.value.tipoCespedId)) filters.value.tipoCespedId = '' }
async function loadMovimientos(reset = false) {
  if (reset) page.value = 1
  const { data } = await http.get('/stock/movimientos', { params: { depositoId: filters.value.depositoId || undefined, tipoCespedId: filters.value.tipoCespedId || undefined, desde: filters.value.desde || undefined, hasta: filters.value.hasta || undefined, page: page.value, pageSize: 50 } })
  movimientos.value = data.items; total.value = data.total; totalPages.value = data.totalPages
}
async function load() {
  loading.value = true; loadError.value = ''
  try { await Promise.all([loadStock(), loadMovimientos()]) } catch (e: any) { loadError.value = apiErrorMessage(e, 'No se pudo cargar el stock.') } finally { loading.value = false }
}
function openIngreso() {
  form.value = { depositoId: depositosIngreso.value[0]?.id ?? '', tipoCespedId: '', color: '', cantidadM2: 0, observaciones: '', rollos: newRolls() }
  ensureIngresoProduct(); saveError.value = ''; showIngreso.value = true
}
async function saveIngreso() {
  saveError.value = ''
  try {
    const controlled = selectedProduct.value?.controlPorLotes
    await http.post(controlled ? '/stock/lotes' : '/stock/ingresos', controlled
      ? { depositoId: form.value.depositoId, tipoCespedId: form.value.tipoCespedId, color: form.value.color || null, rollos: form.value.rollos, observaciones: form.value.observaciones }
      : { depositoId: form.value.depositoId, tipoCespedId: form.value.tipoCespedId, cantidadM2: form.value.cantidadM2, observaciones: form.value.observaciones })
    showIngreso.value = false; expandedLots.value = {}; lotsByProduct.value = {}
    await Promise.all([loadStock(), loadMovimientos(true)])
  } catch (e: any) { saveError.value = apiErrorMessage(e, 'No se pudo registrar el ingreso de stock.') }
}
async function toggleLots(depositoId: string, tipoCespedId: string) {
  const key = lotKey(depositoId, tipoCespedId); expandedLots.value[key] = !expandedLots.value[key]
  if (!expandedLots.value[key] || lotsByProduct.value[key]) return
  lotsLoading.value[key] = true
  try { lotsByProduct.value[key] = (await http.get('/stock/lotes', { params: { depositoId, tipoCespedId, estado: 'Disponible' } })).data }
  catch { lotsByProduct.value[key] = [] } finally { lotsLoading.value[key] = false }
}
async function searchBarcode() {
  barcodeError.value = ''; barcodeResult.value = null; barcodeLoading.value = true
  try { barcodeResult.value = (await http.get('/stock/lotes/buscar-por-codigo', { params: { codigoBarra: barcode.value.trim() } })).data }
  catch (e: any) { barcodeError.value = apiErrorMessage(e, 'No se encontró el código ingresado.') } finally { barcodeLoading.value = false }
}
async function applyFilters() { loading.value = true; loadError.value = ''; try { await loadMovimientos(true) } catch (e: any) { loadError.value = apiErrorMessage(e, 'No se pudieron cargar los movimientos.') } finally { loading.value = false } }
async function clearFilters() { filters.value = { depositoId: '', tipoCespedId: '', desde: '', hasta: '' }; await applyFilters() }
async function changePage(next: number) { page.value = next; await loadMovimientos() }
onMounted(load)
</script>

<template>
  <section class="page">
    <div class="page-title"><div><h2>Stock</h2><p>Disponibilidad de césped y trazabilidad de los depósitos habilitados.</p></div><button class="btn" :disabled="!depositosIngreso.length" @click="openIngreso">+ Registrar ingreso</button></div>
    <div v-if="loadError" class="error load-state">{{ loadError }} <button class="btn secondary compact" @click="load">Reintentar</button></div>
    <template v-else>
      <div class="grid stock-cards">
        <article v-for="deposito in depositos" :key="deposito.id" class="card stock-deposit-card">
          <h3>{{ deposito.nombre }}</h3>
          <div class="stock-product-list">
            <div v-for="producto in deposito.productos" :key="producto.tipoCespedId" class="stock-product-row">
              <span>{{ producto.nombre }} <small v-if="producto.controlPorLotes" class="badge">Por lotes</small></span>
              <b :class="{ negative: producto.stockActualM2 < 0 }">{{ meters(producto.stockActualM2) }}</b>
              <button v-if="producto.controlPorLotes" class="link-button stock-lot-link" type="button" @click="toggleLots(deposito.id, producto.tipoCespedId)">{{ expandedLots[lotKey(deposito.id, producto.tipoCespedId)] ? 'Ocultar lotes' : 'Ver lotes' }}</button>
              <div v-if="expandedLots[lotKey(deposito.id, producto.tipoCespedId)]" class="stock-lots">
                <small v-if="lotsLoading[lotKey(deposito.id, producto.tipoCespedId)]">Cargando lotes…</small>
                <div v-for="lote in lotsByProduct[lotKey(deposito.id, producto.tipoCespedId)]" :key="lote.id" class="stock-lot"><b>{{ lote.color || 'Sin color' }} · {{ meters(lote.cantidadM2) }}</b><span v-for="rollo in lote.rollos" :key="rollo.id">{{ rollo.posicion }}: {{ rollo.codigoBarra }} ({{ meters(rollo.cantidadM2) }})</span></div>
                <small v-if="!lotsLoading[lotKey(deposito.id, producto.tipoCespedId)] && !lotsByProduct[lotKey(deposito.id, producto.tipoCespedId)]?.length">No hay lotes disponibles.</small>
              </div>
            </div>
          </div>
        </article>
      </div>
      <form class="panel barcode-search" @submit.prevent="searchBarcode"><div class="field"><label>Buscar trazabilidad por código de barra</label><input v-model.trim="barcode" required maxlength="150" placeholder="Escaneá o escribí el código"></div><button class="btn" :disabled="barcodeLoading">{{ barcodeLoading ? 'Buscando…' : 'Buscar' }}</button></form>
      <div v-if="barcodeError" class="error">{{ barcodeError }}</div>
      <article v-if="barcodeResult" class="panel barcode-result"><div class="panel-head"><div><h3>Rollo {{ barcodeResult.rollo.codigoBarra }}</h3><small>{{ barcodeResult.lote.tipoCespedNombre }} · {{ barcodeResult.lote.depositoNombre }}</small></div><span class="badge" :class="{ strong: barcodeResult.lote.estado === 'Disponible', warn: barcodeResult.lote.estado === 'Vendido' }">{{ barcodeResult.lote.estado }}</span></div><div class="barcode-detail"><p><b>Color:</b> {{ barcodeResult.lote.color || 'Sin color' }}</p><p><b>Posición encontrada:</b> {{ barcodeResult.rollo.posicion }} · {{ meters(barcodeResult.rollo.cantidadM2) }}</p><p><b>Lote completo:</b></p><div class="roll-code-list"><span v-for="rollo in barcodeResult.lote.rollos" :key="rollo.id">{{ rollo.posicion }} · {{ rollo.codigoBarra }} · {{ meters(rollo.cantidadM2) }}</span></div><p v-if="barcodeResult.venta"><b>Venta:</b> {{ barcodeResult.venta.cliente }} · {{ barcodeResult.venta.fechaVenta }}</p></div></article>
      <form class="panel stock-filters" @submit.prevent="applyFilters"><div class="field"><label>Depósito</label><select v-model="filters.depositoId" @change="ensureFilterProduct"><option value="">Todos los depósitos</option><option v-for="deposito in depositos" :key="deposito.id" :value="deposito.id">{{ deposito.nombre }}</option></select></div><div class="field"><label>Producto</label><select v-model="filters.tipoCespedId"><option value="">Todos los productos</option><option v-for="producto in productosFiltro" :key="producto.tipoCespedId" :value="producto.tipoCespedId">{{ producto.nombre }}</option></select></div><div class="field"><label>Desde</label><input v-model="filters.desde" type="date"></div><div class="field"><label>Hasta</label><input v-model="filters.hasta" type="date"></div><div class="filter-actions"><button class="btn">Aplicar filtros</button><button class="btn secondary" type="button" @click="clearFilters">Restablecer</button></div></form>
      <div class="panel"><div class="panel-head"><div><h3>Historial de movimientos</h3><small>{{ pluralize(total, 'movimiento registrado', 'movimientos registrados') }}</small></div></div><div v-if="loading" class="loading">Cargando movimientos…</div><table v-else><thead><tr><th>Fecha</th><th>Depósito</th><th>Producto</th><th>Tipo</th><th>Usuario</th><th>Observaciones</th><th class="num">Cantidad</th></tr></thead><tbody><tr v-for="movimiento in movimientos" :key="movimiento.id"><td>{{ new Date(movimiento.fecha).toLocaleString('es-AR') }}</td><td><b>{{ movimiento.depositoNombre }}</b></td><td>{{ movimiento.tipoCespedNombre }}</td><td><span class="badge" :class="{ warn: movimiento.tipo === 'Ajuste', danger: movimiento.tipo === 'SalidaPorVenta' }">{{ movimiento.tipo }}</span></td><td>{{ movimiento.usuario }}</td><td>{{ movimiento.observaciones || '—' }}</td><td class="num"><b :class="{ negative: movimiento.cantidadConSigno < 0 }">{{ movimiento.cantidadConSigno > 0 ? '+ ' : '− ' }}{{ meters(Math.abs(movimiento.cantidadConSigno)) }}</b></td></tr></tbody></table><div v-if="!loading && !movimientos.length" class="empty">No hay movimientos para los filtros seleccionados.</div><div v-if="totalPages > 1" class="actions"><button class="btn secondary" :disabled="page <= 1" @click="changePage(page - 1)">Anterior</button><span>Página {{ page }} de {{ totalPages }}</span><button class="btn secondary" :disabled="page >= totalPages" @click="changePage(page + 1)">Siguiente</button></div></div>
    </template>
    <div v-if="showIngreso" class="modal-bg"><form class="modal stock-entry-modal" @submit.prevent="saveIngreso"><h3>Registrar ingreso de stock</h3><p v-if="saveError" class="error">{{ saveError }}</p><div class="form-grid"><div class="field"><label>Depósito</label><select v-model="form.depositoId" :disabled="!isAdmin" required @change="ensureIngresoProduct"><option v-for="deposito in depositosIngreso" :key="deposito.id" :value="deposito.id">{{ deposito.nombre }}</option></select><small v-if="!isAdmin">Sólo podés ingresar stock en el depósito de tu sucursal.</small></div><div class="field"><label>Producto</label><select v-model="form.tipoCespedId" required @change="ensureIngresoProduct"><option value="" disabled>Seleccionar producto</option><option v-for="producto in productosIngreso" :key="producto.tipoCespedId" :value="producto.tipoCespedId">{{ producto.nombre }}</option></select></div></div><template v-if="selectedProduct?.controlPorLotes"><p class="lot-mode-note">Este producto se controla por lotes: registrá exactamente los rollos A, B y C.</p><div v-if="selectedProduct.colores.length" class="field"><label>Color</label><select v-model="form.color" required><option v-for="color in selectedProduct.colores" :key="color" :value="color">{{ color }}</option></select></div><div class="lot-roll-grid"><fieldset v-for="rollo in form.rollos" :key="rollo.posicion"><legend>Rollo {{ rollo.posicion }}</legend><div class="field"><label>Código de barra</label><input :ref="element => setBarcodeInput(rollo.posicion, element)" v-model.trim="rollo.codigoBarra" maxlength="150" required autocomplete="off" @keydown.enter.prevent="focusNext(rollo.posicion)" @blur="rollo.codigoBarra && focusNext(rollo.posicion)"></div><div class="field"><label>Cantidad (m²)</label><input v-model.number="rollo.cantidadM2" type="number" min="0.01" step="0.01" required></div></fieldset></div><strong class="lot-total">Total del lote: {{ meters(form.rollos.reduce((sum, rollo) => sum + Number(rollo.cantidadM2 || 0), 0)) }}</strong></template><div v-else class="field"><label>Cantidad (m²)</label><input v-model.number="form.cantidadM2" type="number" min="0.01" step="0.01" required></div><div class="field"><label>Observaciones</label><textarea v-model="form.observaciones" maxlength="500" rows="4" placeholder="Ej.: recepción de rollos del proveedor"></textarea></div><div class="actions"><button class="btn secondary" type="button" @click="showIngreso = false">Cancelar</button><button class="btn">Registrar ingreso</button></div></form></div>
  </section>
</template>
