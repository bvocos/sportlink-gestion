<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import { Pencil, Trash2 } from "lucide-vue-next";
import { http } from "@/shared/api/httpClient";
import ClienteAutocomplete from "@/shared/components/ClienteAutocomplete.vue";
const items = ref<any[]>([]),
  clientes = ref<any[]>([]),
  maestros = ref<any>({ tiposCesped: [], alicuotasIva: [] }),
  show = ref(false),
  error = ref(""),
  editingId = ref<string | null>(null);
const money = (v: number) =>
  v.toLocaleString("es-AR", { style: "currency", currency: "ARS" });
const blank = () => ({
  clienteId: "",
  fechaVenta: new Date().toISOString().slice(0, 10),
  tipoCespedId: "",
  cantidadM2: 1,
  precioUnitario: 0,
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
async function load() {
  const [v, c, m] = await Promise.all([
    http.get("/ventas?pageSize=100"),
    http.get("/clientes?pageSize=100"),
    http.get("/maestros"),
  ]);
  items.value = v.data.items;
  clientes.value = c.data.items;
  maestros.value = m.data;
}
/* El precio maestro se completa al cambiar producto, pero sigue siendo editable. */
function enhanceForm() {
  const selects = document.querySelectorAll<HTMLSelectElement>(
    ".modal .form-grid select",
  );
  const client = selects[0],
    grass = selects[1];
  if (client && !document.querySelector(".client-search")) {
    const input = document.createElement("input");
    input.className = "client-search";
    input.type = "search";
    input.placeholder = "Buscar por nombre, teléfono o localidad";
    input.addEventListener("input", () => {
      const q = input.value.toLocaleLowerCase();
      Array.from(client.options).forEach((o, i) => {
        if (i > 0) {
          const c = clientes.value.find((x: any) => x.id === o.value);
          const text =
            `${c?.nombreCompleto} ${c?.telefono} ${c?.localidad}`.toLocaleLowerCase();
          o.hidden = !text.includes(q);
        }
      });
    });
    client.before(input);
  }
  if (grass && !grass.dataset.pricing) {
    grass.dataset.pricing = "true";
    grass.addEventListener("change", () => {
      const t = maestros.value.tiposCesped.find(
        (x: any) => x.id === grass.value,
      );
      if (t) {
        form.value.precioUnitario = t.precioVentaM2;
        form.value.costoCompraUnitario = t.costoM2;
      }
    });
  }
}
watch(()=>form.value.tipoCespedId,(id)=>{if(editingId.value)return;const t=maestros.value.tiposCesped.find((x:any)=>x.id===id);if(t){form.value.precioUnitario=t.precioVentaM2;form.value.costoCompraUnitario=t.costoM2}})
function openNew() {
  editingId.value = null;
  form.value = blank();
  error.value = "";
  show.value = true;
}
function edit(v: any) {
  editingId.value = v.id;
  form.value = {
    clienteId: v.clienteId,
    fechaVenta: v.fechaVenta,
    tipoCespedId: v.tipoCespedId,
    cantidadM2: v.cantidadM2,
    precioUnitario: v.precioUnitario,
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
  error.value = "";
  show.value = true;
}
async function save() {
  try {
    editingId.value
      ? await http.put(`/ventas/${editingId.value}`, form.value)
      : await http.post("/ventas", form.value);
    show.value = false;
    await load();
  } catch (e: any) {
    error.value =
      e.response?.data?.message ??
      e.response?.data?.detail ??
      e.response?.data?.title ??
      "Revisá los datos ingresados.";
  }
}
async function remove(v: any) {
  if (
    !confirm(`¿Eliminar la venta de ${v.cliente} por ${money(v.precioTotal)}?`)
  )
    return;
  try {
    await http.delete(`/ventas/${v.id}`);
    await load();
  } catch (e: any) {
    alert(e.response?.data?.message ?? "No se pudo eliminar la venta.");
  }
}
async function deliver(id: string) {
  try {
    await http.post(`/ventas/${id}/entregar`);
    await load();
  } catch (e: any) {
    alert(e.response?.data?.message ?? "No se pudo actualizar.");
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
      <button class="btn" @click="openNew">+ Registrar venta</button>
    </div>
    <div class="panel">
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
          <tr v-for="v in items" :key="v.id">
            <td>
              <b>{{ v.cliente }}</b
              ><br /><small>{{ v.tipoCesped }} · {{ v.cantidadM2 }} m²</small>
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
                ><button class="icon-btn" @click="edit(v)"><Pencil /></button
                ><button class="icon-btn danger" @click="remove(v)">
                  <Trash2 />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="empty">Registrá la primera venta.</div>
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
            <label>Monto de entrega inicial</label
            ><input
              v-model.number="form.montoEntrega"
              type="number"
              min="0.01"
              :max="form.precioUnitario * form.cantidadM2"
              step="0.01"
              required
            /><small>Se registra automáticamente como ingreso en Caja.</small>
          </div>
          <div class="field">
            <label>Costo compra por m²</label
            ><input
              v-model.number="form.costoCompraUnitario"
              type="number"
              min="0"
              step="0.01"
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
