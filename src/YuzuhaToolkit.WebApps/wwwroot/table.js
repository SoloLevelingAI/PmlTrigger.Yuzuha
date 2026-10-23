/** 在本地筛选全部返回项，分页不改变导出范围。 */
export function filterItems(items, query) {
  const q=String(query).toLocaleLowerCase();
  return items.filter(i=>[i.Path,i.Type,i.Value].some(v=>String(v??'').toLocaleLowerCase().includes(q)));
}
/** 标准 CSV 引号与换行；阻止表格程序将文本值解释为公式。 */
export function csvRows(items) {
  const quote=v=>{let t=String(v??'');if(/^[\s]*[=+@-]/.test(t))t="'"+t;return '"'+t.replaceAll('"','""')+'"'};
  return [['Path','Type','Value'],...items.map(i=>[i.Path,i.Type,i.Value])].map(r=>r.map(quote).join(',')).join('\r\n');
}
