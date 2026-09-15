<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import Textarea from 'primevue/textarea'
import AppIcon from '@/shared/components/AppIcon.vue'
import { faPen, faPowerOff, faTrash } from '@/shared/icons'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { formatCurrency as money } from '@/shared/formatters'
import { confirmAction, notify } from '@/shared/uiFeedback'
import { TABLE_ROWS, TABLE_ROWS_OPTIONS } from '@/shared/tablePagination'

const items = ref<any[]>([])
const showForm = ref(false)
const showPdfSection = ref(false)
const error = ref('')
const saving = ref(false)
const loading = ref(false)
const loadError = ref('')
const editingId = ref<string | null>(null)
const colorInput = ref('')
const formPanelRef = ref<HTMLElement | null>(null)
const nombreInputRef = ref<{ $el?: HTMLElement } | null>(null)

const blank = () => ({
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
const form = ref(blank())

const marginPct = computed(() => {
  const sale = form.value.precioVentaM2
  const cost = form.value.costoM2
  if (!sale || sale <= 0) return null
  return ((sale - cost) / sale) * 100
})

const hasPdfContent = computed(() =>
  !!form.value.descripcionPresupuesto.trim()
  || !!form.value.especificacionesPresupuesto.trim()
  || !!form.value.fichaTecnicaUrl.trim(),
)

watch(() => form.value.precioVentaM2, (value) => {
  if (editingId.value || !value) return
  if (!form.value.precioContadoM2) form.value.precioContadoM2 = value
  if (!form.value.precioFinanciadoM2) form.value.precioFinanciadoM2 = value
})

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    items.value = (await http.get('/maestros/tipos-cesped')).data
  } catch (e) {
    loadError.value = apiErrorMessage(e, 'No se pudieron cargar los productos.')
  } finally {
    loading.value = false
  }
}

function focusForm() {
  nextTick(() => {
    formPanelRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    const input = nombreInputRef.value?.$el?.querySelector('input') as HTMLInputElement | null
    input?.focus()
  })
}

function cancelForm() {
  showForm.value = false
  showPdfSection.value = false
  editingId.value = null
  colorInput.value = ''
  form.value = blank()
  error.value = ''
}

function create() {
  editingId.value = null
  colorInput.value = ''
  form.value = blank()
  error.value = ''
  showPdfSection.value = false
  showForm.value = true
  focusForm()
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
    controlPorLotes: Boolean(x.controlPorLotes),
    activo: x.activo,
  }
  error.value = ''
  showPdfSection.value = !!(x.descripcionPresupuesto || x.especificacionesPresupuesto || x.fichaTecnicaUrl)
  showForm.value = true
  focusForm()
}

function addColor() {
  const color = colorInput.value.trim()
  if (!color || form.value.colores.some(x => x.toLocaleLowerCase() === color.toLocaleLowerCase())) return
  form.value.colores.push(color)
  colorInput.value = ''
}

function removeColor(index: number) { form.value.colores.splice(index, 1) }

function copySalePrices() {
  form.value.precioContadoM2 = form.value.precioVentaM2
  form.value.precioFinanciadoM2 = form.value.precioVentaM2
}

async function save() {
  if (!form.value.nombre.trim()) {
    error.value = 'Ingresá el nombre del producto.'
    return
  }
  if (form.value.precioVentaM2 <= 0 || form.value.costoM2 < 0) {
    error.value = 'Completá precio de venta y costo.'
    return
  }

  saving.value = true
  error.value = ''
  try {
    editingId.value
      ? await http.put(`/maestros/tipos-cesped/${editingId.value}`, form.value)
      : await http.post('/maestros/tipos-cesped', form.value)
    cancelForm()
    await load()
  } catch (e: any) {
    error.value = apiErrorMessage(e, 'No se pudo guardar.')
  } finally {
    saving.value = false
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
    controlPorLotes: Boolean(x.controlPorLotes),
    activo: !x.activo,
  })
  await load()
}

async function remove(x: any) {
  if (!await confirmAction({ title: 'Eliminar tipo de césped', message: `¿Querés eliminar ${x.nombre}?`, confirmText: 'Eliminar', danger: true })) return
  try {
    if (editingId.value === x.id) cancelForm()
    await http.delete(`/maestros/tipos-cesped/${x.id}`)
    await load()
  } catch (e: any) {
    notify(apiErrorMessage(e, 'No se pudo eliminar.'))
  }
}

onMounted(load)
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-2">
      <p class="page-desc text-color-secondary m-0">
        {{ showForm
          ? (editingId ? 'Editar producto' : 'Nuevo producto')
          : 'Productos, precios y costos maestros.' }}
      </p>
      <AppButton
        v-if="showForm"
        label="Volver al listado"
        icon="pi pi-arrow-left"
        severity="secondary"
        size="small"
        @click="cancelForm"
      />
      <AppButton
        v-else
        label="Tipo de césped"
        icon="pi pi-plus"
        size="small"
        @click="create"
      />
    </div>

    <Message v-if="loadError && !showForm" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

    <div v-else-if="!showForm" class="table-panel">
      <DataTable
        :value="items"
        :loading="loading"
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

    <article v-else ref="formPanelRef" class="card inline-form-panel inline-form-panel-wide">
      <form @submit.prevent="save">
        <Message v-if="error" severity="error" class="inline-form-error" :closable="false">{{ error }}</Message>

        <section class="inline-form-block">
          <header class="inline-form-block-head">
            <span>1. Datos del producto</span>
          </header>
          <div class="grid formgrid p-fluid inline-form-grid">
            <div class="col-12 product-name-colors-row">
              <div class="field product-name-field">
                <label for="nombre">Nombre</label>
                <InputText id="nombre" ref="nombreInputRef" v-model="form.nombre" size="small" required />
              </div>
              <div class="field product-colors-field color-editor">
                <label for="colorInput">Variantes de color</label>
                <div class="flex gap-2">
                  <InputText
                    id="colorInput"
                    v-model="colorInput"
                    size="small"
                    maxlength="100"
                    placeholder="Ej. Verde oliva"
                    class="flex-1"
                    @keydown.enter.prevent="addColor"
                  />
                  <AppButton type="button" label="Agregar" severity="secondary" size="small" @click="addColor" />
                </div>
              </div>
            </div>
            <div class="col-12">
              <div class="inline-form-chips">
                <button
                  v-for="(color, index) in form.colores"
                  :key="color"
                  type="button"
                  class="inline-form-chip"
                  :title="`Quitar ${color}`"
                  @click="removeColor(index)"
                >
                  {{ color }} <span aria-hidden="true">×</span>
                </button>
                <small v-if="!form.colores.length" class="text-color-secondary">Opcional · mismo precio para todos los colores</small>
              </div>
            </div>
            <div class="field col-12">
              <label for="descripcion">Descripción interna</label>
              <Textarea id="descripcion" v-model="form.descripcion" rows="2" placeholder="Referencia interna del producto" auto-resize />
            </div>
          </div>
        </section>

        <section class="inline-form-block">
          <header class="inline-form-block-head inline-form-block-head-tools">
            <span>2. Precios por m²</span>
            <AppButton
              type="button"
              label="Igualar contado/financiado al precio venta"
              icon="pi pi-copy"
              severity="secondary"
              size="small"
              text
              @click="copySalePrices"
            />
          </header>
          <div class="grid formgrid p-fluid inline-form-grid inline-form-price-grid">
            <div class="field col-6 md:col-3">
              <label for="precioVentaM2">Venta</label>
              <InputNumber id="precioVentaM2" v-model="form.precioVentaM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" size="small" required />
            </div>
            <div class="field col-6 md:col-3">
              <label for="costoM2">Costo</label>
              <InputNumber id="costoM2" v-model="form.costoM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" size="small" required />
            </div>
            <div class="field col-6 md:col-3">
              <label for="precioContadoM2">Contado</label>
              <InputNumber id="precioContadoM2" v-model="form.precioContadoM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" size="small" required />
            </div>
            <div class="field col-6 md:col-3">
              <label for="precioFinanciadoM2">Financiado</label>
              <InputNumber id="precioFinanciadoM2" v-model="form.precioFinanciadoM2" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" size="small" required />
            </div>
          </div>
          <p v-if="marginPct !== null" class="inline-form-hint m-0">
            Margen estimado: <strong :class="{ 'text-red-500': marginPct < 0 }">{{ marginPct.toFixed(1) }}%</strong>
            · Ganancia {{ money(form.precioVentaM2 - form.costoM2) }} / m²
          </p>
        </section>

        <section class="inline-form-section inline-form-section-collapsible" :class="{ open: showPdfSection }">
          <button type="button" class="inline-form-section-toggle" @click="showPdfSection = !showPdfSection">
            <span>
              3. Contenido del presupuesto PDF
              <small>{{ hasPdfContent ? '· con datos cargados' : '· opcional' }}</small>
            </span>
            <i class="pi" :class="showPdfSection ? 'pi-chevron-up' : 'pi-chevron-down'" />
          </button>
          <div v-show="showPdfSection" class="inline-form-section-body">
            <small class="inline-form-note block mb-2">Se reutiliza automáticamente al armar presupuestos con este producto.</small>
            <div class="grid formgrid p-fluid inline-form-grid">
              <div class="field col-12 md:col-6">
                <label for="descripcionPresupuesto">Descripción general</label>
                <Textarea
                  id="descripcionPresupuesto"
                  v-model="form.descripcionPresupuesto"
                  rows="3"
                  placeholder="Presentación comercial del producto"
                  auto-resize
                />
              </div>
              <div class="field col-12 md:col-6">
                <label for="especificacionesPresupuesto">Especificaciones técnicas</label>
                <Textarea
                  id="especificacionesPresupuesto"
                  v-model="form.especificacionesPresupuesto"
                  rows="3"
                  placeholder="Una característica por línea"
                  auto-resize
                />
              </div>
              <div class="field col-12">
                <label for="fichaTecnicaUrl">Enlace a ficha técnica</label>
                <InputText id="fichaTecnicaUrl" v-model.trim="form.fichaTecnicaUrl" size="small" type="url" placeholder="https://.../ficha-tecnica.pdf" />
              </div>
            </div>
          </div>
        </section>

        <div class="inline-form-footer">
          <div class="flex align-items-center gap-2">
            <ToggleSwitch v-model="form.activo" input-id="productoActivo" />
            <label for="productoActivo">Disponible para nuevas ventas</label>
          </div>
          <div class="flex gap-2">
            <AppButton type="button" label="Cancelar" severity="secondary" size="small" @click="cancelForm" />
            <AppButton type="submit" :label="saving ? 'Guardando…' : 'Guardar producto'" :loading="saving" size="small" />
          </div>
        </div>
      </form>
    </article>
  </section>
</template>