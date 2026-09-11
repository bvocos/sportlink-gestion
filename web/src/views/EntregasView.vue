<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faCalendarDays } from '@/shared/icons'
import { http } from '@/shared/api/httpClient'

const items = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try { items.value = (await http.get('/ventas/proximas-entregas')).data }
  catch { loadError.value = 'No se pudieron cargar las próximas entregas.' }
  finally { loading.value = false }
}

onMounted(load)
const totalM2 = computed(() => items.value.reduce((sum, x) => sum + x.cantidadM2, 0))
function timing(days: number) {
  return days < 0 ? `Atrasada ${Math.abs(days)} días` : days === 0 ? 'Entrega hoy' : days === 1 ? 'Mañana' : `En ${days} días`
}
</script>

<template>
  <section class="page">
    <div class="page-toolbar mb-3">
      <p class="page-desc text-color-secondary m-0">Agenda rápida de ventas futuras y metros comprometidos.</p>
    </div>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

    <div v-else-if="loading" class="grid delivery-metrics" role="status" aria-label="Cargando entregas">
      <Panel v-for="n in 2" :key="n" class="col-12 md:col-6">
        <Skeleton height="5rem" />
      </Panel>
    </div>

    <template v-else>
      <div class="grid delivery-metrics mb-4">
        <Panel class="col-12 md:col-6 card metric">
          <small>Entregas pendientes</small>
          <strong class="block text-3xl">{{ items.length }}</strong>
          <em class="text-color-secondary">Operaciones programadas</em>
        </Panel>
        <Panel class="col-12 md:col-6 card metric">
          <small>Superficie comprometida</small>
          <strong class="block text-3xl">{{ totalM2 }} m²</strong>
          <em class="text-color-secondary">Para planificación y logística</em>
        </Panel>
      </div>

      <div class="delivery-list">
        <Panel v-for="item in items" :key="item.id" class="delivery-card mb-3">
          <div class="flex flex-wrap align-items-center justify-content-between gap-3">
            <div class="delivery-date flex align-items-center gap-3">
              <AppIcon :icon="faCalendarDays" />
              <div>
                <small class="text-color-secondary">FECHA ESTIMADA</small>
                <strong class="block">
                  {{ new Date(item.fechaEntregaEstimada + 'T00:00:00').toLocaleDateString('es-AR', { day: '2-digit', month: 'long', year: 'numeric' }) }}
                </strong>
              </div>
            </div>
            <Tag :value="timing(item.diasRestantes)" :severity="item.diasRestantes <= 3 ? 'warn' : 'info'" />
          </div>
          <div class="delivery-info grid mt-3">
            <div class="col-12 md:col-4">
              <small class="text-color-secondary">Cliente</small>
              <b class="block">{{ item.cliente }}</b>
            </div>
            <div class="col-12 md:col-4">
              <small class="text-color-secondary">Producto</small>
              <b class="block">{{ item.tipoCesped }}</b>
            </div>
            <div class="col-12 md:col-4">
              <small class="text-color-secondary">Superficie</small>
              <b class="block">{{ item.cantidadM2 }} m²</b>
            </div>
          </div>
          <p v-if="item.observaciones" class="delivery-note mt-3 mb-0">{{ item.observaciones }}</p>
        </Panel>

        <Panel v-if="!items.length" class="text-center py-5">
          <p class="font-bold mb-2">No hay entregas programadas</p>
          <p class="text-color-secondary mb-3">Cuando una venta quede con fecha futura, va a aparecer acá.</p>
          <RouterLink to="/ventas/nueva">
            <AppButton label="Nueva venta" icon="pi pi-plus" />
          </RouterLink>
        </Panel>
      </div>
    </template>
  </section>
</template>
