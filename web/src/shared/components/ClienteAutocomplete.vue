<script setup lang="ts">
import AutoComplete from 'primevue/autocomplete'
import { ref, watch } from 'vue'

interface Cliente {
  id: string
  nombreCompleto: string
  telefono?: string
  localidad?: string
  provincia?: string
}

const props = defineProps<{
  modelValue: string
  clientes: Cliente[]
  invalid?: boolean
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const selected = ref<Cliente | null>(null)
const suggestions = ref<Cliente[]>([])

const norm = (value: string) =>
  value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()

function search(event: { query: string }) {
  const q = norm(event.query.trim())
  suggestions.value = props.clientes
    .filter((cliente) => !q || norm(`${cliente.nombreCompleto} ${cliente.telefono ?? ''} ${cliente.localidad ?? ''}`).includes(q))
    .slice(0, 8)
}

watch(
  [() => props.modelValue, () => props.clientes],
  () => {
    selected.value = props.clientes.find((cliente) => cliente.id === props.modelValue) ?? null
  },
  { immediate: true },
)

watch(selected, (cliente) => {
  const next = cliente?.id ?? ''
  if (next !== props.modelValue) emit('update:modelValue', next)
})
</script>

<template>
  <AutoComplete
    v-model="selected"
    :suggestions="suggestions"
    option-label="nombreCompleto"
    placeholder="Escribí nombre, teléfono o localidad"
    empty-search-message="No se encontraron clientes."
    force-selection
    dropdown
    fluid
    :invalid="invalid"
    @complete="search"
  >
    <template #option="{ option }">
      <div>
        <b>{{ option.nombreCompleto }}</b>
        <small class="block text-color-secondary">
          {{ option.telefono || 'Sin teléfono' }} · {{ option.localidad }}
        </small>
      </div>
    </template>
  </AutoComplete>
</template>
