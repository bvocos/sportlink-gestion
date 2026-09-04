<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { auth } from '@/auth'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { pluralize } from '@/shared/formatters'

type StockDeposito = {
  id: string
  nombre: string
  productos: { tipoCespedId: string; nombre: string; stockActualM2: number }[]
  permiteRegistrarIngreso: boolean
}

const depositos = ref<StockDeposito[]>([])
const movimientos = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const showIngreso = ref(false)
const saveError = ref('')
const page = ref(1)
const total = ref(0)
const totalPages = ref(0)
const filters = ref({ depositoId: '', tipoCespedId: '', desde: '', hasta: '' })
const form = ref({ depositoId: '', tipoCespedId: '', cantidadM2: 0, observaciones: '' })
const isAdmin = computed(() => auth.state.user?.rol === 'Administrador')
const depositosIngreso = computed(() => depositos.value.filter(x => x.permiteRegistrarIngreso))
const productos = computed(() => depositos.value[0]?.productos ?? [])
const number = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })

function meters(value: number) {
  return `${number.format(Number(value ?? 0))} m²`
}
async function loadStock() {
  depositos.value = (await http.get('/stock')).data
  if (!depositosIngreso.value.some(x => x.id === form.value.depositoId))
    form.value.depositoId = depositosIngreso.value[0]?.id ?? ''
  if (!productos.value.some(x => x.tipoCespedId === form.value.tipoCespedId))
    form.value.tipoCespedId = productos.value[0]?.tipoCespedId ?? ''
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
      pageSize: 50
    }
  })
  movimientos.value = data.items
  total.value = data.total
  totalPages.value = data.totalPages
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
function openIngreso() {
  form.value = { depositoId: depositosIngreso.value[0]?.id ?? '', tipoCespedId: productos.value[0]?.tipoCespedId ?? '', cantidadM2: 0, observaciones: '' }
  saveError.value = ''
  showIngreso.value = true
}
async function saveIngreso() {
  saveError.value = ''
  try {
    await http.post('/stock/ingresos', form.value)
    showIngreso.value = false
    await Promise.all([loadStock(), loadMovimientos(true)])
  } catch (e: any) {
    saveError.value = apiErrorMessage(e, 'No se pudo registrar el ingreso de stock.')
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
async function changePage(next: number) {
  page.value = next
  await loadMovimientos()
}
onMounted(load)
</script>

<template>
  <section class="page">
    <div class="page-title">
      <div><h2>Stock</h2><p>Disponibilidad de césped en todos los depósitos.</p></div>
      <button class="btn" :disabled="!depositosIngreso.length" :title="depositosIngreso.length ? 'Registrar ingreso' : 'Necesitás una sucursal con depósito asignado'" @click="openIngreso">+ Registrar ingreso</button>
    </div>
    <div v-if="loadError" class="error load-state">{{ loadError }} <button class="btn secondary compact" @click="load">Reintentar</button></div>
    <template v-else>
      <div class="grid stock-cards">
        <article v-for="deposito in depositos" :key="deposito.id" class="card stock-deposit-card">
          <h3>{{ deposito.nombre }}</h3>
          <div class="stock-product-list"><div v-for="producto in deposito.productos" :key="producto.tipoCespedId"><span>{{ producto.nombre }}</span><b :class="{ negative: producto.stockActualM2 < 0 }">{{ meters(producto.stockActualM2) }}</b></div></div>
        </article>
      </div>
      <form class="panel stock-filters" @submit.prevent="applyFilters">
        <div class="field"><label>Depósito</label><select v-model="filters.depositoId"><option value="">Todos los depósitos</option><option v-for="deposito in depositos" :key="deposito.id" :value="deposito.id">{{ deposito.nombre }}</option></select></div>
        <div class="field"><label>Producto</label><select v-model="filters.tipoCespedId"><option value="">Todos los productos</option><option v-for="producto in productos" :key="producto.tipoCespedId" :value="producto.tipoCespedId">{{ producto.nombre }}</option></select></div>
        <div class="field"><label>Desde</label><input v-model="filters.desde" type="date"></div>
        <div class="field"><label>Hasta</label><input v-model="filters.hasta" type="date"></div>
        <div class="filter-actions"><button class="btn">Aplicar filtros</button><button class="btn secondary" type="button" @click="clearFilters">Restablecer</button></div>
      </form>
      <div class="panel">
        <div class="panel-head"><div><h3>Historial de movimientos</h3><small>{{ pluralize(total, 'movimiento registrado', 'movimientos registrados') }}</small></div></div>
        <div v-if="loading" class="loading">Cargando movimientos…</div>
        <table v-else>
          <thead><tr><th>Fecha</th><th>Depósito</th><th>Producto</th><th>Tipo</th><th>Usuario</th><th>Observaciones</th><th class="num">Cantidad</th></tr></thead>
          <tbody><tr v-for="movimiento in movimientos" :key="movimiento.id"><td>{{ new Date(movimiento.fecha).toLocaleString('es-AR') }}</td><td><b>{{ movimiento.depositoNombre }}</b></td><td>{{ movimiento.tipoCespedNombre }}</td><td><span class="badge" :class="{ warn: movimiento.tipo === 'Ajuste', danger: movimiento.tipo === 'SalidaPorVenta' }">{{ movimiento.tipo }}</span></td><td>{{ movimiento.usuario }}</td><td>{{ movimiento.observaciones || '—' }}</td><td class="num"><b :class="{ negative: movimiento.cantidadConSigno < 0 }">{{ movimiento.cantidadConSigno > 0 ? '+ ' : '− ' }}{{ meters(Math.abs(movimiento.cantidadConSigno)) }}</b></td></tr></tbody>
        </table>
        <div v-if="!loading && !movimientos.length" class="empty">No hay movimientos para los filtros seleccionados.</div>
        <div v-if="totalPages > 1" class="actions"><button class="btn secondary" :disabled="page <= 1" @click="changePage(page - 1)">Anterior</button><span>Página {{ page }} de {{ totalPages }}</span><button class="btn secondary" :disabled="page >= totalPages" @click="changePage(page + 1)">Siguiente</button></div>
      </div>
    </template>
    <div v-if="showIngreso" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="saveIngreso">
        <h3>Registrar ingreso de stock</h3>
        <p v-if="saveError" class="error">{{ saveError }}</p>
        <div class="field"><label>Depósito</label><select v-model="form.depositoId" :disabled="!isAdmin" required><option v-for="deposito in depositosIngreso" :key="deposito.id" :value="deposito.id">{{ deposito.nombre }}</option></select><small v-if="!isAdmin">Sólo podés ingresar stock en el depósito de tu sucursal.</small></div>
        <div class="field"><label>Producto</label><select v-model="form.tipoCespedId" required><option value="" disabled>Seleccionar producto</option><option v-for="producto in productos" :key="producto.tipoCespedId" :value="producto.tipoCespedId">{{ producto.nombre }}</option></select></div>
        <div class="field"><label>Cantidad (m²)</label><input v-model.number="form.cantidadM2" type="number" min="0.01" step="0.01" required></div>
        <div class="field"><label>Observaciones</label><textarea v-model="form.observaciones" maxlength="500" rows="4" placeholder="Ej.: recepción de rollos del proveedor"></textarea></div>
        <div class="actions"><button class="btn secondary" type="button" @click="showIngreso = false">Cancelar</button><button class="btn">Registrar ingreso</button></div>
      </form>
    </div>
  </section>
</template>
