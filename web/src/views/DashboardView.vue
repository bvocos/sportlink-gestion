<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { http } from '@/shared/api/httpClient'
import { formatCurrency as money, pluralize } from '@/shared/formatters'
import { auth } from '@/auth'
import MiniSparkline from '@/shared/components/MiniSparkline.vue'

type Summary = { cantidad: number; facturacion: number }
type SeriesPoint = { fecha: string; facturacion: number; finalizadas: number; enCurso: number; gananciaNeta: number }
type DashboardData = {
  total: Summary & { metros: number; gananciaNeta: number }
  finalizadas: Summary
  enCurso: Summary
  cuotasPendientes: number
  entregasPendientes: number
  series: SeriesPoint[]
  periodo: { desde: string; hasta: string }
}

const emptyData = (): DashboardData => ({
  total: { cantidad: 0, facturacion: 0, metros: 0, gananciaNeta: 0 },
  finalizadas: { cantidad: 0, facturacion: 0 }, enCurso: { cantidad: 0, facturacion: 0 },
  cuotasPendientes: 0, entregasPendientes: 0, series: [], periodo: { desde: '', hasta: '' }
})
const data = ref<DashboardData>(emptyData())
const loading = ref(false), loadError = ref('')
async function load() {
  loading.value = true; loadError.value = ''
  try {
    const response = await http.get('/dashboard')
    data.value = response.data
  } catch (error: any) {
    loadError.value = error?.response?.data?.errors?.fechas?.[0] || 'No se pudo cargar el inicio del sistema.'
  } finally { loading.value = false }
}
const series = (field: keyof Omit<SeriesPoint, 'fecha'>) => computed(() => data.value.series.map(point => Number(point[field])))
const billingSeries = series('facturacion'), finishedSeries = series('finalizadas'), activeSeries = series('enCurso'), profitSeries = series('gananciaNeta')
const periodLabel = computed(() => data.value.periodo.desde
  ? `${new Date(data.value.periodo.desde + 'T00:00:00').toLocaleDateString('es-AR')} al ${new Date(data.value.periodo.hasta + 'T00:00:00').toLocaleDateString('es-AR')}`
  : '')
onMounted(load)
</script>

<template>
  <section class="page dashboard-page">
    <div class="page-title">
      <div><h2>Inicio</h2><p>Hola, {{ auth.state.user?.nombre || 'bienvenido' }}. Este es el avance del mes en curso.</p></div>
      <RouterLink v-if="auth.can('ventas')" class="btn" to="/ventas">+ Nueva venta</RouterLink>
    </div>

    <div v-if="loadError" class="error load-state">{{ loadError }} <button class="btn secondary compact" @click="load">Reintentar</button></div>
    <div v-else-if="loading && !data.series.length" class="panel loading">Cargando inicio…</div>
    <template v-else>
      <p class="dashboard-period">Resultados del mes: {{ periodLabel }}</p>
      <div class="grid dashboard-metrics">
        <article class="card metric chart-metric"><small>Facturación del mes</small><strong>{{ money(data.total.facturacion) }}</strong><em>{{ pluralize(data.total.cantidad, 'venta', 'ventas') }} no canceladas</em><MiniSparkline :values="billingSeries" /></article>
        <article class="card metric chart-metric finished"><small>Ventas finalizadas</small><strong>{{ money(data.finalizadas.facturacion) }}</strong><em>{{ pluralize(data.finalizadas.cantidad, 'operación entregada', 'operaciones entregadas') }}</em><MiniSparkline :values="finishedSeries" color="#2f7d4b" /></article>
        <article class="card metric chart-metric active"><small>Ventas en curso</small><strong>{{ money(data.enCurso.facturacion) }}</strong><em>{{ pluralize(data.enCurso.cantidad, 'operación pendiente', 'operaciones pendientes') }}</em><MiniSparkline :values="activeSeries" color="#c27a22" /></article>
        <article class="card metric chart-metric profit"><small>Ganancia neta estimada</small><strong :class="{ negative: data.total.gananciaNeta < 0 }">{{ money(data.total.gananciaNeta) }}</strong><em>Sobre las ventas del mes</em><MiniSparkline :values="profitSeries" color="#5372c8" /></article>
        <article class="card metric"><small>Metros vendidos</small><strong>{{ data.total.metros.toLocaleString('es-AR') }} m²</strong><em>Acumulados durante el mes</em></article>
        <RouterLink v-if="auth.can('cuotas')" class="card metric metric-link" to="/cuotas"><small>Cuotas pendientes</small><strong>{{ data.cuotasPendientes }}</strong><em>Ver cuotas pendientes →</em></RouterLink>
        <article v-else class="card metric"><small>Cuotas pendientes</small><strong>{{ data.cuotasPendientes }}</strong><em>De las ventas del mes</em></article>
        <RouterLink v-if="auth.can('entregas')" class="card metric metric-link" to="/entregas"><small>Próximas entregas</small><strong>{{ data.entregasPendientes }}</strong><em>Ver entregas pendientes →</em></RouterLink>
        <article v-else class="card metric"><small>Próximas entregas</small><strong>{{ data.entregasPendientes }}</strong><em>Entregas pendientes</em></article>
      </div>
    </template>
  </section>
</template>
