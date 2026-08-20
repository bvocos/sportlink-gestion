<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'

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
const line = ref<SVGPolylineElement | null>(null)

onMounted(async () => {
  await nextTick()
  const element = line.value
  if (!element || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

  const length = element.getTotalLength()
  element.style.strokeDasharray = `${length}`
  element.style.strokeDashoffset = `${length}`
  element.getBoundingClientRect()
  element.classList.add('drawing')
  requestAnimationFrame(() => { element.style.strokeDashoffset = '0' })
})
</script>

<template>
  <svg class="mini-chart" viewBox="0 0 100 40" preserveAspectRatio="none" aria-hidden="true">
    <polygon v-if="area" :points="area" :fill="color" opacity=".10" />
    <polyline ref="line" v-if="points" :points="points" fill="none" :stroke="color" stroke-width="2.4" vector-effect="non-scaling-stroke" />
  </svg>
</template>

<style scoped>
polyline.drawing{transition:stroke-dashoffset 600ms ease-out}
@media(prefers-reduced-motion:reduce){polyline.drawing{transition:none}}
</style>
