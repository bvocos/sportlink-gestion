<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import AppButton from "@/shared/components/AppButton.vue";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import Dialog from "primevue/dialog";
import InputNumber from "primevue/inputnumber";
import AppDatePicker from "@/shared/components/AppDatePicker.vue";
import SelectButton from "primevue/selectbutton";
import InputText from "primevue/inputtext";
import Message from "primevue/message";
import Panel from "primevue/panel";
import Select from "primevue/select";
import Tag from "primevue/tag";
import { http, apiErrorMessage } from "@/shared/api/httpClient";
import { auth } from "@/auth";
import { formatCurrency as money } from "@/shared/formatters";
import { downloadCsv } from "@/shared/csv";
import { confirmAction, notify } from "@/shared/uiFeedback";
import { isSearchFilterActive, searchQuery } from "@/shared/composables/useFilterTriggers";
import { TABLE_ROWS, TABLE_ROWS_OPTIONS } from "@/shared/tablePagination";

// ── Tipos ───────────────────────────────────────────────────────────────────────
interface ComprobanteEmpresa {
  razonSocial: string; domicilio: string; localidad: string;
  telefono: string; email: string; cuit: string;
  inicioActividades: string; ingBrutos: string; condicionFiscal: string;
}
interface ComprobanteData {
  numeroRecibo: number; puntoVenta: number; fecha: string;
  empresa: ComprobanteEmpresa;
  cliente: { nombreCompleto: string; domicilio: string; localidad: string; telefono: string; categoriaFiscal: string; cuit: string };
  cuota: { numero: number; totalCuotas: number | null; producto: string };
  pago: { importe: number; medioPago: string; importeEnLetras: string };
}

const pendientes = ref<any[]>([]),
  abonadas = ref<any[]>([]),
  tab = ref<"pendientes" | "abonadas">("pendientes"),
  clienteFiltro = ref(""),
  selected = ref<any | null>(null),
  paymentError = ref(""),
  editingDueDate = ref<any | null>(null),
  dueDate = ref(""),
  dueDateError = ref(""),
  totalPendiente = ref<number | null>(0),
  cantidadPendiente = ref(0),
  resumenLoading = ref(false);
const sucursalFiltro = ref("");
const isAdmin = computed(() => auth.state.user?.rol === "Administrador");
// Comprobante
const comprobante = ref<ComprobanteData | null>(null);
const comprobanteId = ref("");
const comprobanteLoading = ref(false);
const comprobanteError = ref("");
const printRef = ref<HTMLElement | null>(null);
let resumenTimer: ReturnType<typeof setTimeout> | undefined;
let resumenRequest = 0;
const payment = ref({
  importe: 0,
  medioPago: "Transferencia",
  otroMedio: "",
  fechaPago: new Date().toISOString().slice(0, 10),
});
const medios = [
  "Efectivo",
  "Transferencia",
  "Tarjeta",
  "Cheque",
  "Mercado Pago",
  "Otro",
];
const tabOptions = computed(() => [
  { label: `Por pagar (${pendientes.value.length})`, value: "pendientes" as const },
  { label: `Abonadas (${abonadas.value.length})`, value: "abonadas" as const },
]);
const norm = (v: string) =>
  v
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
const visible = computed(() => {
  const source = tab.value === "pendientes" ? pendientes.value : abonadas.value,
    q = norm(clienteFiltro.value.trim());
  if (!q || q.length < 3) return source;
  return source.filter((c) => norm(c.cliente).includes(q));
});

async function load() {
  const [p, a] = await Promise.all([
    http.get("/cuotas/pendientes", { params: { sucursalId: sucursalFiltro.value || undefined } }),
    http.get("/cuotas/abonadas", { params: { sucursalId: sucursalFiltro.value || undefined } }),
  ]);
  pendientes.value = p.data;
  abonadas.value = a.data;
  await loadPendingSummary();
}

async function loadPendingSummary() {
  const request = ++resumenRequest;
  resumenLoading.value = true;
  try {
    const r = await http.get("/cuotas/pendientes/resumen", { params: { buscar: searchQuery(clienteFiltro.value) ?? undefined } });
    if (request === resumenRequest) {
      totalPendiente.value = r.data.totalPendiente == null ? null : Number(r.data.totalPendiente);
      cantidadPendiente.value = Number(r.data.cantidad ?? 0);
    }
  } catch (e) {
    if (request === resumenRequest) notify(apiErrorMessage(e, "No se pudo calcular el total pendiente."));
  } finally {
    if (request === resumenRequest) resumenLoading.value = false;
  }
}

function openPayment(c: any) {
  selected.value = c;
  payment.value = {
    importe: c.importePactado - c.importePagado,
    medioPago: "Transferencia",
    otroMedio: "",
    fechaPago: new Date().toISOString().slice(0, 10),
  };
  paymentError.value = "";
}

function openDueDate(c: any) {
  editingDueDate.value = c;
  dueDate.value = c.fechaVencimiento;
  dueDateError.value = "";
}

function closePayment() {
  selected.value = null;
}

function closeDueDate() {
  editingDueDate.value = null;
}

async function saveDueDate() {
  if (!editingDueDate.value || !dueDate.value) {
    dueDateError.value = "Ingresá una fecha de vencimiento válida.";
    return;
  }
  try {
    await http.put(`/cuotas/${editingDueDate.value.id}/vencimiento`, {
      fechaVencimiento: dueDate.value,
    });
    editingDueDate.value = null;
    await load();
  } catch (e: any) {
    dueDateError.value = apiErrorMessage(e, "No se pudo modificar el vencimiento.");
  }
}

function apiError(e: any) {
  const errors = e.response?.data?.errors;
  const first = errors ? Object.values(errors).flat()[0] : null;
  return first
    ? String(first)
    : (e.response?.data?.detail ?? "No se pudo registrar el pago.");
}

async function pay() {
  if (!selected.value) return;
  const medio =
    payment.value.medioPago === "Otro"
      ? payment.value.otroMedio.trim()
      : payment.value.medioPago;
  if (!medio) {
    paymentError.value = "Indicá el medio de pago.";
    return;
  }
  try {
    await http.post(`/cuotas/${selected.value.id}/pagos`, {
      importe: payment.value.importe,
      medioPago: medio,
      fechaPago: payment.value.fechaPago,
    });
    const cuotaId = selected.value.id;
    selected.value = null;
    await load();
    await downloadRecibo(cuotaId);
  } catch (e: any) {
    paymentError.value = apiError(e);
  }
}

async function downloadRecibo(cuotaId: string) {
  try {
    const resp = await http.get(`/cuotas/${cuotaId}/recibo`, { responseType: "blob" });
    const url = URL.createObjectURL(resp.data as Blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `recibo-cuota.pdf`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  } catch {
    // Silencioso — el pago ya fue registrado
  }
}

async function openComprobante(c: any) {
  comprobante.value = null;
  comprobanteId.value = c.id;
  comprobanteError.value = "";
  comprobanteLoading.value = true;
  try {
    const resp = await http.get(`/cuotas/${c.id}/comprobante`);
    comprobante.value = resp.data;
  } catch (e: any) {
    comprobanteError.value = apiErrorMessage(e, "No se pudo cargar el comprobante.");
  } finally {
    comprobanteLoading.value = false;
  }
}

function closeComprobante() {
  comprobante.value = null;
}

function fmtFecha(iso: string) {
  return iso?.slice(0, 10).split("-").reverse().join("/") ?? "";
}

function padStart(n: number, len: number) {
  return String(n).padStart(len, "0");
}

function printComprobante() {
  const content = printRef.value?.innerHTML;
  if (!content) return;
  const win = window.open("", "_blank", "width=860,height=1050");
  if (!win) return;
  win.document.write(`<!DOCTYPE html><html><head><meta charset="utf-8">
<style>
  body{font-family:Arial,sans-serif;font-size:9pt;color:#000;margin:20px}
  .rec-outer{border:1px solid #222}
  .rec-row{border-bottom:1px solid #222}
  .rec-header{display:flex}
  .rec-left{flex:5;border-right:1px solid #222}
  .rec-left-logo{border-bottom:1px solid #222;padding:8px;min-height:52px;display:flex;align-items:center;justify-content:center}
  .rec-left-company{padding:6px;font-size:8.5pt}
  .rec-center{width:55px;border-right:1px solid #222;display:flex;align-items:center;justify-content:center;font-size:28pt;font-weight:bold}
  .rec-right{flex:7}
  .rec-right-title{border-bottom:1px solid #222;padding:8px;text-align:right}
  .rec-right-title .rec-doc-type{font-size:7.5pt}
  .rec-right-title .rec-title{font-size:22pt;font-weight:bold}
  .rec-right-title .rec-num{font-size:11pt}
  .rec-right-title .rec-date{font-size:11pt;font-weight:bold}
  .rec-right-fiscal{padding:6px;font-size:8.5pt;display:flex;gap:16px;justify-content:space-between}
  .rec-label-row{display:flex;padding:3px 4px;border-bottom:1px solid #222;font-size:9pt}
  .rec-label{font-weight:bold;white-space:nowrap;margin-right:4px}
  .rec-cat-row{display:flex;border-bottom:1px solid #222}
  .rec-cat-left{flex:3;padding:3px 4px;display:flex}
  .rec-cat-right{flex:2;padding:3px 4px;display:flex;border-left:1px solid #222}
  .rec-spacer{border-bottom:1px solid #222;height:6px}
  .rec-table-header{background:#e0e0e0;border-bottom:1px solid #222;padding:4px;text-align:center;font-weight:bold;font-size:9pt}
  .rec-pay-row{display:flex;border-bottom:1px solid #222}
  .rec-pay-desc{flex:1;padding:5px}
  .rec-pay-sep{width:1px;background:#222}
  .rec-pay-amount{width:115px;padding:5px;text-align:right;font-weight:bold}
  .rec-empty-row{border-bottom:1px solid #ddd;height:14px}
  .rec-total-row{display:flex;border-top:1px solid #222}
  .rec-total-spacer{flex:1}
  .rec-total-sep{width:1px;background:#222}
  .rec-total-box{width:240px;display:flex}
  .rec-total-label{flex:1;padding:4px;text-align:right;font-weight:bold;border-right:1px solid #222}
  .rec-total-amount{width:115px;padding:4px;text-align:right;font-weight:bold}
  .rec-letras{border-top:1px solid #222;padding:8px;font-size:8.5pt}
  .rec-son{border-top:1px solid #222;display:flex;justify-content:flex-end;padding:8px;gap:16px;font-weight:bold;font-size:11pt}
  .rec-son-num{text-align:right;font-size:9pt}
  @media print{body{margin:0}}
</style></head><body>${content}</body></html>`);
  win.document.close();
  win.focus();
  win.print();
  setTimeout(() => win.close(), 500);
}

async function cancelPayment(c: any) {
  if (
    !(await confirmAction({
      title: "Anular cobro de cuota",
      message: `¿Querés anular el cobro de la cuota #${c.numero} de ${c.cliente} por ${money(c.importePagado)}? Se registrará un retiro compensatorio en Caja.`,
      confirmText: "Anular cobro",
      danger: true,
    }))
  )
    return;
  try {
    await http.post(`/cuotas/${c.id}/anular-pago`);
    await load();
  } catch (e: any) {
    notify(
      e.response?.data?.message ??
        e.response?.data?.detail ??
        "No se pudo anular el cobro.",
    );
  }
}

function exportCsv() {
  const date = new Date().toISOString().slice(0, 10);
  if (tab.value === "pendientes") {
    downloadCsv(
      `cuotas-pendientes-${date}.csv`,
      ["Cliente", "ID venta", "Producto", "Fecha de venta", "Cuota", "Vencimiento", "Importe pactado", "Importe pagado", "Saldo pendiente", "Estado"],
      visible.value.map((c) => [
        c.cliente, c.ventaId, c.tipoCesped, c.fechaVenta, c.numero, c.fechaVencimiento,
        c.importePactado, c.importePagado, c.importePactado - c.importePagado, c.estado,
      ]),
    );
    return;
  }
  downloadCsv(
    `cuotas-abonadas-${date}.csv`,
    ["Cliente", "ID venta", "Producto", "Fecha de venta", "Cuota", "Fecha de pago", "Importe abonado", "Impactado en sistema", "Medio de pago", "Estado"],
    visible.value.map((c) => [
      c.cliente, c.ventaId, c.tipoCesped, c.fechaVenta, c.numero, c.fechaPago,
      c.importePagado, new Date(c.fechaImpacto).toLocaleString("es-AR"), c.medioPago, c.estado,
    ]),
  );
}

onMounted(load);
watch(clienteFiltro, () => {
  const q = clienteFiltro.value.trim();
  if (q.length > 0 && q.length < 3) return;
  if (resumenTimer) clearTimeout(resumenTimer);
  resumenTimer = setTimeout(loadPendingSummary, 300);
});
onBeforeUnmount(() => { if (resumenTimer) clearTimeout(resumenTimer); });
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Seguimiento de cobranza por cliente y compra.</p>
      <!-- <AppButton label="Exportar a CSV" icon="pi pi-download" severity="secondary" :disabled="!visible.length" @click="exportCsv" /> -->
    </div>

    <!-- ── Vista inline de comprobante ─────────────────────────────── -->
    <template v-if="comprobante !== null || comprobanteLoading || comprobanteError">
      <div v-if="comprobanteLoading" class="text-center py-6">
        <i class="pi pi-spin pi-spinner" style="font-size:2rem" />
        <p class="mt-2 text-color-secondary">Cargando comprobante…</p>
      </div>

      <template v-else>
        <div class="flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
          <AppButton label="Volver" icon="pi pi-arrow-left" severity="secondary" @click="closeComprobante" />
          <div v-if="comprobante" class="flex gap-2">
            <AppButton label="Imprimir" icon="pi pi-print" severity="secondary" @click="printComprobante" />
            <AppButton label="Descargar PDF" icon="pi pi-file-pdf" @click="downloadRecibo(comprobanteId)" />
          </div>
        </div>

        <Message v-if="comprobanteError" severity="error" :closable="false">{{ comprobanteError }}</Message>

        <div v-else-if="comprobante" ref="printRef" class="rec-outer">
          <!-- Header -->
          <div class="rec-header rec-row">
            <div class="rec-left">
              <div class="rec-left-logo">
                <strong style="font-size:14pt;letter-spacing:1px">SPORTLINK</strong>
              </div>
              <div class="rec-left-company">
                <div><b>{{ comprobante.empresa.razonSocial }}</b></div>
                <div v-if="comprobante.empresa.domicilio">{{ comprobante.empresa.domicilio }}</div>
                <div v-if="comprobante.empresa.localidad">{{ comprobante.empresa.localidad }}</div>
                <div v-if="comprobante.empresa.telefono">TELEFONO: {{ comprobante.empresa.telefono }}</div>
                <div v-if="comprobante.empresa.email">{{ comprobante.empresa.email }}</div>
              </div>
            </div>
            <div class="rec-center">X</div>
            <div class="rec-right">
              <div class="rec-right-title">
                <div class="rec-doc-type">DOCUMENTO NO VALIDO COMO FACTURA</div>
                <div class="rec-title">RECIBO</div>
                <div class="rec-num">N° {{ padStart(comprobante.puntoVenta, 5) }}-&nbsp;{{ padStart(comprobante.numeroRecibo, 8) }}</div>
                <div class="rec-date">FECHA: {{ fmtFecha(comprobante.fecha) }}</div>
              </div>
              <div class="rec-right-fiscal">
                <div>
                  <b>{{ comprobante.empresa.condicionFiscal }}</b><br>
                  <span v-if="comprobante.empresa.inicioActividades">INICIO ACT.: {{ comprobante.empresa.inicioActividades }}</span>
                </div>
                <div style="text-align:right">
                  <span v-if="comprobante.empresa.cuit">C.U.I.T.: {{ comprobante.empresa.cuit }}</span><br>
                  <span v-if="comprobante.empresa.ingBrutos">ING. BRUTOS: {{ comprobante.empresa.ingBrutos }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Datos del cliente -->
          <div class="rec-label-row rec-row"><span class="rec-label">SEÑOR/ES:</span> {{ comprobante.cliente.nombreCompleto }}</div>
          <div class="rec-label-row rec-row"><span class="rec-label">DOMICILIO:</span> {{ comprobante.cliente.domicilio }}</div>
          <div class="rec-label-row rec-row"><span class="rec-label">LOCALIDAD:</span> {{ comprobante.cliente.localidad }}</div>
          <div class="rec-cat-row rec-row">
            <div class="rec-cat-left"><span class="rec-label">CATEGORIA FISCAL:</span>&nbsp;{{ comprobante.cliente.categoriaFiscal }}</div>
            <div class="rec-cat-right"><span class="rec-label">C.U.I.T.:</span>&nbsp;{{ comprobante.cliente.cuit }}</div>
          </div>
          <div class="rec-label-row rec-row"><span class="rec-label">TELEFONOS:</span> {{ comprobante.cliente.telefono }}</div>
          <div class="rec-label-row rec-row"><span class="rec-label">CONCEPTO:</span> PAGO A CUENTA {{ comprobante.cuota.producto.toUpperCase() }}</div>
          <div class="rec-label-row rec-row"><span class="rec-label">OBSERVACIONES:</span> COBRO DE CUOTA {{ comprobante.cuota.numero }} DE {{ comprobante.cuota.totalCuotas ?? '?' }}</div>
          <div class="rec-spacer"></div>

          <!-- Detalle de pagos -->
          <div class="rec-table-header">DETALLE DE PAGOS RECIBIDOS</div>
          <div class="rec-pay-row rec-row">
            <div class="rec-pay-desc">{{ comprobante.pago.medioPago.toUpperCase() }}</div>
            <div class="rec-pay-sep"></div>
            <div class="rec-pay-amount">{{ money(comprobante.pago.importe) }}</div>
          </div>
          <div v-for="n in 3" :key="n" class="rec-empty-row"></div>
          <div class="rec-total-row">
            <div class="rec-total-spacer"></div>
            <div class="rec-total-sep"></div>
            <div class="rec-total-box">
              <div class="rec-total-label">TOTAL RECIBIDO:</div>
              <div class="rec-total-amount">{{ money(comprobante.pago.importe) }}</div>
            </div>
          </div>

          <!-- Letras -->
          <div class="rec-letras">
            Recibimos la suma de <b>{{ comprobante.pago.importeEnLetras }}</b> en concepto de los items detallados anteriormente
          </div>

          <!-- SON -->
          <div class="rec-son">
            <span>SON:</span>
            <div class="rec-son-num">
              <div>$</div>
              <div>{{ comprobante.pago.importe.toLocaleString("es-AR", { minimumFractionDigits: 2 }) }}</div>
            </div>
          </div>
        </div>
      </template>
    </template>

    <!-- ── Vista normal (tabla) ──────────────────────────────────────── -->
    <template v-else>
    <SelectButton v-model="tab" :options="tabOptions" option-label="label" option-value="value" class="mb-3" />

    <Panel class="filter-panel">
      <div class="filter-form">
        <label for="clienteFiltro" class="block mb-1">Buscar cliente</label>
        <InputText
          id="clienteFiltro"
          v-model="clienteFiltro"
          type="search"
          placeholder="Nombre o apellido"
          class="w-full"
        />
        <small v-if="clienteFiltro.trim() && !isSearchFilterActive(clienteFiltro)" class="text-color-secondary mt-1 block">Escribí al menos 3 caracteres</small>
        <small v-else class="text-color-secondary mt-1 block">{{ visible.length }} cuotas</small>
      </div>
    </Panel>

    <article v-if="tab === 'pendientes'" class="card cuotas-pending-total">
      <div>
        <small>{{ clienteFiltro.trim() ? "Pendiente del cliente buscado" : "Pendiente total por cobrar" }}</small>
        <strong>{{ resumenLoading ? "Calculando…" : money(totalPendiente) }}</strong>
      </div>
      <span>{{ cantidadPendiente }} {{ cantidadPendiente === 1 ? "cuota pendiente" : "cuotas pendientes" }}</span>
    </article>

    <div class="table-panel">
      <DataTable
        :value="visible"
        paginator
        :rows="TABLE_ROWS"
        :rows-per-page-options="TABLE_ROWS_OPTIONS"
        striped-rows
      >
        <template #empty>
          <div class="text-center py-5 text-color-secondary">
            No hay cuotas en esta sección para ese cliente.
          </div>
        </template>
        <Column header="Cliente" body-class="cell-wrap">
          <template #body="{ data: c }"><b>{{ c.cliente }}</b></template>
        </Column>
        <Column header="Compra" body-class="cell-wrap">
          <template #body="{ data: c }">
            <b>{{ c.tipoCesped }}</b><br>
            <small class="text-color-secondary">
              {{ c.fechaVenta }} · {{ money(c.totalVenta) }}<br>
              Venta {{ c.ventaId.slice(0, 8).toUpperCase() }}
            </small>
          </template>
        </Column>
        <Column header="Cuota">
          <template #body="{ data: c }">#{{ c.numero }}</template>
        </Column>
        <Column :header="tab === 'pendientes' ? 'Vencimiento' : 'Fecha de pago'">
          <template #body="{ data: c }">
            {{ tab === "pendientes" ? c.fechaVencimiento : c.fechaPago }}
          </template>
        </Column>
        <Column :header="tab === 'pendientes' ? 'Pactado' : 'Importe abonado'" body-class="cell-num">
          <template #body="{ data: c }">
            {{ money(tab === "pendientes" ? c.importePactado : c.importePagado) }}
          </template>
        </Column>
        <Column v-if="tab === 'abonadas'" header="Impactado en sistema" body-class="cell-wrap">
          <template #body="{ data: c }">
            {{ new Date(c.fechaImpacto).toLocaleString("es-AR") }}<br>
            <small class="text-color-secondary">{{ c.medioPago }}</small>
          </template>
        </Column>
        <Column header="Estado">
          <template #body="{ data: c }">
            <Tag :value="c.estado" :severity="tab === 'pendientes' ? 'warn' : 'success'" />
          </template>
        </Column>
        <Column header="" body-class="cell-actions">
          <template #body="{ data: c }">
            <div class="cuotas-actions">
              <template v-if="tab === 'pendientes'">
                <AppButton
                  tooltip="Registrar pago"
                  icon="pi pi-wallet"
                  size="small"
                  rounded
                  text
                  aria-label="Registrar pago"
                  @click="openPayment(c)"
                />
              </template>
              <template v-else>
                <AppButton
                  tooltip="Ver comprobante"
                  icon="pi pi-receipt"
                  size="small"
                  rounded
                  text
                  severity="info"
                  aria-label="Ver comprobante"
                  @click="openComprobante(c)"
                />
                <AppButton
                  tooltip="Descargar comprobante"
                  icon="pi pi-download"
                  size="small"
                  rounded
                  text
                  severity="info"
                  aria-label="Descargar comprobante"
                  @click="downloadRecibo(c.id)"
                />
                <AppButton
                  tooltip="Anular cobro"
                  icon="pi pi-times-circle"
                  size="small"
                  rounded
                  text
                  severity="danger"
                  aria-label="Anular cobro"
                  @click="cancelPayment(c)"
                />
              </template>
              <AppButton
                tooltip="Editar vencimiento"
                icon="pi pi-calendar"
                size="small"
                rounded
                text
                severity="secondary"
                aria-label="Editar vencimiento"
                @click="openDueDate(c)"
              />
            </div>
          </template>
        </Column>
      </DataTable>
    </div>
    </template>

    <Dialog
      :visible="selected !== null"
      modal
      header="Registrar pago de cuota"
      :style="{ width: 'min(480px, 96vw)' }"
      @update:visible="(v) => { if (!v) closePayment() }"
    >
      <form v-if="selected" @submit.prevent="pay">
        <p class="mt-0">
          <b>{{ selected.cliente }}</b><br>
          {{ selected.tipoCesped }} · Cuota #{{ selected.numero }}
        </p>
        <Message v-if="paymentError" severity="error" class="mb-3" :closable="false">{{ paymentError }}</Message>

        <div class="grid formgrid p-fluid">
          <div class="field col-12">
            <label>Importe</label>
            <InputNumber
              v-model="payment.importe"
              :min="0.01"
              :max="selected.importePactado - selected.importePagado"
              :min-fraction-digits="2"
              :max-fraction-digits="2"
              mode="currency"
              currency="ARS"
              locale="es-AR"
              required
            />
            <small class="text-color-secondary">
              Saldo pendiente: {{ money(selected.importePactado - selected.importePagado) }}
            </small>
          </div>
          <div class="field col-12">
            <label>Medio de pago</label>
            <Select v-model="payment.medioPago" :options="medios" />
          </div>
          <div v-if="payment.medioPago === 'Otro'" class="field col-12">
            <label>Especificar medio</label>
            <InputText v-model="payment.otroMedio" maxlength="100" required />
          </div>
          <div class="field col-12">
            <label>Fecha de pago</label>
            <AppDatePicker v-model="payment.fechaPago" required />
          </div>
        </div>

        <div class="flex justify-content-end gap-2 mt-4">
          <AppButton type="button" label="Cancelar" severity="secondary" @click="closePayment" />
          <AppButton type="submit" label="Confirmar pago" />
        </div>
      </form>
    </Dialog>

    <Dialog
      :visible="editingDueDate !== null"
      modal
      header="Editar vencimiento"
      :style="{ width: 'min(480px, 96vw)' }"
      @update:visible="(v) => { if (!v) closeDueDate() }"
    >
      <form v-if="editingDueDate" @submit.prevent="saveDueDate">
        <p class="mt-0">
          <b>{{ editingDueDate.cliente }}</b><br>
          {{ editingDueDate.tipoCesped }} · Cuota #{{ editingDueDate.numero }}
        </p>
        <Message v-if="dueDateError" severity="error" class="mb-3" :closable="false">{{ dueDateError }}</Message>

        <div class="field">
          <label>Fecha de vencimiento</label>
          <AppDatePicker v-model="dueDate" required />
          <small class="text-color-secondary">Únicamente se modificará esta fecha.</small>
        </div>

        <div class="flex justify-content-end gap-2 mt-4">
          <AppButton type="button" label="Cancelar" severity="secondary" @click="closeDueDate" />
          <AppButton type="submit" label="Guardar fecha" />
        </div>
      </form>
    </Dialog>
  </section>
</template>