<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { auth } from '@/auth'

const router = useRouter()
const actual = ref('')
const nueva = ref('')
const confirmacion = ref('')
const error = ref('')
const loading = ref(false)

async function submit() {
  error.value = ''
  if (nueva.value.length < 8) {
    error.value = 'La nueva contraseña debe tener al menos 8 caracteres.'
    return
  }
  if (nueva.value !== confirmacion.value) {
    error.value = 'Las contraseñas nuevas no coinciden.'
    return
  }
  loading.value = true
  try {
    await auth.changePassword(actual.value, nueva.value, confirmacion.value)
    await router.push('/')
  } catch (e: any) {
    const errors = e?.response?.data?.errors
    error.value = errors ? Object.values(errors).flat().join(' ') : 'No se pudo cambiar la contraseña.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <form class="login-card" @submit.prevent="submit">
      <img src="/brand/sportlink-logo.png" alt="Sportlink by Empire">
      <h1>Cambiar contraseña</h1>
      <p>Por seguridad, debés crear una contraseña personal antes de continuar.</p>
      <div class="field"><label>Contraseña actual</label><input v-model="actual" type="password" autocomplete="current-password" required autofocus></div>
      <div class="field"><label>Nueva contraseña</label><input v-model="nueva" type="password" autocomplete="new-password" minlength="8" required></div>
      <div class="field"><label>Confirmar nueva contraseña</label><input v-model="confirmacion" type="password" autocomplete="new-password" minlength="8" required></div>
      <p v-if="error" class="error">{{ error }}</p>
      <button class="btn" :disabled="loading">{{ loading ? 'Guardando...' : 'Cambiar contraseña' }}</button>
    </form>
  </main>
</template>
