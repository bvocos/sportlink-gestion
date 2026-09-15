<script setup lang="ts">
import AutoComplete from 'primevue/autocomplete'
import { computed, ref, watch } from 'vue'

interface Option {
  id: string
  nombre: string
}

const props = defineProps<{
  modelValue: string
  options: Option[]
  placeholder?: string
  disabled?: boolean
  invalid?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
  select: [value: Option]
}>()

const selected = ref<Option | null>(null)
const suggestions = ref<Option[]>([])

const norm = (value: string) =>
  value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()

const totalMatches = computed(() => {
  const q = norm(selected.value?.nombre ?? '')
  return props.options.filter((option) => !q || norm(option.nombre).includes(q)).length
})

function search(event: { query: string }) {
  const q = norm(event.query.trim())
  suggestions.value = props.options
    .filter((option) => !q || norm(option.nombre).includes(q))
    .slice(0, 12)
}

watch(
  [() => props.modelValue, () => props.options],
  () => {
    selected.value = props.options.find((option) => option.id === props.modelValue) ?? null
  },
  { immediate: true },
)

watch(selected, (option) => {
  const next = option?.id ?? ''
  if (next !== props.modelValue) emit('update:modelValue', next)
})

function onSelect(event: { value: Option }) {
  emit('select', event.value)
}
</script>

<template>
  <AutoComplete
    v-model="selected"
    :suggestions="suggestions"
    option-label="nombre"
    :placeholder="placeholder || 'Buscar localidad'"
    empty-search-message="No se encontraron coincidencias."
    force-selection
    dropdown
    fluid
    :disabled="disabled"
    :invalid="invalid"
    @complete="search"
    @option-select="onSelect"
  >
    <template #footer v-if="totalMatches > suggestions.length">
      <small class="text-color-secondary px-2 py-1 block">
        Mostrando {{ suggestions.length }} de {{ totalMatches }} — seguí escribiendo para afinar.
      </small>
    </template>
  </AutoComplete>
</template>
