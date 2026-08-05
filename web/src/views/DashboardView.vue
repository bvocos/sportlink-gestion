<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { http } from '@/shared/api/httpClient'
import { formatCurrency as money, pluralize } from '@/shared/formatters'
import { auth } from '@/auth'
import MiniSparkline from '@/shared/components/MiniSparkline.vue'

type Summary = { cantidad: number; facturacion: number }
type SeriesPoint = { fecha: string; facturacion: number; finalizadas: number; enCurso: number; gananciaNeta: number }
type Sale = { id: string; cliente: string; tipoCesped: string; fechaVenta: string; precioTotal: number; cantidadM2: number; estado: string }
type Option = { id: string; nombre: string; activo?: boolean }
type DashboardData = {
  total: Summary & { metros: number; gananciaNeta: number }
  finalizadas: Summary
  enCurso: Summary
  saldo: number
  cuotasPendientes: number
  series: SeriesPoint[]
  ventas: Sale[]
  filtros: { clientes: Option[]; productos: Option[] }
}

const dateKey = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
const today = () => dateKey(new Date())
const daysAgo = (days: number) => { const date = new Date(); date.setDate(date.getDate() - days); return dateKey(date) }
const emptyData = (): DashboardData => ({
  total: { cantidad: 0, facturacion: 0, metros: 0, gananciaNeta: 0 },
  finalizadas: { cantidad: 0, facturacion: 0 }, enCurso: { cantidad: 0, facturacion: 0 },
  saldo: 0, cuotasPendientes: 0, series: [], ventas: [], filtros: { clientes: [], productos: [] }
})
const data = ref<DashboardData>(emptyData())
const loading = ref(false), loadError = ref('')
const filters = reactive({ periodo: '7', clienteId: '', tipoCespedId: '', desde: daysAgo(6), hasta: today() })

function applyPeriod() {
  const now = new Date()
  if (filters.periodo === 'custom') return
  if (filters.periodo === 'month') {
    filters.desde = dateKey(new Date(now.getFullYear(), now.getMonth(), 1))
    filters.hasta = today()
  } else if (filters.periodo === 'previous') {
    filters.desde = dateKey(new Date(now.getFullYear(), now.getMonth() - 1, 1))
    filters.hasta = dateKey(new Date(now.getFullYear(), now.getMonth(), 0))
  } else {
    const days = Number(filters.periodo)
    filters.desde = daysAgo(days - 1)
    filters.hasta = today()
  }
}
function customDates() { filters.periodo = 'custom' }
async function load() {
  loading.value = true; loadError.value = ''
  try {
    const params = { desde: filters.desde, hasta: filters.hasta, clienteId: filters.clienteId || undefined, tipoCespedId: filters.tipoCespedId || undefined }
    const response = await http.get('/dashboard', { params })
    data.value = response.data
  } catch (error: any) {
    loadError.value = error?.response?.data?.errors?.fechas?.[0] || 'No se pudo cargar el inicio del sistema.'
  } finally { loading.value = false }
}
function resetFilters() {
  filters.periodo = '7'; filters.clienteId = ''; filters.tipoCespedId = ''; applyPeriod(); load()
}
const series = (field: keyof Omit<SeriesPoint, 'fecha'>) => computed(() => data.value.series.map(point => Number(point[field])))
const billingSeries = series('facturacion'), finishedSeries = series('finalizadas'), activeSeries = series('enCurso'), profitSeries = series('gananciaNeta')
const periodLabel = computed(() => `${new Date(filters.desde + 'T00:00:00').toLocaleDateString('es-AR')} al ${new Date(filters.hasta + 'T00:00:00').toLocaleDateString('es-AR')}`)
onMounted(load)
</script>

<template>
  <section class="page dashboard-page">
    <div class="page-title">
      <div><h2>Inicio</h2><p>Hola, {{ auth.state.user?.nombre || 'bienvenido' }}. Analizá la operación comercial por período.</p></div>
      <RouterLink v-if="auth.can('ventas')" class="btn" to="/ventas">+ Nueva venta</RouterLink>
    </div>

    <form class="panel dashboard-filters" @submit.prevent="load">
      <div class="field"><label>Período</label><select v-model="filters.periodo" @change="applyPeriod"><option value="7">Últimos 7 días</option><option value="30">Últimos 30 días</option><option value="month">Este mes</option><option value="previous">Mes anterior</option><option value="custom">Personalizado</option></select></div>
      <div class="field"><label>Desde</label><input v-model="filters.desde" type="date" required @change="customDates"></div>
      <div class="field"><label>Hasta</label><input v-model="filters.hasta" type="date" required @change="customDates"></div>
      <div class="field"><label>Cliente</label><select v-model="filters.clienteId"><option value="">Todos los clientes</option><option v-for="item in data.filtros.clientes" :key="item.id" :value="item.id">{{ item.nombre }}</option></select></div>
      <div class="field"><label>Tipo de producto</label><select v-model="filters.tipoCespedId"><option value="">Todos los productos</option><option v-for="item in data.filtros.productos" :key="item.id" :value="item.id">{{ item.nombre }}{{ item.activo === false ? ' (inactivo)' : '' }}</option></select></div>
      <div class="filter-actions"><button class="btn" :disabled="loading">{{ loading ? 'Actualizando…' : 'Aplicar filtros' }}</button><button type="button" class="btn secondary" @click="resetFilters">Restablecer</button></div>
    </form>

    <div v-if="loadError" class="error load-state">{{ loadError }} <button class="btn secondary compact" @click="load">Reintentar</button></div>
    <div v-else-if="loading && !data.series.length" class="panel loading">Cargando inicio…</div>
    <template v-else>
      <p class="dashboard-period">Resultados del {{ periodLabel }}</p>
      <div class="grid dashboard-metrics">
        <article class="card metric chart-metric"><small>Facturación del período</small><strong>{{ money(data.total.facturacion) }}</strong><em>{{ pluralize(data.total.cantidad, 'venta', 'ventas') }} no canceladas</em><MiniSparkline :values="billingSeries" /></article>
        <article class="card metric chart-metric finished"><small>Ventas finalizadas</small><strong>{{ money(data.finalizadas.facturacion) }}</strong><em>{{ pluralize(data.finalizadas.cantidad, 'operación entregada', 'operaciones entregadas') }}</em><MiniSparkline :values="finishedSeries" color="#2f7d4b" /></article>
        <article class="card metric chart-metric active"><small>Ventas en curso</small><strong>{{ money(data.enCurso.facturacion) }}</strong><em>{{ pluralize(data.enCurso.cantidad, 'operación pendiente', 'operaciones pendientes') }}</em><MiniSparkline :values="activeSeries" color="#c27a22" /></article>
        <article class="card metric chart-metric profit"><small>Ganancia neta estimada</small><strong :class="{ negative: data.total.gananciaNeta < 0 }">{{ money(data.total.gananciaNeta) }}</strong><em>Sobre las ventas filtradas</em><MiniSparkline :values="profitSeries" color="#5372c8" /></article>
        <article class="card metric"><small>Metros vendidos</small><strong>{{ data.total.metros.toLocaleString('es-AR') }} m²</strong><em>En el período seleccionado</em></article>
        <article class="card metric"><small>Cuotas pendientes</small><strong>{{ data.cuotasPendientes }}</strong><em>De las ventas filtradas</em></article>
        <article class="card metric"><small>Saldo actual en caja</small><strong>{{ money(data.saldo) }}</strong><em>Indicador global, no afectado por filtros</em></article>
      </div>

      <div class="panel">
        <div class="panel-head"><div><h3>Ventas del período</h3><small>Últimas {{ Math.min(data.ventas.length, 8) }} coincidencias</small></div><RouterLink v-if="auth.can('ventas')" to="/ventas">Ver todas</RouterLink></div>
        <table><thead><tr><th>Cliente</th><th>Producto</th><th>Fecha</th><th>Metros</th><th>Total</th><th>Estado</th></tr></thead><tbody><tr v-for="sale in data.ventas" :key="sale.id"><td><b>{{ sale.cliente }}</b></td><td>{{ sale.tipoCesped }}</td><td>{{ new Date(sale.fechaVenta + 'T00:00:00').toLocaleDateString('es-AR') }}</td><td class="num">{{ sale.cantidadM2 }} m²</td><td class="num">{{ money(sale.precioTotal) }}</td><td><span class="badge" :class="{ warn: sale.estado !== 'Entregada' }">{{ sale.estado }}</span></td></tr></tbody></table>
        <div v-if="!data.ventas.length" class="empty">No hay ventas para los filtros seleccionados.</div>
      </div>
    </template>
  </section>
</template>
