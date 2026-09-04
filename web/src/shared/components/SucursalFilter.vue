<script setup lang="ts">
import { onMounted, ref } from "vue";
import { http } from "@/shared/api/httpClient";

defineProps<{ modelValue: string }>();
const emit = defineEmits<{ "update:modelValue": [value: string]; change: [] }>();
const sucursales = ref<any[]>([]);

onMounted(async () => {
  sucursales.value = (await http.get("/sucursales")).data.filter((x: any) => x.activo);
});

function changed(event: Event) {
  emit("update:modelValue", (event.target as HTMLSelectElement).value);
  emit("change");
}
</script>

<template>
  <div class="field">
    <label>Sucursal</label>
    <select :value="modelValue" @change="changed">
      <option value="">Todas las sucursales</option>
      <option v-for="sucursal in sucursales" :key="sucursal.id" :value="sucursal.id">
        {{ sucursal.nombre }}
      </option>
    </select>
  </div>
</template>
