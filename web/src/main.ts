import {createApp} from 'vue';import {registerSW} from'virtual:pwa-register';import App from './App.vue';import router from './router';import './style.css';import './extras.css';import './delivery.css';import './brand.css';import './cuotas.css';import './auth.css';import './dolar-widget.css';
const updateSW=registerSW({immediate:true,onNeedRefresh(){updateSW(true)},onRegisteredSW(_url,registration){if(registration)window.setInterval(()=>registration.update(),5*60*1000)}})
createApp(App).use(router).mount('#app');
