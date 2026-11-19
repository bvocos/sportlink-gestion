<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/shared/components/AppButton.vue'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { auth } from '@/auth'
import { confirmAction, notify } from '@/shared/uiFeedback'

const items = ref<any[]>([]), total = ref(0), page = ref(1), pageSize = ref(20), buscar = ref('')
const route = useRoute(), router = useRouter()
const loading = ref(false), error = ref(''), visible = ref(false), saving = ref(false)
const editingId = ref<string | null>(null), detail = ref<any | null>(null)
const importSales = ref<any[]>([]), importSalesLoading = ref(false), importSalesLoaded = ref(false), importSalesError = ref('')
const blank = () => ({ codigo: '', numeroDespacho: '', fecha: new Date().toISOString().slice(0, 10), observaciones: '' })
const form = ref(blank())
const dateFormat = new Intl.DateTimeFormat('es-AR', { timeZone: 'UTC' })
const number = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 })
function formatDate(value: string) { return value ? dateFormat.format(new Date(`${value}T00:00:00Z`)) : '—' }
function formatDateTime(value: string) { return value ? new Date(value).toLocaleString('es-AR') : '—' }
function meters(value: number) { return number.format(Number(value ?? 0)) }

async function load() {
  loading.value = true
  try { const { data } = await http.get('/importaciones', { params: { buscar: buscar.value || undefined, page: page.value, pageSize: pageSize.value } }); items.value = data.items; total.value = data.total }
  catch (e) { error.value = apiErrorMessage(e, 'No se pudieron cargar las importaciones.') }
  finally { loading.value = false }
}
function create() { editingId.value = null; form.value = blank(); error.value = ''; visible.value = true }
function edit(row: any) { editingId.value = row.id; form.value = { codigo: row.codigo, numeroDespacho: row.numeroDespacho ?? '', fecha: row.fecha, observaciones: row.observaciones ?? '' }; error.value = ''; visible.value = true }
async function save() { saving.value = true; error.value = ''; try { editingId.value ? await http.put(`/importaciones/${editingId.value}`, form.value) : await http.post('/importaciones', form.value); visible.value = false; await load() } catch (e) { error.value = apiErrorMessage(e, 'No se pudo guardar la importación.') } finally { saving.value = false } }
async function openDetail(row: any) { try { detail.value = (await http.get(`/importaciones/${row.id}`)).data; importSales.value = []; importSalesLoaded.value = false; importSalesError.value = '' } catch (e) { notify(apiErrorMessage(e, 'No se pudo cargar el detalle.')) } }
async function loadImportSales(event: Event) {
  if (!(event.currentTarget as HTMLDetailsElement).open || importSalesLoaded.value || !detail.value) return
  importSalesLoading.value = true; importSalesError.value = ''
  try { importSales.value = (await http.get(`/importaciones/${detail.value.id}/ventas`)).data; importSalesLoaded.value = true }
  catch (e) { importSalesError.value = apiErrorMessage(e, 'No se pudieron cargar las ventas de esta importación.') }
  finally { importSalesLoading.value = false }
}
async function remove(row: any) { if (!await confirmAction({ title: 'Eliminar importación', message: `¿Eliminar ${row.codigo}?`, confirmText: 'Eliminar', danger: true })) return; try { await http.delete(`/importaciones/${row.id}`); await load() } catch (e) { notify(apiErrorMessage(e, 'No se pudo eliminar la importación.')) } }
onMounted(async () => {
  await load()
  const detailId = route.query.detalle as string | undefined
  if (detailId) await openDetail({ id: detailId })
})
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between gap-2 flex-wrap">
      <p class="page-desc text-color-secondary m-0">Códigos de importación y saldos de la sucursal activa.</p>
      <div class="flex gap-2"><AppButton v-if="auth.can('stock', 'crear')" label="Nueva recepción" icon="pi pi-plus" size="small" @click="router.push('/importaciones/recepcion')" /><AppButton v-if="auth.can('stock', 'crear')" label="Solo código" severity="secondary" size="small" @click="create" /></div>
    </div>
    <div class="imp-search mb-3">
      <InputText v-model="buscar" placeholder="Código o N° de despacho" @keyup.enter="page = 1; load()" />
      <AppButton label="Buscar" severity="secondary" @click="page = 1; load()" />
    </div>
    <Message v-if="error && !visible" severity="error" :closable="false">{{ error }}</Message>
    <DataTable :value="items" :loading="loading" paginator :rows="pageSize" :total-records="total" lazy @page="e => { page = e.page + 1; pageSize = e.rows; load() }">
      <Column field="codigo" header="Código" />
      <Column field="numeroDespacho" header="N° despacho" />
      <Column field="fecha" header="Fecha"><template #body="{ data }">{{ formatDate(data.fecha) }}</template></Column>
      <Column field="totalIngresadoM2" header="Ingresado m²" body-class="imp-number"><template #body="{ data }">{{ meters(data.totalIngresadoM2) }}</template></Column>
      <Column field="saldoM2" header="Saldo m²" body-class="imp-number"><template #body="{ data }"><span :class="['imp-balance', { 'imp-balance-negative': data.saldoM2 < 0 }]">{{ meters(data.saldoM2) }}</span></template></Column>
      <Column header="" body-class="imp-actions"><template #body="{ data }"><div class="imp-actions"><AppButton text label="Detalle" @click="openDetail(data)" /><AppButton v-if="auth.can('stock','editar')" text label="Editar" @click="edit(data)" /><AppButton v-if="auth.can('stock','eliminar')" text severity="danger" label="Eliminar" @click="remove(data)" /></div></template></Column>
    </DataTable>
    <Dialog v-model:visible="visible" modal :header="editingId ? 'Editar importación' : 'Nueva importación'" :style="{ width: '40rem', maxWidth: '96vw' }">
      <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>
      <form class="p-fluid" @submit.prevent="save"><div class="field"><label>Código</label><InputText v-model="form.codigo" maxlength="50" required /></div><div class="field"><label>N° de despacho</label><InputText v-model="form.numeroDespacho" maxlength="100" /></div><div class="field"><label>Fecha</label><AppDatePicker v-model="form.fecha" required /></div><div class="field"><label>Observaciones</label><Textarea v-model="form.observaciones" maxlength="1000" rows="3" /></div><div class="flex justify-content-end gap-2"><AppButton type="button" label="Cancelar" severity="secondary" @click="visible = false" /><AppButton type="submit" label="Guardar" :loading="saving" /></div></form>
    </Dialog>
    <Dialog :visible="!!detail" modal header="Detalle de importación"
      :style="{ width: '64rem', maxWidth: '96vw' }"
      :content-style="{ maxHeight: '85vh', overflowY: 'auto' }"
      @update:visible="value => { if (!value) detail = null }">
      <template v-if="detail">
        <div class="flex justify-content-between align-items-center gap-2 flex-wrap">
          <div class="imp-detail-summary flex-grow-1">
            <div><small>Código</small><strong class="block">{{ detail.codigo }}</strong></div>
            <div><small>N° despacho</small><strong class="block">{{ detail.numeroDespacho || '—' }}</strong></div>
            <div><small>Fecha</small><strong class="block">{{ formatDate(detail.fecha) }}</strong></div>
            <div><small>Observaciones</small><span class="block">{{ detail.observaciones || '—' }}</span></div>
          </div>
          <div class="imp-actions">
            <AppButton v-if="auth.can('stock','crear')" label="Agregar mercadería" icon="pi pi-plus" @click="router.push(`/importaciones/${detail.id}/recepcion`)" />
            <AppButton v-if="auth.can('stock','editar')" label="Editar" severity="secondary" @click="edit(detail)" />
          </div>
        </div>

        <h4 class="mb-2">Saldos por producto y color</h4>
        <DataTable :value="detail.saldos" class="imp-detail-table">
          <Column field="depositoNombre" header="Depósito" />
          <Column field="productoNombre" header="Producto" />
          <Column header="Color"><template #body="{ data }">{{ data.color || '—' }}</template></Column>
          <Column header="Ingresado" body-class="imp-number"><template #body="{ data }">{{ meters(data.ingresadoM2) }}</template></Column>
          <Column header="Vendido" body-class="imp-number"><template #body="{ data }">{{ meters(data.vendidoM2) }}</template></Column>
          <Column header="Saldo" body-class="imp-number"><template #body="{ data }"><span :class="['imp-balance', { 'imp-balance-negative': data.saldoM2 < 0 }]">{{ meters(data.saldoM2) }}</span></template></Column>
        </DataTable>

        <h4 v-if="detail.lotes?.length" class="mb-2">Lotes</h4>
        <DataTable v-if="detail.lotes?.length" :value="detail.lotes" class="imp-detail-table">
          <Column field="productoNombre" header="Producto" />
          <Column header="Color"><template #body="{ data }">{{ data.color || '—' }}</template></Column>
          <Column field="estado" header="Estado" />
          <Column header="m²" body-class="imp-number"><template #body="{ data }">{{ meters(data.cantidadM2) }}</template></Column>
          <Column header="Rollos"><template #body="{ data }">{{ data.rollos.map((r: any) => `${r.posicion}: ${r.codigoBarra} (${meters(r.cantidadM2)} m²)`).join(' · ') }}</template></Column>
        </DataTable>

        <h4 class="mb-2">Movimientos (últimos 50)</h4>
        <details>
          <summary>Mostrar u ocultar movimientos</summary>
          <DataTable :value="detail.movimientos" class="imp-detail-table mt-2">
            <Column header="Fecha"><template #body="{ data }">{{ formatDateTime(data.fecha) }}</template></Column>
            <Column field="productoNombre" header="Producto" />
            <Column header="Color"><template #body="{ data }">{{ data.color || '—' }}</template></Column>
            <Column field="tipo" header="Tipo" />
            <Column header="m²" body-class="imp-number"><template #body="{ data }">{{ meters(data.cantidadM2) }}</template></Column>
          </DataTable>
        </details>

        <h4 class="mb-2">Ventas de esta importación</h4>
        <details @toggle="loadImportSales">
          <summary>Mostrar u ocultar ventas</summary>
          <div class="mt-2">
            <Message v-if="importSalesError" severity="error" :closable="false">{{ importSalesError }}</Message>
            <p v-if="importSalesLoading" class="text-color-secondary">Cargando ventas…</p>
            <template v-else-if="importSalesLoaded">
              <DataTable :value="importSales" class="imp-detail-table">
                <Column header="Fecha"><template #body="{ data }">{{ formatDate(data.fechaVenta) }}</template></Column>
                <Column field="cliente" header="Cliente" />
                <Column field="producto" header="Producto" />
                <Column header="Color"><template #body="{ data }">{{ data.color || '—' }}</template></Column>
                <Column header="m²" body-class="imp-number"><template #body="{ data }">{{ meters(data.cantidadM2) }}</template></Column>
              </DataTable>
              <p class="text-right mt-2 mb-0"><strong>Total vendido: {{ meters(importSales.reduce((sum, sale) => sum + Number(sale.cantidadM2 || 0), 0)) }} m²</strong></p>
            </template>
          </div>
        </details>
      </template>
    </Dialog>
  </section>
</template>
