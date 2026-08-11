<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { http } from "@/shared/api/httpClient";
import { formatCurrency as money } from "@/shared/formatters";
import { downloadCsv } from "@/shared/csv";
import { confirmAction, notify } from "@/shared/uiFeedback";
const pendientes = ref<any[]>([]),
  abonadas = ref<any[]>([]),
  tab = ref<"pendientes" | "abonadas">("pendientes"),
  clienteFiltro = ref(""),
  selected = ref<any | null>(null),
  paymentError = ref("");
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
const norm = (v: string) =>
  v
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
const visible = computed(() => {
  const source = tab.value === "pendientes" ? pendientes.value : abonadas.value,
    q = norm(clienteFiltro.value.trim());
  return !q ? source : source.filter((c) => norm(c.cliente).includes(q));
});
async function load() {
  const [p, a] = await Promise.all([
    http.get("/cuotas/pendientes"),
    http.get("/cuotas/abonadas"),
  ]);
  pendientes.value = p.data;
  abonadas.value = a.data;
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
</script>
<template>
  <section class="page">
    <div class="page-title">
      <div>
        <h2>Cuotas</h2>
        <p>Seguimiento de cobranza por cliente y compra.</p>
      </div>
      <button class="btn secondary" :disabled="!visible.length" @click="exportCsv">Exportar a CSV</button>
    </div>
    <div class="cuotas-tabs">
      <button
        :class="{ active: tab === 'pendientes' }"
        @click="tab = 'pendientes'"
      >
        Por pagar <span>{{ pendientes.length }}</span></button
      ><button
        :class="{ active: tab === 'abonadas' }"
        @click="tab = 'abonadas'"
      >
        Abonadas <span>{{ abonadas.length }}</span>
      </button>
    </div>
    <div class="card cuotas-filter">
      <label>Buscar cliente</label
      ><input
        v-model="clienteFiltro"
        type="search"
        placeholder="Nombre o apellido"
      /><small>{{ visible.length }} cuotas</small>
    </div>
    <div class="panel">
      <table>
        <thead>
          <tr>
            <th>Cliente</th>
            <th>Compra</th>
            <th>Cuota</th>
            <th>
              {{ tab === "pendientes" ? "Vencimiento" : "Fecha de pago" }}
            </th>
            <th>{{ tab === "pendientes" ? "Pactado" : "Importe abonado" }}</th>
            <th v-if="tab === 'abonadas'">Impactado en sistema</th>
            <th>Estado</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="c in visible" :key="c.id">
            <td>
              <b>{{ c.cliente }}</b>
            </td>
            <td>
              <b>{{ c.tipoCesped }}</b
              ><br /><small
                >{{ c.fechaVenta }} · {{ money(c.totalVenta) }}<br />Venta
                {{ c.ventaId.slice(0, 8).toUpperCase() }}</small
              >
            </td>
            <td>#{{ c.numero }}</td>
            <td>
              {{ tab === "pendientes" ? c.fechaVencimiento : c.fechaPago }}
            </td>
            <td class="num">
              {{
                money(tab === "pendientes" ? c.importePactado : c.importePagado)
              }}
            </td>
            <td v-if="tab === 'abonadas'">
              {{ new Date(c.fechaImpacto).toLocaleString("es-AR")
              }}<br /><small>{{ c.medioPago }}</small>
            </td>
            <td>
              <span class="badge" :class="{ warn: tab === 'pendientes' }">{{
                c.estado
              }}</span>
            </td>
            <td>
              <button
                v-if="tab === 'pendientes'"
                class="btn"
                @click="openPayment(c)"
              >
                Registrar pago</button
              ><button
                v-else
                class="btn danger-btn compact"
                @click="cancelPayment(c)"
              >
                Anular cobro
              </button>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!visible.length" class="empty">
        No hay cuotas en esta sección para ese cliente.
      </div>
    </div>
    <div v-if="selected" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="pay">
        <h3>Registrar pago de cuota</h3>
        <p>
          <b>{{ selected.cliente }}</b
          ><br />{{ selected.tipoCesped }} · Cuota #{{ selected.numero }}
        </p>
        <p v-if="paymentError" class="error">{{ paymentError }}</p>
        <div class="field">
          <label>Importe</label
          ><input
            v-model.number="payment.importe"
            type="number"
            min="0.01"
            :max="selected.importePactado - selected.importePagado"
            step="0.01"
            required
          /><small
            >Saldo pendiente:
            {{ money(selected.importePactado - selected.importePagado) }}</small
          >
        </div>
        <div class="field">
          <label>Medio de pago</label
          ><select v-model="payment.medioPago">
            <option v-for="medio in medios" :key="medio">{{ medio }}</option>
          </select>
        </div>
        <div v-if="payment.medioPago === 'Otro'" class="field">
          <label>Especificar medio</label
          ><input v-model="payment.otroMedio" maxlength="100" required />
        </div>
        <div class="field">
          <label>Fecha de pago</label
          ><input v-model="payment.fechaPago" type="date" required />
        </div>
        <div class="actions">
          <button type="button" class="btn secondary" @click="selected = null">
            Cancelar</button
          ><button class="btn">Confirmar pago</button>
        </div>
      </form>
    </div>
  </section>
</template>
