<script setup lang="ts">
import { computed } from 'vue'
import { Bar } from 'vue-chartjs'
import '@/shared/chartSetup'
import { formatCurrency as money } from '@/shared/formatters'
import type { DashboardSeriesPoint } from '@/shared/dashboardSeries'

const props = defineProps<{ series: DashboardSeriesPoint[] }>()

const labels = computed(() =>
  props.series.map((point) =>
    new Date(`${point.fecha}T00:00:00`).toLocaleDateString('es-AR', { day: '2-digit', month: 'short' }),
  ),
)

const chartData = computed(() => ({
  labels: labels.value,
  datasets: [
    {
      label: 'Finalizadas',
      data: props.series.map((point) => point.finalizadas),
      backgroundColor: '#2f7d4bcc',
      borderRadius: 4,
      stack: 'ventas',
    },
    {
      label: 'En curso',
      data: props.series.map((point) => point.enCurso),
      backgroundColor: '#c27a22cc',
      borderRadius: 4,
      stack: 'ventas',
    },
  ],
}))

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
        footer: (items: Array<{ parsed: { y: number | null } }>) => {
          const total = items.reduce((sum, item) => sum + (item.parsed.y ?? 0), 0)
          return `Total: ${money(total)}`
        },
      },
    },
  },
  scales: {
    x: {
      stacked: true,
      grid: { display: false },
      ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8, font: { size: 10 } },
    },
    y: {
      stacked: true,
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
  <Bar :data="chartData" :options="chartOptions" class="dashboard-bar-chart" />
</template>
