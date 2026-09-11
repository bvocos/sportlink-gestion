<script setup lang="ts">
import { ref, onMounted } from "vue";
import AppButton from "@/shared/components/AppButton.vue";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import Dialog from "primevue/dialog";
import InputNumber from "primevue/inputnumber";
import Message from "primevue/message";
import Tag from "primevue/tag";
import Textarea from "primevue/textarea";
import { http, apiErrorMessage } from "@/shared/api/httpClient";
import { formatCurrency as money } from "@/shared/formatters";
import { downloadCsv } from "@/shared/csv";
import { auth } from "@/auth";
import { TABLE_ROWS, TABLE_ROWS_OPTIONS } from "@/shared/tablePagination";

const data = ref<any>({ saldo: 0, movimientos: [] }),
  show = ref(false),
  error = ref(""),
  loadError = ref(""),
  loading = ref(false),
  editingMovement = ref<any | null>(null),
  observation = ref(""),
  observationError = ref("");
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

function closeObservation() {
  editingMovement.value = null;
}

onMounted(load);
</script>

<template>
  <section class="page compact-page">
    <div class="page-toolbar flex justify-content-between align-items-center flex-wrap gap-3">
      <p class="page-desc text-color-secondary m-0">Ingresos y retiros trazables.</p>
      <div class="flex gap-2 flex-wrap">
        <AppButton
          label="Exportar a CSV"
          icon="pi pi-download"
          severity="secondary"
          :disabled="!data.movimientos.length"
          @click="exportCsv"
        />
        <AppButton label="Ingresar dinero" icon="pi pi-plus" @click="openForm('Ingreso')" />
        <AppButton label="Retirar dinero" severity="danger" @click="openForm('Retiro')" />
      </div>
    </div>

    <Message v-if="loadError" severity="error" class="mb-3" :closable="false">
      {{ loadError }}
      <AppButton label="Reintentar" size="small" severity="secondary" class="ml-2" @click="load" />
    </Message>

    <template v-else>
      <div class="summary-metrics">
        <article class="card metric">
          <small>Saldo actual</small>
          <strong>{{ money(data.saldo) }}</strong>
          <em>Calculado desde el historial</em>
        </article>
      </div>

      <div class="table-panel">
        <DataTable
          :value="data.movimientos"
          :loading="loading"
          paginator
          :rows="TABLE_ROWS"
          :rows-per-page-options="TABLE_ROWS_OPTIONS"
          striped-rows
        >
          <template #empty>
            <div class="text-center py-5">
              <p class="font-bold mb-2">Caja sin movimientos</p>
              <p class="text-color-secondary mb-3">Registrá un ingreso o un retiro para empezar el historial.</p>
              <AppButton label="Ingresar dinero" icon="pi pi-plus" @click="openForm('Ingreso')" />
            </div>
          </template>
          <Column header="Fecha">
            <template #body="{ data: m }">{{ new Date(m.fecha).toLocaleString("es-AR") }}</template>
          </Column>
          <Column header="Tipo">
            <template #body="{ data: m }">
              <Tag :value="m.tipo" :severity="m.tipo === 'Retiro' ? 'warn' : 'success'" />
            </template>
          </Column>
          <Column field="concepto" header="Observación" body-class="cell-wrap" />
          <Column field="usuario" header="Usuario" />
          <Column header="Monto" body-class="cell-num">
            <template #body="{ data: m }">
              <b :class="{ negative: m.tipo === 'Retiro' }">
                {{ m.tipo === "Retiro" ? "− " : "+ " }}{{ money(m.monto) }}
              </b>
            </template>
          </Column>
          <Column header="Acciones" body-class="cell-actions">
            <template #body="{ data: m }">
              <AppButton label="Editar observación" size="small" severity="secondary" @click="openObservation(m)" />
            </template>
          </Column>
        </DataTable>
      </div>
    </template>

    <Dialog
      v-model:visible="show"
      modal
      :header="form.tipo === 'Ingreso' ? 'Ingresar dinero' : 'Retirar dinero'"
      :style="{ width: 'min(480px, 96vw)' }"
    >
      <form @submit.prevent="save">
        <p class="mt-0">
          Se registrará a nombre de <b>{{ auth.state.user?.nombre }}</b>.
        </p>
        <Message v-if="error" severity="error" class="mb-3" :closable="false">{{ error }}</Message>

        <div class="grid formgrid p-fluid">
          <div class="field col-12">
            <label>Monto</label>
            <InputNumber
              v-model="form.monto"
              :min="0.01"
              :min-fraction-digits="2"
              :max-fraction-digits="2"
              mode="currency"
              currency="ARS"
              locale="es-AR"
              required
            />
          </div>
          <div class="field col-12">
            <label>Observación / motivo</label>
            <Textarea
              v-model="form.concepto"
              maxlength="500"
              rows="4"
              placeholder="Ej.: compra de insumos, aporte de capital..."
              required
              auto-resize
            />
          </div>
        </div>

        <div class="flex justify-content-end gap-2 mt-4">
          <AppButton type="button" label="Cancelar" severity="secondary" @click="show = false" />
          <AppButton
            type="submit"
            :label="`Confirmar ${form.tipo.toLowerCase()}`"
            :severity="form.tipo === 'Retiro' ? 'danger' : undefined"
          />
        </div>
      </form>
    </Dialog>

    <Dialog
      :visible="editingMovement !== null"
      modal
      header="Editar observación"
      :style="{ width: 'min(480px, 96vw)' }"
      @update:visible="(v) => { if (!v) closeObservation() }"
    >
      <form v-if="editingMovement" @submit.prevent="saveObservation">
        <p class="mt-0">
          {{ new Date(editingMovement.fecha).toLocaleString("es-AR") }} ·
          <b>{{ editingMovement.tipo }}</b> · {{ money(editingMovement.monto) }}
        </p>
        <Message v-if="observationError" severity="error" class="mb-3" :closable="false">{{ observationError }}</Message>

        <div class="field">
          <label>Observación / motivo</label>
          <Textarea v-model="observation" maxlength="500" rows="4" required auto-resize />
          <small class="text-color-secondary">El tipo y el monto del movimiento no se modificarán.</small>
        </div>

        <div class="flex justify-content-end gap-2 mt-4">
          <AppButton type="button" label="Cancelar" severity="secondary" @click="closeObservation" />
          <AppButton type="submit" label="Guardar observación" />
        </div>
      </form>
    </Dialog>
  </section>
</template>
