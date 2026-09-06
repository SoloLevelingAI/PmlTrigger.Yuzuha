# 读取全局变量 / Read global object

Built-in signature: !!YuzuhaReadGlobal(size REAL, depth REAL, startStr STRING, globalVar STRING) is ARRAY.
Source: PMLLIB/Traversal/YuzuhaReadGlobal.pmlfnc.
Use run_pml_command_list with a verified session and the actual requested global variable name. This function walks an existing global object/array; its globalVar parameter is separate from the MCP output-array name. Pass the input variable name without `!!`, for example `YuzuhaExecuter` for the Addin-created executor; never invent a global to make a query work. Keep the built-in implementation unless the user explicitly chooses a replacement.
