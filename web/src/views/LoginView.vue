<script setup lang="ts">
import{computed,nextTick,ref}from'vue'
import{useRoute,useRouter}from'vue-router'
import{auth}from'@/auth'
const router=useRouter(),route=useRoute(),usuario=ref(''),password=ref(''),error=ref(route.query.expired?'Tu sesión venció. Volvé a iniciar sesión.':''),loading=ref(false),waking=ref(false)
const buttonText=computed(()=>waking.value?'Iniciando el sistema…':loading.value?'Ingresando…':'Ingresar')
async function submit(){
  if(loading.value)return
  loading.value=true
  waking.value=false
  error.value=''
  const wakeTimer=window.setTimeout(()=>waking.value=true,4000)
  try{
    const user=await auth.login(usuario.value,password.value)
    const destination=user?.debeCambiarPassword?'/cambiar-password':'/'
    await nextTick()
    await router.replace(destination)
  }catch(e:any){
    if(e?.code==='ECONNABORTED')error.value='El sistema tardó demasiado en iniciar. Intentá nuevamente en unos segundos.'
    else if(!e?.response)error.value='No se pudo conectar con el sistema. Verificá tu conexión a internet e intentá nuevamente.'
    else if(e.response.status===429)error.value='Demasiados intentos. Esperá unos minutos antes de volver a intentar.'
    else if(e.response.status===401)error.value='Usuario o contraseña incorrectos.'
    else error.value='El backend respondió con un error. Revisá la terminal de la API.'
  }finally{window.clearTimeout(wakeTimer);waking.value=false;loading.value=false}
}
</script>
<template><main class="login-page"><form class="login-card" @submit.prevent="submit"><img src="/brand/sportlink-logo.png" alt="Sportlink by Empire"><h1>Ingresar al sistema</h1><p>Gestión comercial de césped sintético</p><div class="field"><label>Usuario</label><input v-model="usuario" autocomplete="username" required autofocus></div><div class="field"><label>Contraseña</label><input v-model="password" type="password" autocomplete="current-password" required></div><p v-if="waking" class="login-waking">La versión gratuita puede demorar unos segundos en activarse.</p><p v-if="error" class="error">{{error}}</p><button class="btn" :disabled="loading">{{buttonText}}</button></form></main></template>
