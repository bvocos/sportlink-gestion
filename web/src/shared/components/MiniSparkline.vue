<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{ values: number[]; color?: string }>(), { color: '#337a4e' })
const points = computed(() => {
  if (!props.values.length) return ''
  const min = Math.min(...props.values, 0)
  const max = Math.max(...props.values, 0)
  const span = max - min || 1
  return props.values.map((value, index) => {
    const x = props.values.length === 1 ? 50 : index * 100 / (props.values.length - 1)
    const y = 35 - ((value - min) / span) * 29
    return `${x.toFixed(2)},${y.toFixed(2)}`
  }).join(' ')
})
const area = computed(() => points.value ? `0,40 ${points.value} 100,40` : '')
</script>

<template>
  <svg class="mini-chart" viewBox="0 0 100 40" preserveAspectRatio="none" aria-hidden="true">
    <polygon v-if="area" :points="area" :fill="color" opacity=".10" />
    <polyline v-if="points" :points="points" fill="none" :stroke="color" stroke-width="2.4" vector-effect="non-scaling-stroke" />
  </svg>
</template>
