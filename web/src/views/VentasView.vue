<script setup lang="ts">
import { ref, onMounted, watch, computed, reactive } from "vue";
import { Pencil, Trash2 } from "lucide-vue-next";
import { http } from "@/shared/api/httpClient";
import ClienteAutocomplete from "@/shared/components/ClienteAutocomplete.vue";
import { formatCurrency as money } from "@/shared/formatters";
import { confirmAction, notify } from "@/shared/uiFeedback";
const items = ref<any[]>([]),
  clientes = ref<any[]>([]),
  maestros = ref<any>({ tiposCesped: [], alicuotasIva: [] }),
  show = ref(false),
  error = ref(""),
  editingId = ref<string | null>(null),
  totalEdited = ref(false),
  total = ref(0),
  loading = ref(false),
  loadError = ref("");
const dateKey = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
const filters = reactive({ periodo: "all", desde: "", hasta: "", clienteId: "", tipoCespedId: "" });
const blank = () => ({
  clienteId: "",
  fechaVenta: new Date().toISOString().slice(0, 10),
  tipoCespedId: "",
  color: "",
  cantidadM2: 1,
  precioUnitario: 0,
  precioTotal: 0,
  montoEntrega: 0,
  formaPago: "Contado",
  cantidadCuotas: null as number | null,
  estado: "Confirmada",
  fechaEntregaEstimada: null as string | null,
  observaciones: "",
  costoCompraUnitario: 0,
  costoEnvio: 0,
  otrosCostos: 0,
  alicuotaIvaId: "",
});
const form = ref(blank());
const selectedProduct = computed(() => maestros.value.tiposCesped.find((x:any) => x.id === form.value.tipoCespedId));
const availableColors = computed<string[]>(() => selectedProduct.value?.colores ?? []);
const calculatedTotal = computed(() =>
  Math.round(
    (Number(form.value.cantidadM2) || 0) *
      (Number(form.value.precioUnitario) || 0) *
      100,
  ) / 100
);
function useCalculatedTotal() {
  form.value.precioTotal = calculatedTotal.value;
  totalEdited.value = false;
}
watch(
  () => [form.value.cantidadM2, form.value.precioUnitario],
  () => {
    if (!totalEdited.value) form.value.precioTotal = calculatedTotal.value;
  },
);
function applyPeriod() {
  if (filters.periodo === "all" || filters.periodo === "custom") {
    if (filters.periodo === "all") { filters.desde = ""; filters.hasta = ""; }
    return;
  }
  const end = new Date();
  const start = new Date(end);
  if (filters.periodo === "week") start.setDate(start.getDate() - 6);
  if (filters.periodo === "month") start.setDate(start.getDate() - 29);
  if (filters.periodo === "sixMonths") start.setMonth(start.getMonth() - 6);
  filters.desde = dateKey(start);
  filters.hasta = dateKey(end);
}
function customDates() { filters.periodo = "custom"; }
function resetFilters() {
  filters.periodo = "all"; filters.desde = ""; filters.hasta = ""; filters.clienteId = ""; filters.tipoCespedId = "";
  loadSales();
}
async function loadSales() {
  loading.value = true; loadError.value = "";
  try {
    const response = await http.get("/ventas", { params: { pageSize: 100, desde: filters.desde || undefined, hasta: filters.hasta || undefined, clienteId: filters.clienteId || undefined, tipoCespedId: filters.tipoCespedId || undefined } });
    items.value = response.data.items;
    total.value = response.data.total;
  } catch { loadError.value = "No se pudieron cargar las ventas."; }
  finally { loading.value = false; }
}
async function load() {
  const [filterData, masterData] = await Promise.all([http.get("/ventas/filtros"), http.get("/maestros")]);
  clientes.value = filterData.data.clientes;
  maestros.value = { ...masterData.data, tiposCespedFiltro: filterData.data.tiposCesped };
  await loadSales();
}
/* El precio maestro se completa al cambiar producto, pero sigue siendo editable. */
watch(()=>form.value.tipoCespedId,(id)=>{const t=maestros.value.tiposCesped.find((x:any)=>x.id===id);if(!t)return;if(!editingId.value){form.value.precioUnitario=t.precioVentaM2;form.value.costoCompraUnitario=t.costoM2}if(!t.colores?.includes(form.value.color))form.value.color=t.colores?.length===1?t.colores[0]:""})
function openNew() {
  editingId.value = null;
  form.value = blank();
  totalEdited.value = false;
  error.value = "";
  show.value = true;
}
function edit(v: any) {
  editingId.value = v.id;
  form.value = {
    clienteId: v.clienteId,
    fechaVenta: v.fechaVenta,
    tipoCespedId: v.tipoCespedId,
    color: v.color ?? "",
    cantidadM2: v.cantidadM2,
    precioUnitario: v.precioUnitario,
    precioTotal: v.precioTotal,
    montoEntrega: v.montoEntrega ?? 0,
    formaPago: v.formaPago,
    cantidadCuotas: v.cantidadCuotas,
    estado: v.estado,
    fechaEntregaEstimada: v.fechaEntregaEstimada,
    observaciones: v.observaciones ?? "",
    costoCompraUnitario: v.costoCompraUnitario,
    costoEnvio: v.costoEnvio,
    otrosCostos: v.otrosCostos,
    alicuotaIvaId: v.alicuotaIvaId,
  };
  totalEdited.value = true;
  error.value = "";
  show.value = true;
}
async function save() {
  try {
    editingId.value
      ? await http.put(`/ventas/${editingId.value}`, form.value)
      : await http.post("/ventas", form.value);
    show.value = false;
    await loadSales();
  } catch (e: any) {
    error.value =
      e.response?.data?.message ??
      e.response?.data?.detail ??
      e.response?.data?.title ??
      "Revisá los datos ingresados.";
  }
}
async function remove(v: any) {
  if (!await confirmAction({title:"Eliminar venta",message:`¿Querés eliminar la venta de ${v.cliente} por ${money(v.precioTotal)}? También se eliminarán sus cuotas cobradas y los movimientos de caja relacionados.`,confirmText:"Eliminar",danger:true})) return;
  try {
    await http.delete(`/ventas/${v.id}`);
    await loadSales();
  } catch (e: any) {
    notify(e.response?.data?.message ?? "No se pudo eliminar la venta.");
  }
}
async function deliver(id: string) {
  try {
    await http.post(`/ventas/${id}/entregar`);
    await loadSales();
  } catch (e: any) {
    notify(e.response?.data?.message ?? "No se pudo actualizar.");
  }
}
onMounted(load);
</script>
<template>
  <section class="page">
    <div class="page-title">
      <div>
        <h2>Ventas</h2>
        <p>Operaciones, costos y margen en un solo lugar.</p>
      </div>
      <RouterLink class="btn" to="/ventas/nueva">+ Registrar venta</RouterLink>
    </div>
    <form class="panel sales-filters" @submit.prevent="loadSales">
      <div class="field"><label>Período</label><select v-model="filters.periodo" @change="applyPeriod"><option value="all">Todo el historial</option><option value="week">Última semana</option><option value="month">Último mes</option><option value="sixMonths">Últimos 6 meses</option><option value="custom">Personalizado</option></select></div>
      <div class="field"><label>Desde</label><input v-model="filters.desde" type="date" @change="customDates"></div>
      <div class="field"><label>Hasta</label><input v-model="filters.hasta" type="date" @change="customDates"></div>
      <div class="field"><label>Cliente</label><select v-model="filters.clienteId"><option value="">Todos los clientes</option><option v-for="client in clientes" :key="client.id" :value="client.id">{{ client.nombreCompleto || client.nombre }}</option></select></div>
      <div class="field"><label>Tipo de césped</label><select v-model="filters.tipoCespedId"><option value="">Todos los tipos</option><option v-for="type in maestros.tiposCespedFiltro || []" :key="type.id" :value="type.id">{{ type.nombre }}{{ type.activo === false ? " (inactivo)" : "" }}</option></select></div>
      <div class="filter-actions"><button class="btn" :disabled="loading">{{ loading ? "Buscando…" : "Aplicar filtros" }}</button><button type="button" class="btn secondary" @click="resetFilters">Restablecer</button></div>
    </form>
    <div v-if="loadError" class="error load-state">{{ loadError }} <button class="btn secondary compact" @click="loadSales">Reintentar</button></div>
    <div class="panel">
      <div class="panel-head"><h3>{{ total }} {{ total === 1 ? "venta encontrada" : "ventas encontradas" }}</h3></div>
      <div v-if="loading" class="loading">Cargando ventas…</div>
      <table>
        <thead>
          <tr>
            <th>Cliente / Césped</th>
            <th>Fecha</th>
            <th>Total</th>
            <th>Entrega</th>
            <th>Estado</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="v in items" v-show="!loading" :key="v.id">
            <td>
              <b>{{ v.cliente }}</b
              ><br /><small><template v-if="v.lineas?.length > 1">{{v.lineas.length}} productos</template><template v-else>{{ v.tipoCesped }}<template v-if="v.color"> · {{ v.color }}</template></template> · {{ v.cantidadM2 }} m²</small>
            </td>
            <td>{{ v.fechaVenta }}</td>
            <td>{{ money(v.precioTotal) }}</td>
            <td>{{ money(v.montoEntrega) }}</td>
            <td>
              <span class="badge">{{ v.estado }}</span>
            </td>
            <td>
              <div class="row-actions">
                <button
                  v-if="!['Entregada', 'Cancelada'].includes(v.estado)"
                  class="btn secondary compact"
                  @click="deliver(v.id)"
                >
                  Entregar</button
                ><RouterLink class="icon-btn" :to="`/ventas/${v.id}/editar`" title="Editar venta"><Pencil /></RouterLink
                ><button class="icon-btn danger" @click="remove(v)">
                  <Trash2 />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!loading && !items.length" class="empty">No hay ventas para los filtros seleccionados.</div>
    </div>
    <div v-if="show" class="modal-bg">
      <form class="modal" @submit.prevent="save">
        <h3>{{ editingId ? "Modificar venta" : "Registrar venta" }}</h3>
        <p v-if="error" class="error">{{ error }}</p>
        <div class="form-grid">
          <div class="field">
            <label>Cliente</label><ClienteAutocomplete v-model="form.clienteId" :clientes="clientes" />
          </div>
          <div class="field">
            <label>Tipo de césped</label
            ><select v-model="form.tipoCespedId" required>
              <option value="" disabled>Seleccionar</option>
              <option v-for="t in maestros.tiposCesped" :value="t.id">
                {{ t.nombre }}
              </option>
            </select>
          </div>
          <div v-if="availableColors.length" class="field">
            <label>Color</label><select v-model="form.color" required>
              <option value="" disabled>Seleccionar color</option>
              <option v-for="color in availableColors" :key="color" :value="color">{{ color }}</option>
            </select>
          </div>
          <div class="field">
            <label>Fecha de venta</label
            ><input v-model="form.fechaVenta" type="date" required />
          </div>
          <div class="field">
            <label>Estado</label
            ><select v-model="form.estado">
              <option
                v-for="x in ['Confirmada', 'Futura', 'Entregada', 'Cancelada']"
              >
                {{ x }}
              </option>
            </select>
          </div>
          <div v-if="form.estado === 'Futura'" class="field">
            <label>Entrega estimada</label
            ><input v-model="form.fechaEntregaEstimada" type="date" required />
          </div>
          <div class="field">
            <label>Cantidad m²</label
            ><input
              v-model.number="form.cantidadM2"
              type="number"
              min="0.01"
              step="0.01"
              required
            />
          </div>
          <div class="field">
            <label>Precio por m²</label
            ><input
              v-model.number="form.precioUnitario"
              type="number"
              min="0.01"
              step="0.01"
              required
            />
          </div>
          <div class="field highlight-field">
            <label>Importe final de la venta</label
            ><input
              v-model.number="form.precioTotal"
              type="number"
              min="0.01"
              step="0.01"
              required
              @input="totalEdited = true"
            /><small>
              Cálculo por m²: {{ money(calculatedTotal) }}
              <button
                v-if="form.precioTotal !== calculatedTotal"
                type="button"
                class="link-button"
                @click="useCalculatedTotal"
              >
                Usar cálculo
              </button>
            </small>
          </div>
          <div class="field highlight-field">
            <label>Monto de entrega inicial</label
            ><input
              v-model.number="form.montoEntrega"
              type="number"
              min="0.01"
              :max="form.precioTotal"
              step="0.01"
              required
            /><small>Se registra automáticamente como ingreso en Caja.</small>
          </div>
          <div class="field">
            <label>Costo compra por m²</label
            ><input
              v-model.number="form.costoCompraUnitario"
              type="number"
              min="0.01"
              step="0.01"
              required
            />
          </div>
          <div class="field">
            <label>Envío</label
            ><input v-model.number="form.costoEnvio" type="number" min="0" />
          </div>
          <div class="field">
            <label>Otros costos</label
            ><input v-model.number="form.otrosCostos" type="number" min="0" />
          </div>
          <div class="field">
            <label>Forma de pago</label
            ><select v-model="form.formaPago">
              <option
                v-for="x in [
                  'Contado',
                  'Transferencia',
                  'Cheque',
                  'Cuotas',
                  'Otros',
                ]"
              >
                {{ x }}
              </option>
            </select>
          </div>
          <div v-if="form.formaPago === 'Cuotas'" class="field">
            <label>Cuotas sobre el saldo</label
            ><input
              v-model.number="form.cantidadCuotas"
              type="number"
              min="1"
              max="60"
              required
            />
          </div>
          <div class="field">
            <label>IVA</label
            ><select v-model="form.alicuotaIvaId" required>
              <option value="" disabled>Seleccionar</option>
              <option v-for="a in maestros.alicuotasIva" :value="a.id">
                {{ a.nombre }}
              </option>
            </select>
          </div>
          <div class="field">
            <label>Observaciones</label
            ><textarea v-model="form.observaciones"></textarea>
          </div>
        </div>
        <div class="actions">
          <button type="button" class="btn secondary" @click="show = false">
            Cancelar</button
          ><button class="btn">
            {{ editingId ? "Guardar cambios" : "Confirmar venta" }}
          </button>
        </div>
      </form>
    </div>
  </section>
</template>
