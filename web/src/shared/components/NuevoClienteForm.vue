<script setup lang="ts">
import { onMounted, ref } from 'vue'
import AppButton from '@/shared/components/AppButton.vue'
import AppDatePicker from '@/shared/components/AppDatePicker.vue'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import GeografiaAutocomplete from './GeografiaAutocomplete.vue'

interface GeoOption { id: string; nombre: string }

defineProps<{
  hint?: string
}>()

const emit = defineEmits<{
  cancel: []
  created: [cliente: any]
}>()

const provincias = ref<GeoOption[]>([])
const localidades = ref<GeoOption[]>([])
const geoAvailable = ref(false)
const geoMode = ref(false)
const geoLoading = ref(false)
const saving = ref(false)
const error = ref('')
const geoNotice = ref('')
const tipos = ['Particular', 'Club', 'Empresa', 'Constructor', 'Revendedor', 'Otro']

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
  <div class="nuevo-cliente-form">
    <p v-if="hint" class="nuevo-cliente-form-hint text-color-secondary m-0 mb-3">{{ hint }}</p>

    <form @submit.prevent="save">
      <Message v-if="error" severity="error" class="inline-form-error mb-3" :closable="false">{{ error }}</Message>
      <Message v-if="geoNotice" severity="info" class="mb-3" :closable="false">{{ geoNotice }}</Message>

      <div class="p-fluid cliente-form-pairs">
        <div class="field">
          <label for="nuevo-cliente-nombre">Nombre</label>
          <InputText id="nuevo-cliente-nombre" v-model="form.nombre" size="small" maxlength="100" required />
        </div>
        <div class="field">
          <label for="nuevo-cliente-apellido">Apellido</label>
          <InputText id="nuevo-cliente-apellido" v-model="form.apellido" size="small" maxlength="100" required />
        </div>
        <div class="field">
          <label for="nuevo-cliente-telefono">Teléfono</label>
          <InputText id="nuevo-cliente-telefono" v-model="form.telefono" type="tel" size="small" maxlength="30" required />
        </div>
        <div class="field">
          <label for="nuevo-cliente-correo">Correo electrónico</label>
          <InputText id="nuevo-cliente-correo" v-model="form.correo" type="email" size="small" maxlength="200" />
        </div>

        <template v-if="geoMode">
          <div class="cliente-form-banner"><Tag value="Ubicación oficial de Argentina" severity="success" /></div>
          <div class="field">
            <label>Provincia</label>
            <Select
              v-model="form.provinciaId"
              :options="provincias"
              option-label="nombre"
              option-value="id"
              placeholder="Seleccionar provincia"
              size="small"
              required
              @change="provinceChanged"
            />
          </div>
          <div class="field">
            <label>Localidad</label>
            <GeografiaAutocomplete
              v-model="form.localidadId"
              :options="localidades"
              :disabled="!form.provinciaId || geoLoading"
              :placeholder="geoLoading ? 'Cargando localidades…' : 'Buscar localidad'"
              @select="localitySelected"
            />
          </div>
          <div class="cliente-form-action">
            <AppButton type="button" label="Cargar manualmente" severity="secondary" size="small" @click="useManual" />
          </div>
        </template>

        <template v-else>
          <div class="cliente-form-banner"><Tag value="Modo manual: la ubicación se guardará sin códigos oficiales." severity="warn" /></div>
          <div class="field">
            <label>Provincia</label>
            <InputText v-model="form.provincia" maxlength="100" size="small" required />
          </div>
          <div class="field">
            <label>Localidad</label>
            <InputText v-model="form.localidad" maxlength="100" size="small" required />
          </div>
          <div v-if="geoAvailable" class="cliente-form-action">
            <AppButton type="button" label="Usar ubicaciones oficiales de Argentina" severity="secondary" size="small" @click="enableOfficial" />
          </div>
        </template>

        <div class="field">
          <label>Tipo</label>
          <Select v-model="form.tipo" :options="tipos" size="small" />
        </div>
        <div class="field">
          <label>Primer contacto</label>
          <AppDatePicker v-model="form.fechaPrimerContacto" required />
        </div>
        <div class="field field-full">
          <label>Observaciones</label>
          <Textarea v-model="form.observaciones" maxlength="1000" rows="3" auto-resize />
        </div>
      </div>

      <div class="inline-form-footer">
        <small class="inline-form-note text-color-secondary">Los datos quedan disponibles para ventas y presupuestos.</small>
        <div class="flex gap-2">
          <AppButton type="button" label="Cancelar" severity="secondary" size="small" @click="emit('cancel')" />
          <AppButton type="submit" :label="saving ? 'Guardando…' : 'Guardar cliente'" :loading="saving" size="small" />
        </div>
      </div>
    </form>
  </div>
</template>
