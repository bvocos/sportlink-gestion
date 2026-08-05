<script setup lang="ts">
import { ref, onMounted } from "vue";
import { http, apiErrorMessage } from "@/shared/api/httpClient";
import { formatCurrency as money } from "@/shared/formatters";
import { downloadCsv } from "@/shared/csv";
import { auth } from "@/auth";
const data = ref<any>({ saldo: 0, movimientos: [] }),
  show = ref(false),
  error = ref(""),
  loadError = ref(""),
  loading = ref(false);
const form = ref({ tipo: "Ingreso", monto: 0, concepto: "" });
async function load() {
  loading.value = true;
  loadError.value = "";
  try {
    data.value = (await http.get("/caja")).data;
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, "No se pudo cargar la caja.");
  } finally {
    loading.value = false;
  }
}
function openForm(tipo: "Ingreso" | "Retiro") {
  form.value = { tipo, monto: 0, concepto: "" };
  error.value = "";
  show.value = true;
}
async function save() {
  try {
    await http.post("/caja/movimientos", form.value);
    show.value = false;
    await load();
  } catch (e: any) {
    error.value = apiErrorMessage(e, "No se pudo registrar el movimiento.");
  }
}
function exportCsv() {
  downloadCsv(
    `caja-${new Date().toISOString().slice(0, 10)}.csv`,
    ["Fecha", "Tipo", "Observación", "Usuario", "Monto"],
    data.value.movimientos.map((m: any) => [
      new Date(m.fecha).toLocaleString("es-AR"),
      m.tipo,
      m.concepto,
      m.usuario,
      m.tipo === "Retiro" ? -m.monto : m.monto,
    ]),
  );
}
onMounted(load);
</script>
<template>
  <section class="page">
    <div class="page-title">
      <div>
        <h2>Caja</h2>
        <p>Ingresos y retiros trazables.</p>
      </div>
      <div class="row-actions">
        <button
          class="btn secondary"
          :disabled="!data.movimientos.length"
          @click="exportCsv"
        >
          Exportar CSV</button
        ><button class="btn" @click="openForm('Ingreso')">
          + Ingresar dinero</button
        ><button class="btn danger-btn" @click="openForm('Retiro')">
          − Retirar dinero
        </button>
      </div>
    </div>
    <div v-if="loadError" class="error load-state">
      {{ loadError }}
      <button class="btn secondary compact" @click="load">Reintentar</button>
    </div>
    <template v-else
      ><div class="grid">
        <article class="card metric">
          <small>Saldo actual</small><strong>{{ money(data.saldo) }}</strong
          ><em>Calculado desde el historial</em>
        </article>
      </div>
      <div class="panel">
        <div v-if="loading" class="loading">Cargando movimientos…</div>
        <table v-else>
          <thead>
            <tr>
              <th>Fecha</th>
              <th>Tipo</th>
              <th>Observación</th>
              <th>Usuario</th>
              <th>Monto</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="m in data.movimientos" :key="m.id">
              <td>{{ new Date(m.fecha).toLocaleString("es-AR") }}</td>
              <td>
                <span class="badge" :class="{ warn: m.tipo === 'Retiro' }">{{
                  m.tipo
                }}</span>
              </td>
              <td>{{ m.concepto }}</td>
              <td>{{ m.usuario }}</td>
              <td class="num">
                <b :class="{ negative: m.tipo === 'Retiro' }"
                  >{{ m.tipo === "Retiro" ? "− " : "+ "
                  }}{{ money(m.monto) }}</b
                >
              </td>
            </tr>
          </tbody>
        </table>
        <div v-if="!loading && !data.movimientos.length" class="empty">
          No hay movimientos todavía.
        </div>
      </div></template
    >
    <div v-if="show" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="save">
        <h3>
          {{ form.tipo === "Ingreso" ? "Ingresar dinero" : "Retirar dinero" }}
        </h3>
        <p>
          Se registrará a nombre de <b>{{ auth.state.user?.nombre }}</b
          >.
        </p>
        <p v-if="error" class="error">{{ error }}</p>
        <div class="field">
          <label>Monto</label
          ><input
            v-model.number="form.monto"
            type="number"
            min="0.01"
            step="0.01"
            required
          />
        </div>
        <div class="field">
          <label>Observación / motivo</label
          ><textarea
            v-model="form.concepto"
            maxlength="500"
            rows="4"
            placeholder="Ej.: compra de insumos, aporte de capital..."
            required
          ></textarea>
        </div>
        <div class="actions">
          <button type="button" class="btn secondary" @click="show = false">
            Cancelar</button
          ><button
            class="btn"
            :class="{ 'danger-btn': form.tipo === 'Retiro' }"
          >
            Confirmar {{ form.tipo.toLowerCase() }}
          </button>
        </div>
      </form>
    </div>
  </section>
</template>
