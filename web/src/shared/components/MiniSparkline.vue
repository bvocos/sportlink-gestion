<script setup lang="ts">
import { computed } from 'vue'
import { Line } from 'vue-chartjs'
import '@/shared/chartSetup'

const props = withDefaults(defineProps<{ values: number[]; color?: string }>(), { color: '#337a4e' })

const chartData = computed(() => ({
  labels: props.values.map((_, index) => index),
  datasets: [{
    data: props.values,
    borderColor: props.color,
    backgroundColor: `${props.color}1a`,
    fill: true,
    tension: 0.35,
    pointRadius: 0,
    borderWidth: 2,
  }],
}))

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  animation: { duration: 600 },
  plugins: { legend: { display: false }, tooltip: { enabled: false } },
  scales: {
    x: { display: false },
    y: { display: false },
  },
}
</script>

<template>
  <Line :data="chartData" :options="chartOptions" class="mini-chart" />
</template>
