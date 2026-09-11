import { createApp } from 'vue'
import { registerSW } from 'virtual:pwa-register'
import PrimeVue from 'primevue/config'
import ConfirmationService from 'primevue/confirmationservice'
import ToastService from 'primevue/toastservice'
import Tooltip from 'primevue/tooltip'
import App from './App.vue'
import router from './router'
import { SportlinkPreset } from './shared/theme/sportlink-preset'
import { esLocale } from './shared/locale/es'

import '@fortawesome/fontawesome-free/css/all.min.css'
import 'primeicons/primeicons.css'
import 'primeflex/primeflex.css'
import './style.css'
import './extras.css'
import './delivery.css'
import './brand.css'
import './cuotas.css'
import './auth.css'
import './dolar-widget.css'
import './quote.css'
import './primevue-overrides.css'

const updateSW = registerSW({
  immediate: true,
  onNeedRefresh() { updateSW(true) },
  onRegisteredSW(_url, registration) {
    if (registration) window.setInterval(() => registration.update(), 5 * 60 * 1000)
  },
})

const app = createApp(App)
app.use(router)
app.use(PrimeVue, {
  locale: esLocale,
  theme: {
    preset: SportlinkPreset,
    options: {
      prefix: 'p',
      darkModeSelector: false,
    },
  },
})
app.use(ConfirmationService)
app.use(ToastService)
app.directive('tooltip', Tooltip)
app.mount('#app')
