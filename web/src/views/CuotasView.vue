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
import { formatCurrency as money } from "@/shared/formatters";
import { downloadCsv } from "@/shared/csv";
import { confirmAction, notify } from "@/shared/uiFeedback";
import { isSearchFilterActive, searchQuery } from "@/shared/composables/useFilterTriggers";
import { TABLE_ROWS, TABLE_ROWS_OPTIONS } from "@/shared/tablePagination";

const pendientes = ref<any[]>([]),
  abonadas = ref<any[]>([]),
  tab = ref<"pendientes" | "abonadas">("pendientes"),
  clienteFiltro = ref(""),
  selected = ref<any | null>(null),
  paymentError = ref(""),
  editingDueDate = ref<any | null>(null),
  dueDate = ref(""),
  dueDateError = ref(""),
  totalPendiente = ref(0),
  cantidadPendiente = ref(0),
  resumenLoading = ref(false);
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
    http.get("/cuotas/pendientes"),
    http.get("/cuotas/abonadas"),
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
      totalPendiente.value = Number(r.data.totalPendiente ?? 0);
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
    selected.value = null;
    await load();
  } catch (e: any) {
    paymentError.value = apiError(e);
  }
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
      <AppButton label="Exportar a CSV" icon="pi pi-download" severity="secondary" :disabled="!visible.length" @click="exportCsv" />
    </div>

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
              <AppButton
                v-if="tab === 'pendientes'"
                tooltip="Registrar pago"
                icon="pi pi-wallet"
                size="small"
                rounded
                text
                aria-label="Registrar pago"
                @click="openPayment(c)"
              />
              <AppButton
                v-else
                tooltip="Anular cobro"
                icon="pi pi-times-circle"
                size="small"
                rounded
                text
                severity="danger"
                aria-label="Anular cobro"
                @click="cancelPayment(c)"
              />
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
