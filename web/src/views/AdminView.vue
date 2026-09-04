<script setup lang="ts">
import { computed, ref, onMounted } from "vue";
import { Pencil, Trash2, Power } from "lucide-vue-next";
import { http } from "@/shared/api/httpClient";
import { formatCurrency as money } from "@/shared/formatters";
import { confirmAction, notify } from "@/shared/uiFeedback";
import { auth } from "@/auth";
const items = ref<any[]>([]),
  show = ref(false),
  error = ref(""),
  editingId = ref<string | null>(null),
  colorInput = ref(""),
  form = ref({
    nombre: "",
    descripcion: "",
    descripcionPresupuesto: "",
    especificacionesPresupuesto: "",
    fichaTecnicaUrl: "",
    precioVentaM2: 0,
    precioContadoM2: 0,
    precioFinanciadoM2: 0,
    costoM2: 0,
    colores: [] as string[],
    activo: true,
  });
const depositos = ref<any[]>([])
const sucursales = ref<any[]>([])
const depositoModal = ref(false)
const depositoEditingId = ref<string | null>(null)
const depositoError = ref('')
const depositoForm = ref({ nombre: '', activo: true })
const sucursalModal = ref(false)
const sucursalEditingId = ref<string | null>(null)
const sucursalError = ref('')
const sucursalForm = ref({ nombre: '', depositoPropioId: '', puntoVentaAfip: null as number | null, activo: true })
const isAdmin = computed(() => auth.state.user?.rol === 'Administrador')
async function load() {
  items.value = (await http.get("/maestros/tipos-cesped")).data;
}
async function loadSucursales() {
  const [depositosResponse, sucursalesResponse] = await Promise.all([
    http.get('/sucursales/depositos'),
    http.get('/sucursales')
  ])
  depositos.value = depositosResponse.data
  sucursales.value = sucursalesResponse.data
}
function create() {
  editingId.value = null;
  colorInput.value = "";
  form.value = {
    nombre: "",
    descripcion: "",
    descripcionPresupuesto: "",
    especificacionesPresupuesto: "",
    fichaTecnicaUrl: "",
    precioVentaM2: 0,
    precioContadoM2: 0,
    precioFinanciadoM2: 0,
    costoM2: 0,
    colores: [],
    activo: true,
  };
  error.value = "";
  show.value = true;
}
function edit(x: any) {
  editingId.value = x.id;
  colorInput.value = "";
  form.value = {
    nombre: x.nombre,
    descripcion: x.descripcion ?? "",
    descripcionPresupuesto: x.descripcionPresupuesto ?? "",
    especificacionesPresupuesto: x.especificacionesPresupuesto ?? "",
    fichaTecnicaUrl: x.fichaTecnicaUrl ?? "",
    precioVentaM2: x.precioVentaM2,
    precioContadoM2: x.precioContadoM2 ?? x.precioVentaM2,
    precioFinanciadoM2: x.precioFinanciadoM2 ?? x.precioVentaM2,
    costoM2: x.costoM2,
    colores: [...(x.colores ?? [])],
    activo: x.activo,
  };
  error.value = "";
  show.value = true;
}
function addColor() {
  const color = colorInput.value.trim();
  if (!color || form.value.colores.some(x => x.toLocaleLowerCase() === color.toLocaleLowerCase())) return;
  form.value.colores.push(color);
  colorInput.value = "";
}
function removeColor(index: number) { form.value.colores.splice(index, 1); }
async function save() {
  try {
    editingId.value
      ? await http.put(`/maestros/tipos-cesped/${editingId.value}`, form.value)
      : await http.post("/maestros/tipos-cesped", form.value);
    show.value = false;
    await load();
  } catch (e: any) {
    error.value =
      e.response?.data?.message ??
      e.response?.data?.detail ??
      "No se pudo guardar.";
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
  });
  await load();
}
async function remove(x: any) {
  if (await confirmAction({title:"Eliminar tipo de césped",message:`¿Querés eliminar ${x.nombre}?`,confirmText:"Eliminar",danger:true}))
    try {
      await http.delete(`/maestros/tipos-cesped/${x.id}`);
      await load();
    } catch (e: any) {
      notify(e.response?.data?.message ?? "No se pudo eliminar.");
    }
}
function createDeposito() {
  depositoEditingId.value = null
  depositoForm.value = { nombre: '', activo: true }
  depositoError.value = ''
  depositoModal.value = true
}
function editDeposito(x: any) {
  depositoEditingId.value = x.id
  depositoForm.value = { nombre: x.nombre, activo: x.activo }
  depositoError.value = ''
  depositoModal.value = true
}
async function saveDeposito() {
  try {
    depositoEditingId.value
      ? await http.put(`/sucursales/depositos/${depositoEditingId.value}`, depositoForm.value)
      : await http.post('/sucursales/depositos', depositoForm.value)
    depositoModal.value = false
    await loadSucursales()
  } catch (e: any) {
    depositoError.value = e.response?.data?.message ?? e.response?.data?.detail ?? 'No se pudo guardar el depósito.'
  }
}
async function toggleDeposito(x: any) {
  try {
    await http.patch(`/sucursales/depositos/${x.id}/estado`, { activo: !x.activo })
    await loadSucursales()
  } catch (e: any) {
    notify(e.response?.data?.message ?? 'No se pudo cambiar el estado del depósito.')
  }
}
function createSucursal() {
  const firstActiveDeposit = depositos.value.find(x => x.activo)
  if (!firstActiveDeposit) {
    notify('Creá o activá un depósito antes de agregar una sucursal.')
    return
  }
  sucursalEditingId.value = null
  sucursalForm.value = { nombre: '', depositoPropioId: firstActiveDeposit.id, puntoVentaAfip: null, activo: true }
  sucursalError.value = ''
  sucursalModal.value = true
}
function editSucursal(x: any) {
  sucursalEditingId.value = x.id
  sucursalForm.value = { nombre: x.nombre, depositoPropioId: x.depositoPropioId, puntoVentaAfip: x.puntoVentaAfip, activo: x.activo }
  sucursalError.value = ''
  sucursalModal.value = true
}
async function saveSucursal() {
  try {
    sucursalEditingId.value
      ? await http.put(`/sucursales/${sucursalEditingId.value}`, sucursalForm.value)
      : await http.post('/sucursales', sucursalForm.value)
    sucursalModal.value = false
    await loadSucursales()
  } catch (e: any) {
    sucursalError.value = e.response?.data?.message ?? e.response?.data?.detail ?? 'No se pudo guardar la sucursal.'
  }
}
async function toggleSucursal(x: any) {
  try {
    await http.patch(`/sucursales/${x.id}/estado`, { activo: !x.activo })
    await loadSucursales()
  } catch (e: any) {
    notify(e.response?.data?.message ?? 'No se pudo cambiar el estado de la sucursal.')
  }
}
onMounted(() => isAdmin.value ? Promise.all([load(), loadSucursales()]) : load());
</script>
<template>
  <section class="page">
    <div class="page-title">
      <div>
        <h2>Administración</h2>
        <p>Productos, precios y costos maestros.</p>
      </div>
      <button class="btn" @click="create">+ Tipo de césped</button>
    </div>
    <div class="panel">
      <table>
        <thead>
          <tr>
            <th>Nombre</th>
            <th>Descripción</th>
            <th>Venta / m²</th>
            <th>Costo / m²</th>
            <th>Contado / m²</th>
            <th>Financiado / m²</th>
            <th>Colores</th>
            <th>Estado</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="x in items" :key="x.id">
            <td>
              <b>{{ x.nombre }}</b>
            </td>
            <td>{{ x.descripcion || "—" }}</td>
            <td>{{ money(x.precioVentaM2) }}</td>
            <td>{{ money(x.costoM2) }}</td>
            <td>{{ money(x.precioContadoM2) }}</td>
            <td>{{ money(x.precioFinanciadoM2) }}</td>
            <td><div class="color-list"><span v-for="color in x.colores" :key="color" class="badge color-badge">{{ color }}</span><span v-if="!x.colores?.length">—</span></div></td>
            <td>
              <span class="badge" :class="{ warn: !x.activo }">{{
                x.activo ? "Activo" : "Inactivo"
              }}</span>
            </td>
            <td>
              <div class="row-actions product-actions">
                <button class="icon-btn tooltip" type="button" title="Editar producto" aria-label="Editar producto" data-tooltip="Editar producto" @click="edit(x)"><Pencil /></button
                ><button class="icon-btn tooltip" type="button" :title="x.activo ? 'Desactivar producto' : 'Activar producto'" :aria-label="x.activo ? 'Desactivar producto' : 'Activar producto'" :data-tooltip="x.activo ? 'Desactivar producto' : 'Activar producto'" @click="toggle(x)"><Power /></button
                ><button class="icon-btn danger tooltip" type="button" title="Eliminar producto" aria-label="Eliminar producto" data-tooltip="Eliminar producto" @click="remove(x)">
                  <Trash2 />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div v-if="isAdmin" class="admin-master-grid">
      <div class="panel">
        <div class="panel-head">
          <div><h3>Depósitos</h3><small>Ubicaciones habilitadas para recibir ingresos de stock.</small></div>
          <button class="btn secondary compact" type="button" @click="createDeposito">+ Depósito</button>
        </div>
        <table>
          <thead><tr><th>Nombre</th><th>Estado</th><th></th></tr></thead>
          <tbody>
            <tr v-for="deposito in depositos" :key="deposito.id">
              <td><b>{{ deposito.nombre }}</b></td>
              <td><span class="badge" :class="{ warn: !deposito.activo }">{{ deposito.activo ? 'Activo' : 'Inactivo' }}</span></td>
              <td><div class="row-actions"><button class="icon-btn tooltip" type="button" title="Editar depósito" aria-label="Editar depósito" data-tooltip="Editar depósito" @click="editDeposito(deposito)"><Pencil /></button><button class="icon-btn tooltip" type="button" :title="deposito.activo ? 'Desactivar depósito' : 'Activar depósito'" :aria-label="deposito.activo ? 'Desactivar depósito' : 'Activar depósito'" :data-tooltip="deposito.activo ? 'Desactivar depósito' : 'Activar depósito'" @click="toggleDeposito(deposito)"><Power /></button></div></td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="panel">
        <div class="panel-head">
          <div><h3>Sucursales</h3><small>Cada sucursal opera con un depósito propio.</small></div>
          <button class="btn secondary compact" type="button" @click="createSucursal">+ Sucursal</button>
        </div>
        <table>
          <thead><tr><th>Nombre</th><th>Depósito propio</th><th>Estado</th><th></th></tr></thead>
          <tbody>
            <tr v-for="sucursal in sucursales" :key="sucursal.id">
              <td><b>{{ sucursal.nombre }}</b></td><td>{{ sucursal.depositoPropioNombre }}</td>
              <td><span class="badge" :class="{ warn: !sucursal.activo }">{{ sucursal.activo ? 'Activa' : 'Inactiva' }}</span></td>
              <td><div class="row-actions"><button class="icon-btn tooltip" type="button" title="Editar sucursal" aria-label="Editar sucursal" data-tooltip="Editar sucursal" @click="editSucursal(sucursal)"><Pencil /></button><button class="icon-btn tooltip" type="button" :title="sucursal.activo ? 'Desactivar sucursal' : 'Activar sucursal'" :aria-label="sucursal.activo ? 'Desactivar sucursal' : 'Activar sucursal'" :data-tooltip="sucursal.activo ? 'Desactivar sucursal' : 'Activar sucursal'" @click="toggleSucursal(sucursal)"><Power /></button></div></td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
    <div v-if="depositoModal" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="saveDeposito">
        <h3>{{ depositoEditingId ? 'Editar' : 'Nuevo' }} depósito</h3>
        <p v-if="depositoError" class="error">{{ depositoError }}</p>
        <div class="field"><label>Nombre</label><input v-model.trim="depositoForm.nombre" maxlength="150" required></div>
        <label class="check"><input v-model="depositoForm.activo" type="checkbox"> Depósito activo</label>
        <div class="actions"><button class="btn secondary" type="button" @click="depositoModal = false">Cancelar</button><button class="btn">Guardar depósito</button></div>
      </form>
    </div>
    <div v-if="sucursalModal" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="saveSucursal">
        <h3>{{ sucursalEditingId ? 'Editar' : 'Nueva' }} sucursal</h3>
        <p v-if="sucursalError" class="error">{{ sucursalError }}</p>
        <div class="field"><label>Nombre</label><input v-model.trim="sucursalForm.nombre" maxlength="150" required></div>
        <div class="field"><label>Depósito propio</label><select v-model="sucursalForm.depositoPropioId" required><option value="" disabled>Seleccionar depósito</option><option v-for="deposito in depositos.filter(x => x.activo)" :key="deposito.id" :value="deposito.id">{{ deposito.nombre }}</option></select></div>
        <label class="check"><input v-model="sucursalForm.activo" type="checkbox"> Sucursal activa</label>
        <div class="actions"><button class="btn secondary" type="button" @click="sucursalModal = false">Cancelar</button><button class="btn">Guardar sucursal</button></div>
      </form>
    </div>
    <div v-if="show" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="save">
        <h3>{{ editingId ? "Modificar" : "Nuevo" }} tipo de césped</h3>
        <p v-if="error" class="error">{{ error }}</p>
        <div class="field">
          <label>Nombre</label><input v-model="form.nombre" required />
        </div>
        <div class="field color-editor">
          <label>Variantes de color</label>
          <div class="color-entry"><input v-model="colorInput" maxlength="100" placeholder="Ej. Verde oliva" @keydown.enter.prevent="addColor"><button type="button" class="btn secondary" @click="addColor">Agregar</button></div>
          <div class="color-list"><button v-for="(color,index) in form.colores" :key="color" type="button" class="badge color-chip" :title="`Quitar ${color}`" @click="removeColor(index)">{{ color }} ×</button><small v-if="!form.colores.length">Todavía no agregaste colores.</small></div>
          <small>Todos los colores usan el mismo precio y costo del producto.</small>
        </div>
        <div class="field">
          <label>Descripción</label
          ><textarea v-model="form.descripcion"></textarea>
        </div>
        <fieldset class="quote-product-content">
          <legend>Contenido del presupuesto</legend>
          <small>Se carga una sola vez y se incorpora automáticamente al PDF cuando este producto forma parte del presupuesto.</small>
          <div class="field">
            <label>Descripción general para PDF</label>
            <textarea v-model="form.descripcionPresupuesto" rows="5" placeholder="Presentación comercial y características generales del producto"></textarea>
          </div>
          <div class="field">
            <label>Especificaciones técnicas / cotización</label>
            <textarea v-model="form.especificacionesPresupuesto" rows="7" placeholder="Una característica por línea. Ej.:&#10;Ancho del rollo: 4 m&#10;Largo del rollo: 25 m&#10;Altura de hilo: 50 mm"></textarea>
            <small>Los saltos de línea se respetan en el PDF. Los metros, precios y totales se completan desde cada presupuesto.</small>
          </div>
          <div class="field">
            <label>Enlace a ficha técnica</label>
            <input v-model.trim="form.fichaTecnicaUrl" type="url" placeholder="https://.../ficha-tecnica.pdf">
            <small>Puede ser un PDF público de Google Drive, OneDrive o tu sitio web.</small>
          </div>
        </fieldset>
        <div class="form-grid price-grid">
          <div class="field">
            <label>Precio de venta por m²</label
            ><input
              v-model.number="form.precioVentaM2"
              type="number"
              min="0"
              step="0.01"
              required
            />
          </div>
          <div class="field">
            <label>Costo por m²</label
            ><input
              v-model.number="form.costoM2"
              type="number"
              min="0"
              step="0.01"
              required
            />
          </div>
        </div>
        <div class="form-grid price-grid">
          <div class="field"><label>Precio contado por m²</label><input v-model.number="form.precioContadoM2" type="number" min="0" step="0.01" required /></div>
          <div class="field"><label>Precio financiado por m²</label><input v-model.number="form.precioFinanciadoM2" type="number" min="0" step="0.01" required /></div>
        </div>
        <label class="check"
          ><input v-model="form.activo" type="checkbox" /> Disponible para
          nuevas ventas</label
        >
        <div class="actions">
          <button type="button" class="btn secondary" @click="show = false">
            Cancelar</button
          ><button class="btn">Guardar</button>
        </div>
      </form>
    </div>
  </section>
</template>
