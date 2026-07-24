import axios from'axios'
export const http=axios.create({baseURL:import.meta.env.VITE_API_BASE_URL??'/api',timeout:15000,withCredentials:true})
export function apiErrorMessage(error:any,fallback='No se pudo completar la operación.'){
  if(!error?.response)return 'No se pudo conectar con el sistema. Verificá tu conexión y que el backend esté iniciado.'
  if(error.response.status>=500)return 'El servidor encontró un error. Intentá nuevamente o revisá la terminal del backend.'
  return error.response.data?.detail??error.response.data?.message??error.response.data?.title??fallback
}
let redirecting=false
http.interceptors.response.use(response=>response,error=>{
  const url=String(error.config?.url??'')
  const isAuthBootstrap=url.includes('/auth/login')||url.includes('/auth/me')
  const isAlreadyOnLogin=window.location.pathname==='/login'
  if(error.response?.status===401&&!isAuthBootstrap&&!isAlreadyOnLogin&&!redirecting){
    redirecting=true
    window.location.assign('/login?expired=1')
  }
  return Promise.reject(error)
})
