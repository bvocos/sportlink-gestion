<script setup lang="ts">
import { ref, onMounted } from "vue";
import { Pencil, Trash2, Power } from "lucide-vue-next";
import { http } from "@/shared/api/httpClient";
import { formatCurrency as money } from "@/shared/formatters";
import { confirmAction, notify } from "@/shared/uiFeedback";
const items = ref<any[]>([]),
  show = ref(false),
  error = ref(""),
  editingId = ref<string | null>(null),
  form = ref({
    nombre: "",
    descripcion: "",
    precioVentaM2: 0,
    costoM2: 0,
    activo: true,
  });
async function load() {
  items.value = (await http.get("/maestros/tipos-cesped")).data;
}
function create() {
  editingId.value = null;
  form.value = {
    nombre: "",
    descripcion: "",
    precioVentaM2: 0,
    costoM2: 0,
    activo: true,
  };
  error.value = "";
  show.value = true;
}
function edit(x: any) {
  editingId.value = x.id;
  form.value = {
    nombre: x.nombre,
    descripcion: x.descripcion ?? "",
    precioVentaM2: x.precioVentaM2,
    costoM2: x.costoM2,
    activo: x.activo,
  };
  error.value = "";
  show.value = true;
}
async function save() {
  try {
    editingId.value
      ? await http.put(`/maestros/tipos-cesped/${editingId.value}`, form.value)
      : await http.post("/maestros/tipos-cesped", form.value);
    show.value = false;
    await load();
  } catch (e: any) {
    error.value =
      e.response?.data?.message ??
      e.response?.data?.detail ??
      "No se pudo guardar.";
  }
}
async function toggle(x: any) {
  await http.put(`/maestros/tipos-cesped/${x.id}`, {
    nombre: x.nombre,
    descripcion: x.descripcion,
    precioVentaM2: x.precioVentaM2,
    costoM2: x.costoM2,
    activo: !x.activo,
  });
  await load();
}
async function remove(x: any) {
  if (await confirmAction({title:"Eliminar tipo de césped",message:`¿Querés eliminar ${x.nombre}?`,confirmText:"Eliminar",danger:true}))
    try {
      await http.delete(`/maestros/tipos-cesped/${x.id}`);
      await load();
    } catch (e: any) {
      notify(e.response?.data?.message ?? "No se pudo eliminar.");
    }
}
onMounted(load);
</script>
<template>
  <section class="page">
    <div class="page-title">
      <div>
        <h2>Administración</h2>
        <p>Productos, precios y costos maestros.</p>
      </div>
      <button class="btn" @click="create">+ Tipo de césped</button>
    </div>
    <div class="panel">
      <table>
        <thead>
          <tr>
            <th>Nombre</th>
            <th>Descripción</th>
            <th>Venta / m²</th>
            <th>Costo / m²</th>
            <th>Estado</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="x in items" :key="x.id">
            <td>
              <b>{{ x.nombre }}</b>
            </td>
            <td>{{ x.descripcion || "—" }}</td>
            <td>{{ money(x.precioVentaM2) }}</td>
            <td>{{ money(x.costoM2) }}</td>
            <td>
              <span class="badge" :class="{ warn: !x.activo }">{{
                x.activo ? "Activo" : "Inactivo"
              }}</span>
            </td>
            <td>
              <div class="row-actions product-actions">
                <button class="icon-btn tooltip" type="button" title="Editar producto" aria-label="Editar producto" data-tooltip="Editar producto" @click="edit(x)"><Pencil /></button
                ><button class="icon-btn tooltip" type="button" :title="x.activo ? 'Desactivar producto' : 'Activar producto'" :aria-label="x.activo ? 'Desactivar producto' : 'Activar producto'" :data-tooltip="x.activo ? 'Desactivar producto' : 'Activar producto'" @click="toggle(x)"><Power /></button
                ><button class="icon-btn danger tooltip" type="button" title="Eliminar producto" aria-label="Eliminar producto" data-tooltip="Eliminar producto" @click="remove(x)">
                  <Trash2 />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div v-if="show" class="modal-bg">
      <form class="modal small-modal" @submit.prevent="save">
        <h3>{{ editingId ? "Modificar" : "Nuevo" }} tipo de césped</h3>
        <p v-if="error" class="error">{{ error }}</p>
        <div class="field">
          <label>Nombre</label><input v-model="form.nombre" required />
        </div>
        <div class="field">
          <label>Descripción</label
          ><textarea v-model="form.descripcion"></textarea>
        </div>
        <div class="form-grid price-grid">
          <div class="field">
            <label>Precio de venta por m²</label
            ><input
              v-model.number="form.precioVentaM2"
              type="number"
              min="0"
              step="0.01"
              required
            />
          </div>
          <div class="field">
            <label>Costo por m²</label
            ><input
              v-model.number="form.costoM2"
              type="number"
              min="0"
              step="0.01"
              required
            />
          </div>
        </div>
        <label class="check"
          ><input v-model="form.activo" type="checkbox" /> Disponible para
          nuevas ventas</label
        >
        <div class="actions">
          <button type="button" class="btn secondary" @click="show = false">
            Cancelar</button
          ><button class="btn">Guardar</button>
        </div>
      </form>
    </div>
  </section>
</template>
