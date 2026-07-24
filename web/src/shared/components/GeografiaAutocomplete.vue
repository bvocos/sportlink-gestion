<script setup lang="ts">
import{computed,ref,watch}from'vue'
interface Option{id:string;nombre:string}
const props=defineProps<{modelValue:string;options:Option[];placeholder?:string;disabled?:boolean}>()
const emit=defineEmits<{(e:'update:modelValue',value:string):void;(e:'select',value:Option):void}>()
const query=ref(''),open=ref(false),active=ref(0)
const norm=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase()
const results=computed(()=>{const q=norm(query.value.trim());return props.options.filter(x=>!q||norm(x.nombre).includes(q)).slice(0,12)})
function select(option:Option){query.value=option.nombre;emit('update:modelValue',option.id);emit('select',option);open.value=false}
function input(){emit('update:modelValue','');open.value=true;active.value=0}
function key(event:KeyboardEvent){if(event.key==='ArrowDown'){event.preventDefault();active.value=Math.min(active.value+1,results.value.length-1)}else if(event.key==='ArrowUp'){event.preventDefault();active.value=Math.max(active.value-1,0)}else if(event.key==='Enter'){event.preventDefault();const option=results.value[active.value];if(option)select(option)}else if(event.key==='Escape')open.value=false}
function blur(){window.setTimeout(()=>open.value=false,150)}
watch([()=>props.modelValue,()=>props.options],()=>{const option=props.options.find(x=>x.id===props.modelValue);if(option)query.value=option.nombre;else if(!props.modelValue)query.value=''},{immediate:true})
</script>
<template><div class="autocomplete"><input v-model="query" type="search" autocomplete="off" :placeholder="placeholder||'Buscar localidad'" :disabled="disabled" required @focus="open=true;active=0" @input="input" @keydown="key" @blur="blur"><div v-if="open&&!disabled" class="autocomplete-menu"><button v-for="(option,index) in results" :key="option.id" type="button" :class="{active:index===active}" @mousedown.prevent="select(option)">{{option.nombre}}</button><p v-if="!results.length">No se encontraron coincidencias.</p></div></div></template>
