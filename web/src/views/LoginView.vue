<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/shared/components/AppButton.vue'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Password from 'primevue/password'
import { auth } from '@/auth'

const router = useRouter()
const route = useRoute()
const usuario = ref('')
const password = ref('')
const error = ref(route.query.expired ? 'Tu sesión venció. Volvé a iniciar sesión.' : '')
const loading = ref(false)
const waking = ref(false)
const buttonText = computed(() => waking.value ? 'Iniciando el sistema…' : loading.value ? 'Ingresando…' : 'Ingresar')

async function submit() {
  if (loading.value) return
  loading.value = true
  waking.value = false
  error.value = ''
  const wakeTimer = window.setTimeout(() => waking.value = true, 4000)
  try {
    const user = await auth.login(usuario.value, password.value)
    const destination = user?.debeCambiarPassword ? '/cambiar-password' : '/'
    await nextTick()
    await router.replace(destination)
  } catch (e: any) {
    if (e?.code === 'ECONNABORTED') error.value = 'El sistema tardó demasiado en iniciar. Intentá nuevamente en unos segundos.'
    else if (!e?.response) error.value = 'No se pudo conectar con el sistema. Verificá tu conexión a internet e intentá nuevamente.'
    else if (e.response.status === 429) error.value = 'Demasiados intentos. Esperá unos minutos antes de volver a intentar.'
    else if (e.response.status === 401) error.value = 'Usuario o contraseña incorrectos.'
    else error.value = 'El backend respondió con un error. Revisá la terminal de la API.'
  } finally {
    window.clearTimeout(wakeTimer)
    waking.value = false
    loading.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <form class="login-card p-fluid" @submit.prevent="submit">
      <img src="/brand/sportlink-logo.png" alt="Sportlink by Empire">
      <h1>Ingresar al sistema</h1>
      <p>Gestión comercial de césped sintético</p>
      <div class="field">
        <label for="usuario">Usuario</label>
        <InputText id="usuario" v-model="usuario" autocomplete="username" required autofocus />
      </div>
      <div class="field">
        <label for="password">Contraseña</label>
        <Password
          id="password"
          v-model="password"
          :feedback="false"
          toggle-mask
          autocomplete="current-password"
          required
          input-class="w-full"
        />
      </div>
      <Message v-if="waking" severity="info" :closable="false">La versión gratuita puede demorar unos segundos en activarse.</Message>
      <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>
      <AppButton type="submit" :label="buttonText" :loading="loading" class="w-full" />
    </form>
  </main>
</template>
