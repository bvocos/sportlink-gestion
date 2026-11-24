<script setup lang="ts">
import { ref, onMounted, watch, computed, reactive } from "vue";
import { useRoute } from "vue-router";
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
import { faPen, faTrash, faTruck, faWallet } from "@/shared/icons";
import { http, apiErrorMessage } from "@/shared/api/httpClient";
import ClienteAutocomplete from "@/shared/components/ClienteAutocomplete.vue";
import { formatCurrency as money } from "@/shared/formatters";
import { confirmAction, notify } from "@/shared/uiFeedback";
import { formatDateRangeLabel, monthRange, sixMonthsRange, weekRange } from "@/shared/dateFilters";
import { useImmediateFilters } from "@/shared/composables/useFilterTriggers";
import { TABLE_ROWS, TABLE_ROWS_OPTIONS, tableFirst, type DataTablePageEvent } from "@/shared/tablePagination";
import { auth } from "@/auth";
import { condicionIvaLabels } from "@/shared/fiscal";
import { importeEnPesos, validarCotizacion, type MonedaCaja } from "@/monedaCaja";

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
  loadError = ref(""),
  remitoVisible = ref(false),
  remitoSale = ref<any | null>(null),
  remitoDraft = ref<any | null>(null),
  remitos = ref<any[]>([]),
  remitoLoading = ref(false),
  remitoSaving = ref(false),
  remitoError = ref("");

const canCreateRemito = computed(() => auth.can("ventas", "crear"));
const condicionIvaOptions = condicionIvaLabels;
const currencyOptions = [{ label: "Dólares", value: "USD" }, { label: "Pesos", value: "ARS" }];
const blueVenta = ref<number | null>(null);
const route = useRoute();
const cobrosVisible = ref(false);
const cobrosLoading = ref(false);
const cobrosSaving = ref(false);
const cobrosError = ref('');
const cobrosData = ref<any | null>(null);
const cobroSale = ref<any | null>(null);
const cobroForm = ref({ importeUsd: 0, fechaCobro: new Date().toISOString().slice(0, 10), monedaCobro: 'USD' as MonedaCaja, cotizacion: null as number | null, medioPago: 'Efectivo', observaciones: '' });
const anularId = ref<string | null>(null);
const motivoAnulacion = ref('');
const medioPagoOptions = ['Efectivo', 'Transferencia', 'Cheque', 'Tarjeta', 'Otros'];
const blankRemito = () => ({
  receptorNombre: "",
  receptorDomicilio: "",
  receptorLocalidad: "",
  receptorProvincia: "",
  receptorTelefono: "",
  receptorCuit: "",
  receptorCondicionIva: null as string | null,
  observaciones: "",
});
const remitoForm = ref(blankRemito());

const periodoOptions = [
  { label: "Todo el historial", value: "all" },
  { label: "Última semana", value: "week" },
  { label: "Último mes", value: "month" },
  { label: "Últimos 6 meses", value: "sixMonths" },
  { label: "Personalizado", value: "custom" },
];
const estadoOptions = ["Confirmada", "Futura", "Entregada", "Cancelada"];
const formaPagoOptions = ["Contado", "Transferencia", "Cheque", "Cuotas", "Otros"];

const filters = reactive({ periodo: "month", ...monthRange(), clienteId: "", tipoCespedId: "", cobro: route.query.cobro === 'pendiente' || route.query.cobro === 'cobrada' ? String(route.query.cobro) : "" });
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
  monedaEntrega: "USD" as MonedaCaja,
  cotizacionEntrega: null as number | null,
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
const entregaPesos = computed(() => importeEnPesos(form.value.montoEntrega, form.value.cotizacionEntrega));

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
  Object.assign(filters, monthRange(), { clienteId: "", tipoCespedId: "", cobro: "" });
}

function onPage(event: DataTablePageEvent) {
  page.value = event.page + 1;
  pageSize.value = event.rows;
  loadSales();
}

async function loadSales() {
  loading.value = true; loadError.value = "";
  try {
    const response = await http.get("/ventas", { params: { page: page.value, pageSize: pageSize.value, desde: filters.desde || undefined, hasta: filters.hasta || undefined, clienteId: filters.clienteId || undefined, tipoCespedId: filters.tipoCespedId || undefined, cobro: filters.cobro || undefined } });
    items.value = response.data.items;
    total.value = response.data.total;
  } catch { loadError.value = "No se pudieron cargar las ventas."; }
  finally { loading.value = false; }
}

async function load() {
  loading.value = true;
  loadError.value = "";
  try {
    const [filterData, masterData] = await Promise.all([http.get("/ventas/filtros"), http.get("/maestros")]);
    clientes.value = filterData.data.clientes;
    maestros.value = { ...masterData.data, tiposCespedFiltro: filterData.data.tiposCesped };
    try { blueVenta.value = (await http.get('/cotizaciones/dolares')).data?.blue?.venta ?? null; } catch { blueVenta.value = null; }
    await loadSales();
  } catch (e) {
    loadError.value = apiErrorMessage(e, "No se pudieron cargar los datos necesarios para las ventas.");
  } finally {
    loading.value = false;
  }
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
    monedaEntrega: v.monedaEntrega ?? "USD",
    cotizacionEntrega: v.cotizacionEntrega ?? null,
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
  const currencyError = validarCotizacion(form.value.monedaEntrega, form.value.cotizacionEntrega);
  if (currencyError) { error.value = currencyError; return; }
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
watch(() => form.value.monedaEntrega, moneda => {
  if (moneda === 'USD') form.value.cotizacionEntrega = null;
  else if (!form.value.cotizacionEntrega) form.value.cotizacionEntrega = blueVenta.value;
});

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
    const delivered = items.value.find(x => x.id === id);
    if (delivered?.saldoUsd > 0) notify(`Se entregó con saldo pendiente de ${money(delivered.saldoUsd)}.`);
    await loadSales();
  } catch (e: any) {
    notify(e.response?.data?.message ?? "No se pudo actualizar.");
  }
}

async function loadCobros() {
  if (!cobroSale.value) return;
  cobrosLoading.value = true; cobrosError.value = '';
  try { cobrosData.value = (await http.get(`/ventas/${cobroSale.value.id}/cobros`)).data; }
  catch (e) { cobrosError.value = apiErrorMessage(e, 'No se pudieron cargar los cobros.'); }
  finally { cobrosLoading.value = false; }
}
async function openCobros(v: any) {
  cobroSale.value = v; cobrosVisible.value = true; anularId.value = null;
  await loadCobros();
  cobroForm.value = { importeUsd: cobrosData.value?.saldoUsd ?? 0, fechaCobro: new Date().toISOString().slice(0, 10), monedaCobro: 'USD', cotizacion: cobrosData.value?.cotizacionReferencia ?? blueVenta.value, medioPago: 'Efectivo', observaciones: '' };
}
async function registrarCobro() {
  if (!cobroSale.value || cobrosSaving.value) return;
  cobrosSaving.value = true; cobrosError.value = '';
  try {
    const created = (await http.post(`/ventas/${cobroSale.value.id}/cobros`, cobroForm.value)).data;
    await Promise.all([loadCobros(), loadSales()]);
    notify(`Cobro registrado. Recibo N° ${String(created.numeroRecibo).padStart(8, '0')}.`, 'success');
  } catch (e) { cobrosError.value = apiErrorMessage(e, 'No se pudo registrar el cobro.'); }
  finally { cobrosSaving.value = false; }
}
async function activarSeguimiento() {
  if (!cobroSale.value || !await confirmAction({ title: 'Controlar saldo', message: '¿Querés comenzar a controlar el saldo de esta venta?', confirmText: 'Activar' })) return;
  try { await http.post(`/ventas/${cobroSale.value.id}/seguimiento-saldo`); await Promise.all([loadCobros(), loadSales()]); }
  catch (e) { cobrosError.value = apiErrorMessage(e, 'No se pudo activar el seguimiento.'); }
}
async function anularCobro() {
  if (!cobroSale.value || !anularId.value || !motivoAnulacion.value.trim()) return;
  try { await http.post(`/ventas/${cobroSale.value.id}/cobros/${anularId.value}/anular`, { motivo: motivoAnulacion.value }); anularId.value = null; motivoAnulacion.value = ''; await Promise.all([loadCobros(), loadSales()]); }
  catch (e) { cobrosError.value = apiErrorMessage(e, 'No se pudo anular el cobro.'); }
}
async function descargarRecibo(cobro: any) {
  if (!cobroSale.value) return;
  const response = await http.get(`/ventas/${cobroSale.value.id}/cobros/${cobro.id}/recibo`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data); const a = document.createElement('a'); a.href = url; a.download = `Recibo-${cobro.numeroRecibo}.pdf`; a.click(); URL.revokeObjectURL(url);
}

async function openRemito(v: any) {
  remitoSale.value = v;
  remitoVisible.value = true;
  remitoLoading.value = true;
  remitoError.value = "";
  remitoDraft.value = null;
  remitos.value = [];
  try {
    const [draft, existing] = await Promise.all([
      http.get(`/ventas/${v.id}/remito-borrador`),
      http.get(`/ventas/${v.id}/remitos`),
    ]);
    remitoDraft.value = draft.data;
    remitos.value = existing.data;
    remitoForm.value = {
      receptorNombre: draft.data.receptorNombre ?? "",
      receptorDomicilio: draft.data.receptorDomicilio ?? "",
      receptorLocalidad: draft.data.receptorLocalidad ?? "",
      receptorProvincia: draft.data.receptorProvincia ?? "",
      receptorTelefono: draft.data.receptorTelefono ?? "",
      receptorCuit: draft.data.receptorCuit ?? "",
      receptorCondicionIva: draft.data.receptorCondicionIva || null,
      observaciones: draft.data.observaciones ?? "",
    };
  } catch (e: any) {
    remitoError.value = apiErrorMessage(e, "No se pudieron cargar los datos del remito.");
  } finally {
    remitoLoading.value = false;
  }
}

async function refreshRemitos() {
  if (!remitoSale.value) return;
  remitos.value = (await http.get(`/ventas/${remitoSale.value.id}/remitos`)).data;
}

async function downloadRemito(remito: any) {
  try {
    const response = await http.get(`/remitos/${remito.id}/pdf`, { responseType: "blob" });
    const url = URL.createObjectURL(new Blob([response.data], { type: "application/pdf" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `Remito-${remito.numeroTexto}.pdf`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  } catch (e: any) {
    notify(apiErrorMessage(e, "No se pudo descargar el remito."));
  }
}

async function createRemito() {
  if (!remitoSale.value) return;
  remitoSaving.value = true;
  remitoError.value = "";
  try {
    const created = (await http.post(`/ventas/${remitoSale.value.id}/remitos`, remitoForm.value)).data;
    await downloadRemito(created);
    await refreshRemitos();
    notify("Remito generado correctamente.", "success");
  } catch (e: any) {
    remitoError.value = apiErrorMessage(e, "No se pudo generar el remito.");
  } finally {
    remitoSaving.value = false;
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
          <div class="w-full">
            <label for="cobroFiltro">Cobro</label>
            <Select id="cobroFiltro" v-model="filters.cobro" :options="[
              { label: 'Todas', value: '' }, { label: 'Con saldo pendiente', value: 'pendiente' }, { label: 'Cobradas', value: 'cobrada' }
            ]" option-label="label" option-value="value" />
          </div>
        </div>
        <div class="field col-12 md:col-6 lg:col-3 filter-actions flex align-items-end">
          <AppButton type="button" label="Restablecer" icon="pi pi-filter-slash" severity="secondary" @click="resetFilters" />
        </div>
      </div>
    </Panel>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
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
        <Column v-if="auth.canVerMontos('ventas')" header="Cobro">
          <template #body="{ data: v }">
            <Tag v-if="v.seguimientoSaldo && v.saldoUsd > 0" :value="`Debe ${money(v.saldoUsd)}`" severity="warn" />
            <Tag v-else-if="v.seguimientoSaldo" value="Cobrada" severity="success" />
          </template>
        </Column>
        <Column header="Acciones" body-class="cell-actions">
          <template #body="{ data: v }">
            <div class="flex gap-1 flex-wrap">
              <AppButton v-if="v.formaPago !== 'Cuotas' && v.estado !== 'Cancelada' && auth.canVerMontos('ventas')"
                text rounded severity="secondary" tooltip="Cobros" aria-label="Cobros" @click="openCobros(v)">
                <AppIcon :icon="faWallet" />
              </AppButton>
              <AppButton
                v-if="!['Entregada', 'Cancelada'].includes(v.estado)"
                text
                rounded
                severity="success"
                tooltip="Entrega"
                aria-label="Entrega"
                @click="deliver(v.id)"
              >
                <AppIcon :icon="faTruck" />
              </AppButton>
              <AppButton
                v-if="v.estado !== 'Cancelada'"
                text
                rounded
                severity="secondary"
                icon="pi pi-truck"
                tooltip="Remito"
                aria-label="Remito"
                @click="openRemito(v)"
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
            <label>Cobrado en</label>
            <Select v-model="form.monedaEntrega" :options="currencyOptions" option-label="label" option-value="value" />
          </div>
          <div v-if="form.monedaEntrega === 'ARS'" class="field col-12 md:col-6">
            <label>Cotización (ARS por USD)</label>
            <InputNumber v-model="form.cotizacionEntrega" :min="0.0001" :min-fraction-digits="2" :max-fraction-digits="4" required />
            <small v-if="entregaPesos !== null" class="text-color-secondary">Equivale a ARS {{ entregaPesos.toLocaleString('es-AR', { minimumFractionDigits: 2 }) }}</small>
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

    <Dialog
      v-model:visible="remitoVisible"
      modal
      header="Remito de venta"
      :style="{ width: 'min(820px, 96vw)' }"
    >
      <Message v-if="remitoError" severity="error" class="mb-3" :closable="false">{{ remitoError }}</Message>
      <Message v-if="remitoLoading" severity="info" :closable="false">Cargando datos del remito…</Message>
      <div v-else class="flex flex-column gap-4">
        <section v-if="remitos.length">
          <h4 class="mt-0 mb-2">Remitos generados</h4>
          <div v-for="remito in remitos" :key="remito.id"
            class="flex justify-content-between align-items-center flex-wrap gap-2 border-1 surface-border border-round p-2 mb-2">
            <div>
              <b>{{ remito.numeroTexto }}</b>
              <small class="block text-color-secondary">{{ remito.fecha }} · {{ remito.receptorNombre }}</small>
            </div>
            <AppButton type="button" label="Descargar PDF" icon="pi pi-download" severity="secondary"
              size="small" @click="downloadRemito(remito)" />
          </div>
        </section>

        <form v-if="canCreateRemito && remitoDraft" @submit.prevent="createRemito">
          <h4 class="mt-0 mb-2">Nuevo remito</h4>
          <div class="grid formgrid p-fluid">
            <div class="field col-12 md:col-6">
              <label>Nombre de quien recibe</label>
              <InputText v-model="remitoForm.receptorNombre" maxlength="200" required />
            </div>
            <div class="field col-12 md:col-6">
              <label>Documento (CUIT/CUIL/DNI)</label>
              <InputText v-model="remitoForm.receptorCuit" maxlength="13" />
            </div>
            <div class="field col-12">
              <label>Domicilio</label>
              <InputText v-model="remitoForm.receptorDomicilio" maxlength="250" />
            </div>
            <div class="field col-12 md:col-6">
              <label>Localidad</label>
              <InputText v-model="remitoForm.receptorLocalidad" maxlength="150" />
            </div>
            <div class="field col-12 md:col-6">
              <label>Provincia</label>
              <InputText v-model="remitoForm.receptorProvincia" maxlength="150" />
            </div>
            <div class="field col-12 md:col-6">
              <label>Teléfono</label>
              <InputText v-model="remitoForm.receptorTelefono" maxlength="50" />
            </div>
            <div class="field col-12 md:col-6">
              <label>Condición de IVA</label>
              <Select v-model="remitoForm.receptorCondicionIva" :options="condicionIvaOptions"
                placeholder="Sin especificar" show-clear />
            </div>
            <div class="field col-12">
              <label>Observaciones</label>
              <Textarea v-model="remitoForm.observaciones" maxlength="1000" rows="3" auto-resize />
            </div>
          </div>

          <div class="border-1 surface-border border-round p-3 mb-3">
            <h5 class="mt-0 mb-2">Productos incluidos</h5>
            <div v-for="(linea, index) in remitoDraft.lineas" :key="index"
              class="flex justify-content-between gap-3 py-2 border-bottom-1 surface-border">
              <span>
                <span>{{ linea.producto }}<template v-if="linea.color"> · {{ linea.color }}</template></span>
                <small v-for="(importacion, importIndex) in linea.importaciones ?? []" :key="importIndex"
                  class="block text-color-secondary mt-1">
                  <template v-if="importacion.codigo">Imp. {{ importacion.codigo }}<template v-if="importacion.numeroDespacho"> · Despacho {{ importacion.numeroDespacho }}</template></template>
                </small>
              </span>
              <b class="white-space-nowrap">{{ linea.cantidadM2 }} m²</b>
            </div>
          </div>

          <div class="flex justify-content-end gap-2 flex-wrap">
            <AppButton type="button" label="Cancelar" severity="secondary" @click="remitoVisible = false" />
            <AppButton type="submit" label="Generar y descargar PDF" icon="pi pi-download"
              :loading="remitoSaving" />
          </div>
        </form>
        <Message v-else-if="!canCreateRemito && !remitos.length" severity="info" :closable="false">
          No hay remitos generados para esta venta.
        </Message>
      </div>
    </Dialog>

    <Dialog v-model:visible="cobrosVisible" modal header="Cobros de la venta" :style="{ width: 'min(980px, 96vw)' }">
      <Message v-if="cobrosError" severity="error" :closable="false" class="mb-3">{{ cobrosError }}</Message>
      <Message v-if="cobrosLoading" severity="info" :closable="false">Cargando cobros…</Message>
      <div v-else-if="cobrosData" class="flex flex-column gap-3">
        <div class="grid">
          <div class="col-12 md:col-3"><small>Cliente</small><b class="block">{{ cobrosData.cliente }}</b></div>
          <div class="col-6 md:col-3"><small>Total</small><b class="block">{{ money(cobrosData.precioTotalUsd) }}</b></div>
          <div class="col-6 md:col-3"><small>Cobrado</small><b class="block">{{ money(cobrosData.cobradoUsd) }}</b></div>
          <div class="col-12 md:col-3"><small>Saldo</small><b class="block text-orange-600">{{ money(cobrosData.saldoUsd) }}</b></div>
        </div>
        <div class="surface-200 border-round overflow-hidden" style="height:.65rem"><div class="bg-green-500 h-full" :style="{ width: `${Math.min(100, cobrosData.precioTotalUsd ? cobrosData.cobradoUsd / cobrosData.precioTotalUsd * 100 : 0)}%` }" /></div>
        <Message v-if="!cobrosData.seguimientoSaldo" severity="warn" :closable="false">Esta venta no tiene seguimiento de saldo.<AppButton v-if="auth.can('ventas', 'editar')" label="Controlar saldo de esta venta" size="small" class="ml-2" @click="activarSeguimiento" /></Message>
        <div class="table-panel">
          <DataTable :value="[{ id:'entrega', fechaCobro:cobrosData.entrega.fecha, importeUsd:cobrosData.entrega.montoUsd, importeMonedaCobro:cobrosData.entrega.importeMoneda, monedaCobro:cobrosData.entrega.moneda, cotizacion:cobrosData.entrega.cotizacion, medioPago:'Entrega inicial', inicial:true }, ...cobrosData.cobros]" striped-rows>
            <Column field="fechaCobro" header="Fecha" /><Column field="medioPago" header="Concepto / medio" />
            <Column header="Recibido"><template #body="{ data:c }">{{ c.monedaCobro }} {{ Number(c.importeMonedaCobro).toLocaleString('es-AR', { minimumFractionDigits:2 }) }}</template></Column>
            <Column header="USD aplicado"><template #body="{ data:c }"><span :class="{ 'line-through text-color-secondary':c.anulado }">{{ money(c.importeUsd) }}</span></template></Column>
            <Column header="Cotización"><template #body="{ data:c }">{{ c.cotizacion ? Number(c.cotizacion).toLocaleString('es-AR') : '—' }}</template></Column>
            <Column header="Recibo"><template #body="{ data:c }">{{ c.numeroRecibo ? String(c.numeroRecibo).padStart(8, '0') : '—' }}</template></Column>
            <Column header="Estado"><template #body="{ data:c }"><Tag :value="c.anulado ? 'Anulado' : 'Vigente'" :severity="c.anulado ? 'danger' : 'success'" /></template></Column>
            <Column header=""><template #body="{ data:c }"><div v-if="!c.inicial && !c.anulado" class="flex gap-1"><AppButton icon="pi pi-file-pdf" text rounded tooltip="Recibo PDF" @click="descargarRecibo(c)" /><AppButton icon="pi pi-ban" text rounded severity="danger" tooltip="Anular" @click="anularId = c.id" /></div></template></Column>
          </DataTable>
        </div>
        <form v-if="cobrosData.seguimientoSaldo && cobrosData.saldoUsd > 0" class="card p-3" @submit.prevent="registrarCobro">
          <h4 class="mt-0">Registrar cobro</h4>
          <div class="grid formgrid p-fluid">
            <div class="field col-12 md:col-3"><label>Importe USD</label><InputNumber v-model="cobroForm.importeUsd" :min="0.01" :max="cobrosData.saldoUsd" :min-fraction-digits="2" required /></div>
            <div class="field col-12 md:col-3"><label>Fecha</label><AppDatePicker v-model="cobroForm.fechaCobro" required /></div>
            <div class="field col-12 md:col-3"><label>Cobrado en</label><Select v-model="cobroForm.monedaCobro" :options="currencyOptions" option-label="label" option-value="value" /></div>
            <div class="field col-12 md:col-3"><label>Cotización</label><InputNumber v-model="cobroForm.cotizacion" :min="0.01" :min-fraction-digits="2" /></div>
            <div class="field col-12 md:col-4"><label>Medio de pago</label><Select v-model="cobroForm.medioPago" :options="medioPagoOptions" /></div>
            <div class="field col-12 md:col-8"><label>Observación</label><InputText v-model="cobroForm.observaciones" maxlength="500" /></div>
          </div>
          <small class="block mb-2">Si cargás un cobro de días anteriores, ajustá la cotización a la de ese día.</small>
          <div class="text-right"><AppButton type="submit" label="Registrar cobro" :loading="cobrosSaving" /></div>
        </form>
        <form v-if="anularId" class="card p-3" @submit.prevent="anularCobro"><label>Motivo de anulación</label><Textarea v-model="motivoAnulacion" rows="2" maxlength="500" class="w-full mb-2" required /><div class="text-right"><AppButton label="Confirmar anulación" severity="danger" type="submit" /></div></form>
      </div>
    </Dialog>
  </section>
</template>
