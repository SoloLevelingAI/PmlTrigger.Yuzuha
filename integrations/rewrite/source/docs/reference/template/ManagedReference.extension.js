// DocFX's server-side template hook. ES5 syntax keeps it compatible with its JS interpreter.
exports.postTransform = function (model) {
  var declaration = model.syntax && model.syntax.content && model.syntax.content[0] ? model.syntax.content[0].value : '';
  var record = /\brecord\b/.test(declaration);
  var kind = String(model.type).toLowerCase();
  model.ldKind = record ? 'RECORD' : kind.toUpperCase();
  model.ldCategory = kind === 'interface' ? '接口契约' : record || kind === 'enum' || kind === 'struct' || kind === 'delegate' || model.uid === 'LaunchDesk.Kernel.Result`1' ? '数据模型' : '类型实现';
  if(model.ldCategory === '类型实现' && model.uid.indexOf('LaunchDesk.Desktop.') === 0) {
    var name = model.uid.split('.').pop();
    model.ldCategory = /(?:View|Window)$|^(DesignedPage|PasswordEditor|PasswordEntryControl)$/.test(name) ? '视图展示' :
      /ViewModel$|Editor$|^(ObservableObject|CatalogPageBase|WorkspacePresentationState)$/.test(name) ? '交互模型' : '交互支持';
  }
  model.ldHasRelations = !!((model.inheritance || []).length || (model.implements || []).length || (model.derivedClasses || []).length || (model.inheritedMembers || []).length || (model.extensionMethods || []).length);
  if (kind !== 'namespace') model._disableToc = false;
  var seenOverloads = {};
  var groups = [];
  (model.children || []).forEach(function (group) {
    group.ldTable = !!(group.inProperty || group.inField || group.inEvent);
    group.ldFold = !!(record && (group.inConstructor || group.inMethod || group.inOperator));
    (group.children || []).forEach(function (member) {
      member.ldAuxiliary = group.ldFold;
      var syntax = member.syntax || {};
      var content = syntax.content && syntax.content[0] ? syntax.content[0].value : '';
      var slot = syntax.propertyValue || syntax.fieldValue || syntax.eventType;
      member.ldType = slot && slot.type && slot.type.specName && slot.type.specName[0] ? slot.type.specName[0].value : '';
      member.ldAccess = /\binit\s*;/.test(content) ? 'get · init' : /\bset\s*;/.test(content) ? 'get · set' : /\bget\s*;/.test(content) || content.indexOf('=>') >= 0 ? 'get' : /\bconst\b/.test(content) ? 'const' : /\breadonly\b/.test(content) ? 'readonly' : '—';
      member.ldShortName = member.name && member.name[0] ? member.name[0].value.split('(')[0] : member.uid;
      // Each overload group has one anchor, while every signature retains its own original ID.
      var overloads = member.overload ? (Array.isArray(member.overload) ? member.overload : [member.overload]) : [];
      member.ldOverloads = overloads.filter(function (overload) {
        if (!overload.id || overload.id === member.id || seenOverloads[overload.id]) return false;
        seenOverloads[overload.id] = true;
        return true;
      });
      member.ldGenerated = /编译器生成的记录成员|编译器提供的隐式构造函数/.test(member.summary || '');
    });
    var generated = (group.children || []).filter(function (member) { return member.ldGenerated; });
    var authored = (group.children || []).filter(function (member) { return !member.ldGenerated; });
    if (!generated.length || group.ldTable) { groups.push(group); return; }
    if (authored.length) {
      var original = {};
      Object.keys(group).forEach(function (key) { original[key] = group[key]; });
      original.children = authored;
      groups.push(original);
    }
    var auxiliary = {};
    Object.keys(group).forEach(function (key) { auxiliary[key] = group[key]; });
    auxiliary.children = generated;
    auxiliary.ldFold = true;
    auxiliary.ldGeneratedGroup = true;
    auxiliary.ldFoldLabel = '编译器生成成员 · ' + generated.length;
    generated.forEach(function (member) { member.ldAuxiliary = true; });
    groups.push(auxiliary);
  });
  model.children = groups;
  if (record) model.children.sort(function (a,b) { return (a.inProperty ? -1 : 0) - (b.inProperty ? -1 : 0); });
  return model;
};
