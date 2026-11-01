(function(root){
  'use strict';
  const mode=value=>value==='full'?'full':'summary';
  const keyValid=key=>typeof key==='string'&&key.length>2&&key.length<=512&&key.includes('|')&&!/[\u0000-\u001f]/.test(key);
  class Controller {
    constructor(options={}){this.mode=mode(options.mode);this.scope=options.scope||'published';this.scopes=new Map([[this.scope,new Map()]]);this.onChange=options.onChange||(()=>{});}
    get overrides(){if(!this.scopes.has(this.scope))this.scopes.set(this.scope,new Map());return this.scopes.get(this.scope);}
    isOpen(key){return this.overrides.has(key)?this.overrides.get(key):this.mode==='full';}
    getState(){return {mode:this.mode,scope:this.scope,custom:this.overrides.size>0,overrides:this.overrides.size};}
    setAll(value,options={}){this.mode=mode(value);this.scopes.clear();this.scopes.set(this.scope,new Map());if(options.notify!==false)this.onChange({type:'all',...this.getState()});}
    restore(value){const next=mode(value);if(next!==this.mode)this.setAll(next,{notify:false});}
    setScope(value){const next=typeof value==='string'&&value?value:'published';if(next===this.scope)return;this.scope=next;void this.overrides;if(this.scopes.size>12){for(const key of this.scopes.keys()){if(key!==this.scope&&key!=='published'){this.scopes.delete(key);break;}}}}
    record(key,open){if(!keyValid(key)||typeof open!=='boolean')return false;const before=this.isOpen(key);if(open===(this.mode==='full'))this.overrides.delete(key);else this.overrides.set(key,open);if(before===open)return false;this.onChange({type:'toggle',key,open,...this.getState()});return true;}
    prune(owners){const allowed=new Set(owners);allowed.add('reference');for(const key of this.overrides.keys())if(!allowed.has(key.slice(0,key.indexOf('|'))))this.overrides.delete(key);}
    model(){return {isOpen:key=>this.isOpen(key)};}
  }
  root.ContractAtlasDisclosure=Object.freeze({Controller});
})(globalThis);
