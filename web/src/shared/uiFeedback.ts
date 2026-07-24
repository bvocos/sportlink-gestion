import{reactive}from'vue'
type ConfirmOptions={title:string;message:string;confirmText?:string;danger?:boolean}
type ConfirmState=ConfirmOptions&{open:boolean;resolve?:((value:boolean)=>void)}
const confirmState=reactive<ConfirmState>({open:false,title:'',message:''})
const toastState=reactive({visible:false,message:'',type:'error' as'error'|'success'})
let toastTimer:number|undefined
export function confirmAction(options:ConfirmOptions){return new Promise<boolean>(resolve=>Object.assign(confirmState,options,{open:true,resolve}))}
export function resolveConfirmation(value:boolean){confirmState.open=false;confirmState.resolve?.(value);confirmState.resolve=undefined}
export function notify(message:string,type:'error'|'success'='error'){toastState.message=message;toastState.type=type;toastState.visible=true;if(toastTimer)clearTimeout(toastTimer);toastTimer=window.setTimeout(()=>toastState.visible=false,4500)}
export{confirmState,toastState}
