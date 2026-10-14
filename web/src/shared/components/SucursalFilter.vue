<script setup lang="ts">
import { onMounted, ref } from "vue";
import { http } from "@/shared/api/httpClient";
import { auth } from "@/auth";
import { branchContext } from "@/branchContext";

defineProps<{ modelValue: string }>();
const emit = defineEmits<{ "update:modelValue": [value: string]; change: [] }>();
const sucursales = ref<any[]>([]);

onMounted(async () => {
  if (auth.state.user?.rol !== 'Administrador') {
    sucursales.value = auth.state.user?.sucursalId
      ? [{ id: auth.state.user.sucursalId, nombre: auth.state.user.sucursalNombre ?? 'Sucursal asignada' }]
      : [];
    return;
  }
  const activeId = branchContext.state.active?.id ?? auth.state.user?.sucursalId;
  sucursales.value = (await http.get("/sucursales")).data.filter((x: any) => x.activo && x.id === activeId);
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
      <option value="">Sucursal activa</option>
      <option v-for="sucursal in sucursales" :key="sucursal.id" :value="sucursal.id">
        {{ sucursal.nombre }}
      </option>
    </select>
  </div>
</template>
