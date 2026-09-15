<script setup lang="ts">
import { computed } from 'vue'
import { Doughnut } from 'vue-chartjs'
import '@/shared/chartSetup'
import { formatCurrency as money } from '@/shared/formatters'

const props = defineProps<{
  finalizadas: number
  enCurso: number
}>()

const chartData = computed(() => ({
  labels: ['Finalizadas', 'En curso'],
  datasets: [{
    data: [props.finalizadas, props.enCurso],
    backgroundColor: ['#2f7d4b', '#c27a22'],
    borderWidth: 0,
    hoverOffset: 6,
  }],
}))

const total = computed(() => props.finalizadas + props.enCurso)

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  cutout: '62%',
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
        label: (context: { label?: string; parsed: number; dataset: { data: number[] } }) => {
          const value = context.parsed
          const sum = context.dataset.data.reduce((acc, item) => acc + item, 0)
          const pct = sum ? Math.round((value / sum) * 100) : 0
          return `${context.label}: ${money(value)} (${pct}%)`
        },
      },
    },
  },
}))
</script>

<template>
  <div class="dashboard-doughnut-wrap">
    <Doughnut :data="chartData" :options="chartOptions" class="dashboard-doughnut-chart" />
    <div class="dashboard-doughnut-center">
      <small>Total</small>
      <strong>{{ money(total) }}</strong>
    </div>
  </div>
</template>
