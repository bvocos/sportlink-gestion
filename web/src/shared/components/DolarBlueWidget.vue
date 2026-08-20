<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ChevronDown, ChevronUp, CircleDollarSign, RefreshCw } from 'lucide-vue-next'
import { http } from '@/shared/api/httpClient'
import { formatCurrency as money } from '@/shared/formatters'

interface Quote { compra:number; venta:number; fechaActualizacion:string; fuente:string; desactualizada:boolean }
interface Quotes { blue:Quote|null; oficial:Quote|null }
const quotes=ref<Quotes|null>(null),loading=ref(false),failed=ref(false),minimized=ref(false)
let timer:number|undefined
const updated=(value:string)=>new Intl.DateTimeFormat('es-AR',{hour:'2-digit',minute:'2-digit'}).format(new Date(value))
async function load(){if(loading.value)return;loading.value=true;try{quotes.value=(await http.get('/cotizaciones/dolares')).data;failed.value=false}catch{failed.value=true}finally{loading.value=false}}
function toggle(){minimized.value=!minimized.value;localStorage.setItem('sportlink.dolarBlueMinimized',String(minimized.value))}
onMounted(()=>{minimized.value=localStorage.getItem('sportlink.dolarBlueMinimized')==='true';load();timer=window.setInterval(load,5*60*1000)})
onBeforeUnmount(()=>timer&&window.clearInterval(timer))
</script>

<template>
  <aside class="dolar-widget" :class="{minimized}" aria-live="polite">
    <div class="dolar-title">
      <CircleDollarSign/>
      <div><b>Cotización del dólar</b><small v-if="!minimized&&quotes?.blue">Actualizado {{updated(quotes.blue.fechaActualizacion)}}</small><small v-else-if="!minimized">Cotización actual</small></div>
      <strong v-if="minimized&&quotes?.blue" class="dolar-mini-value">Blue {{money(quotes.blue.venta)}}</strong>
      <div class="dolar-actions">
        <button v-if="!minimized" type="button" title="Actualizar cotización" aria-label="Actualizar cotización" :disabled="loading" @click="load"><RefreshCw :class="{spinning:loading}"/></button>
        <button type="button" :title="minimized?'Mostrar cotización':'Minimizar cotización'" :aria-label="minimized?'Mostrar cotización':'Minimizar cotización'" :aria-expanded="!minimized" @click="toggle"><ChevronUp v-if="minimized"/><ChevronDown v-else/></button>
      </div>
    </div>
    <template v-if="!minimized">
      <div v-if="quotes" class="dolar-quotes"><section v-if="quotes.blue"><b>Dólar blue</b><div class="dolar-values"><div><small>Compra</small><strong>{{money(quotes.blue.compra)}}</strong></div><div><small>Venta</small><strong>{{money(quotes.blue.venta)}}</strong></div></div></section><section v-if="quotes.oficial"><b>Dólar oficial</b><div class="dolar-values"><div><small>Compra</small><strong>{{money(quotes.oficial.compra)}}</strong></div><div><small>Venta</small><strong>{{money(quotes.oficial.venta)}}</strong></div></div></section></div>
      <p v-else-if="failed" class="dolar-error">Sin conexión. Reintentando…</p><p v-else class="dolar-loading">Consultando cotización…</p>
      <small v-if="quotes" class="dolar-source" :class="{stale:quotes.blue?.desactualizada||quotes.oficial?.desactualizada}">{{quotes.blue?.desactualizada||quotes.oficial?.desactualizada?'Último valor disponible':'Fuente: DolarApi'}}</small>
    </template>
  </aside>
</template>
