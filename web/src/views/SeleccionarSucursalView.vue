<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import Select from 'primevue/select'
import Message from 'primevue/message'
import AppButton from '@/shared/components/AppButton.vue'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { branchContext } from '@/branchContext'

const router = useRouter()
const sucursales = ref<any[]>([])
const sucursalId = ref('')
const loading = ref(true)
const error = ref('')

onMounted(async () => {
  try {
    sucursales.value = (await http.get('/sucursales')).data.filter((x: any) => x.activo)
  } catch (e) {
    error.value = apiErrorMessage(e, 'No se pudieron cargar las sucursales.')
  } finally {
    loading.value = false
  }
})

async function continueToSystem() {
  const selected = sucursales.value.find(x => x.id === sucursalId.value)
  if (!selected) { error.value = 'Seleccioná una sucursal para continuar.'; return }
  branchContext.select(selected)
  await router.replace('/')
}
</script>

<template>
  <main class="login-page">
    <form class="login-card p-fluid" @submit.prevent="continueToSystem">
      <img src="/brand/sportlink-logo.png" alt="Sportlink by Empire">
      <h1>Seleccionar sucursal</h1>
      <p>Los datos del sistema se mostrarán para esta sucursal y su depósito.</p>
      <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>
      <div class="field">
        <label for="sucursal">Sucursal</label>
        <Select id="sucursal" v-model="sucursalId" :options="sucursales" option-label="nombre" option-value="id"
          placeholder="Seleccioná una sucursal" :loading="loading" />
      </div>
      <AppButton type="submit" label="Ingresar a la sucursal" :disabled="loading || !sucursalId" />
    </form>
  </main>
</template>
