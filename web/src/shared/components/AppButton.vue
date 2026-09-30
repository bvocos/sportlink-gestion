<script setup lang="ts">
import Button from 'primevue/button'
import { computed, useAttrs } from 'vue'

const props = defineProps<{
  tooltip?: string
  label?: string
}>()

const attrs = useAttrs()

const tip = computed(() => {
  const explicit = props.tooltip?.trim()
  if (explicit) return explicit
  const lbl = props.label?.trim()
  if (lbl) return lbl
  const aria = (attrs['aria-label'] as string | undefined)?.trim()
  if (aria) return aria
  const title = (attrs.title as string | undefined)?.trim()
  return title || ''
})
</script>

<template>
  <Button v-bind="$attrs" :label="label" v-tooltip.top="tip">
    <slot />
  </Button>
</template>
