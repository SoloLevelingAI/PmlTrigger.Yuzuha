# Yuzuha PMLLIB 参考（双语） / PMLLIB Reference (Bilingual)

适用版本 / Applies to: **YuzuhaToolkit.Agent 0.2.2**（PmlTrigger.Yuzuha 包）。
本文按 PMLLIB 实际源码整理，覆盖启动链、读取 API、执行器与示例对象。
This document is derived from the actual PMLLIB sources and covers the
bootstrap chain, the read APIs, the command executor and the example objects.

## 1. 包结构 / Package layout

```text
PMLLIB/
├─ Addins/     YuzuhaAddin.pmlobj  YuzuhaRpc.pmlobj          启动链 / bootstrap chain
├─ Bootstrap/  YuzuhaResolveRuntimePath.pmlfnc               运行时路径解析 / runtime path resolution
├─ Traversal/  YuzuhaObjectWalker.pmlobj                     BFS 对象图遍历核心 / BFS walker core
│              YuzuhaReadCurrentElement.pmlfnc               读当前元素 / read CE
│              YuzuhaReadDbref.pmlfnc                        读指定元素 / read named element
│              YuzuhaReadGlobal.pmlfnc                       读全局变量 / read global variable
├─ Examples/   YuzuhaExcuter.pmlobj  YuzuhaTargetQuery.pmlobj
│              YuzuhaBoxExample.pmlobj  YuzuhaCreateBoxExample.pmlfnc
└─ Obsolete/   （已废弃的 pmlcmd，不再使用 / retired commands, unused）
```

## 2. 启动链与环境变量 / Bootstrap chain & environment

AVEVA 启动时由 Addin 机制自动完成 / On AVEVA startup the Addin chain runs
automatically:

```text
YuzuhaAddin() 构造
  → !!YuzuhaRpc    = object YuzuhaRpc()      -- AgentSetup()
  → !!YuzuhaExecuter = object YuzuhaExcuter()
YuzuhaRpc.AgentSetup()
  → !!YuzuhaResolveRuntimePath()             -- 解析 Host DLL 路径 / resolve host DLL
  → import '<YuzuhaToolkit.PmlHost.NetXX>'
  → !!YuzuhaRpcHost = object PmlCommandMethod()
  → !!YuzuhaRpcHost.GetRpcServerStatus()     -- 须返回 RUNNING / must return RUNNING
```

`!!YuzuhaResolveRuntimePath()` 读取 EVAR `Yuzuha`（**无下划线**，取值如
`E3D2.1` / `AM` / `PDMS`），并在 `PMLUI` EVAR 各路径中查找包含 `PMLTRI`
的安装根目录，拼出
`<根>\runtime\profiles\<profile>\net35|net48\YuzuhaToolkit.PmlHost.NetXX`，
同时设置全局变量 `!!YuzuhaRuntimePath`、`!!YuzuhaNamespace`、
`!!YuzuhaAutoSetup`。
It reads the `Yuzuha` EVAR (no underscore; values like `E3D2.1` / `AM` /
`PDMS`), locates the toolkit root by scanning the `PMLUI` EVAR paths for the
`PMLTRI` marker, builds the host DLL path above and sets the
`!!YuzuhaRuntimePath` / `!!YuzuhaNamespace` / `!!YuzuhaAutoSetup` globals.

### EVAR 托管块规则 / EVAR managed-block rules（安装脚本写入）

安装脚本（`Register-YuzuhaMcp.ps1`）在 EVAR 批处理文件中写入托管块，
规则如下 / the installer writes a managed block with these rules:

```bat
rem >>> Yuzuha managed settings
rem Custom variable name must remain Yuzuha (no underscore).
set Yuzuha=E3D2.1
set pmllib=C:\...\PmlTrigger.Yuzuha\PMLLIB;%pmllib%
set pmlui=C:\...\PmlTrigger.Yuzuha\PMLUI;%pmlui%
rem <<< Yuzuha managed settings
```

- **不带引号**：`set` 行一律不写引号 / never quote the `set` lines.
- **追加在文件末尾**：必须放在批处理自身初始化（原有的
  `set pmllib=...` / `set pmlui=...`）**之后**，这样 `%pmllib%` / `%pmlui%`
  已包含原始路径，前置 Yuzuha 路径不会被后续绝对赋值覆盖。
  The block must be appended **after** the file's own initialization so the
  original paths survive; setting it earlier would be clobbered by a later
  absolute `set pmllib=...`.
- 变量名：**E3D 用 `pmlui`，AM/PDMS 用 `pdmsui`**（安装脚本的
  `-AvevaProfile` 仅支持 AM/PDMS，E3D 的 EVAR 为手工管理）。
  E3D uses `pmlui`; AM/PDMS use `pdmsui`.
- 卸载时按 `rem >>> / rem <<< Yuzuha managed settings` 标记整块移除，
  原始路径不受影响。Uninstall removes the marked block only.

## 3. 读取 API / Read APIs（核心 / core）

三个全局函数都基于 `YuzuhaObjectWalker` 的 BFS 遍历，返回**扁平数组**，
每行形如 `[depth] path<TYPE> value`（经 MCP 桥接后为结构化 JSON 的
`depth/path/type/value`）。参数含义一致：
All three functions wrap the `YuzuhaObjectWalker` BFS traversal and return a
flattened array; through the MCP bridge each row becomes a structured
`depth/path/type/value` item.

| 函数 / Function | 签名 / Signature | 说明 / Description |
|---|---|---|
| `!!YuzuhaReadCurrentElement(size, depth, startStr)` | `(REAL, REAL, STRING) is ARRAY` | 读当前元素 CE 对象图 / read the current element graph |
| `!!YuzuhaReadDbref(size, depth, startStr, name)` | `(REAL, REAL, STRING, STRING) is ARRAY` | 读指定元素（短名 / `/全路径` / `=dbref`），读后**恢复原 CE** / read a named element, original CE restored |
| `!!YuzuhaReadGlobal(size, depth, startStr, globalVar)` | `(REAL, REAL, STRING, STRING) is ARRAY` | 读全局变量（名字**不带** `!!` 前缀）/ read a global variable (name without `!!`) |

- `size`：返回行数硬上限（行数到顶即停）/ hard cap on returned rows.
- `depth`：BFS 层数上限 / BFS depth cap.
- `startStr`：点分起始属性路径，如 `'member'`、`'member.1'`、`'hpos'`、
  `''`（空 = 从元素本身开始）/ dot-separated start attribute path; `''`
  starts at the element itself.

### 预算指引 / Budget guidance（重要 / important）

**首次探测一律用小预算**（例如 30 行、2 层），确认结构后再定向加深，
不要一上来就 300：
**Always probe small first** (about 30 rows, depth 2), then drill down with a
targeted `startStr`; do not start with 300 rows:

```pml
-- 第一步：小预算探测结构 / step 1: small probe
!!YuzuhaReadCurrentElement(30,2,'')

-- 第二步：锁定感兴趣的属性后定向加深 / step 2: targeted drill-down
!!YuzuhaReadCurrentElement(30,2,'member.1')
!!YuzuhaReadDbref(30,3,'hpos','/ZONE-A/EQ-100')
!!YuzuhaReadGlobal(30,2,'','YuzuhaExecuter')
```

### YuzuhaObjectWalker（直接使用 / direct use）

一般用上面的函数即可；需要自定义时可直接实例化：
Usually prefer the wrapper functions; instantiate directly for custom use:

```pml
!ob = object YuzuhaObjectWalker()   -- 默认 / defaults: MaxArraySize=500, MaxBFSDepth=3
!ob.InitializeFromCurrentElement()  -- 或 / or InitializeFromGlobal('varName') / InitializeFromArray(!arr)
!ob.MaxArraySize = 30               -- 预算仍建议从小开始 / keep budgets small
!ob.MaxBFSDepth   = 2
!ob.startStr      = 'member'
!arr = !ob.Read()                   -- 返回扁平数组 / returns the flattened array
```

| 成员/方法 / Member·Method | 类型/返回 / Type·Return | 说明 / Description |
|---|---|---|
| `.MaxArraySize` | REAL（默认 500）| 行数硬上限 / row cap |
| `.MaxBFSDepth` | REAL（默认 3）| BFS 深度上限 / depth cap |
| `.startStr` | STRING | 起始属性路径 / start attribute path |
| `.InitializeFromCurrentElement()` | - | 以 CE 为根 / root at CE |
| `.InitializeFromGlobal(name)` | - | 以全局变量为根（不带 `!!`）/ root at a global |
| `.InitializeFromArray(arr)` | - | 以数组为根 / root at an array |
| `.Read()` | ARRAY | 执行遍历 / run the traversal |

## 4. 执行器与示例对象 / Executor & example objects

### YuzuhaExcuter（命令执行器，全局 `!!YuzuhaExecuter`）

启动链创建的全局对象，供 RPC 宿主调用：
Global object created by the bootstrap chain, used by the RPC host:

- `InitArgs(str)`：暂存下一次执行的参数串 / stash the argument string.
- `execute(str)`：按 `'ExecuteCommand'` / `'ExecuteFile'` 分发 / dispatch.
- `ExecuteSimpleCommand(cmd)`：**危险命令守卫**——命令文本（大写化后）含
  `DELETE` / `KILL` / `DESIGN` / `QUIT` / `DRAW` / `PARAGON` 任一即拒绝执行，
  需要 `RunDangerCommand(cmd)` 显式放行。Danger-command guard: those
  keywords are rejected; `RunDangerCommand` is the explicit bypass.
- `ExecuteFile(file)`：以 `$M <file>` 运行宏文件 / run a macro file via `$M`.
- `Query()`：返回结果数组（可经
  `!!YuzuhaReadGlobal(30,2,'','YuzuhaExecuter')` 读回）/ result array for
  read-back.

注意拼写：对象类名是 `YuzuhaExcuter`，全局变量名是
`!!YuzuhaExecuter`（多一个 e），二者均有效。
Note the spelling difference: class `YuzuhaExcuter`, global `!!YuzuhaExecuter`.

### YuzuhaTargetQuery

- `ResolveDbref(name)` → DBREF：短名 / `/全路径` / `=dbref` 归一化，
  找不到返回 badref。
- `ReadTarget(name)` → ARRAY：切 CE → 遍历（固定预算 10/3）→ 恢复原 CE。

### YuzuhaBoxExample / !!YuzuhaCreateBoxExample

写模型示例（**仅在用户显式要求时运行** / run only on explicit request）：

```pml
!!YuzuhaCreateBoxExample('<localAt>', '<name>', xlen, ylen, zlen)
-- 例 / e.g. !!YuzuhaCreateBoxExample('ABC111', 'TestName2', 50, 50, 50)
```

在 localAt 下创建 BOX（可加 `YuzuhaBoxExample.InitializeWithPosition(...)`
带 E/N/U 定位），成功与否通过 `state` / `errorText` 报告（`NameUsed` /
`LocalDb ...`），并把结果对象图经 `YuzuhaReadGlobal` 读回。临时全局
`!!YUZUHAEXAMPLERES` 用后即删。
Creates a BOX under localAt (optionally positioned via
`InitializeWithPosition`), reports through `state` / `errorText` and reads
the resulting graph back; the temporary `!!YUZUHAEXAMPLERES` global is
deleted afterwards.

## 5. 桥接约定 / Bridge conventions

- 属性路径分隔符用 `.`（`'hpos.east'`、`'member.1'`）；数组元素在桥接侧
  归一化为 `MEMBERS.|N|`。The `.` is the attribute separator; array elements
  normalize to `MEMBERS.|N|` on the bridge side.
- **`@` 字符会被宿主 `Command.Run()` 解析器拒绝**，仅在 E3D 交互命令行可用。
  The `@` character is rejected by the host parser; interactive-only.
- 元素定位三种写法：短名 `'EQ-100'`、全路径 `'/ZONE-A/EQ-100'`、
  DBREF `'=1000/2'`。Three element-locator forms are accepted.
- `run_pml_command_list` 约定：`pmlCommand` 填完整函数调用（其结果赋给全局
  数组），`globalVar` 为数组名（如 `PMLGLOBALARRFORRPC`），读后
  `deleteGlobalVar=true` 清理。
- 读取函数失败时返回空数组或 `failedToGetValue ...` 行，不是异常。Reads
  degrade to empty arrays / failure rows instead of throwing.

## 6. 安全 / Safety

- 读取类函数无副作用（`YuzuhaReadDbref` 会临时切换 CE 但保证恢复）。
  Read functions are side-effect free (CE is restored).
- 写模型 / 执行命令类（`YuzuhaCreateBoxExample`、`YuzuhaExcuter` 的
  `RunDangerCommand`）只能由用户显式要求时执行，且不得自动重试超时。
  Write/execute paths run only on explicit user request; never auto-retry a
  timeout.
