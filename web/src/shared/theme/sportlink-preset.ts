import { definePreset } from '@primevue/themes'
import Aura from '@primevue/themes/aura'

export const SportlinkPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#eef6e9',
      100: '#d9f0cd',
      200: '#b7e46c',
      300: '#7fb58e',
      400: '#52805f',
      500: '#337a4e',
      600: '#2f7a4c',
      700: '#285743',
      800: '#214f3a',
      900: '#12372a',
      950: '#0c241c',
    },
  },
})
