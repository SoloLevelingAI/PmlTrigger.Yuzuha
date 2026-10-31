namespace LaunchDesk.Core;

public static class VariableEditor
{
 public static ToolVariable Apply(ToolVariable variable,string expandedValue,Func<string,VariableScope,string?> read,Action<string,string?,VariableScope> write)
 {
  ToolEnvironment.ValidateVariable(variable);
  if(variable.Scope!=VariableScope.Process&&string.IsNullOrEmpty(expandedValue))throw new InvalidDataException("写入 Windows 的变量值不能为空。");
  var next=WorkspaceStore.Clone(variable);
  var oldPersistent=variable.WasApplied&&variable.AppliedScope!=VariableScope.Process;
  if(oldPersistent) {var old=WorkspaceStore.Clone(variable);old.Name=variable.AppliedName;ToolEnvironment.ValidateVariable(old);if(read(old.Name,variable.AppliedScope)!=variable.AppliedValue)throw new InvalidDataException("原环境变量已被其他程序修改，请重新确认后再应用。");}
  bool same=oldPersistent&&variable.Scope==variable.AppliedScope&&variable.Name.Equals(variable.AppliedName,StringComparison.OrdinalIgnoreCase);
  string? before=variable.Scope==VariableScope.Process?null:read(variable.Name,variable.Scope);
  if(variable.Scope!=VariableScope.Process&&!same&&before is not null)throw new InvalidDataException("新名称已存在于所选范围，请选择其他名称，避免覆盖其他工具的变量。");
  bool newWritten=false,oldRemoved=false;
  try {
   if(variable.Scope!=VariableScope.Process){write(variable.Name,expandedValue,variable.Scope);newWritten=true;}
   if(oldPersistent&&!same){write(variable.AppliedName,null,variable.AppliedScope);oldRemoved=true;}
   next.WasApplied=variable.Scope!=VariableScope.Process;next.AppliedName=next.WasApplied?variable.Name:"";next.AppliedValue=next.WasApplied?expandedValue:"";next.AppliedScope=variable.Scope;
   return next;
  } catch {
   if(oldRemoved)write(variable.AppliedName,variable.AppliedValue,variable.AppliedScope);
   if(newWritten)write(variable.Name,before,variable.Scope);
   throw;
  }
 }
}
