<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import PresupuestoPdfPreview from '@/shared/components/PresupuestoPdfPreview.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Message from 'primevue/message'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faDownload, faEye, faPen, faTrash } from '@/shared/icons'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { confirmAction, notify } from '@/shared/uiFeedback'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS } from '@/shared/tablePagination'

const items = ref<any[]>([])
const loading = ref(true)
const error = ref('')
const previewUrl = ref('')
const previewTitle = ref('')
const previewLoading = ref(false)
const selectedId = ref<string | null>(null)
const previewPanelRef = ref<HTMLElement | null>(null)

const usd = (v: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'USD' }).format(v)

async function load() {
  loading.value = true
  try {
    items.value = (await http.get('/presupuestos')).data
  } catch (e) {
    error.value = apiErrorMessage(e, 'No se pudieron cargar los presupuestos.')
  } finally {
    loading.value = false
  }
}

async function pdf(x: any) {
  const r = await http.get(`/presupuestos/${x.id}/pdf`, { responseType: 'blob' })
  const url = URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }))
  const a = document.createElement('a')
  a.href = url
  a.download = `Sportlink-Presupuesto-${String(x.numero).padStart(5, '0')}.pdf`
  a.click()
  URL.revokeObjectURL(url)
}

function closePreview() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  previewTitle.value = ''
  previewLoading.value = false
  selectedId.value = null
}

function rowClass(data: any) {
  return data.id === selectedId.value ? 'presupuesto-row-selected' : ''
}

async function preview(x: any) {
  if (selectedId.value === x.id && !previewLoading.value) {
    closePreview()
    return
  }

  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  selectedId.value = x.id
  previewTitle.value = `Presupuesto #${String(x.numero).padStart(5, '0')}`
  previewLoading.value = true

  await nextTick()
  previewPanelRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })

  try {
    const r = await http.get(`/presupuestos/${x.id}/pdf`, { responseType: 'blob' })
    previewUrl.value = URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }))
  } catch (e) {
    closePreview()
    notify(apiErrorMessage(e, 'No se pudo abrir la vista previa.'))
  } finally {
    previewLoading.value = false
  }
}

async function remove(x: any) {
  if (!await confirmAction({ title: 'Eliminar presupuesto', message: `¿Eliminar el presupuesto #${String(x.numero).padStart(5, '0')}?`, confirmText: 'Eliminar', danger: true })) return
  try {
    await http.delete(`/presupuestos/${x.id}`)
    if (selectedId.value === x.id) closePreview()
    await load()
  } catch (e) {
    notify(apiErrorMessage(e, 'No se pudo eliminar.'))
  }
}

onMounted(load)
onBeforeUnmount(closePreview)
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Propuestas comerciales en USD listas para enviar.</p>
      <RouterLink to="/presupuestos/nuevo">
        <AppButton label="Nuevo presupuesto" icon="pi pi-plus" />
      </RouterLink>
    </div>

    <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>
    <div v-else class="table-panel">
      <DataTable
        :value="items"
        :loading="loading"
        :row-class="rowClass"
        paginator
        :rows="TABLE_ROWS"
        :rows-per-page-options="TABLE_ROWS_OPTIONS"
        striped-rows
      >
        <template #empty>
          <div class="text-center py-5">
            <p class="font-bold mb-2">Todavía no hay presupuestos</p>
            <p class="text-color-secondary mb-3">Armá una propuesta en USD lista para enviar al cliente.</p>
            <RouterLink to="/presupuestos/nuevo">
              <AppButton label="Nuevo presupuesto" icon="pi pi-plus" />
            </RouterLink>
          </div>
        </template>
        <Column header="N.º">
          <template #body="{ data }"><b>#{{ String(data.numero).padStart(5, '0') }}</b></template>
        </Column>
        <Column header="Fecha">
          <template #body="{ data }">{{ new Date(data.fecha + 'T00:00:00').toLocaleDateString('es-AR') }}</template>
        </Column>
        <Column field="cliente" header="Cliente" body-class="cell-wrap" />
        <Column header="Contado" body-class="cell-num">
          <template #body="{ data }">{{ usd(data.totalContado) }}</template>
        </Column>
        <Column header="Financiado" body-class="cell-num">
          <template #body="{ data }">{{ usd(data.totalFinanciado) }}</template>
        </Column>
        <Column header="Vigencia">
          <template #body>24 horas</template>
        </Column>
        <Column header="" body-class="cell-actions">
          <template #body="{ data }">
            <div class="flex gap-1">
              <AppButton
                text
                rounded
                :severity="selectedId === data.id ? 'success' : 'secondary'"
                title="Visualizar presupuesto"
                @click="preview(data)"
              >
                <AppIcon :icon="faEye" />
              </AppButton>
              <AppButton text rounded severity="secondary" title="Descargar PDF" @click="pdf(data)">
                <AppIcon :icon="faDownload" />
              </AppButton>
              <RouterLink :to="`/presupuestos/${data.id}/editar`">
                <AppButton text rounded severity="secondary" title="Editar presupuesto">
                  <AppIcon :icon="faPen" />
                </AppButton>
              </RouterLink>
              <AppButton text rounded severity="danger" title="Eliminar presupuesto" @click="remove(data)">
                <AppIcon :icon="faTrash" />
              </AppButton>
            </div>
          </template>
        </Column>
      </DataTable>
    </div>

    <div v-if="selectedId" ref="previewPanelRef">
      <PresupuestoPdfPreview
        :title="previewTitle"
        :url="previewUrl"
        :loading="previewLoading"
        @close="closePreview"
      />
    </div>
  </section>
</template>
