<script setup lang="ts">
import DatePicker from 'primevue/datepicker'
import { computed } from 'vue'
import { isoDate, parseIsoDate } from '@/shared/dateFilters'

const props = defineProps<{
  modelValue?: string | null
  inputId?: string
  placeholder?: string
  disabled?: boolean
  required?: boolean
  invalid?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
  change: []
}>()

const date = computed({
  get: () => parseIsoDate(props.modelValue ?? undefined),
  set: (value: Date | Date[] | (Date | null)[] | null | undefined) => {
    const picked = Array.isArray(value) ? value[0] : value
    emit('update:modelValue', picked instanceof Date ? isoDate(picked) : '')
  },
})

function onDateSelect() {
  emit('change')
}
</script>

<template>
  <DatePicker
    :input-id="inputId"
    v-model="date"
    date-format="dd/mm/yy"
    show-icon
    icon-display="input"
    :placeholder="placeholder"
    :disabled="disabled"
    :required="required"
    :invalid="invalid"
    fluid
    @date-select="onDateSelect"
  />
</template>
