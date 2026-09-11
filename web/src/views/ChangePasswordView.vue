<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import AppButton from '@/shared/components/AppButton.vue'
import Message from 'primevue/message'
import Password from 'primevue/password'
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
    <form class="login-card p-fluid" @submit.prevent="submit">
      <img src="/brand/sportlink-logo.png" alt="Sportlink by Empire">
      <h1>Cambiar contraseña</h1>
      <p>Por seguridad, debés crear una contraseña personal antes de continuar.</p>
      <div class="field">
        <label for="actual">Contraseña actual</label>
        <Password
          id="actual"
          v-model="actual"
          :feedback="false"
          toggle-mask
          autocomplete="current-password"
          required
          autofocus
          input-class="w-full"
        />
      </div>
      <div class="field">
        <label for="nueva">Nueva contraseña</label>
        <Password
          id="nueva"
          v-model="nueva"
          :feedback="false"
          toggle-mask
          autocomplete="new-password"
          required
          input-class="w-full"
        />
      </div>
      <div class="field">
        <label for="confirmacion">Confirmar nueva contraseña</label>
        <Password
          id="confirmacion"
          v-model="confirmacion"
          :feedback="false"
          toggle-mask
          autocomplete="new-password"
          required
          input-class="w-full"
        />
      </div>
      <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>
      <AppButton type="submit" :label="loading ? 'Guardando…' : 'Cambiar contraseña'" :loading="loading" class="w-full" />
    </form>
  </main>
</template>
