<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { pluralize } from '@/shared/formatters'

const items = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    items.value = (await http.get('/ventas/proximas-entregas')).data
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, 'No se pudieron cargar las próximas entregas.')
  } finally {
    loading.value = false
  }
}

onMounted(load)

const totalM2 = computed(() =>
  items.value.reduce((sum, item) => sum + Number(item.cantidadM2 ?? 0), 0),
)

function timing(days: number) {
  if (days < 0) return `Atrasada ${Math.abs(days)} días`
  if (days === 0) return 'Entrega hoy'
  if (days === 1) return 'Mañana'
  return `En ${days} días`
}

function timingSeverity(days: number) {
  if (days < 0) return 'danger'
  if (days <= 3) return 'warn'
  return 'info'
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString('es-AR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-2">
      <p class="page-desc text-color-secondary m-0">Agenda rápida de ventas futuras y metros comprometidos.</p>
      <RouterLink to="/ventas/nueva">
        <AppButton label="Nueva venta" icon="pi pi-plus" size="small" />
      </RouterLink>
    </div>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

    <template v-else>
      <div class="summary-metrics">
        <article class="card metric">
          <small>Entregas pendientes</small>
          <strong>{{ items.length }}</strong>
          <em>Operaciones programadas</em>
        </article>
        <article class="card metric">
          <small>Superficie comprometida</small>
          <strong>{{ totalM2.toLocaleString('es-AR') }} m²</strong>
          <em>Para planificación y logística</em>
        </article>
      </div>

      <div class="table-panel">
        <p class="page-desc text-color-secondary m-0 mb-2 px-1">
          {{ pluralize(items.length, 'entrega programada', 'entregas programadas') }}
        </p>
        <DataTable :value="items" :loading="loading" striped-rows>
          <template #empty>
            <div class="text-center py-5">
              <p class="font-bold mb-2">No hay entregas programadas</p>
              <p class="text-color-secondary mb-3">Cuando una venta quede con fecha futura, va a aparecer acá.</p>
              <RouterLink to="/ventas/nueva">
                <AppButton label="Nueva venta" icon="pi pi-plus" size="small" />
              </RouterLink>
            </div>
          </template>
          <Column header="Fecha estimada">
            <template #body="{ data }">
              <b>{{ formatDate(data.fechaEntregaEstimada) }}</b>
            </template>
          </Column>
          <Column header="Plazo">
            <template #body="{ data }">
              <Tag :value="timing(data.diasRestantes)" :severity="timingSeverity(data.diasRestantes)" />
            </template>
          </Column>
          <Column header="Cliente" body-class="cell-wrap">
            <template #body="{ data }"><b>{{ data.cliente }}</b></template>
          </Column>
          <Column header="Producto" body-class="cell-wrap">
            <template #body="{ data }">{{ data.tipoCesped }}</template>
          </Column>
          <Column header="Superficie" body-class="cell-num">
            <template #body="{ data }">{{ data.cantidadM2 }} m²</template>
          </Column>
          <Column header="Observaciones" body-class="cell-wrap">
            <template #body="{ data }">{{ data.observaciones || '—' }}</template>
          </Column>
        </DataTable>
      </div>
    </template>
  </section>
</template>
