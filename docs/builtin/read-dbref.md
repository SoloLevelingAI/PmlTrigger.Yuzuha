# 读取指定元素 / Read DBREF

Built-in signature: !!YuzuhaReadDbref(size REAL, depth REAL, startStr STRING, name STRING) is ARRAY.
Source: PMLLIB/Traversal/YuzuhaReadDbref.pmlfnc.
Use run_pml_command_list after selecting and verifying a session. Example expression: !!YuzuhaReadDbref(30,2,'','/EXAMPLE'). Replace /EXAMPLE with the actual user-specified target. name accepts a short name, full /path or =dbref. The implementation temporarily changes CE and restores it; do not treat this as unrelated model creation. Consult returned errors/empty results rather than guessing another target. Preserve parameter order. Built-in method priority applies unless the user explicitly selects a replacement.
