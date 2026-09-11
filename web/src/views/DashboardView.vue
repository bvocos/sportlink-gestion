<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Message from 'primevue/message'
import Skeleton from 'primevue/skeleton'
import { http } from '@/shared/api/httpClient'
import { formatCurrency as money, pluralize } from '@/shared/formatters'
import { auth } from '@/auth'
import MiniSparkline from '@/shared/components/MiniSparkline.vue'
import DashboardLineChart from '@/shared/components/DashboardLineChart.vue'
import DashboardBarChart from '@/shared/components/DashboardBarChart.vue'
import DashboardMixChart from '@/shared/components/DashboardMixChart.vue'
import type { DashboardSeriesPoint } from '@/shared/dashboardSeries'

type Summary = { cantidad: number; facturacion: number }
type DashboardData = {
  total: Summary & { metros: number; gananciaNeta: number }
  finalizadas: Summary
  enCurso: Summary
  saldo: number
  cuotasPendientes: number
  entregasPendientes: number
  series: DashboardSeriesPoint[]
  periodo: { desde: string; hasta: string }
}

const emptyData = (): DashboardData => ({
  total: { cantidad: 0, facturacion: 0, metros: 0, gananciaNeta: 0 },
  finalizadas: { cantidad: 0, facturacion: 0 }, enCurso: { cantidad: 0, facturacion: 0 },
  saldo: 0, cuotasPendientes: 0, entregasPendientes: 0, series: [], periodo: { desde: '', hasta: '' },
})
const data = ref<DashboardData>(emptyData())
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const response = await http.get('/dashboard')
    data.value = response.data
  } catch (error: any) {
    loadError.value = error?.response?.data?.errors?.fechas?.[0] || 'No se pudo cargar el inicio del sistema.'
  } finally {
    loading.value = false
  }
}

const series = (field: keyof Omit<DashboardSeriesPoint, 'fecha'>) =>
  computed(() => data.value.series.map((point) => Number(point[field])))
const billingSeries = series('facturacion')
const finishedSeries = series('finalizadas')
const activeSeries = series('enCurso')
const profitSeries = series('gananciaNeta')
const hasChartData = computed(() => data.value.series.some((point) =>
  point.facturacion || point.finalizadas || point.enCurso || point.gananciaNeta,
))
const periodLabel = computed(() => data.value.periodo.desde
  ? `${new Date(data.value.periodo.desde + 'T00:00:00').toLocaleDateString('es-AR')} al ${new Date(data.value.periodo.hasta + 'T00:00:00').toLocaleDateString('es-AR')}`
  : '')
const greeting = computed(() => {
  const hour = new Date().getHours()
  if (hour < 12) return 'Buenos días'
  if (hour < 19) return 'Buenas tardes'
  return 'Buenas noches'
})
const todayLabel = computed(() => new Date().toLocaleDateString('es-AR', { weekday: 'long', day: 'numeric', month: 'long' }))

onMounted(load)
</script>

<template>
  <section class="page dashboard-page">
    <div class="dashboard-brand">
      <div class="page-title flex justify-content-between align-items-center flex-wrap gap-3">
        <div>
          <h2 class="m-0">{{ greeting }}, {{ auth.state.user?.nombre || 'bienvenido' }}</h2>
          <p class="mb-0">Hoy es {{ todayLabel }}. Este es el avance del mes en curso.</p>
        </div>
        <RouterLink v-if="auth.can('ventas')" to="/ventas/nueva">
          <AppButton label="Nueva venta" icon="pi pi-plus" />
        </RouterLink>
      </div>
    </div>

    <Message v-if="loadError" severity="error" class="my-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

      <div v-else-if="loading && !data.series.length" class="mt-3">
        <div class="dashboard-metrics">
          <Skeleton v-for="n in 8" :key="n" height="118px" />
        </div>
        <div class="dashboard-charts">
          <Skeleton v-for="n in 4" :key="'chart-' + n" class="dashboard-chart-skeleton" height="260px" />
        </div>
      </div>

    <template v-else>
      <p class="dashboard-period">Resultados del mes: {{ periodLabel }}</p>
      <div class="dashboard-metrics">
        <article class="card metric chart-metric featured">
          <small>Facturación del mes</small>
          <strong>{{ money(data.total.facturacion) }}</strong>
          <em>{{ pluralize(data.total.cantidad, 'venta', 'ventas') }} no canceladas</em>
          <MiniSparkline :values="billingSeries" />
        </article>
        <article class="card metric chart-metric finished">
          <small>Ventas finalizadas</small>
          <strong>{{ money(data.finalizadas.facturacion) }}</strong>
          <em>{{ pluralize(data.finalizadas.cantidad, 'operación entregada', 'operaciones entregadas') }}</em>
          <MiniSparkline :values="finishedSeries" color="#2f7d4b" />
        </article>
        <article class="card metric chart-metric active">
          <small>Ventas en curso</small>
          <strong>{{ money(data.enCurso.facturacion) }}</strong>
          <em>{{ pluralize(data.enCurso.cantidad, 'operación pendiente', 'operaciones pendientes') }}</em>
          <MiniSparkline :values="activeSeries" color="#c27a22" />
        </article>
        <article class="card metric chart-metric profit">
          <small>Ganancia neta estimada</small>
          <strong :class="{ negative: data.total.gananciaNeta < 0 }">{{ money(data.total.gananciaNeta) }}</strong>
          <em>Sobre las ventas del mes</em>
          <MiniSparkline :values="profitSeries" color="#5372c8" />
        </article>
        <article class="card metric">
          <small>Metros vendidos</small>
          <strong>{{ data.total.metros.toLocaleString('es-AR') }} m²</strong>
          <em>Acumulados durante el mes</em>
        </article>
        <RouterLink v-if="auth.can('cuotas')" class="card metric metric-link" to="/cuotas">
          <small>Cuotas pendientes</small>
          <strong>{{ data.cuotasPendientes }}</strong>
          <em>Ver cuotas pendientes →</em>
        </RouterLink>
        <article v-else class="card metric">
          <small>Cuotas pendientes</small>
          <strong>{{ data.cuotasPendientes }}</strong>
          <em>De las ventas del mes</em>
        </article>
        <RouterLink v-if="auth.can('entregas')" class="card metric metric-link" to="/entregas">
          <small>Próximas entregas</small>
          <strong>{{ data.entregasPendientes }}</strong>
          <em>No se filtra por período · Ver detalle →</em>
        </RouterLink>
        <article v-else class="card metric">
          <small>Próximas entregas</small>
          <strong>{{ data.entregasPendientes }}</strong>
          <em>Entregas pendientes (todas)</em>
        </article>
        <RouterLink v-if="auth.can('caja')" class="card metric metric-link" to="/caja">
          <small>Saldo de caja</small>
          <strong>{{ money(data.saldo) }}</strong>
          <em>Actualizado al momento · Ver caja →</em>
        </RouterLink>
        <article v-else class="card metric">
          <small>Saldo de caja</small>
          <strong>{{ money(data.saldo) }}</strong>
          <em>Actualizado al momento</em>
        </article>
      </div>

      <div v-if="hasChartData" class="dashboard-charts">
        <article class="card dashboard-chart-card">
          <header class="dashboard-chart-head">
            <h3>Evolución de facturación</h3>
            <p>Facturación total, entregadas y en curso por día.</p>
          </header>
          <div class="dashboard-chart-body">
            <DashboardLineChart :series="data.series" mode="billing" />
          </div>
        </article>

        <article class="card dashboard-chart-card">
          <header class="dashboard-chart-head">
            <h3>Composición del mes</h3>
            <p>Finalizadas vs. en curso.</p>
          </header>
          <div class="dashboard-chart-body">
            <DashboardMixChart
              :finalizadas="data.finalizadas.facturacion"
              :en-curso="data.enCurso.facturacion"
            />
          </div>
        </article>

        <article class="card dashboard-chart-card">
          <header class="dashboard-chart-head">
            <h3>Facturación diaria</h3>
            <p>Desglose apilado por día.</p>
          </header>
          <div class="dashboard-chart-body">
            <DashboardBarChart :series="data.series" />
          </div>
        </article>

        <article class="card dashboard-chart-card">
          <header class="dashboard-chart-head">
            <h3>Ganancia neta diaria</h3>
            <p>Estimación por fecha de venta.</p>
          </header>
          <div class="dashboard-chart-body">
            <DashboardLineChart :series="data.series" mode="profit" />
          </div>
        </article>
      </div>

      <article v-else class="card dashboard-chart-empty">
        <p class="font-bold mb-1">Todavía no hay datos para graficar</p>
        <p class="text-color-secondary m-0">Cuando registres ventas en el mes, vas a ver la evolución acá.</p>
      </article>
    </template>
  </section>
</template>
