<script setup lang="ts">
import { onMounted, ref } from 'vue'
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
const loading = ref(false), error = ref(''), visible = ref(false), saving = ref(false)
const editingId = ref<string | null>(null), detail = ref<any | null>(null)
const blank = () => ({ codigo: '', numeroDespacho: '', fecha: new Date().toISOString().slice(0, 10), observaciones: '' })
const form = ref(blank())

async function load() {
  loading.value = true
  try { const { data } = await http.get('/importaciones', { params: { buscar: buscar.value || undefined, page: page.value, pageSize: pageSize.value } }); items.value = data.items; total.value = data.total }
  catch (e) { error.value = apiErrorMessage(e, 'No se pudieron cargar las importaciones.') }
  finally { loading.value = false }
}
function create() { editingId.value = null; form.value = blank(); error.value = ''; visible.value = true }
function edit(row: any) { editingId.value = row.id; form.value = { codigo: row.codigo, numeroDespacho: row.numeroDespacho ?? '', fecha: row.fecha, observaciones: row.observaciones ?? '' }; error.value = ''; visible.value = true }
async function save() { saving.value = true; error.value = ''; try { editingId.value ? await http.put(`/importaciones/${editingId.value}`, form.value) : await http.post('/importaciones', form.value); visible.value = false; await load() } catch (e) { error.value = apiErrorMessage(e, 'No se pudo guardar la importación.') } finally { saving.value = false } }
async function openDetail(row: any) { try { detail.value = (await http.get(`/importaciones/${row.id}`)).data } catch (e) { notify(apiErrorMessage(e, 'No se pudo cargar el detalle.')) } }
async function remove(row: any) { if (!await confirmAction({ title: 'Eliminar importación', message: `¿Eliminar ${row.codigo}?`, confirmText: 'Eliminar', danger: true })) return; try { await http.delete(`/importaciones/${row.id}`); await load() } catch (e) { notify(apiErrorMessage(e, 'No se pudo eliminar la importación.')) } }
onMounted(load)
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between gap-2 flex-wrap">
      <p class="page-desc text-color-secondary m-0">Códigos de importación y saldos de la sucursal activa.</p>
      <AppButton v-if="auth.can('stock', 'crear')" label="Nueva importación" icon="pi pi-plus" size="small" @click="create" />
    </div>
    <div class="flex gap-2 mb-3">
      <InputText v-model="buscar" placeholder="Código o N° de despacho" @keyup.enter="page = 1; load()" />
      <AppButton label="Buscar" severity="secondary" @click="page = 1; load()" />
    </div>
    <Message v-if="error && !visible" severity="error" :closable="false">{{ error }}</Message>
    <DataTable :value="items" :loading="loading" paginator :rows="pageSize" :total-records="total" lazy @page="e => { page = e.page + 1; pageSize = e.rows; load() }">
      <Column field="codigo" header="Código" />
      <Column field="numeroDespacho" header="N° despacho" />
      <Column field="fecha" header="Fecha" />
      <Column field="totalIngresadoM2" header="Ingresado m²" />
      <Column field="saldoM2" header="Saldo m²" />
      <Column header=""><template #body="{ data }"><div class="flex gap-1"><AppButton text label="Detalle" @click="openDetail(data)" /><AppButton v-if="auth.can('stock','editar')" text label="Editar" @click="edit(data)" /><AppButton v-if="auth.can('stock','eliminar')" text severity="danger" label="Eliminar" @click="remove(data)" /></div></template></Column>
    </DataTable>
    <Dialog v-model:visible="visible" modal :header="editingId ? 'Editar importación' : 'Nueva importación'" :style="{ width: '36rem', maxWidth: '95vw' }">
      <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>
      <form class="p-fluid" @submit.prevent="save"><div class="field"><label>Código</label><InputText v-model="form.codigo" maxlength="50" required /></div><div class="field"><label>N° de despacho</label><InputText v-model="form.numeroDespacho" maxlength="100" /></div><div class="field"><label>Fecha</label><AppDatePicker v-model="form.fecha" required /></div><div class="field"><label>Observaciones</label><Textarea v-model="form.observaciones" maxlength="1000" rows="3" /></div><div class="flex justify-content-end gap-2"><AppButton type="button" label="Cancelar" severity="secondary" @click="visible = false" /><AppButton type="submit" label="Guardar" :loading="saving" /></div></form>
    </Dialog>
    <Dialog :visible="!!detail" modal header="Detalle de importación" :style="{ width: '52rem', maxWidth: '95vw' }" @update:visible="value => { if (!value) detail = null }">
      <template v-if="detail"><h3>{{ detail.codigo }}</h3><p>{{ detail.numeroDespacho || 'Sin despacho' }} · {{ detail.fecha }}</p><DataTable :value="detail.saldos"><Column field="depositoNombre" header="Depósito" /><Column field="productoNombre" header="Producto" /><Column field="saldoM2" header="Saldo m²" /></DataTable></template>
    </Dialog>
  </section>
</template>
