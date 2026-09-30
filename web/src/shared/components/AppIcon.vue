<script setup lang="ts">
import type { AppIconClass } from '@/shared/icons'

defineProps<{ icon: AppIconClass; fixedWidth?: boolean }>()
</script>

<template>
  <i :class="[icon, { 'fa-fw': fixedWidth }]" aria-hidden="true" />
</template>
