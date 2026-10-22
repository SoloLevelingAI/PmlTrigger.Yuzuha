// Run before styles and DocFX initialization so saved preferences do not flash the wrong palette.
(function () {
  var defaults={theme:'auto',accent:'blue',density:'comfortable'};
  var choices={theme:['auto','light','dark'],accent:['blue','violet','teal'],density:['comfortable','compact']};
  var media=window.matchMedia('(prefers-color-scheme: dark)');
  function read() {
    var result=Object.assign({},defaults);
    try {
      var legacy=localStorage.getItem('theme');
      if(choices.theme.indexOf(legacy)>=0)result.theme=legacy;
      var stored=JSON.parse(localStorage.getItem('yuzuha.appearance')||'{}');
      Object.keys(choices).forEach(function(key){if(choices[key].indexOf(stored[key])>=0)result[key]=stored[key];});
    } catch (_) { /* Invalid or unavailable storage falls back to explicit defaults. */ }
    return result;
  }
  var state=read();
  function apply() {
    var root=document.documentElement;
    root.dataset.bsTheme=state.theme==='auto'?(media.matches?'dark':'light'):state.theme;
    root.dataset.accent=state.accent;root.dataset.density=state.density;
    window.dispatchEvent(new CustomEvent('yuzuha:appearance',{detail:Object.assign({},state)}));
  }
  window.YuzuhaAppearance={
    get:function(){return Object.assign({},state);},
    set:function(change){
      Object.keys(choices).forEach(function(key){if(choices[key].indexOf(change[key])>=0)state[key]=change[key];});
      try {localStorage.setItem('yuzuha.appearance',JSON.stringify(state));localStorage.setItem('theme',state.theme);}catch(_){}
      apply();
    }
  };
  media.addEventListener('change',function(){if(state.theme==='auto')apply();});
  window.addEventListener('storage',function(event){if(event.key==='yuzuha.appearance'){state=read();apply();}});
  apply();
}());
