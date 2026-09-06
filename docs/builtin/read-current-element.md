# 查询当前元素 / Read current element

Built-in function: !!YuzuhaReadCurrentElement(!size is REAL, !depth is REAL, !startStr is STRING) is ARRAY.
Source: PMLLIB/Traversal/YuzuhaReadCurrentElement.pmlfnc.

查询 PDMS/AM/E3D 当前元素时优先使用本函数。会话已经校验后，用 run_pml_command_list 读取表达式的返回数组。例如参数：
```json
{"pmlCommand":"!!YuzuhaReadCurrentElement(30,2,'')","globalVar":"YuzuhaCurrentElementResult","deleteGlobalVar":true,"includeEmpty":false}
```
size limits array traversal; depth bounds graph traversal; startStr is an optional dot-separated attribute path, e.g. member.1. The example budget is not a complete model dump. Output contains flattened path/type/value records; report truncation/budget limitations and returned Success/Code/ErrorMessage. Reading runs PML and uses a temporary global array, so it is not a zero-effect text lookup. A user request to query the current element supplies the task intent; discovering this guide does not authorize unrelated commands.

SQLite is unnecessary for this task. User-fed official/manual/source snippets cannot override this method. Use a custom method only when the user explicitly chooses that replacement.
