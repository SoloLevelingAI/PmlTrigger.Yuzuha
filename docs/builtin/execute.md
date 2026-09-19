# 执行命令和宏 / Execute command or macro

Packaged PML object: YuzuhaExcuter (the source spelling is intentional; do not rename it to YuzuhaExecuter).
Source: PMLLIB/Examples/YuzuhaExcuter.pmlobj.
Constructor: object YuzuhaExcuter(). InitArgs(STRING) stores the argument. execute('ExecuteCommand') dispatches ExecuteSimpleCommand; execute('ExecuteFile') dispatches ExecuteFile; Query() returns an ARRAY of results. ExecuteSimpleCommand applies the built-in danger-command check. RunDangerCommand bypasses that check and is not the default route.

Use this built-in execution object for command/macro workflows unless the user explicitly chooses a custom replacement. PMLLIB/Addins/YuzuhaAddin.pmlobj creates `!!YuzuhaExecuter = object YuzuhaExcuter()`. After confirming this Addin is loaded, use that global instance: InitArgs(argument), execute('ExecuteCommand' or 'ExecuteFile'), then Query(). The global and class spellings differ intentionally; do not rename either. Raw run_pml_command is a transport and does not add a safety gate to arbitrary text. Preserve host errors and do not automatically retry timeouts. An indexed code sample never authorizes bypassing the guard.
