<script setup lang="ts">
import { ref, onMounted, watch, computed, reactive } from "vue";
import AppButton from "@/shared/components/AppButton.vue";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import Dialog from "primevue/dialog";
import InputNumber from "primevue/inputnumber";
import AppDatePicker from "@/shared/components/AppDatePicker.vue";
import InputText from "primevue/inputtext";
import Message from "primevue/message";
import Panel from "primevue/panel";
import Select from "primevue/select";
import Tag from "primevue/tag";
import Textarea from "primevue/textarea";
import AppIcon from "@/shared/components/AppIcon.vue";
import { faPen, faTrash } from "@/shared/icons";
import { http } from "@/shared/api/httpClient";
import ClienteAutocomplete from "@/shared/components/ClienteAutocomplete.vue";
import { formatCurrency as money } from "@/shared/formatters";
import { confirmAction, notify } from "@/shared/uiFeedback";
import { formatDateRangeLabel, monthRange, sixMonthsRange, weekRange } from "@/shared/dateFilters";
import { useImmediateFilters } from "@/shared/composables/useFilterTriggers";
import { TABLE_ROWS, TABLE_ROWS_OPTIONS, tableFirst, type DataTablePageEvent } from "@/shared/tablePagination";

const items = ref<any[]>([]),
  clientes = ref<any[]>([]),
  maestros = ref<any>({ tiposCesped: [], alicuotasIva: [] }),
  show = ref(false),
  error = ref(""),
  editingId = ref<string | null>(null),
  totalEdited = ref(false),
  total = ref(0),
  page = ref(1),
  pageSize = ref(TABLE_ROWS),
  first = computed(() => tableFirst(page.value, pageSize.value)),
  loading = ref(false),
  loadError = ref("");

const periodoOptions = [
  { label: "Todo el historial", value: "all" },
  { label: "Última semana", value: "week" },
  { label: "Último mes", value: "month" },
  { label: "Últimos 6 meses", value: "sixMonths" },
  { label: "Personalizado", value: "custom" },
];
const estadoOptions = ["Confirmada", "Futura", "Entregada", "Cancelada"];
const formaPagoOptions = ["Contado", "Transferencia", "Cheque", "Cuotas", "Otros"];

const filters = reactive({ periodo: "month", ...monthRange(), clienteId: "", tipoCespedId: "" });
const filterPeriodLabel = computed(() => formatDateRangeLabel(filters.desde, filters.hasta));
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

function saleStatusSeverity(status: string) {
  return ({
    Cancelada: "danger",
    Futura: "secondary",
    Confirmada: "warn",
    Entregada: "success",
  } as const)[status] ?? "secondary";
}

const clienteFilterOptions = computed(() => [
  { id: "", label: "Todos los clientes" },
  ...clientes.value.map((c: any) => ({ id: c.id, label: c.nombreCompleto || c.nombre })),
]);
const tipoCespedFilterOptions = computed(() => [
  { id: "", label: "Todos los tipos" },
  ...(maestros.value.tiposCespedFiltro || []).map((t: any) => ({
    id: t.id,
    label: `${t.nombre}${t.activo === false ? " (inactivo)" : ""}`,
  })),
]);
const selectedProduct = computed(() => maestros.value.tiposCesped.find((x: any) => x.id === form.value.tipoCespedId));
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
  if (filters.periodo === "all") {
    filters.desde = "";
    filters.hasta = "";
    return;
  }
  if (filters.periodo === "custom") return;
  const ranges = { week: weekRange(), month: monthRange(), sixMonths: sixMonthsRange() };
  const range = ranges[filters.periodo as keyof typeof ranges];
  if (range) {
    filters.desde = range.desde;
    filters.hasta = range.hasta;
  }
}

function customDates() { filters.periodo = "custom"; }

function resetFilters() {
  filters.periodo = "month";
  Object.assign(filters, monthRange(), { clienteId: "", tipoCespedId: "" });
}

function onPage(event: DataTablePageEvent) {
  page.value = event.page + 1;
  pageSize.value = event.rows;
  loadSales();
}

async function loadSales() {
  loading.value = true; loadError.value = "";
  try {
    const response = await http.get("/ventas", { params: { page: page.value, pageSize: pageSize.value, desde: filters.desde || undefined, hasta: filters.hasta || undefined, clienteId: filters.clienteId || undefined, tipoCespedId: filters.tipoCespedId || undefined } });
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

watch(() => form.value.tipoCespedId, (id) => { const t = maestros.value.tiposCesped.find((x: any) => x.id === id); if (!t) return; if (!editingId.value) { form.value.precioUnitario = t.precioVentaM2; form.value.costoCompraUnitario = t.costoM2; } if (!t.colores?.includes(form.value.color)) form.value.color = t.colores?.length === 1 ? t.colores[0] : ""; });

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
  if (!await confirmAction({ title: "Eliminar venta", message: `¿Querés eliminar la venta de ${v.cliente} por ${money(v.precioTotal)}? También se eliminarán sus cuotas cobradas y los movimientos de caja relacionados.`, confirmText: "Eliminar", danger: true })) return;
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

watch(() => filters.periodo, () => applyPeriod());

useImmediateFilters(() => filters, () => {
  page.value = 1;
  loadSales();
});

onMounted(load);
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Operaciones, costos y margen en un solo lugar.</p>
      <RouterLink to="/ventas/nueva">
        <AppButton label="Registrar venta" icon="pi pi-plus" />
      </RouterLink>
    </div>

    <Panel class="filter-panel">
      <p v-if="filters.periodo !== 'all'" class="filter-period-hint pt-2">
        Período activo: <strong>{{ filterPeriodLabel }}</strong>
      </p>
      <div class="grid formgrid p-fluid filter-form">
        <div class="field col-12 md:col-6 lg:col-3">
          <label for="periodo">Período</label>
          <Select
            id="periodo"
            v-model="filters.periodo"
            :options="periodoOptions"
            option-label="label"
            option-value="value"
          />
        </div>
        <div class="field col-12 md:col-6 lg:col-3">
          <label for="desde">Desde</label>
          <AppDatePicker id="desde" v-model="filters.desde" @change="customDates" />
        </div>
        <div class="field col-12 md:col-6 lg:col-3">
          <label for="hasta">Hasta</label>
          <AppDatePicker id="hasta" v-model="filters.hasta" @change="customDates" />
        </div>
        <div class="field col-12 md:col-6 lg:col-3">
          <label for="clienteFiltro">Cliente</label>
          <Select
            id="clienteFiltro"
            v-model="filters.clienteId"
            :options="clienteFilterOptions"
            option-label="label"
            option-value="id"
          />
        </div>
        <div class="field col-12 md:col-6 lg:col-3">
          <label for="tipoCespedFiltro">Tipo de césped</label>
          <Select
            id="tipoCespedFiltro"
            v-model="filters.tipoCespedId"
            :options="tipoCespedFilterOptions"
            option-label="label"
            option-value="id"
          />
        </div>
        <div class="field col-12 md:col-6 lg:col-3 filter-actions flex align-items-end">
          <AppButton type="button" label="Restablecer" icon="pi pi-filter-slash" severity="secondary" @click="resetFilters" />
        </div>
      </div>
    </Panel>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="loadSales" />
    </Message>

    <div v-else class="table-panel">
      <DataTable
        :value="items"
        :loading="loading"
        lazy
        paginator
        :rows="pageSize"
        :total-records="total"
        :first="first"
        :rows-per-page-options="TABLE_ROWS_OPTIONS"
        striped-rows
        @page="onPage"
      >
        <template #empty>
          <div class="text-center py-5">
            <p class="font-bold mb-2">No hay ventas</p>
            <p class="text-color-secondary mb-3">No encontramos operaciones para los filtros seleccionados.</p>
            <RouterLink to="/ventas/nueva">
              <AppButton label="Registrar venta" icon="pi pi-plus" />
            </RouterLink>
          </div>
        </template>
        <Column header="Cliente / Césped" body-class="cell-wrap">
          <template #body="{ data: v }">
            <b>{{ v.cliente }}</b><br>
            <small class="text-color-secondary">
              <template v-if="v.lineas?.length > 1">{{ v.lineas.length }} productos</template>
              <template v-else>{{ v.tipoCesped }}<template v-if="v.color"> · {{ v.color }}</template></template>
              · {{ v.cantidadM2 }} m²
            </small>
          </template>
        </Column>
        <Column field="fechaVenta" header="Fecha" />
        <Column header="Total" body-class="cell-num">
          <template #body="{ data: v }">{{ money(v.precioTotal) }}</template>
        </Column>
        <Column header="Entrega" body-class="cell-num">
          <template #body="{ data: v }">{{ money(v.montoEntrega) }}</template>
        </Column>
        <Column header="Estado">
          <template #body="{ data: v }">
            <Tag :value="v.estado" :severity="saleStatusSeverity(v.estado)" />
          </template>
        </Column>
        <Column header="Acciones" body-class="cell-actions">
          <template #body="{ data: v }">
            <div class="flex gap-1 flex-wrap">
              <AppButton
                v-if="!['Entregada', 'Cancelada'].includes(v.estado)"
                label="Entregar"
                size="small"
                @click="deliver(v.id)"
              />
              <RouterLink :to="`/ventas/${v.id}/editar`">
                <AppButton text rounded severity="secondary" aria-label="Editar venta">
                  <AppIcon :icon="faPen" />
                </AppButton>
              </RouterLink>
              <AppButton text rounded severity="danger" aria-label="Eliminar venta" @click="remove(v)">
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
      :header="editingId ? 'Modificar venta' : 'Registrar venta'"
      :style="{ width: 'min(760px, 96vw)' }"
    >
      <form @submit.prevent="save">
        <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>

        <div class="grid formgrid p-fluid">
          <div class="field col-12 md:col-6">
            <label>Cliente</label>
            <ClienteAutocomplete v-model="form.clienteId" :clientes="clientes" />
          </div>
          <div class="field col-12 md:col-6">
            <label>Tipo de césped</label>
            <Select
              v-model="form.tipoCespedId"
              :options="maestros.tiposCesped"
              option-label="nombre"
              option-value="id"
              placeholder="Seleccionar"
              required
            />
          </div>
          <div v-if="availableColors.length" class="field col-12 md:col-6">
            <label>Color</label>
            <Select
              v-model="form.color"
              :options="availableColors"
              placeholder="Seleccionar color"
              required
            />
          </div>
          <div class="field col-12 md:col-6">
            <label>Fecha de venta</label>
            <AppDatePicker v-model="form.fechaVenta" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>Estado</label>
            <Select v-model="form.estado" :options="estadoOptions" />
          </div>
          <div v-if="form.estado === 'Futura'" class="field col-12 md:col-6">
            <label>Entrega estimada</label>
            <AppDatePicker v-model="form.fechaEntregaEstimada" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>Cantidad m²</label>
            <InputNumber v-model="form.cantidadM2" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>Precio por m²</label>
            <InputNumber v-model="form.precioUnitario" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" required />
          </div>
          <div class="field col-12 md:col-6 highlight-field">
            <label>Importe final de la venta</label>
            <InputNumber
              v-model="form.precioTotal"
              :min="0.01"
              :min-fraction-digits="2"
              :max-fraction-digits="2"
              mode="currency"
              currency="ARS"
              locale="es-AR"
              required
              @update:model-value="totalEdited = true"
            />
            <small class="text-color-secondary">
              Cálculo por m²: {{ money(calculatedTotal) }}
              <AppButton
                v-if="form.precioTotal !== calculatedTotal"
                type="button"
                label="Usar cálculo"
                link
                class="p-0 ml-1"
                @click="useCalculatedTotal"
              />
            </small>
          </div>
          <div class="field col-12 md:col-6 highlight-field">
            <label>Monto de entrega inicial</label>
            <InputNumber
              v-model="form.montoEntrega"
              :min="0.01"
              :max="form.precioTotal"
              :min-fraction-digits="2"
              :max-fraction-digits="2"
              mode="currency"
              currency="ARS"
              locale="es-AR"
              required
            />
            <small class="text-color-secondary">Se registra automáticamente como ingreso en Caja.</small>
          </div>
          <div class="field col-12 md:col-6">
            <label>Costo compra por m²</label>
            <InputNumber v-model="form.costoCompraUnitario" :min="0.01" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>Envío</label>
            <InputNumber v-model="form.costoEnvio" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" />
          </div>
          <div class="field col-12 md:col-6">
            <label>Otros costos</label>
            <InputNumber v-model="form.otrosCostos" :min="0" :min-fraction-digits="2" :max-fraction-digits="2" mode="currency" currency="ARS" locale="es-AR" />
          </div>
          <div class="field col-12 md:col-6">
            <label>Forma de pago</label>
            <Select v-model="form.formaPago" :options="formaPagoOptions" />
          </div>
          <div v-if="form.formaPago === 'Cuotas'" class="field col-12 md:col-6">
            <label>Cuotas sobre el saldo</label>
            <InputNumber v-model="form.cantidadCuotas" :min="1" :max="60" required />
          </div>
          <div class="field col-12 md:col-6">
            <label>IVA</label>
            <Select
              v-model="form.alicuotaIvaId"
              :options="maestros.alicuotasIva"
              option-label="nombre"
              option-value="id"
              placeholder="Seleccionar"
              required
            />
          </div>
          <div class="field col-12">
            <label>Observaciones</label>
            <Textarea v-model="form.observaciones" rows="3" auto-resize />
          </div>
        </div>

        <div class="flex justify-content-end gap-2 mt-4">
          <AppButton type="button" label="Cancelar" severity="secondary" @click="show = false" />
          <AppButton type="submit" :label="editingId ? 'Guardar cambios' : 'Confirmar venta'" />
        </div>
      </form>
    </Dialog>
  </section>
</template>
