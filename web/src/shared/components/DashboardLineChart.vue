<script setup lang="ts">
import { computed } from 'vue'
import { Line } from 'vue-chartjs'
import '@/shared/chartSetup'
import { formatCurrency as money } from '@/shared/formatters'
import type { DashboardSeriesPoint } from '@/shared/dashboardSeries'

const props = withDefaults(defineProps<{
  series: DashboardSeriesPoint[]
  mode?: 'billing' | 'profit'
}>(), {
  mode: 'billing',
})

const labels = computed(() =>
  props.series.map((point) =>
    new Date(`${point.fecha}T00:00:00`).toLocaleDateString('es-AR', { day: '2-digit', month: 'short' }),
  ),
)

const chartData = computed(() => {
  if (props.mode === 'profit') {
    return {
      labels: labels.value,
      datasets: [{
        label: 'Ganancia neta',
        data: props.series.map((point) => point.gananciaNeta),
        borderColor: '#5372c8',
        backgroundColor: '#5372c824',
        fill: true,
        tension: 0.35,
        pointRadius: 2,
        pointHoverRadius: 4,
        borderWidth: 2,
      }],
    }
  }

  return {
    labels: labels.value,
    datasets: [
      {
        label: 'Facturación',
        data: props.series.map((point) => point.facturacion),
        borderColor: '#337a4e',
        backgroundColor: '#337a4e14',
        fill: false,
        tension: 0.35,
        pointRadius: 2,
        pointHoverRadius: 4,
        borderWidth: 2,
      },
      {
        label: 'Finalizadas',
        data: props.series.map((point) => point.finalizadas),
        borderColor: '#2f7d4b',
        backgroundColor: '#2f7d4b14',
        fill: false,
        tension: 0.35,
        pointRadius: 0,
        borderWidth: 2,
      },
      {
        label: 'En curso',
        data: props.series.map((point) => point.enCurso),
        borderColor: '#c27a22',
        backgroundColor: '#c27a2214',
        fill: false,
        tension: 0.35,
        pointRadius: 0,
        borderWidth: 2,
      },
    ],
  }
})

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  plugins: {
    legend: {
      position: 'bottom' as const,
      labels: {
        usePointStyle: true,
        boxWidth: 7,
        padding: 10,
        font: { size: 10, weight: 'bold' as const },
      },
    },
    tooltip: {
      callbacks: {
        label: (context: { dataset: { label?: string }; parsed: { y: number | null } }) =>
          `${context.dataset.label}: ${money(context.parsed.y ?? 0)}`,
      },
    },
  },
  scales: {
    x: {
      grid: { display: false },
      ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8, font: { size: 10 } },
    },
    y: {
      grid: { color: '#e8eee6' },
      ticks: {
        callback: (value: string | number) => money(Number(value)),
        maxTicksLimit: 5,
        font: { size: 10 },
      },
    },
  },
}))
</script>

<template>
  <Line :data="chartData" :options="chartOptions" class="dashboard-line-chart" />
</template>
