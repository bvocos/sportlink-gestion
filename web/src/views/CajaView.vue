<script setup lang="ts">
import { ref, onMounted } from "vue";
import { http, apiErrorMessage } from "@/shared/api/httpClient";
import { formatCurrency as money } from "@/shared/formatters";
import { downloadCsv } from "@/shared/csv";
import { auth } from "@/auth";
import SucursalFilter from "@/shared/components/SucursalFilter.vue";
const data = ref<any>({ saldo: 0, movimientos: [] }),
  show = ref(false),
  error = ref(""),
  loadError = ref(""),
  loading = ref(false),
  editingMovement = ref<any | null>(null),
  observation = ref(""),
  observationError = ref("");
const form = ref({ tipo: "Ingreso", monto: 0, concepto: "", sucursalId: null as string|null });
const isAdmin = auth.state.user?.rol === "Administrador";
const sucursalFiltro = ref("");
async function load() {
  loading.value = true;
  loadError.value = "";
  try {
    data.value = (await http.get("/caja", { params: { sucursalId: sucursalFiltro.value || undefined } })).data;
  } catch (e: any) {
    loadError.value = apiErrorMessage(e, "No se pudo cargar la caja.");
  } finally {
    loading.value = false;
  }
}
function openForm(tipo: "Ingreso" | "Retiro") {
  form.value = { tipo, monto: 0, concepto: "", sucursalId: isAdmin ? (sucursalFiltro.value || data.value.sucursales?.[0]?.id || null) : auth.state.user?.sucursalId??null };
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
function openObservation(movement: any) {
  editingMovement.value = movement;
  observation.value = movement.concepto;
  observationError.value = "";
}
async function saveObservation() {
  if (!editingMovement.value) return;
  if (!observation.value.trim()) {
    observationError.value = "La observación es obligatoria.";
    return;
  }
  try {
    await http.put(`/caja/movimientos/${editingMovement.value.id}/observacion`, {
      observacion: observation.value,
    });
    editingMovement.value = null;
    await load();
  } catch (e: any) {
    observationError.value = apiErrorMessage(
      e,
      "No se pudo modificar la observación.",
    );
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
          Exportar a CSV</button
        ><button v-if="auth.can('caja','crear')" class="btn" @click="openForm('Ingreso')">
          + Ingresar dinero</button
        ><button v-if="auth.can('caja','crear')" class="btn danger-btn" @click="openForm('Retiro')">
          − Retirar dinero
        </button>
      </div>
    </div>
    <div v-if="loadError" class="error load-state">
      {{ loadError }}
      <button class="btn secondary compact" @click="load">Reintentar</button>
    </div>
    <div v-if="isAdmin" class="panel client-filters">
      <SucursalFilter v-model="sucursalFiltro" @change="load" />
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
              <th>Acciones</th>
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
              <td>
                <button v-if="auth.can('caja','editar')" class="btn secondary compact" @click="openObservation(m)">
                  Editar observación
                </button>
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
        <div v-if="isAdmin" class="field">
          <label>Sucursal</label><select v-model="form.sucursalId" required>
            <option :value="null" disabled>Seleccionar sucursal</option>
            <option v-for="s in data.sucursales" :key="s.id" :value="s.id">{{ s.nombre }}</option>
          </select>
        </div>
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
    <div v-if="editingMovement" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="saveObservation">
        <h3>Editar observación</h3>
        <p>
          {{ new Date(editingMovement.fecha).toLocaleString("es-AR") }} ·
          <b>{{ editingMovement.tipo }}</b> · {{ money(editingMovement.monto) }}
        </p>
        <p v-if="observationError" class="error">{{ observationError }}</p>
        <div class="field">
          <label>Observación / motivo</label>
          <textarea
            v-model="observation"
            maxlength="500"
            rows="4"
            required
          ></textarea>
          <small>El tipo y el monto del movimiento no se modificarán.</small>
        </div>
        <div class="actions">
          <button
            type="button"
            class="btn secondary"
            @click="editingMovement = null"
          >
            Cancelar
          </button>
          <button class="btn">Guardar observación</button>
        </div>
      </form>
    </div>
  </section>
</template>
