<script setup lang="ts">
import { ref, onMounted } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import Textarea from 'primevue/textarea'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faPen, faPowerOff, faTrash } from '@/shared/icons'
import { http } from '@/shared/api/httpClient'
import { formatCurrency as money } from '@/shared/formatters'
import { confirmAction, notify } from '@/shared/uiFeedback'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS } from '@/shared/tablePagination'

const items = ref<any[]>([])
const show = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const colorInput = ref('')
const form = ref({
  nombre: '',
  descripcion: '',
  descripcionPresupuesto: '',
  especificacionesPresupuesto: '',
  fichaTecnicaUrl: '',
  precioVentaM2: 0,
  precioContadoM2: 0,
  precioFinanciadoM2: 0,
  costoM2: 0,
  colores: [] as string[],
  activo: true,
})

async function load() {
  items.value = (await http.get('/maestros/tipos-cesped')).data
}
function create() {
  editingId.value = null
  colorInput.value = ''
  form.value = {
    nombre: '',
    descripcion: '',
    descripcionPresupuesto: '',
    especificacionesPresupuesto: '',
    fichaTecnicaUrl: '',
    precioVentaM2: 0,
    precioContadoM2: 0,
    precioFinanciadoM2: 0,
    costoM2: 0,
    colores: [],
    activo: true,
  }
  error.value = ''
  show.value = true
}
function edit(x: any) {
  editingId.value = x.id
  colorInput.value = ''
  form.value = {
    nombre: x.nombre,
    descripcion: x.descripcion ?? '',
    descripcionPresupuesto: x.descripcionPresupuesto ?? '',
    especificacionesPresupuesto: x.especificacionesPresupuesto ?? '',
    fichaTecnicaUrl: x.fichaTecnicaUrl ?? '',
    precioVentaM2: x.precioVentaM2,
    precioContadoM2: x.precioContadoM2 ?? x.precioVentaM2,
    precioFinanciadoM2: x.precioFinanciadoM2 ?? x.precioVentaM2,
    costoM2: x.costoM2,
    colores: [...(x.colores ?? [])],
    activo: x.activo,
  }
  error.value = ''
  show.value = true
}
function addColor() {
  const color = colorInput.value.trim()
  if (!color || form.value.colores.some(x => x.toLocaleLowerCase() === color.toLocaleLowerCase())) return
  form.value.colores.push(color)
  colorInput.value = ''
}
function removeColor(index: number) { form.value.colores.splice(index, 1) }
async function save() {
  try {
    editingId.value
      ? await http.put(`/maestros/tipos-cesped/${editingId.value}`, form.value)
      : await http.post('/maestros/tipos-cesped', form.value)
    show.value = false
    await load()
  } catch (e: any) {
    error.value =
      e.response?.data?.message ??
      e.response?.data?.detail ??
      'No se pudo guardar.'
  }
}
async function toggle(x: any) {
  await http.put(`/maestros/tipos-cesped/${x.id}`, {
    nombre: x.nombre,
    descripcion: x.descripcion,
    descripcionPresupuesto: x.descripcionPresupuesto,
    especificacionesPresupuesto: x.especificacionesPresupuesto,
    fichaTecnicaUrl: x.fichaTecnicaUrl,
    precioVentaM2: x.precioVentaM2,
    precioContadoM2: x.precioContadoM2,
    precioFinanciadoM2: x.precioFinanciadoM2,
    costoM2: x.costoM2,
    colores: x.colores ?? [],
    activo: !x.activo,
  })
  await load()
}
async function remove(x: any) {
  if (await confirmAction({ title: 'Eliminar tipo de césped', message: `¿Querés eliminar ${x.nombre}?`, confirmText: 'Eliminar', danger: true }))
    try {
      await http.delete(`/maestros/tipos-cesped/${x.id}`)
      await load()
    } catch (e: any) {
      notify(e.response?.data?.message ?? 'No se pudo eliminar.')
    }
}
onMounted(load)
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Productos, precios y costos maestros.</p>
      <AppButton label="Tipo de césped" icon="pi pi-plus" @click="create" />
    </div>

    <div class="table-panel">
      <DataTable
        :value="items"
        paginator
        :rows="TABLE_ROWS"
        :rows-per-page-options="TABLE_ROWS_OPTIONS"
        striped-rows
      >
        <Column header="Nombre">
          <template #body="{ data }"><b>{{ data.nombre }}</b></template>
        </Column>
        <Column header="Descripción">
          <template #body="{ data }">{{ data.descripcion || '—' }}</template>
        </Column>
        <Column header="Venta / m²">
          <template #body="{ data }">{{ money(data.precioVentaM2) }}</template>
        </Column>
        <Column header="Costo / m²">
          <template #body="{ data }">{{ money(data.costoM2) }}</template>
        </Column>
        <Column header="Contado / m²">
          <template #body="{ data }">{{ money(data.precioContadoM2) }}</template>
        </Column>
        <Column header="Financiado / m²">
          <template #body="{ data }">{{ money(data.precioFinanciadoM2) }}</template>
        </Column>
        <Column header="Colores">
          <template #body="{ data }">
            <div class="flex flex-wrap gap-1">
              <Tag v-for="color in data.colores" :key="color" :value="color" severity="secondary" />
              <span v-if="!data.colores?.length">—</span>
            </div>
          </template>
        </Column>
        <Column header="Estado">
          <template #body="{ data }">
            <Tag :value="data.activo ? 'Activo' : 'Inactivo'" :severity="data.activo ? 'success' : 'warn'" />
          </template>
        </Column>
        <Column header="" style="width: 9rem">
          <template #body="{ data }">
            <div class="flex gap-1">
              <AppButton text rounded severity="secondary" title="Editar producto" @click="edit(data)">
                <AppIcon :icon="faPen" />
              </AppButton>
              <AppButton
                text
                rounded
                severity="secondary"
                :title="data.activo ? 'Desactivar producto' : 'Activar producto'"
                @click="toggle(data)"
              >
                <AppIcon :icon="faPowerOff" />
              </AppButton>
              <AppButton text rounded severity="danger" title="Eliminar producto" @click="remove(data)">
                <AppIcon :icon="faTrash" />
              </AppButton>
            </div>
          </template>
        </Column>
      </DataTable>
    </div>

    <Dialog
      v-model:visible="show"
      modal
      :header="`${editingId ? 'Modificar' : 'Nuevo'} tipo de césped`"
      :style="{ width: 'min(720px, 96vw)' }"
    >
      <form @submit.prevent="save">
        <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>
        <div class="grid formgrid p-fluid">
          <div class="field col-12">
            <label for="nombre">Nombre</label>
            <InputText id="nombre" v-model="form.nombre" required />
          </div>
          <div class="field col-12 color-editor">
            <label for="colorInput">Variantes de color</label>
            <div class="flex gap-2">
              <InputText
                id="colorInput"
                v-model="colorInput"
                maxlength="100"
                placeholder="Ej. Verde oliva"
                class="flex-1"
                @keydown.enter.prevent="addColor"
              />
              <AppButton type="button" label="Agregar" severity="secondary" @click="addColor" />
            </div>
            <div class="flex flex-wrap gap-1 mt-2">
              <AppButton
                v-for="(color, index) in form.colores"
                :key="color"
                type="button"
                size="small"
                severity="secondary"
                :label="`${color} ×`"
                :title="`Quitar ${color}`"
                @click="removeColor(index)"
              />
              <small v-if="!form.colores.length" class="text-color-secondary">Todavía no agregaste colores.</small>
            </div>
            <small class="text-color-secondary">Todos los colores usan el mismo precio y costo del producto.</small>
          </div>
          <div class="field col-12">
            <label for="descripcion">Descripción</label>
            <Textarea id="descripcion" v-model="form.descripcion" auto-resize />
          </div>
        </div>

        <fieldset class="quote-product-content border-none p-0 mt-3">
          <legend class="font-bold mb-2">Contenido del presupuesto</legend>
          <small class="text-color-secondary block mb-3">
            Se carga una sola vez y se incorpora automáticamente al PDF cuando este producto forma parte del presupuesto.
          </small>
          <div class="grid formgrid p-fluid">
            <div class="field col-12">
              <label for="descripcionPresupuesto">Descripción general para PDF</label>
              <Textarea
                id="descripcionPresupuesto"
                v-model="form.descripcionPresupuesto"
                rows="5"
                placeholder="Presentación comercial y características generales del producto"
                auto-resize
              />
            </div>
            <div class="field col-12">
              <label for="especificacionesPresupuesto">Especificaciones técnicas / cotización</label>
              <Textarea
                id="especificacionesPresupuesto"
                v-model="form.especificacionesPresupuesto"
                rows="7"
                placeholder="Una característica por línea. Ej.:&#10;Ancho del rollo: 4 m&#10;Largo del rollo: 25 m&#10;Altura de hilo: 50 mm"
                auto-resize
              />
              <small class="text-color-secondary">Los saltos de línea se respetan en el PDF. Los metros, precios y totales se completan desde cada presupuesto.</small>
            </div>
            <div class="field col-12">
              <label for="fichaTecnicaUrl">Enlace a ficha técnica</label>
              <InputText id="fichaTecnicaUrl" v-model.trim="form.fichaTecnicaUrl" type="url" placeholder="https://.../ficha-tecnica.pdf" />
              <small class="text-color-secondary">Puede ser un PDF público de Google Drive, OneDrive o tu sitio web.</small>
            </div>
          </div>
        </fieldset>

        <div class="grid formgrid p-fluid mt-3">
          <div class="field col-12 md:col-6">
            <label for="precioVentaM2">Precio de venta por m²</label>
            <InputNumber id="precioVentaM2" v-model="form.precioVentaM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" required />
          </div>
          <div class="field col-12 md:col-6">
            <label for="costoM2">Costo por m²</label>
            <InputNumber id="costoM2" v-model="form.costoM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" required />
          </div>
          <div class="field col-12 md:col-6">
            <label for="precioContadoM2">Precio contado por m²</label>
            <InputNumber id="precioContadoM2" v-model="form.precioContadoM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" required />
          </div>
          <div class="field col-12 md:col-6">
            <label for="precioFinanciadoM2">Precio financiado por m²</label>
            <InputNumber id="precioFinanciadoM2" v-model="form.precioFinanciadoM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" required />
          </div>
        </div>

        <div class="flex align-items-center gap-2 mt-3">
          <ToggleSwitch v-model="form.activo" input-id="productoActivo" />
          <label for="productoActivo">Disponible para nuevas ventas</label>
        </div>

        <div class="flex justify-content-end gap-2 mt-4">
          <AppButton type="button" label="Cancelar" severity="secondary" @click="show = false" />
          <AppButton type="submit" label="Guardar" />
        </div>
      </form>
    </Dialog>
  </section>
</template>
