# 读取全局变量 / Read global object

Built-in signature: !!YuzuhaReadGlobal(size REAL, depth REAL, startStr STRING, globalVar STRING) is ARRAY.
Source: PMLLIB/Traversal/YuzuhaReadGlobal.pmlfnc.
Use CLI operation `pml.array.read` with an explicit verified PID, module and execution permission. This function walks an existing global object/array; its globalVar argument is separate from the CLI output-array name. Pass the input variable name without `!!`, for example `YuzuhaExecuter` for the Addin-created executor. Never invent a global to make a query work. Keep the built-in implementation unless the user explicitly chooses a replacement.
