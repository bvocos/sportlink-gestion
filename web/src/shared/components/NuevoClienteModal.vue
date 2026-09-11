<script setup lang="ts">
import { onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import GeografiaAutocomplete from './GeografiaAutocomplete.vue'

interface GeoOption { id: string; nombre: string }

const emit = defineEmits<{ (e: 'close'): void; (e: 'created', cliente: any): void }>()

const show = ref(true)
const provincias = ref<GeoOption[]>([])
const localidades = ref<GeoOption[]>([])
const geoAvailable = ref(false)
const geoMode = ref(false)
const geoLoading = ref(false)
const saving = ref(false)
const error = ref('')
const geoNotice = ref('')
const tipos = ['Particular', 'Club', 'Empresa', 'Constructor', 'Revendedor', 'Otro']

const fields = [
  { key: 'nombre', label: 'Nombre', type: 'text' },
  { key: 'apellido', label: 'Apellido', type: 'text' },
  { key: 'telefono', label: 'Teléfono', type: 'tel' },
  { key: 'correo', label: 'Correo electrónico', type: 'email' },
] as const

const form = ref({
  nombre: '', apellido: '', telefono: '', correo: '', localidad: '', provincia: '',
  localidadId: '', provinciaId: '', tipo: 'Particular',
  fechaPrimerContacto: new Date().toISOString().slice(0, 10), observaciones: '',
})

async function loadProvincias() {
  try {
    provincias.value = (await http.get('/geografia/provincias')).data
    geoAvailable.value = true
    geoMode.value = true
  } catch {
    geoAvailable.value = false
    geoMode.value = false
    geoNotice.value = 'Georef no está disponible. Podés cargar la ubicación manualmente.'
  }
}
async function loadLocalidades() {
  localidades.value = []
  if (!form.value.provinciaId) return
  geoLoading.value = true
  try {
    localidades.value = (await http.get('/geografia/localidades', { params: { provincia: form.value.provinciaId } })).data
  } catch {
    useManual()
    geoNotice.value = 'Georef no respondió. Se habilitó la carga manual.'
  } finally {
    geoLoading.value = false
  }
}
async function provinceChanged() {
  const option = provincias.value.find(x => x.id === form.value.provinciaId)
  form.value.provincia = option?.nombre ?? ''
  form.value.localidad = ''
  form.value.localidadId = ''
  await loadLocalidades()
}
function localitySelected(option: GeoOption) { form.value.localidad = option.nombre }
function useManual() {
  geoMode.value = false
  form.value.provinciaId = ''
  form.value.localidadId = ''
  localidades.value = []
  geoNotice.value = 'Ubicación en modo manual: se guardarán los nombres sin códigos oficiales.'
}
function enableOfficial() {
  geoMode.value = true
  geoNotice.value = ''
  form.value.provincia = ''
  form.value.localidad = ''
  form.value.provinciaId = ''
  form.value.localidadId = ''
  localidades.value = []
}
function message(e: any) {
  const first = Object.values(e.response?.data?.errors ?? {}).flat()[0]
  return first ? String(first) : apiErrorMessage(e, 'No se pudo guardar el cliente.')
}
function close() {
  show.value = false
  emit('close')
}
async function save() {
  error.value = ''
  if (!/^\+?[0-9 ()-]{6,30}$/.test(form.value.telefono.trim())) {
    error.value = 'Ingresá un teléfono válido usando números, espacios, paréntesis, + o -.'
    return
  }
  if (geoMode.value && (!form.value.provinciaId || !form.value.localidadId)) {
    error.value = 'Seleccioná una provincia y una localidad de las opciones oficiales.'
    return
  }
  saving.value = true
  try {
    const { data } = await http.post('/clientes', form.value)
    emit('created', data)
  } catch (e: any) {
    error.value = message(e)
  } finally {
    saving.value = false
  }
}
onMounted(loadProvincias)
</script>

<template>
  <Dialog
    v-model:visible="show"
    modal
    header="Nuevo cliente"
    :style="{ width: 'min(680px, 96vw)' }"
    @hide="close"
  >
    <p class="mt-0 text-color-secondary">Se guardará y quedará seleccionado en esta venta.</p>
    <form @submit.prevent="save">
      <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>
      <Message v-if="geoNotice" severity="info" class="mb-3" :closable="false">{{ geoNotice }}</Message>

      <div class="grid formgrid p-fluid">
        <div v-for="field in fields" :key="field.key" class="field col-12 md:col-6">
          <label :for="field.key">{{ field.label }}</label>
          <InputText
            :id="field.key"
            v-model="(form as any)[field.key]"
            :type="field.type"
            :required="field.key !== 'correo'"
            :maxlength="field.key === 'telefono' ? 30 : field.key === 'correo' ? 200 : 100"
          />
        </div>

        <template v-if="geoMode">
          <div class="col-12"><Tag value="Ubicación oficial de Argentina" severity="success" /></div>
          <div class="field col-12 md:col-6">
            <label>País</label>
            <InputText model-value="Argentina" readonly />
          </div>
          <div class="field col-12 md:col-6">
            <label>Provincia</label>
            <Select
              v-model="form.provinciaId"
              :options="provincias"
              option-label="nombre"
              option-value="id"
              placeholder="Seleccionar provincia"
              required
              @change="provinceChanged"
            />
          </div>
          <div class="field col-12 md:col-6">
            <label>Localidad</label>
            <GeografiaAutocomplete
              v-model="form.localidadId"
              :options="localidades"
              :disabled="!form.provinciaId || geoLoading"
              :placeholder="geoLoading ? 'Cargando localidades…' : 'Buscar localidad'"
              @select="localitySelected"
            />
          </div>
          <div class="field col-12 md:col-6 flex align-items-end">
            <AppButton type="button" label="Cargar manualmente" severity="secondary" @click="useManual" />
          </div>
        </template>

        <template v-else>
          <div class="col-12"><Tag value="Modo manual: la ubicación se guardará sin códigos oficiales." severity="warn" /></div>
          <div class="field col-12 md:col-6">
            <label>Provincia</label>
            <InputText v-model="form.provincia" maxlength="100" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>Localidad</label>
            <InputText v-model="form.localidad" maxlength="100" required />
          </div>
          <div v-if="geoAvailable" class="col-12">
            <AppButton type="button" label="Usar ubicaciones oficiales de Argentina" severity="secondary" @click="enableOfficial" />
          </div>
        </template>

        <div class="field col-12 md:col-6">
          <label>Tipo</label>
          <Select v-model="form.tipo" :options="tipos" />
        </div>
        <div class="field col-12 md:col-6">
          <label>Primer contacto</label>
          <AppDatePicker v-model="form.fechaPrimerContacto" required />
        </div>
        <div class="field col-12">
          <label>Observaciones</label>
          <Textarea v-model="form.observaciones" maxlength="1000" rows="3" auto-resize />
        </div>
      </div>

      <div class="flex justify-content-end gap-2 mt-4">
        <AppButton type="button" label="Cancelar" severity="secondary" @click="close" />
        <AppButton type="submit" :label="saving ? 'Guardando…' : 'Guardar cliente'" :loading="saving" />
      </div>
    </form>
  </Dialog>
</template>
