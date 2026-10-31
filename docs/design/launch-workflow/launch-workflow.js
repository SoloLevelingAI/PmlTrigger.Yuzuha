/* Same-origin adapter: all business decisions remain in Manager operations. */
(function (root) {
  'use strict';
  const state = {
    connected: false,
    discovery: null,
    draft: null,
    overrides: {},
    profileIds: {},
    savedProfiles: [],
    selectedProfileId: null,
    preferredExecutable: null,
    plan: null,
    operationId: null,
    busy: false
  };
  const $ = id => document.getElementById(id);
  const token = () => document.querySelector('meta[name="pmltwin-token"]').content;
  async function invoke(id, input = {}, grants = {}) {
    const response = await fetch('/api/operations/'+encodeURIComponent(id), {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-PmlTwin-Token': token()
      },
      body: JSON.stringify({
        input,
        allowWrite: false,
        allowLaunch: false,
        ...grants
      })
    });
    if(!response.ok) throw new Error('本机服务 HTTP '+response.status+'；请检查 workbench 是否运行及会话令牌。');
    const result = await response.json();
    if(result.schemaVersion!==1 || typeof result.success!=='boolean') throw new Error('操作回执格式不匹配。');
    $('receipt').textContent=JSON.stringify(result,null,2);
    if(!result.success && result.error?.code!=='ENVIRONMENT_NEEDS_INPUT') {
      const error=new Error((result.error?.code||'FAILED')+' · '+(result.error?.message||'请查看回执'));
      error.result=result;
      throw error;
    }
    return result.data;
  }
  function invalidate() {
    state.plan=null;
    $('execute').disabled=true;
    $('plan-state').textContent='待重新生成';
  }
  function message(text,error=false) {
    $('message').textContent=text;
    $('message').classList.toggle('error',error);
  }
  async function run(action) {
    if(state.busy)return;
    state.busy=true;
    $('refresh').disabled=true;
    $('plan').disabled=true;
    $('execute').disabled=true;
    try {
      await action();
      return true;
    }
    catch(e) {
      message(e.message,true);
      return false;
    }
    finally {
      state.busy=false;
      $('refresh').disabled=false;
      $('plan').disabled=!state.connected||!state.draft;
      $('execute').disabled=!state.plan;
    }
  }
  function options(id,values) {
    const list=$(id);
    list.replaceChildren(...values.map(v=> {
      const option=document.createElement('option');
      option.value=v;
      return option;
    }));
  }
  const editableFields = ['project','module','user','mdb','initialization','workingDirectory'];
  function chooseSavedProfile(id) {
    const profile=state.savedProfiles.find(row=>row.profile.id===id)?.profile;
    state.selectedProfileId=profile?.id||null;
    state.preferredExecutable=profile?.executable||null;
    state.overrides={};
    if(!profile)state.profileIds={};
    $('installation').value='';
    for(const field of editableFields) {
      $(field).value=profile?.[field]||'';
      if(profile)state.overrides[field]=profile[field]||'';
    }
    if(profile)state.profileIds[profile.executable]=profile.id;
    invalidate();
  }
  async function loadProfiles() {
    state.savedProfiles=await invoke('launch.profile.list');
    const select=$('saved-profile');
    select.replaceChildren();
    const empty=document.createElement('option');
    empty.value='';
    empty.textContent='新建配置 · 或选择已有配置';
    select.append(empty);
    for(const row of state.savedProfiles) {
      const option=document.createElement('option');
      option.value=row.profile.id;
      option.textContent=row.profile.name+(row.profile.isDefault?' · 默认':'');
      select.append(option);
    }
    const defaults=state.savedProfiles.filter(row=>row.profile.isDefault);
    const preferred=state.savedProfiles.find(row=>row.profile.id===state.selectedProfileId)
      ||(defaults.length===1?defaults[0]:state.savedProfiles.length===1?state.savedProfiles[0]:null);
    select.value=preferred?.profile.id||'';
    chooseSavedProfile(select.value);
    $('profile-hint').textContent=preferred?'已读取保存的非秘密字段；安装与字段仍由后端重新验证。'
      :state.savedProfiles.length?'存在多个配置，请明确选择；不会自动覆盖已有配置。':'尚无保存配置；完成一次后可在刷新页面时复用。';
  }
  function renderDiscovery(data) {
    state.discovery=data;
    const selected=$('installation').value;
    const select=$('installation');
    select.replaceChildren();
    let blank=document.createElement('option');
    blank.value='';
    blank.textContent=data.candidates.length?'请选择产品安装':'未发现可用安装';
    select.append(blank);
    for(const c of data.candidates) {
      const i=c.installation.identity;
      const option=document.createElement('option');
      option.value=i.id;
      const release=i.versionHint||'产品版本待核验';
      const component=i.productVersion&&i.productVersion!==i.versionHint?' · 组件 '+i.productVersion:'';
      option.textContent=i.product+' · '+release+component+' — '+i.root;
      select.append(option);
    }
    if(data.candidates.some(c=>c.installation.identity.id===selected))select.value=selected;
    if(data.draft) {
      state.draft=data.draft;
      const candidate=data.candidates.find(c=>c.installation.identity.executable===data.draft.executable);
      if(candidate)select.value=candidate.installation.identity.id;
      const identity=data.draft.executable;
      if(state.profileIds[identity])state.draft.id=state.profileIds[identity];
      else if(data.draft.id)state.profileIds[identity]=data.draft.id;
      for(const key of ['project','module','user','mdb','executable','initialization','workingDirectory'])if(!(key in state.overrides))$(key).value=data.draft[key]||'';
      options('projects',candidate?.installation.projectModules.projects.filter(p=>p.directoryExists).map(p=>p.code)||[]);
      options('modules',data.fields.find(field=>field.field==='module')?.alternatives||[]);
    }
    else {
      state.draft=null;
      options('projects',[]);
      options('modules',[]);
    }
    for(const f of data.fields) {
      const target=document.querySelector('[data-origin="'+f.field+'"]');
      if(target)target.textContent=(f.userOverride?'人工覆盖':f.origin)+' · '+f.validation;
    }
    $('coverage').textContent=(data.coverage.complete?'扫描范围完整':'扫描范围不完整')+' · '+data.coverage.filesSeen+' 个文件 · 下拉打开前刷新';
    $('evidence').textContent=data.evidenceKind;
    $('observed').textContent=new Date(data.observedAt).toLocaleString();
    $('needs').textContent=data.needsInput.join('、')||'无';
    $('workflow-state').textContent=data.state==='resolved'?'环境已解析':'等待必要选择';
    message(data.needsInput.length?'请选择或修正待处理字段；候选不代表实际登录成功。':'静态环境已解析，可以补充用户信息并生成计划。');
  }
  async function discover(resolve=false) {
    const candidateId=$('installation').value;
    const input= {
      overrides: {
        ...state.overrides
      }
    };
    if(candidateId)input.candidateId=candidateId;
    else if(state.preferredExecutable)input.preferredExecutable=state.preferredExecutable;
    const data=await invoke(resolve?'environment.resolve':'environment.discover',input);
    if(!candidateId&&state.preferredExecutable&&data.draft&&data.draft.executable!==state.preferredExecutable) {
      renderDiscovery({...data,draft:null,state:'needsInput',needsInput:[...data.needsInput,'executable']});
      message('保存配置的入口已不可用，请明确选择产品安装；不会自动替换目标。',true);
      return;
    }
    renderDiscovery(data);
  }
  async function connect() {
    try {
      const response=await fetch('/api/operations', {
        headers: {
          'X-PmlTwin-Token':token()
        }
      });
      if(!response.ok)throw new Error('HTTP '+response.status);
      await response.json();
      state.connected=true;
      $('connection').textContent='● 本机服务已连接';
      await loadProfiles();
      await discover();
    }
    catch(e) {
      state.connected=false;
      $('connection').textContent='● 本机服务未连接';
      message('未连接：请通过 Manager workbench 打开此页面。'+e.message,true);
      $('workflow-state').textContent='未连接 · 未执行';
    }
  }
  async function plan() {
    invalidate();
    await discover(true);
    if(!state.draft)throw new Error('请选择有效安装。');
    if(state.discovery.needsInput.length)throw new Error('请先处理必要字段：'+state.discovery.needsInput.join('、'));
    const profile= {
      ...state.draft
    };
    const existing=state.savedProfiles.find(row=>row.profile.id===state.selectedProfileId)?.profile;
    if(existing?.executable===profile.executable) {
      // Preserve saved policy and presentation fields; discovery only supplies
      // target evidence and must not silently disable a required precheck.
      for(const key of ['name','isDefault','isFavorite','environment','license','licensePrecheck']) {
        if(existing[key]!==undefined&&existing[key]!==null)profile[key]=existing[key];
      }
    }
    for(const key of ['project','module','user','mdb','initialization','workingDirectory'])profile[key]=$(key).value;
    if(!profile.id)throw new Error('后端草稿缺少配置 ID；未保存。');
    profile.rememberPassword=false;
    profile.credentialKey='';
    const workspace=await invoke('launch.workspace.read');
    await invoke('launch.profile.preview', {
      profile,expectedRevision:workspace.revision
    });
    const saved=await invoke('launch.profile.save', {
      profile,expectedRevision:workspace.revision,reason:'启动工作台显式保存并生成计划'
    }, {
      allowWrite:true
    });
    const committed=saved.snapshot.value.profiles.find(p=>p.id===profile.id);
    if(!committed?.id||committed.executable!==profile.executable)throw new Error('保存回执未包含预期配置，停止生成计划。');
    state.profileIds[profile.executable]=committed.id;
    state.draft.id=committed.id;
    state.selectedProfileId=committed.id;
    state.preferredExecutable=committed.executable;
    state.savedProfiles=saved.snapshot.value.profiles.map(value=>({profile:value,revision:saved.snapshot.revision}));
    if(!Array.from($('saved-profile').options).some(option=>option.value===committed.id)) {
      const option=document.createElement('option');
      option.value=committed.id;
      option.textContent=committed.name||committed.id;
      $('saved-profile').append(option);
    }
    $('saved-profile').value=committed.id;
    const revision=saved.snapshot.revision;
    state.plan=await invoke('launch.plan', {
      profileId:committed.id,expectedRevision:revision,operationId:crypto.randomUUID()
    });
    state.operationId=state.plan.operationId;
    $('operation-id').textContent=state.operationId;
    $('plan-state').textContent='计划已生成';
    $('plan-summary').textContent='配置 '+state.plan.profileId+' · 修订 '+revision+' · 有效至 '+new Date(state.plan.expiresAt).toLocaleString();
    message('计划已冻结。检查目标与有效期，输入口令后显式启动。');
  }
  async function execute() {
    const plan=state.plan;
    state.plan=null;
    try {
      const receipt=await invoke('launch.execute', {
        plan,password:$('password').value,requestId:crypto.randomUUID()
      }, {
        allowLaunch:true
      });
      renderReceipt(receipt);
    }
    catch(error) {
      if(error.result?.data?.operationId)renderReceipt(error.result.data);
      else {
        $('workflow-state').textContent='执行状态待核对';
        message('未获得完整执行回执。请观察原操作编号，勿重复启动。',true);
      }
      throw error;
    }
    finally {
      $('password').value='';
      $('observe').disabled=!state.operationId;
    }
  }
  function renderReceipt(receipt) {
    const observations={
      'entry-process-absent':'入口已退出，模块可能已接管；请核验工程会话',
      'exited':'入口已退出，模块可能已接管；请核验工程会话',
      'same-entry-process-present':'入口运行中',
      'running':'入口运行中',
      'pid-reused':'进程身份不匹配',
      'identity-mismatch':'进程身份不匹配',
      'identity-unverified':'进程身份尚未核验',
      'not-observed':'入口进程尚未观察'
    };
    const process=observations[receipt.processObservation]||'入口进程状态待核对';
    const host=receipt.hostVerification==='verified'?'Host 身份已核验；登录需另行核对':'登录尚未核验';
    $('process-observation').textContent=process;
    $('host-verification').textContent=host;
    const children=receipt.childCandidates||[];
    $('child-candidates').textContent=children.length?children.map(candidate=>{
      const executable=candidate.executable.split(/[\\/]/).pop();
      return executable+' · PID '+candidate.processId+' · 父 PID '+candidate.parentProcessId+' · '+(candidate.hasWindow?'有窗口':'暂无窗口')+' · 会话 '+candidate.sessionId;
    }).join('；'):'未观察到模块候选；不等于工程已退出';
    $('process-observation').dataset.attention=['pid-reused','identity-mismatch'].includes(receipt.processObservation)?'true':'false';
    $('workflow-state').textContent=process;
    message(process+' · '+host+'。'+receipt.code+' · '+receipt.message);
    $('plan-state').textContent='执行已受理；按回执核对';
  }
  async function restoreObservation() {
    const operationId=$('restore-operation-id').value.trim();
    if(!operationId)throw new Error('请填写已有操作编号。');
    const receipt=await invoke('launch.observe',{operationId});
    if(receipt.operationId!==operationId)throw new Error('观察回执的操作编号不匹配，请核对原编号。');
    state.operationId=receipt.operationId;
    $('operation-id').textContent=receipt.operationId;
    $('observe').disabled=false;
    renderReceipt(receipt);
  }
  function initialize() {
    const closeMenus=()=> {
      document.querySelectorAll('.choices').forEach(panel=> {
        if(!panel.hidden&&panel.contains(document.activeElement))$(panel.id.replace('-choices','')).focus();
        panel.hidden=true;
      });
      document.querySelectorAll('[aria-expanded]').forEach(p=>p.setAttribute('aria-expanded','false'));
    };
    $('theme').onclick=()=> {
      document.documentElement.dataset.theme=document.documentElement.dataset.theme==='dark'?'light':'dark';
    };
    $('saved-profile').onchange=()=>run(async()=>{chooseSavedProfile($('saved-profile').value);await discover(true);});
    $('restore-observe').onclick=()=>run(restoreObservation);
    $('refresh').onclick=()=>run(()=>discover());
    $('installation').onchange=()=> {
      invalidate();
      run(()=>discover(true));
    };
    // Open candidate panels only after the shared backend refresh completes.
    for(const id of ['installation','project','module']) {
      if(id!=='installation')$(id).removeAttribute('list');
      const panel=document.createElement('div');
      panel.className='choices';
      panel.hidden=true;
      panel.setAttribute('role','listbox');
      panel.id=id+'-choices';
      $(id).after(panel);
      $(id).setAttribute('aria-controls',panel.id);
      $(id).setAttribute('aria-expanded','false');
      panel.addEventListener('keydown',e=> {
        const buttons=Array.from(panel.querySelectorAll('button'));
        const index=buttons.indexOf(document.activeElement);
        let next=index;
        if(e.key==='ArrowDown')next=(index+1)%buttons.length;
        else if(e.key==='ArrowUp')next=(index-1+buttons.length)%buttons.length;
        else if(e.key==='Home')next=0;
        else if(e.key==='End')next=buttons.length-1;
        else return;
        e.preventDefault();
        buttons[next]?.focus();
      });
      const open=async e=> {
        if(e.type==='keydown'&&!['ArrowDown','Enter',' '].includes(e.key))return;
        if(e.type==='keydown'||id==='installation')e.preventDefault();
        if(!state.connected||state.busy)return;
        document.querySelectorAll('.choices').forEach(p=>p.hidden=true);
        const refreshed=await run(()=>discover(true));
        if(!refreshed||!state.discovery)return;
        const candidates=id==='installation'?state.discovery.candidates.map(c=>( {
          value:c.installation.identity.id,label:c.installation.identity.product+' · '+c.installation.identity.root
        })):Array.from($(id==='project'?'projects':'modules').options).map(o=>( {
          value:o.value,label:o.value
        }));
        panel.replaceChildren(...candidates.map(c=> {
          const button=document.createElement('button');
          button.type='button';
          button.textContent=c.label;
          button.setAttribute('role','option');
          button.onclick=()=> {
            $(id).value=c.value;
            panel.hidden=true;
            $(id).setAttribute('aria-expanded','false');
            invalidate();
            if(id==='installation')run(()=>discover(true));
            else {
              state.overrides[id]=c.value;
              $(id).dispatchEvent(new Event('input'));
            }
            $(id).focus();
          };
          return button;
        }));
        panel.hidden=false;
        $(id).setAttribute('aria-expanded','true');
        if(e.type==='keydown')panel.querySelector('button')?.focus();
      };
      $(id).addEventListener('pointerdown',open);
      $(id).addEventListener('keydown',open);
    }
    document.addEventListener('keydown',e=> {
      if(e.key==='Escape')closeMenus();
    });
    document.addEventListener('pointerdown',e=> {
      if(!e.target.closest('.choices')&&!['installation','project','module'].includes(e.target.id))closeMenus();
    });
    document.addEventListener('focusin',e=> {
      if(!e.target.closest('.choices')&&!['installation','project','module'].includes(e.target.id))closeMenus();
    });
    for(const key of ['project','module','user','mdb','initialization','workingDirectory'])$(key).addEventListener('input',()=> {
      state.overrides[key]=$(key).value;
      invalidate();
    });
    $('plan').onclick=()=>run(plan);
    $('execute').onclick=()=>run(execute);
    $('observe').onclick=()=>run(async()=>renderReceipt(await invoke('launch.observe', {
      operationId:state.operationId
    })));
    run(connect);
  }
  root.PmlTwinLaunch= {
    state,invoke,initialize,renderDiscovery,renderReceipt,plan,execute,loadProfiles,chooseSavedProfile,restoreObservation
  };
  if(typeof document!=='undefined')document.addEventListener('DOMContentLoaded',initialize);
})(typeof window!=='undefined'?window:globalThis);
