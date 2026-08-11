<script setup lang="ts">
import{computed,onBeforeUnmount,onMounted,ref}from'vue'
import{useRoute,useRouter}from'vue-router'
import{Menu,X,ChevronLeft,ChevronRight,LayoutDashboard,Users,ShoppingCart,CalendarClock,Truck,WalletCards,ChartNoAxesCombined,Settings,UserCog,LogOut,ClipboardList}from'lucide-vue-next'
import{auth}from'./auth'
import{http}from'./shared/api/httpClient'
import DolarBlueWidget from'./shared/components/DolarBlueWidget.vue'
import UiFeedback from'./shared/components/UiFeedback.vue'

const open=ref(false),collapsed=ref(localStorage.getItem('sidebar-collapsed')==='true'),route=useRoute(),router=useRouter(),systemOnline=ref(navigator.onLine)
const allLinks=[['/','Inicio',LayoutDashboard,'dashboard'],['/ventas','Ventas',ShoppingCart,'ventas'],['/entregas','Próximas entregas',Truck,'entregas'],['/clientes','Clientes',Users,'clientes'],['/cuotas','Cuotas',CalendarClock,'cuotas'],['/caja','Caja',WalletCards,'caja'],['/rentabilidad','Rentabilidad',ChartNoAxesCombined,'rentabilidad'],['/admin','Productos',Settings,'administracion']] as const
const links=computed(()=>allLinks.filter(x=>auth.can(x[3])))
let connectivityTimer:number|undefined
async function checkSystem(){if(!navigator.onLine){systemOnline.value=false;return}try{await http.get('/health',{timeout:3000});systemOnline.value=true}catch{systemOnline.value=false}}
function setOffline(){systemOnline.value=false}
async function logout(){await auth.logout();router.push('/login')}
function toggleSidebar(){collapsed.value=!collapsed.value;localStorage.setItem('sidebar-collapsed',String(collapsed.value))}
onMounted(()=>{window.addEventListener('online',checkSystem);window.addEventListener('offline',setOffline);checkSystem();connectivityTimer=window.setInterval(checkSystem,30000)})
onBeforeUnmount(()=>{window.removeEventListener('online',checkSystem);window.removeEventListener('offline',setOffline);if(connectivityTimer)window.clearInterval(connectivityTimer)})
</script>
<template><RouterView v-if="route.meta.public||route.meta.passwordChange"/><div v-else class="shell" :class="{'sidebar-collapsed':collapsed}"><aside :class="{open}"><div class="brand"><img src="/brand/sportlink-logo.png" alt="Sportlink by Empire"><button class="mobile-close" @click="open=false"><X/></button></div><button class="sidebar-toggle" :title="collapsed?'Expandir menú':'Minimizar menú'" :aria-label="collapsed?'Expandir menú':'Minimizar menú'" @click="toggleSidebar"><ChevronRight v-if="collapsed"/><ChevronLeft v-else/></button><nav><RouterLink v-for="[to,label,icon] in links" :key="to" :to="to" :title="collapsed?label:undefined" @click="open=false"><component :is="icon"/><span>{{label}}</span></RouterLink><template v-if="auth.state.user?.rol==='Administrador'"><RouterLink to="/usuarios" title="Usuarios" @click="open=false"><UserCog/><span>Usuarios</span></RouterLink><RouterLink to="/auditoria" title="Auditoría" @click="open=false"><ClipboardList/><span>Auditoría</span></RouterLink></template></nav><div class="profile"><b>{{auth.state.user?.nombre}}</b><small>{{auth.state.user?.rol}}</small><button class="logout" title="Salir" @click="logout"><LogOut/><span>Salir</span></button></div></aside><main><header><button class="menu" @click="open=true"><Menu/></button><div class="header-brand"><small>SPORTLINK</small><h1>Gestión de césped sintético</h1><em>by Empire</em></div><span class="online" :class="{offline:!systemOnline}">● {{systemOnline?'Sistema disponible':'Sin conexión'}}</span></header><RouterView/></main><DolarBlueWidget/></div><UiFeedback/></template>
