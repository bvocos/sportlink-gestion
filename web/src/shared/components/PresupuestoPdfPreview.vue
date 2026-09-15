<script setup lang="ts">
import AppButton from '@/shared/components/AppButton.vue'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faXmark } from '@/shared/icons'

defineProps<{
  title: string
  url: string
  loading?: boolean
}>()

defineEmits<{ close: [] }>()
</script>

<template>
  <article class="card pdf-preview-panel">
    <header class="pdf-preview-head">
      <div>
        <h3>{{ title }}</h3>
        <small>Vista previa del PDF — así se verá al descargar o enviar</small>
      </div>
      <AppButton text rounded severity="secondary" title="Cerrar vista previa" @click="$emit('close')">
        <AppIcon :icon="faXmark" />
      </AppButton>
    </header>
    <div class="pdf-preview-body">
      <div v-if="loading" class="pdf-preview-loading">Generando vista previa…</div>
      <iframe v-else-if="url" :src="url" :title="title" class="pdf-preview-frame" />
    </div>
  </article>
</template>
