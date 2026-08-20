<script setup lang="ts">
import { onMounted, ref } from "vue";
import { http, apiErrorMessage } from "@/shared/api/httpClient";
import { formatCurrency as money, pluralize } from "@/shared/formatters";
import { downloadBlob, downloadCsv } from "@/shared/csv";
import { notify } from "@/shared/uiFeedback";
const items = ref<any[]>([]),
  loading = ref(false),
  exporting = ref(false),
  loadError = ref(""),
  buscar = ref(""),
  desde = ref(""),
  hasta = ref(""),
  estadoFinanciero = ref(""),
  page = ref(1),
  totalPages = ref(0),
  total = ref(0);
const totales = ref({
  cantidadVentas: 0,
  facturacionTotal: 0,
  costoTotal: 0,
  gananciaNetaTotal: 0,
  margenPromedioPonderado: 0,
});
function financialStatusClass(status: string) {
  return {
    "En pérdida": "danger",
    "Pendiente de cobro": "warn",
    Rentable: "",
    "Muy rentable": "strong",
  }[status] ?? "";
}
async function load(reset = false) {
  if (reset) page.value = 1;
  loading.value = true;
  loadError.value = "";
  try {
    const { data } = await http.get("/rentabilidad", {
      params: {
        buscar: buscar.value.trim() || undefined,
        desde: desde.value || undefined,
        hasta: hasta.value || undefined,
        estadoFinanciero: estadoFinanciero.value || undefined,
        page: page.value,
        pageSize: 50,
      },
    });
    items.value = data.items;
    totales.value = data.totales;
    total.value = data.total;
    totalPages.value = data.totalPages;
  } catch (e: any) {
    loadError.value = apiErrorMessage(
      e,
      "No se pudo cargar el reporte de rentabilidad.",
    );
  } finally {
    loading.value = false;
  }
}
function clearFilters() {
  buscar.value = "";
  desde.value = "";
  hasta.value = "";
  estadoFinanciero.value = "";
  load(true);
}
function changePage(value: number) {
  page.value = value;
  load();
}
function exportPage() {
  downloadCsv(
    `rentabilidad-pagina-${page.value}-${new Date().toISOString().slice(0, 10)}.csv`,
    [
      "ID venta",
      "Fecha",
      "Cliente",
      "Venta",
      "Costo operativo",
      "IVA",
      "Costo total",
      "Ganancia bruta",
      "Ganancia neta",
      "Margen %",
      "Cobrado",
      "Pendiente total",
      "Pendiente en cuotas",
      "Estado",
    ],
    items.value.map((r) => [
      r.id,
      r.fechaVenta,
      r.cliente,
      r.precioTotal,
      r.costoOperativo,
      r.iva,
      r.costoTotal,
      r.gananciaBruta,
      r.gananciaNeta,
      (r.margen * 100).toFixed(2),
      r.totalCobrado,
      r.totalPendiente,
      r.saldoPendienteCuotas,
      r.estadoFinanciero,
    ]),
  );
}
async function exportAll() {
  exporting.value = true;
  try {
    const response = await http.get("/rentabilidad/exportar", {
      params: {
        buscar: buscar.value.trim() || undefined,
        desde: desde.value || undefined,
        hasta: hasta.value || undefined,
        estadoFinanciero: estadoFinanciero.value || undefined,
      },
      responseType: "blob",
    });
    downloadBlob(
      response.data,
      `rentabilidad-completa-${new Date().toISOString().slice(0, 10)}.csv`,
    );
  } catch (e: any) {
    notify(apiErrorMessage(e, "No se pudo exportar el reporte completo."));
  } finally {
    exporting.value = false;
  }
}
onMounted(() => load());
</script>
<template>
  <section class="page">
    <div class="page-title">
      <div>
        <h2>Rentabilidad</h2>
        <p>Precio − costos operativos − IVA = ganancia neta.</p>
      </div>
      <div class="row-actions">
        <button
          class="btn secondary"
          :disabled="!items.length"
          @click="exportPage"
        >
          Exportar página</button
        ><button class="btn" :disabled="!total || exporting" @click="exportAll">
          {{ exporting ? "Preparando…" : "Exportar todo lo filtrado" }}
        </button>
      </div>
    </div>
    <div class="formula card">
      <span>Precio de venta</span><b>−</b><span>Compra + envío + otros</span
      ><b>−</b><span>IVA aplicado</span><b>=</b><strong>Ganancia neta</strong>
    </div>
    <form class="panel rentabilidad-filters" @submit.prevent="load(true)">
      <div class="field">
        <label>Cliente o ID de venta</label
        ><input
          v-model="buscar"
          type="search"
          placeholder="Nombre, apellido o GUID"
        />
      </div>
      <div class="field">
        <label>Desde</label><input v-model="desde" type="date" />
      </div>
      <div class="field">
        <label>Hasta</label><input v-model="hasta" type="date" />
      </div>
      <div class="field">
        <label>Estado financiero</label>
        <select v-model="estadoFinanciero">
          <option value="">Todos los estados</option>
          <option value="Pendiente de cobro">Pendiente de cobro</option>
          <option value="Rentable">Rentable</option>
          <option value="Muy rentable">Muy rentable</option>
          <option value="En pérdida">En pérdida</option>
        </select>
      </div>
      <div class="filter-actions">
        <button class="btn" :disabled="loading">Buscar</button
        ><button type="button" class="btn secondary" @click="clearFilters">
          Limpiar
        </button>
      </div>
    </form>
    <div v-if="!loadError" class="grid">
      <article class="card metric">
        <small>Facturación</small
        ><strong>{{ money(totales.facturacionTotal) }}</strong
        ><em>{{ pluralize(totales.cantidadVentas, "venta", "ventas") }}</em>
      </article>
      <article class="card metric">
        <small>Costo total</small
        ><strong>{{ money(totales.costoTotal) }}</strong
        ><em>Compra, envío, otros e IVA</em>
      </article>
      <article class="card metric">
        <small>Ganancia neta</small
        ><strong :class="{ negative: totales.gananciaNetaTotal < 0 }">{{
          money(totales.gananciaNetaTotal)
        }}</strong
        ><em>Sobre todo el resultado</em>
      </article>
      <article class="card metric">
        <small>Margen ponderado</small
        ><strong
          >{{ (totales.margenPromedioPonderado * 100).toFixed(2) }}%</strong
        ><em>Ponderado por facturación</em>
      </article>
    </div>
    <div v-if="loadError" class="error load-state">
      {{ loadError }}
      <button class="btn secondary compact" @click="load()">Reintentar</button>
    </div>
    <div v-else class="panel">
      <div class="panel-head">
        <h3>
          {{ pluralize(total, "venta encontrada", "ventas encontradas") }}
        </h3>
      </div>
      <div v-if="loading" class="loading">Cargando reporte…</div>
      <table v-else>
        <thead>
          <tr>
            <th>Fecha</th>
            <th>Cliente</th>
            <th>Venta</th>
            <th>Costo operativo</th>
            <th>IVA</th>
            <th>Costo total</th>
            <th>Ganancia bruta</th>
            <th>Ganancia neta</th>
            <th>Margen</th>
            <th>Pendiente</th>
            <th>Estado</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in items" :key="r.id">
            <td>{{ r.fechaVenta }}</td>
            <td>
              <b>{{ r.cliente }}</b
              ><small class="muted">{{ r.id }}</small>
            </td>
            <td class="num">{{ money(r.precioTotal) }}</td>
            <td class="num">{{ money(r.costoOperativo) }}</td>
            <td class="num">{{ money(r.iva) }}</td>
            <td class="num">{{ money(r.costoTotal) }}</td>
            <td class="num">{{ money(r.gananciaBruta) }}</td>
            <td class="num">
              <b :class="{ negative: r.gananciaNeta < 0 }">{{
                money(r.gananciaNeta)
              }}</b>
            </td>
            <td class="num">{{ (r.margen * 100).toFixed(2) }}%</td>
            <td class="num">
              <b>{{ money(r.totalPendiente) }}</b
              ><small
                v-if="r.formaPago === 'Cuotas'"
                class="collection-detail"
                :class="{
                  negative:
                    Math.abs(r.totalPendiente - r.saldoPendienteCuotas) > 0.01,
                }"
                >En cuotas: {{ money(r.saldoPendienteCuotas) }}</small
              >
            </td>
            <td>
              <span class="badge" :class="financialStatusClass(r.estadoFinanciero)">{{ r.estadoFinanciero }}</span>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!loading && !items.length" class="empty">
        No hay ventas para la búsqueda seleccionada.
      </div>
      <div v-if="totalPages > 1" class="actions">
        <button
          class="btn secondary"
          :disabled="page <= 1 || loading"
          @click="changePage(page - 1)"
        >
          Anterior</button
        ><span>Página {{ page }} de {{ totalPages }}</span
        ><button
          class="btn secondary"
          :disabled="page >= totalPages || loading"
          @click="changePage(page + 1)"
        >
          Siguiente
        </button>
      </div>
    </div>
  </section>
</template>
