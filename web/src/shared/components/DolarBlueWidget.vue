<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { CircleDollarSign, RefreshCw } from 'lucide-vue-next'
import { http } from '@/shared/api/httpClient'

interface Quote { compra:number; venta:number; fechaActualizacion:string; fuente:string; desactualizada:boolean }
const quote=ref<Quote|null>(null),loading=ref(false),failed=ref(false)
let timer:number|undefined
const money=(value:number)=>new Intl.NumberFormat('es-AR',{style:'currency',currency:'ARS',maximumFractionDigits:0}).format(value)
const updated=(value:string)=>new Intl.DateTimeFormat('es-AR',{hour:'2-digit',minute:'2-digit'}).format(new Date(value))
async function load(){if(loading.value)return;loading.value=true;try{quote.value=(await http.get('/cotizaciones/dolar-blue')).data;failed.value=false}catch{failed.value=true}finally{loading.value=false}}
onMounted(()=>{load();timer=window.setInterval(load,5*60*1000)})
onBeforeUnmount(()=>timer&&window.clearInterval(timer))
</script>

<template>
  <aside class="dolar-widget" aria-live="polite">
    <div class="dolar-title"><CircleDollarSign/><div><b>Dólar blue</b><small v-if="quote">Actualizado {{updated(quote.fechaActualizacion)}}</small><small v-else>Cotización actual</small></div><button title="Actualizar cotización" :disabled="loading" @click="load"><RefreshCw :class="{spinning:loading}"/></button></div>
    <div v-if="quote" class="dolar-values"><div><small>Compra</small><strong>{{money(quote.compra)}}</strong></div><div><small>Venta</small><strong>{{money(quote.venta)}}</strong></div></div>
    <p v-else-if="failed" class="dolar-error">Sin conexión. Reintentando…</p><p v-else class="dolar-loading">Consultando cotización…</p>
    <small v-if="quote" class="dolar-source" :class="{stale:quote.desactualizada}">{{quote.desactualizada?'Último valor disponible':'Fuente: '+quote.fuente}}</small>
  </aside>
</template>
