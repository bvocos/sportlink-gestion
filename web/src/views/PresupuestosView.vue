<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Download, Eye, Pencil, Trash2, X } from 'lucide-vue-next'
import { http, apiErrorMessage } from '@/shared/api/httpClient'
import { confirmAction, notify } from '@/shared/uiFeedback'

const items=ref<any[]>([]),loading=ref(true),error=ref(''),previewUrl=ref(''),previewTitle=ref(''),previewLoading=ref(false)
const usd=(v:number)=>new Intl.NumberFormat('es-AR',{style:'currency',currency:'USD'}).format(v)
async function load(){loading.value=true;try{items.value=(await http.get('/presupuestos')).data}catch(e){error.value=apiErrorMessage(e,'No se pudieron cargar los presupuestos.')}finally{loading.value=false}}
async function pdf(x:any){const r=await http.get(`/presupuestos/${x.id}/pdf`,{responseType:'blob'});const url=URL.createObjectURL(new Blob([r.data],{type:'application/pdf'}));const a=document.createElement('a');a.href=url;a.download=`Sportlink-Presupuesto-${String(x.numero).padStart(5,'0')}.pdf`;a.click();URL.revokeObjectURL(url)}
function closePreview(){if(previewUrl.value)URL.revokeObjectURL(previewUrl.value);previewUrl.value='';previewTitle.value='';previewLoading.value=false}
async function preview(x:any){closePreview();previewTitle.value=`Presupuesto #${String(x.numero).padStart(5,'0')}`;previewLoading.value=true;try{const r=await http.get(`/presupuestos/${x.id}/pdf`,{responseType:'blob'});previewUrl.value=URL.createObjectURL(new Blob([r.data],{type:'application/pdf'}))}catch(e){closePreview();notify(apiErrorMessage(e,'No se pudo abrir la vista previa.'))}finally{previewLoading.value=false}}
async function remove(x:any){if(!await confirmAction({title:'Eliminar presupuesto',message:`¿Eliminar el presupuesto #${String(x.numero).padStart(5,'0')}?`,confirmText:'Eliminar',danger:true}))return;try{await http.delete(`/presupuestos/${x.id}`);await load()}catch(e){notify(apiErrorMessage(e,'No se pudo eliminar.'))}}
onMounted(load)
onBeforeUnmount(closePreview)
</script>

<template>
  <section class="page">
    <div class="page-title"><div><h2>Presupuestos</h2><p>Propuestas comerciales en USD listas para enviar.</p></div><RouterLink class="btn" to="/presupuestos/nuevo">+ Nuevo presupuesto</RouterLink></div>
    <div v-if="error" class="error">{{error}}</div>
    <div v-else-if="loading" class="panel loading">Cargando…</div>
    <div v-else class="panel">
      <table>
        <thead><tr><th>N.º</th><th>Fecha</th><th>Cliente</th><th class="num">Contado</th><th class="num">Financiado</th><th>Vigencia</th><th></th></tr></thead>
        <tbody><tr v-for="x in items" :key="x.id"><td><b>#{{String(x.numero).padStart(5,'0')}}</b></td><td>{{new Date(x.fecha+'T00:00:00').toLocaleDateString('es-AR')}}</td><td>{{x.cliente}}</td><td class="num">{{usd(x.totalContado)}}</td><td class="num">{{usd(x.totalFinanciado)}}</td><td>24 horas</td><td><div class="row-actions"><button class="icon-btn" title="Visualizar presupuesto" aria-label="Visualizar presupuesto" @click="preview(x)"><Eye/></button><button class="icon-btn" title="Descargar PDF" aria-label="Descargar PDF" @click="pdf(x)"><Download/></button><RouterLink class="icon-btn" title="Editar" aria-label="Editar presupuesto" :to="`/presupuestos/${x.id}/editar`"><Pencil/></RouterLink><button class="icon-btn danger" title="Eliminar" aria-label="Eliminar presupuesto" @click="remove(x)"><Trash2/></button></div></td></tr></tbody>
      </table>
      <div v-if="!items.length" class="empty"><p>Todavía no cargaste ningún presupuesto.</p><RouterLink class="btn" to="/presupuestos/nuevo">+ Nuevo presupuesto</RouterLink></div>
    </div>
    <div v-if="previewTitle" class="modal-bg pdf-preview-bg" @click.self="closePreview">
      <section class="modal pdf-preview-modal" role="dialog" aria-modal="true" :aria-label="previewTitle">
        <header class="pdf-preview-head"><div><h3>{{previewTitle}}</h3><small>Vista previa del PDF</small></div><button type="button" class="icon-btn" title="Cerrar vista previa" aria-label="Cerrar vista previa" @click="closePreview"><X/></button></header>
        <div v-if="previewLoading" class="pdf-preview-loading">Generando vista previa…</div>
        <iframe v-else-if="previewUrl" :src="previewUrl" :title="previewTitle" class="pdf-preview-frame"></iframe>
      </section>
    </div>
  </section>
</template>
