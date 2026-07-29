<script setup lang="ts">
import{nextTick,ref}from'vue'
import{useRoute,useRouter}from'vue-router'
import{auth}from'@/auth'
const router=useRouter(),route=useRoute(),usuario=ref(''),password=ref(''),error=ref(route.query.expired?'Tu sesión venció. Volvé a iniciar sesión.':''),loading=ref(false)
async function submit(){
  if(loading.value)return
  loading.value=true
  error.value=''
  try{
    const user=await auth.login(usuario.value,password.value)
    const destination=user?.debeCambiarPassword?'/cambiar-password':'/'
    await nextTick()
    await router.replace(destination)
  }catch(e:any){
    if(!e?.response)error.value='No se pudo conectar con el backend. Verificá que la API esté iniciada.'
    else if(e.response.status===429)error.value='Demasiados intentos. Esperá unos minutos antes de volver a intentar.'
    else if(e.response.status===401)error.value='Usuario o contraseña incorrectos.'
    else error.value='El backend respondió con un error. Revisá la terminal de la API.'
  }finally{loading.value=false}
}
</script>
<template><main class="login-page"><form class="login-card" @submit.prevent="submit"><img src="/brand/sportlink-logo.png" alt="Sportlink by Empire"><h1>Ingresar al sistema</h1><p>Gestión comercial de césped sintético</p><div class="field"><label>Usuario</label><input v-model="usuario" autocomplete="username" required autofocus></div><div class="field"><label>Contraseña</label><input v-model="password" type="password" autocomplete="current-password" required></div><p v-if="error" class="error">{{error}}</p><button class="btn" :disabled="loading">{{loading?'Ingresando...':'Ingresar'}}</button></form></main></template>
