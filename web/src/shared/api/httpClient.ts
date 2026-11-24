import axios from'axios'
export const http=axios.create({baseURL:import.meta.env.VITE_API_BASE_URL??'/api',timeout:15000,withCredentials:true})
http.interceptors.request.use(config=>{
  const selected=typeof window==='undefined'?null:JSON.parse(window.localStorage.getItem('active-sucursal')||'null') as {id?:string}|null
  if(selected?.id)config.headers['X-Sucursal-Id']=selected.id
  return config
})
export function apiErrorMessage(error:any,fallback='No se pudo completar la operación.'){
  if(!error?.response)return 'No se pudo conectar con el sistema. Verificá tu conexión y que el backend esté iniciado.'
  const data=error.response.data
  if(data?.errors&&typeof data.errors==='object'){
    const messages=Object.values(data.errors).flatMap(value=>Array.isArray(value)?value:[value]).filter(Boolean).map(String)
    if(messages.length)return messages.join(' · ')
  }
  return data?.detail??data?.message??data?.title??(error.response.status>=500?'El servidor encontró un error. Intentá nuevamente o revisá la terminal del backend.':fallback)
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
