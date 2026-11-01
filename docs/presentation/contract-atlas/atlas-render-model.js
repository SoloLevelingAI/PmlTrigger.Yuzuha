globalThis.CONTRACT_ATLAS_RENDER_MODEL = {
  "schema": "contract-atlas-render/1",
  "schemaVersion": 1,
  "metadataDigest": "b20eca32303344af00679aab7857c1b96a2011962a97ba12c43403535fb8783f",
  "audience": "public",
  "snapshotDate": "2026-11-01",
  "project": {
    "id": "project:pmltwin",
    "title": "PMLTWIN",
    "summary": "让发现、配置、受控执行和证据围绕同一任务协作。",
    "goalIds": [
      "canonical:G1",
      "canonical:G2",
      "canonical:G3"
    ],
    "featureIds": [
      "feature:launch",
      "feature:deployment",
      "feature:sessions",
      "feature:objects",
      "feature:functions",
      "feature:forms",
      "feature:arrays",
      "feature:formuse",
      "feature:knowledge",
      "feature:activity",
      "feature:review",
      "feature:tests",
      "feature:web",
      "feature:ribbon",
      "feature:database",
      "feature:migration",
      "feature:caf",
      "feature:plugins"
    ]
  },
  "cards": [
    {
      "id": "project:pmltwin",
      "entityId": "project:pmltwin",
      "component": "ProjectOverview",
      "title": "PMLTWIN",
      "summary": "让发现、配置、受控执行和证据围绕同一任务协作。",
      "sections": [
        {
          "id": "project:pmltwin/summary",
          "key": "summary",
          "title": "项目任务",
          "text": "让发现、配置、受控执行和证据围绕同一任务协作。",
          "sourceRefs": [
            "source:presentation"
          ]
        },
        {
          "id": "project:pmltwin/boundaries",
          "key": "boundaries",
          "title": "判断边界",
          "text": "规范状态、实现范围、证据分别判断；展示关系不是运行轨迹。",
          "sourceRefs": [
            "source:presentation"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R01",
          "originalId": "R01",
          "status": "planned",
          "rule": "常用任务从工作台完成；字段、下一步与失败出口可见。",
          "acceptance": "非实现者按 PRD 完成启动配置、检索、查询、历史恢复；不编辑 JSON，不需要解释控件含义；记录步骤、阻塞和误操作。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R02",
          "originalId": "R02",
          "status": "verified",
          "rule": "产品改变即更新模块；AM 不出现 DESIGN；安装发现与登录成功分别显示。",
          "acceptance": "E3D/AM 切换后模块属于当前产品；无安装只显示未发现；保存无效组合被拒绝。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R03",
          "originalId": "R03",
          "status": "verified",
          "rule": "配置列表同时应用产品、收藏/最近下拉与输入搜索；空结果可清空条件。",
          "acceptance": "两个下拉和关键字取交集；重置后恢复全部；保留当前草稿。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R04",
          "originalId": "R04",
          "status": "verified",
          "rule": "编辑→差异预览→保存→重开；未保存草稿离开需确认。",
          "acceptance": "保存显示修订；新服务重读相同值；取消离开保留草稿；保存失败不显示成功。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R05",
          "originalId": "R05",
          "status": "verified",
          "rule": "过期修订进入冲突状态；保留草稿，允许重新读取并比较。",
          "acceptance": "外部修改后保存被拒绝；界面不悄悄覆盖，草稿仍可查看。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R06",
          "originalId": "R06",
          "status": "verified",
          "rule": "历史先预览再恢复；恢复生成新修订。",
          "acceptance": "选历史→查看差异→确认；取消不写入；恢复后其他配置不变。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R07",
          "originalId": "R07",
          "status": "planned",
          "rule": "知识入口默认官方 HTML 与源码；个人经验为补充，结果显示来源、产品与版本。",
          "acceptance": "首次使用无需手动登记函数；无官方资料显示定位/导入入口；经验不会覆盖官方来源。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R08",
          "originalId": "R08",
          "status": "partial",
          "rule": "能力从 CLI 契约和已选源码发现，显示签名、来源与可用性。",
          "acceptance": "CLI 契约已可枚举；PML 自动发现仍需实现；缺协议或未授权显示原因，不能冒充不可发现。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R09",
          "originalId": "R09",
          "status": "partial",
          "rule": "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
          "acceptance": "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R10",
          "originalId": "R10",
          "status": "partial",
          "rule": "脚本流程包含写入计划、备份、加载、执行与结果对比。",
          "acceptance": "先看文件差异与执行目标；加载失败有回执；修正后独立验证，不用脚本合集替代工作流。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R11",
          "originalId": "R11",
          "status": "partial",
          "rule": "执行模式与权限可见；结果不明有单独状态和核验入口。",
          "acceptance": "关闭执行仍可查资料/看计划；未知结果不显示成功，不出现自动重试按钮。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R12",
          "originalId": "R12",
          "status": "planned",
          "rule": "业务错误定位字段或动作，并保留可恢复上下文。",
          "acceptance": "键盘完成筛选与确认；焦点可见；空/加载/拒绝/冲突/未知状态都有文案和出口；可用性实测另记。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R13",
          "originalId": "R13",
          "status": "verified",
          "rule": "WPF 与 CLI 共用启动配置服务；存储与产品目录按实际变化点替换。",
          "acceptance": "同输入经两个适配器得到相同规则/修订；替换存储或目录不改用例；不为每个方法新增接口。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R14",
          "originalId": "R14",
          "status": "verified",
          "rule": "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
          "acceptance": "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R15",
          "originalId": "R15",
          "status": "verified",
          "rule": "恢复重新校验当前环境，只恢复目标配置，产生新历史。",
          "acceptance": "失效路径拒绝；其他配置、旧历史与独立凭据不被回写。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R19",
          "originalId": "R19",
          "status": "partial",
          "rule": "复制计划冻结源/目标哈希，执行保留备份；与 PML 加载/调试串联。",
          "acceptance": "复制前变更任一端即拒绝；路径不能越界；部署成功与运行成功分开记录。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R20",
          "originalId": "R20",
          "status": "planned",
          "rule": "官方 HTML/源码是知识主源；按产品版本建立引用，个人资料独立补充。",
          "acceptance": "保留来源/版本/块 ID；旧索引可辨识；无资料不编造答案；默认官方流程尚待实现。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R21",
          "originalId": "R21",
          "status": "verified",
          "rule": "日志关联开始、结束、耗时与目标；未结束操作归为未知。",
          "acceptance": "命名秘密脱敏；日志故障不把已执行改成失败并诱发重试；记录结果与业务结果分别保存。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R22",
          "originalId": "R22",
          "status": "verified",
          "rule": "代码、JSON、TOML 用同一业务路径测试，逐步保存输入、输出与结论。",
          "acceptance": "解析失败/超时保留部分结果；历史证据与本轮运行区分；报告按新目录保存。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R23",
          "originalId": "R23",
          "status": "verified",
          "rule": "启动初始化读 Current Session；产品系列选择 Host；REHASH 由独立策略和环境值控制。",
          "acceptance": "E3D2.1→2.1/net48；3.1→3.1.6/net48；AM/PDMS→net35；none/空关闭，rehash-all 每新会话一次；改值下次进程生效。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R24",
          "originalId": "R24",
          "status": "verified",
          "rule": "文档从 Roslyn 生成签名、源码定位、静态调用与实现候选；手写只写业务约束。",
          "acceptance": "缺符号/失效源码哈希/悬空需求引用使构建失败；静态候选不得描述为实机执行；契约元数据可无损展开。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        },
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:caf-static",
          "scope": "static",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "CAF"
          },
          "claim": "真实SDK接口编译与生命周期隔离核查。",
          "exclusions": [
            "未真实注册宿主",
            "未界面挂载"
          ],
          "evidenceIds": [
            "evidence:caf"
          ],
          "environmentText": "产品：CAF"
        }
      ],
      "limitations": [
        "本图谱不执行登录、部署或宏，不是第二份生效规范"
      ],
      "sourceRefs": [
        "source:canonical",
        "source:capabilities",
        "source:overview",
        "source:presentation",
        "source:evidence",
        "source:source-mappings",
        "source:openspec-proposal",
        "source:openspec-contracts",
        "source:openspec-design",
        "source:openspec-tasks",
        "source:validator",
        "source:render-model",
        "source:evidence-migration",
        "source:evidence-ribbon-ui",
        "source:evidence-ribbon-runtime",
        "source:evidence-database",
        "source:evidence-caf"
      ],
      "props": {},
      "text": {
        "body": "项目任务：让发现、配置、受控执行和证据围绕同一任务协作。\n判断边界：规范状态、实现范围、证据分别判断；展示关系不是运行轨迹。\n规范：R01 planned · R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R07 planned · R08 partial · R09 partial · R10 partial · R11 partial · R12 planned · R13 verified · R14 verified · R15 verified · R16 verified · R17 verified · R18 verified · R19 partial · R20 planned · R21 verified · R22 verified · R23 verified · R24 verified\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换\n\nui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块\n\noffline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储\n\nstatic / passed · 2026-10-31 · 产品：CAF\n真实SDK接口编译与生命周期隔离核查。\n未涵盖：未真实注册宿主；未界面挂载",
        "status": "规范 / 实现 / 证据三轴独立",
        "limit": "本图谱不执行登录、部署或宏，不是第二份生效规范；没有执行数据库切换；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块；未整体切换活动存储；未真实注册宿主；未界面挂载"
      },
      "body": "项目任务：让发现、配置、受控执行和证据围绕同一任务协作。\n判断边界：规范状态、实现范围、证据分别判断；展示关系不是运行轨迹。\n规范：R01 planned · R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R07 planned · R08 partial · R09 partial · R10 partial · R11 partial · R12 planned · R13 verified · R14 verified · R15 verified · R16 verified · R17 verified · R18 verified · R19 partial · R20 planned · R21 verified · R22 verified · R23 verified · R24 verified\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换\n\nui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块\n\noffline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储\n\nstatic / passed · 2026-10-31 · 产品：CAF\n真实SDK接口编译与生命周期隔离核查。\n未涵盖：未真实注册宿主；未界面挂载",
      "status": "规范 / 实现 / 证据三轴独立",
      "limit": "本图谱不执行登录、部署或宏，不是第二份生效规范；没有执行数据库切换；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块；未整体切换活动存储；未真实注册宿主；未界面挂载",
      "evidence": [
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        },
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:caf-static",
          "scope": "static",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "CAF"
          },
          "claim": "真实SDK接口编译与生命周期隔离核查。",
          "exclusions": [
            "未真实注册宿主",
            "未界面挂载"
          ],
          "evidenceIds": [
            "evidence:caf"
          ],
          "environmentText": "产品：CAF"
        }
      ]
    },
    {
      "id": "feature:launch",
      "entityId": "feature:launch",
      "component": "FeatureCard",
      "title": "工程发现与启动",
      "summary": "冻结一次启动，保存修订并观察原编号。",
      "sections": [
        {
          "id": "feature:launch/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "冻结一次启动，保存修订并观察原编号。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:launch/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "配置修订；明确产品与模块",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:launch/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "候选来源；启动计划与关联回执",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:launch/effects",
          "key": "effects",
          "title": "副作用",
          "text": "授权后创建入口进程",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:launch/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "歧义、冲突与unknown",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R02",
          "originalId": "R02",
          "status": "verified",
          "rule": "产品改变即更新模块；AM 不出现 DESIGN；安装发现与登录成功分别显示。",
          "acceptance": "E3D/AM 切换后模块属于当前产品；无安装只显示未发现；保存无效组合被拒绝。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R03",
          "originalId": "R03",
          "status": "verified",
          "rule": "配置列表同时应用产品、收藏/最近下拉与输入搜索；空结果可清空条件。",
          "acceptance": "两个下拉和关键字取交集；重置后恢复全部；保留当前草稿。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R04",
          "originalId": "R04",
          "status": "verified",
          "rule": "编辑→差异预览→保存→重开；未保存草稿离开需确认。",
          "acceptance": "保存显示修订；新服务重读相同值；取消离开保留草稿；保存失败不显示成功。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R05",
          "originalId": "R05",
          "status": "verified",
          "rule": "过期修订进入冲突状态；保留草稿，允许重新读取并比较。",
          "acceptance": "外部修改后保存被拒绝；界面不悄悄覆盖，草稿仍可查看。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R06",
          "originalId": "R06",
          "status": "verified",
          "rule": "历史先预览再恢复；恢复生成新修订。",
          "acceptance": "选历史→查看差异→确认；取消不写入；恢复后其他配置不变。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R13",
          "originalId": "R13",
          "status": "verified",
          "rule": "WPF 与 CLI 共用启动配置服务；存储与产品目录按实际变化点替换。",
          "acceptance": "同输入经两个适配器得到相同规则/修订；替换存储或目录不改用例；不为每个方法新增接口。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R14",
          "originalId": "R14",
          "status": "verified",
          "rule": "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
          "acceptance": "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R15",
          "originalId": "R15",
          "status": "verified",
          "rule": "恢复重新校验当前环境，只恢复目标配置，产生新历史。",
          "acceptance": "失效路径拒绝；其他配置、旧历史与独立凭据不被回写。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R23",
          "originalId": "R23",
          "status": "verified",
          "rule": "启动初始化读 Current Session；产品系列选择 Host；REHASH 由独立策略和环境值控制。",
          "acceptance": "E3D2.1→2.1/net48；3.1→3.1.6/net48；AM/PDMS→net35；none/空关闭，rehash-all 每新会话一次；改值下次进程生效。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "implemented",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "入口创建不等于登录或Host已核验"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：冻结一次启动，保存修订并观察原编号。\n输入：配置修订；明确产品与模块\n输出：候选来源；启动计划与关联回执\n副作用：授权后创建入口进程\n失败与恢复：歧义、冲突与unknown\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "入口创建不等于登录或Host已核验"
      },
      "body": "职责：冻结一次启动，保存修订并观察原编号。\n输入：配置修订；明确产品与模块\n输出：候选来源；启动计划与关联回执\n副作用：授权后创建入口进程\n失败与恢复：歧义、冲突与unknown\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "入口创建不等于登录或Host已核验",
      "evidence": []
    },
    {
      "id": "feature:deployment",
      "entityId": "feature:deployment",
      "component": "FeatureCard",
      "title": "文件与运行目录部署",
      "summary": "冻结文件差异，备份并有条件恢复。",
      "sections": [
        {
          "id": "feature:deployment/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "冻结文件差异，备份并有条件恢复。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:deployment/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "源与专用目标；冻结计划",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:deployment/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "逐文件差异；备份及恢复回执",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:deployment/effects",
          "key": "effects",
          "title": "副作用",
          "text": "写目标文件与托管环境段",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:deployment/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "源或目标变化拒绝；部分恢复明确报告",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R10",
          "originalId": "R10",
          "status": "partial",
          "rule": "脚本流程包含写入计划、备份、加载、执行与结果对比。",
          "acceptance": "先看文件差异与执行目标；加载失败有回执；修正后独立验证，不用脚本合集替代工作流。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R19",
          "originalId": "R19",
          "status": "partial",
          "rule": "复制计划冻结源/目标哈希，执行保留备份；与 PML 加载/调试串联。",
          "acceptance": "复制前变更任一端即拒绝；路径不能越界；部署成功与运行成功分开记录。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "partial",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "复制成功不等于运行时已加载"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：冻结文件差异，备份并有条件恢复。\n输入：源与专用目标；冻结计划\n输出：逐文件差异；备份及恢复回执\n副作用：写目标文件与托管环境段\n失败与恢复：源或目标变化拒绝；部分恢复明确报告\n规范：R10 partial · R19 partial\n证据：未登记独立证据；不能推定通过",
        "status": "实现：partial · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "复制成功不等于运行时已加载"
      },
      "body": "职责：冻结文件差异，备份并有条件恢复。\n输入：源与专用目标；冻结计划\n输出：逐文件差异；备份及恢复回执\n副作用：写目标文件与托管环境段\n失败与恢复：源或目标变化拒绝；部分恢复明确报告\n规范：R10 partial · R19 partial\n证据：未登记独立证据；不能推定通过",
      "status": "实现：partial · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "复制成功不等于运行时已加载",
      "evidence": []
    },
    {
      "id": "feature:sessions",
      "entityId": "feature:sessions",
      "component": "FeatureCard",
      "title": "会话发现与身份核验",
      "summary": "每次调用绑定明确目标身份。",
      "sections": [
        {
          "id": "feature:sessions/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "每次调用绑定明确目标身份。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:sessions/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "选定会话；模块与启动身份",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:sessions/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "身份与协议证据",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:sessions/effects",
          "key": "effects",
          "title": "副作用",
          "text": "创建通信子进程",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:sessions/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "身份变化或协议不足拒绝",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "implemented",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "发现不等于登录"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：每次调用绑定明确目标身份。\n输入：选定会话；模块与启动身份\n输出：身份与协议证据\n副作用：创建通信子进程\n失败与恢复：身份变化或协议不足拒绝\n规范：R17 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "发现不等于登录"
      },
      "body": "职责：每次调用绑定明确目标身份。\n输入：选定会话；模块与启动身份\n输出：身份与协议证据\n副作用：创建通信子进程\n失败与恢复：身份变化或协议不足拒绝\n规范：R17 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "发现不等于登录",
      "evidence": []
    },
    {
      "id": "feature:objects",
      "entityId": "feature:objects",
      "component": "FeatureCard",
      "title": "对象与全局数据查询",
      "summary": "读取当前对象、指定元素及全局路径。",
      "sections": [
        {
          "id": "feature:objects/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "读取当前对象、指定元素及全局路径。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:objects/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "已核验目标；对象路径与预算",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:objects/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "原始数据与读取诊断",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:objects/effects",
          "key": "effects",
          "title": "副作用",
          "text": "部分原生包装临时切换当前对象",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:objects/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "空结果与失败分别核对",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R09",
          "originalId": "R09",
          "status": "partial",
          "rule": "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
          "acceptance": "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "implemented",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "对象身份不能跨工程永久复用"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：读取当前对象、指定元素及全局路径。\n输入：已核验目标；对象路径与预算\n输出：原始数据与读取诊断\n副作用：部分原生包装临时切换当前对象\n失败与恢复：空结果与失败分别核对\n规范：R09 partial · R17 verified · R18 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "对象身份不能跨工程永久复用"
      },
      "body": "职责：读取当前对象、指定元素及全局路径。\n输入：已核验目标；对象路径与预算\n输出：原始数据与读取诊断\n副作用：部分原生包装临时切换当前对象\n失败与恢复：空结果与失败分别核对\n规范：R09 partial · R17 verified · R18 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "对象身份不能跨工程永久复用",
      "evidence": []
    },
    {
      "id": "feature:functions",
      "entityId": "feature:functions",
      "component": "FeatureCard",
      "title": "函数登记、许可与调用",
      "summary": "源码签名、许可和Host加载分别验证。",
      "sections": [
        {
          "id": "feature:functions/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "源码签名、许可和Host加载分别验证。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:functions/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "函数源码摘要；显式许可",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:functions/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "签名列表与调用回执",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:functions/effects",
          "key": "effects",
          "title": "副作用",
          "text": "明确调用可能改变宿主",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:functions/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "源码变化撤销旧许可；不确定结果不重发",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R08",
          "originalId": "R08",
          "status": "partial",
          "rule": "能力从 CLI 契约和已选源码发现，显示签名、来源与可用性。",
          "acceptance": "CLI 契约已可枚举；PML 自动发现仍需实现；缺协议或未授权显示原因，不能冒充不可发现。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R11",
          "originalId": "R11",
          "status": "partial",
          "rule": "执行模式与权限可见；结果不明有单独状态和核验入口。",
          "acceptance": "关闭执行仍可查资料/看计划；未知结果不显示成功，不出现自动重试按钮。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "implemented",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "注册不表示Host已加载"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：源码签名、许可和Host加载分别验证。\n输入：函数源码摘要；显式许可\n输出：签名列表与调用回执\n副作用：明确调用可能改变宿主\n失败与恢复：源码变化撤销旧许可；不确定结果不重发\n规范：R08 partial · R11 partial · R16 verified · R17 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "注册不表示Host已加载"
      },
      "body": "职责：源码签名、许可和Host加载分别验证。\n输入：函数源码摘要；显式许可\n输出：签名列表与调用回执\n副作用：明确调用可能改变宿主\n失败与恢复：源码变化撤销旧许可；不确定结果不重发\n规范：R08 partial · R11 partial · R16 verified · R17 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "注册不表示Host已加载",
      "evidence": []
    },
    {
      "id": "feature:forms",
      "entityId": "feature:forms",
      "component": "FeatureCard",
      "title": "原生Form与Gadget查询",
      "summary": "通过既有元数据读取窗体和控件。",
      "sections": [
        {
          "id": "feature:forms/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "通过既有元数据读取窗体和控件。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:forms/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "明确目标；查询条件与预算",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:forms/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "元数据、原生缺失值与续页",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:forms/effects",
          "key": "effects",
          "title": "副作用",
          "text": "管理请求专用临时变量",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:forms/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "条件变化清除旧续页",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R09",
          "originalId": "R09",
          "status": "partial",
          "rule": "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
          "acceptance": "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "implemented",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "不执行业务回调"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：通过既有元数据读取窗体和控件。\n输入：明确目标；查询条件与预算\n输出：元数据、原生缺失值与续页\n副作用：管理请求专用临时变量\n失败与恢复：条件变化清除旧续页\n规范：R09 partial · R17 verified · R18 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "不执行业务回调"
      },
      "body": "职责：通过既有元数据读取窗体和控件。\n输入：明确目标；查询条件与预算\n输出：元数据、原生缺失值与续页\n副作用：管理请求专用临时变量\n失败与恢复：条件变化清除旧续页\n规范：R09 partial · R17 verified · R18 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "不执行业务回调",
      "evidence": []
    },
    {
      "id": "feature:arrays",
      "entityId": "feature:arrays",
      "component": "FeatureCard",
      "title": "已有数组的有界读取",
      "summary": "按原生索引和字符预算读取窗口。",
      "sections": [
        {
          "id": "feature:arrays/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "按原生索引和字符预算读取窗口。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:arrays/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "起点、项数与字符预算",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:arrays/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "原始行、截断和下一索引",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:arrays/effects",
          "key": "effects",
          "title": "副作用",
          "text": "算法不改写原数组",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:arrays/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "首行超预算明确失败",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "implemented",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "稀疏槽位不压缩"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：按原生索引和字符预算读取窗口。\n输入：起点、项数与字符预算\n输出：原始行、截断和下一索引\n副作用：算法不改写原数组\n失败与恢复：首行超预算明确失败\n规范：R18 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "稀疏槽位不压缩"
      },
      "body": "职责：按原生索引和字符预算读取窗口。\n输入：起点、项数与字符预算\n输出：原始行、截断和下一索引\n副作用：算法不改写原数组\n失败与恢复：首行超预算明确失败\n规范：R18 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "稀疏槽位不压缩",
      "evidence": []
    },
    {
      "id": "feature:formuse",
      "entityId": "feature:formuse",
      "component": "FeatureCard",
      "title": "FormUse跟踪原型",
      "summary": "记录原型支持范围，不冒充完整交付。",
      "sections": [
        {
          "id": "feature:formuse/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "记录原型支持范围，不冒充完整交付。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:formuse/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "原型窗体与跟踪选择",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:formuse/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "原型事件和限制",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:formuse/effects",
          "key": "effects",
          "title": "副作用",
          "text": "可能装饰回调",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:formuse/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "按原型报告具体失败",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R08",
          "originalId": "R08",
          "status": "partial",
          "rule": "能力从 CLI 契约和已选源码发现，显示签名、来源与可用性。",
          "acceptance": "CLI 契约已可枚举；PML 自动发现仍需实现；缺协议或未授权显示原因，不能冒充不可发现。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "planned",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "仍为planned"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：记录原型支持范围，不冒充完整交付。\n输入：原型窗体与跟踪选择\n输出：原型事件和限制\n副作用：可能装饰回调\n失败与恢复：按原型报告具体失败\n规范：R08 partial\n证据：未登记独立证据；不能推定通过",
        "status": "实现：planned · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "仍为planned"
      },
      "body": "职责：记录原型支持范围，不冒充完整交付。\n输入：原型窗体与跟踪选择\n输出：原型事件和限制\n副作用：可能装饰回调\n失败与恢复：按原型报告具体失败\n规范：R08 partial\n证据：未登记独立证据；不能推定通过",
      "status": "实现：planned · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "仍为planned",
      "evidence": []
    },
    {
      "id": "feature:knowledge",
      "entityId": "feature:knowledge",
      "component": "FeatureCard",
      "title": "分层知识与经验",
      "summary": "按产品版本保存来源、库身份和正文。",
      "sections": [
        {
          "id": "feature:knowledge/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "按产品版本保存来源、库身份和正文。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:knowledge/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "明确资料源或已有库",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:knowledge/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "分层检查与来源定位",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:knowledge/effects",
          "key": "effects",
          "title": "副作用",
          "text": "导入发布新隔离库",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:knowledge/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "损坏、冲突或来源变化拒绝启用",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R07",
          "originalId": "R07",
          "status": "planned",
          "rule": "知识入口默认官方 HTML 与源码；个人经验为补充，结果显示来源、产品与版本。",
          "acceptance": "首次使用无需手动登记函数；无官方资料显示定位/导入入口；经验不会覆盖官方来源。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R20",
          "originalId": "R20",
          "status": "planned",
          "rule": "官方 HTML/源码是知识主源；按产品版本建立引用，个人资料独立补充。",
          "acceptance": "保留来源/版本/块 ID；旧索引可辨识；无资料不编造答案；默认官方流程尚待实现。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "partial",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "官方默认接入仍待完成"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：按产品版本保存来源、库身份和正文。\n输入：明确资料源或已有库\n输出：分层检查与来源定位\n副作用：导入发布新隔离库\n失败与恢复：损坏、冲突或来源变化拒绝启用\n规范：R07 planned · R20 planned\n证据：未登记独立证据；不能推定通过",
        "status": "实现：partial · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "官方默认接入仍待完成"
      },
      "body": "职责：按产品版本保存来源、库身份和正文。\n输入：明确资料源或已有库\n输出：分层检查与来源定位\n副作用：导入发布新隔离库\n失败与恢复：损坏、冲突或来源变化拒绝启用\n规范：R07 planned · R20 planned\n证据：未登记独立证据；不能推定通过",
      "status": "实现：partial · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "官方默认接入仍待完成",
      "evidence": []
    },
    {
      "id": "feature:activity",
      "entityId": "feature:activity",
      "component": "FeatureCard",
      "title": "运行记录与日志",
      "summary": "关联本次动作和事实，不确定单列。",
      "sections": [
        {
          "id": "feature:activity/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "关联本次动作和事实，不确定单列。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:activity/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "关联编号与筛选条件",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:activity/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "状态、阶段和保留范围内证据",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:activity/effects",
          "key": "effects",
          "title": "副作用",
          "text": "写本机记录",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:activity/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "日志故障与业务结果分开",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R09",
          "originalId": "R09",
          "status": "partial",
          "rule": "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
          "acceptance": "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R21",
          "originalId": "R21",
          "status": "verified",
          "rule": "日志关联开始、结束、耗时与目标；未结束操作归为未知。",
          "acceptance": "命名秘密脱敏；日志故障不把已执行改成失败并诱发重试；记录结果与业务结果分别保存。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "implemented",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "不是所有平台动作的统一协调者"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：关联本次动作和事实，不确定单列。\n输入：关联编号与筛选条件\n输出：状态、阶段和保留范围内证据\n副作用：写本机记录\n失败与恢复：日志故障与业务结果分开\n规范：R09 partial · R21 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "不是所有平台动作的统一协调者"
      },
      "body": "职责：关联本次动作和事实，不确定单列。\n输入：关联编号与筛选条件\n输出：状态、阶段和保留范围内证据\n副作用：写本机记录\n失败与恢复：日志故障与业务结果分开\n规范：R09 partial · R21 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：implemented · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "不是所有平台动作的统一协调者",
      "evidence": []
    },
    {
      "id": "feature:review",
      "entityId": "feature:review",
      "component": "FeatureCard",
      "title": "AI草稿评审与迁移",
      "summary": "面向人工差异审核的候选桌面能力。",
      "sections": [
        {
          "id": "feature:review/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "面向人工差异审核的候选桌面能力。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:review/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "独立草稿与审核选择",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:review/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "批准快照及迁移回执",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:review/effects",
          "key": "effects",
          "title": "副作用",
          "text": "按明确批准提交",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:review/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "失败保留审核上下文",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R01",
          "originalId": "R01",
          "status": "planned",
          "rule": "常用任务从工作台完成；字段、下一步与失败出口可见。",
          "acceptance": "非实现者按 PRD 完成启动配置、检索、查询、历史恢复；不编辑 JSON，不需要解释控件含义；记录步骤、阻塞和误操作。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R12",
          "originalId": "R12",
          "status": "planned",
          "rule": "业务错误定位字段或动作，并保留可恢复上下文。",
          "acceptance": "键盘完成筛选与确认；焦点可见；空/加载/拒绝/冲突/未知状态都有文案和出口；可用性实测另记。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "planned",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "完整GUI仍planned"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：面向人工差异审核的候选桌面能力。\n输入：独立草稿与审核选择\n输出：批准快照及迁移回执\n副作用：按明确批准提交\n失败与恢复：失败保留审核上下文\n规范：R01 planned · R12 planned\n证据：未登记独立证据；不能推定通过",
        "status": "实现：planned · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "完整GUI仍planned"
      },
      "body": "职责：面向人工差异审核的候选桌面能力。\n输入：独立草稿与审核选择\n输出：批准快照及迁移回执\n副作用：按明确批准提交\n失败与恢复：失败保留审核上下文\n规范：R01 planned · R12 planned\n证据：未登记独立证据；不能推定通过",
      "status": "实现：planned · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "完整GUI仍planned",
      "evidence": []
    },
    {
      "id": "feature:tests",
      "entityId": "feature:tests",
      "component": "FeatureCard",
      "title": "桌面测试执行中心",
      "summary": "复用按ID执行器，桌面中心仍待接线。",
      "sections": [
        {
          "id": "feature:tests/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "复用按ID执行器，桌面中心仍待接线。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:tests/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "真实测试ID",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:tests/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "独立运行记录与证据",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:tests/effects",
          "key": "effects",
          "title": "副作用",
          "text": "启动自有测试夹具并写报告",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:tests/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "跳过与未运行不算通过",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R22",
          "originalId": "R22",
          "status": "verified",
          "rule": "代码、JSON、TOML 用同一业务路径测试，逐步保存输入、输出与结论。",
          "acceptance": "解析失败/超时保留部分结果；历史证据与本轮运行区分；报告按新目录保存。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R24",
          "originalId": "R24",
          "status": "verified",
          "rule": "文档从 Roslyn 生成签名、源码定位、静态调用与实现候选；手写只写业务约束。",
          "acceptance": "缺符号/失效源码哈希/悬空需求引用使构建失败；静态候选不得描述为实机执行；契约元数据可无损展开。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "planned",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "执行器存在不代表桌面中心完成"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：复用按ID执行器，桌面中心仍待接线。\n输入：真实测试ID\n输出：独立运行记录与证据\n副作用：启动自有测试夹具并写报告\n失败与恢复：跳过与未运行不算通过\n规范：R22 verified · R24 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：planned · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "执行器存在不代表桌面中心完成"
      },
      "body": "职责：复用按ID执行器，桌面中心仍待接线。\n输入：真实测试ID\n输出：独立运行记录与证据\n副作用：启动自有测试夹具并写报告\n失败与恢复：跳过与未运行不算通过\n规范：R22 verified · R24 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：planned · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "执行器存在不代表桌面中心完成",
      "evidence": []
    },
    {
      "id": "feature:web",
      "entityId": "feature:web",
      "component": "FeatureCard",
      "title": "本机HTML应用管理",
      "summary": "按钮参数绑定既有IPC操作。",
      "sections": [
        {
          "id": "feature:web/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "按钮参数绑定既有IPC操作。",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:web/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "本机应用定义与参数",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:web/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "目录和原始调用回执",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:web/effects",
          "key": "effects",
          "title": "副作用",
          "text": "目录编辑及获准按钮动作",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        },
        {
          "id": "feature:web/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "保留原操作授权与失败",
          "sourceRefs": [
            "source:presentation",
            "source:capabilities"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R11",
          "originalId": "R11",
          "status": "partial",
          "rule": "执行模式与权限可见；结果不明有单独状态和核验入口。",
          "acceptance": "关闭执行仍可查资料/看计划；未知结果不显示成功，不出现自动重试按钮。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "partial",
        "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
        "sourceRefs": [
          "source:capabilities"
        ]
      },
      "verifications": [],
      "limitations": [
        "远程分发未完成"
      ],
      "sourceRefs": [
        "source:capabilities",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：按钮参数绑定既有IPC操作。\n输入：本机应用定义与参数\n输出：目录和原始调用回执\n副作用：目录编辑及获准按钮动作\n失败与恢复：保留原操作授权与失败\n规范：R11 partial · R16 verified\n证据：未登记独立证据；不能推定通过",
        "status": "实现：partial · 既有目录登记状态；完整平台范围见限制与独立证据",
        "limit": "远程分发未完成"
      },
      "body": "职责：按钮参数绑定既有IPC操作。\n输入：本机应用定义与参数\n输出：目录和原始调用回执\n副作用：目录编辑及获准按钮动作\n失败与恢复：保留原操作授权与失败\n规范：R11 partial · R16 verified\n证据：未登记独立证据；不能推定通过",
      "status": "实现：partial · 既有目录登记状态；完整平台范围见限制与独立证据",
      "limit": "远程分发未完成",
      "evidence": []
    },
    {
      "id": "feature:ribbon",
      "entityId": "feature:ribbon",
      "component": "FeatureCard",
      "title": "Ribbon编辑与冻结部署",
      "summary": "编辑历史、磁盘部署和实际加载分开。",
      "sections": [
        {
          "id": "feature:ribbon/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "编辑历史、磁盘部署和实际加载分开。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:ribbon/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "文档修订；目标模块与冻结引用",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:ribbon/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "差异、备份和分层回执",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:ribbon/effects",
          "key": "effects",
          "title": "副作用",
          "text": "仅写获准UIC或选定清单",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:ribbon/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "源变化拒绝；unknown保留原编号",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        }
      ],
      "requirements": [],
      "implementation": {
        "status": "partial",
        "scope": "明确平台切片；未声称整体完成",
        "sourceRefs": [
          "source:overview"
        ]
      },
      "verifications": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ],
      "limitations": [
        "首次新文件为v1兼容",
        "限定运行时外观通过，未点击宏"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "职责：编辑历史、磁盘部署和实际加载分开。\n输入：文档修订；目标模块与冻结引用\n输出：差异、备份和分层回执\n副作用：仅写获准UIC或选定清单\n失败与恢复：源变化拒绝；unknown保留原编号\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
        "status": "实现：partial · 明确平台切片；未声称整体完成",
        "limit": "首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块"
      },
      "body": "职责：编辑历史、磁盘部署和实际加载分开。\n输入：文档修订；目标模块与冻结引用\n输出：差异、备份和分层回执\n副作用：仅写获准UIC或选定清单\n失败与恢复：源变化拒绝；unknown保留原编号\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "status": "实现：partial · 明确平台切片；未声称整体完成",
      "limit": "首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "evidence": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ]
    },
    {
      "id": "feature:database",
      "entityId": "feature:database",
      "component": "FeatureCard",
      "title": "独立应用数据库",
      "summary": "提供隔离范围的文本修订与CAS事务。",
      "sections": [
        {
          "id": "feature:database/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "提供隔离范围的文本修订与CAS事务。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:database/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "DataScope；身份与预期修订",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:database/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "历史、提交或冲突",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:database/effects",
          "key": "effects",
          "title": "副作用",
          "text": "写显式应用数据库",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:database/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "不兼容格式和只读拒绝写入",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R14",
          "originalId": "R14",
          "status": "verified",
          "rule": "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
          "acceptance": "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": {
        "status": "partial",
        "scope": "明确平台切片；未声称整体完成",
        "sourceRefs": [
          "source:overview"
        ]
      },
      "verifications": [
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        }
      ],
      "limitations": [
        "尚未整体替换活动写者"
      ],
      "sourceRefs": [
        "source:overview",
        "source:canonical"
      ],
      "props": {},
      "text": {
        "body": "职责：提供隔离范围的文本修订与CAS事务。\n输入：DataScope；身份与预期修订\n输出：历史、提交或冲突\n副作用：写显式应用数据库\n失败与恢复：不兼容格式和只读拒绝写入\n规范：R14 verified\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储",
        "status": "实现：partial · 明确平台切片；未声称整体完成",
        "limit": "尚未整体替换活动写者；未整体切换活动存储"
      },
      "body": "职责：提供隔离范围的文本修订与CAS事务。\n输入：DataScope；身份与预期修订\n输出：历史、提交或冲突\n副作用：写显式应用数据库\n失败与恢复：不兼容格式和只读拒绝写入\n规范：R14 verified\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储",
      "status": "实现：partial · 明确平台切片；未声称整体完成",
      "limit": "尚未整体替换活动写者；未整体切换活动存储",
      "evidence": [
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        }
      ]
    },
    {
      "id": "feature:migration",
      "entityId": "feature:migration",
      "component": "FeatureCard",
      "title": "只读迁移预览",
      "summary": "保留字段和历史，剔除秘密并说明不支持项。",
      "sections": [
        {
          "id": "feature:migration/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "保留字段和历史，剔除秘密并说明不支持项。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:migration/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "明确来源格式与路径",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:migration/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "摘要、净化材料和诊断",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:migration/effects",
          "key": "effects",
          "title": "副作用",
          "text": "只读来源，不激活",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:migration/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "未来版本、损坏或预算拒绝",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        }
      ],
      "requirements": [],
      "implementation": {
        "status": "implemented",
        "scope": "只读迁移预览；不包含写入切换",
        "sourceRefs": [
          "source:overview"
        ]
      },
      "verifications": [
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        }
      ],
      "limitations": [
        "不替代写入切换和恢复"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "职责：保留字段和历史，剔除秘密并说明不支持项。\n输入：明确来源格式与路径\n输出：摘要、净化材料和诊断\n副作用：只读来源，不激活\n失败与恢复：未来版本、损坏或预算拒绝\n规范：未单独关联规范条目\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换",
        "status": "实现：implemented · 只读迁移预览；不包含写入切换",
        "limit": "不替代写入切换和恢复；没有执行数据库切换"
      },
      "body": "职责：保留字段和历史，剔除秘密并说明不支持项。\n输入：明确来源格式与路径\n输出：摘要、净化材料和诊断\n副作用：只读来源，不激活\n失败与恢复：未来版本、损坏或预算拒绝\n规范：未单独关联规范条目\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换",
      "status": "实现：implemented · 只读迁移预览；不包含写入切换",
      "limit": "不替代写入切换和恢复；没有执行数据库切换",
      "evidence": [
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        }
      ]
    },
    {
      "id": "feature:caf",
      "entityId": "feature:caf",
      "component": "FeatureCard",
      "title": "CAF薄宿主适配",
      "summary": "验证真实SDK身份，隔离旧运行时。",
      "sections": [
        {
          "id": "feature:caf/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "验证真实SDK身份，隔离旧运行时。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:caf/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "匹配宿主服务上下文",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:caf/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "只读适配描述",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:caf/effects",
          "key": "effects",
          "title": "副作用",
          "text": "当前仅内存生命周期",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:caf/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "未支持版本明确拒绝",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        }
      ],
      "requirements": [],
      "implementation": {
        "status": "partial",
        "scope": "明确平台切片；未声称整体完成",
        "sourceRefs": [
          "source:overview"
        ]
      },
      "verifications": [
        {
          "id": "verification:caf-static",
          "scope": "static",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "CAF"
          },
          "claim": "真实SDK接口编译与生命周期隔离核查。",
          "exclusions": [
            "未真实注册宿主",
            "未界面挂载"
          ],
          "evidenceIds": [
            "evidence:caf"
          ],
          "environmentText": "产品：CAF"
        }
      ],
      "limitations": [
        "SDK编译不等于真实宿主注册或UI挂载"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "职责：验证真实SDK身份，隔离旧运行时。\n输入：匹配宿主服务上下文\n输出：只读适配描述\n副作用：当前仅内存生命周期\n失败与恢复：未支持版本明确拒绝\n规范：未单独关联规范条目\n证据：static / passed · 2026-10-31 · 产品：CAF\n真实SDK接口编译与生命周期隔离核查。\n未涵盖：未真实注册宿主；未界面挂载",
        "status": "实现：partial · 明确平台切片；未声称整体完成",
        "limit": "SDK编译不等于真实宿主注册或UI挂载；未真实注册宿主；未界面挂载"
      },
      "body": "职责：验证真实SDK身份，隔离旧运行时。\n输入：匹配宿主服务上下文\n输出：只读适配描述\n副作用：当前仅内存生命周期\n失败与恢复：未支持版本明确拒绝\n规范：未单独关联规范条目\n证据：static / passed · 2026-10-31 · 产品：CAF\n真实SDK接口编译与生命周期隔离核查。\n未涵盖：未真实注册宿主；未界面挂载",
      "status": "实现：partial · 明确平台切片；未声称整体完成",
      "limit": "SDK编译不等于真实宿主注册或UI挂载；未真实注册宿主；未界面挂载",
      "evidence": [
        {
          "id": "verification:caf-static",
          "scope": "static",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "CAF"
          },
          "claim": "真实SDK接口编译与生命周期隔离核查。",
          "exclusions": [
            "未真实注册宿主",
            "未界面挂载"
          ],
          "evidenceIds": [
            "evidence:caf"
          ],
          "environmentText": "产品：CAF"
        }
      ]
    },
    {
      "id": "feature:plugins",
      "entityId": "feature:plugins",
      "component": "FeatureCard",
      "title": "可选许可与VM插件",
      "summary": "显式插件清单复用既有worker边界。",
      "sections": [
        {
          "id": "feature:plugins/responsibility",
          "key": "responsibility",
          "title": "职责",
          "text": "显式插件清单复用既有worker边界。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:plugins/inputs",
          "key": "inputs",
          "title": "输入",
          "text": "插件身份、计划与明确许可",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:plugins/outputs",
          "key": "outputs",
          "title": "输出",
          "text": "状态和独立证据",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:plugins/effects",
          "key": "effects",
          "title": "副作用",
          "text": "明确执行才改变VM等外部资源",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "feature:plugins/failures",
          "key": "failures",
          "title": "失败与恢复",
          "text": "缺失、停用、崩溃和unknown分别报告",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        }
      ],
      "requirements": [],
      "implementation": {
        "status": "partial",
        "scope": "明确平台切片；未声称整体完成",
        "sourceRefs": [
          "source:overview"
        ]
      },
      "verifications": [],
      "limitations": [
        "基础包不应依赖可选实验能力"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "职责：显式插件清单复用既有worker边界。\n输入：插件身份、计划与明确许可\n输出：状态和独立证据\n副作用：明确执行才改变VM等外部资源\n失败与恢复：缺失、停用、崩溃和unknown分别报告\n规范：未单独关联规范条目\n证据：未登记独立证据；不能推定通过",
        "status": "实现：partial · 明确平台切片；未声称整体完成",
        "limit": "基础包不应依赖可选实验能力"
      },
      "body": "职责：显式插件清单复用既有worker边界。\n输入：插件身份、计划与明确许可\n输出：状态和独立证据\n副作用：明确执行才改变VM等外部资源\n失败与恢复：缺失、停用、崩溃和unknown分别报告\n规范：未单独关联规范条目\n证据：未登记独立证据；不能推定通过",
      "status": "实现：partial · 明确平台切片；未声称整体完成",
      "limit": "基础包不应依赖可选实验能力",
      "evidence": []
    },
    {
      "id": "script:workbench-entry",
      "entityId": "script:workbench-entry",
      "component": "ScriptContractCard",
      "title": "启动工作台与本地服务编排",
      "summary": "NoBrowser/NoBuild、端口、构建配置；整合入口另接 Ribbon 数据目录与端口。",
      "language": "powershell",
      "role": "workflow",
      "featureIds": [
        "feature:launch"
      ],
      "sections": [
        {
          "id": "script:workbench-entry/input",
          "key": "input",
          "title": "输入",
          "text": "NoBrowser/NoBuild、端口、构建配置；整合入口另接 Ribbon 数据目录与端口。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:workbench-entry/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "匹配仓库 SDK；所需 CLI/Ribbon 后端存在或允许本次构建；本机端口可用。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:workbench-entry/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "增量构建 → 启动本机 Ribbon（整合入口）→ 健康核对 → 调用 Manager workbench；不复制发现/启动算法。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:workbench-entry/effect",
          "key": "effect",
          "title": "副作用",
          "text": "可构建、启动本机服务/浏览器、创建应用工作目录；此入口本身不登录或启动 AVEVA。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:workbench-entry/validation",
          "key": "validation",
          "title": "校验",
          "text": "构建退出码、文件存在、health 实现/版本/状态与自有进程仍存活。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:workbench-entry/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "前台退出码、本机端点及下游操作回执；服务ready不是产品已连接。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:workbench-entry/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "失败不继续启动下游；finally 停自有服务并还原本进程环境。停止服务不撤销已经明确受理的产品动作。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R02",
          "originalId": "R02",
          "status": "verified",
          "rule": "产品改变即更新模块；AM 不出现 DESIGN；安装发现与登录成功分别显示。",
          "acceptance": "E3D/AM 切换后模块属于当前产品；无安装只显示未发现；保存无效组合被拒绝。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R03",
          "originalId": "R03",
          "status": "verified",
          "rule": "配置列表同时应用产品、收藏/最近下拉与输入搜索；空结果可清空条件。",
          "acceptance": "两个下拉和关键字取交集；重置后恢复全部；保留当前草稿。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R04",
          "originalId": "R04",
          "status": "verified",
          "rule": "编辑→差异预览→保存→重开；未保存草稿离开需确认。",
          "acceptance": "保存显示修订；新服务重读相同值；取消离开保留草稿；保存失败不显示成功。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R05",
          "originalId": "R05",
          "status": "verified",
          "rule": "过期修订进入冲突状态；保留草稿，允许重新读取并比较。",
          "acceptance": "外部修改后保存被拒绝；界面不悄悄覆盖，草稿仍可查看。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R06",
          "originalId": "R06",
          "status": "verified",
          "rule": "历史先预览再恢复；恢复生成新修订。",
          "acceptance": "选历史→查看差异→确认；取消不写入；恢复后其他配置不变。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R13",
          "originalId": "R13",
          "status": "verified",
          "rule": "WPF 与 CLI 共用启动配置服务；存储与产品目录按实际变化点替换。",
          "acceptance": "同输入经两个适配器得到相同规则/修订；替换存储或目录不改用例；不为每个方法新增接口。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R14",
          "originalId": "R14",
          "status": "verified",
          "rule": "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
          "acceptance": "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R15",
          "originalId": "R15",
          "status": "verified",
          "rule": "恢复重新校验当前环境，只恢复目标配置，产生新历史。",
          "acceptance": "失效路径拒绝；其他配置、旧历史与独立凭据不被回写。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R23",
          "originalId": "R23",
          "status": "verified",
          "rule": "启动初始化读 Current Session；产品系列选择 Host；REHASH 由独立策略和环境值控制。",
          "acceptance": "E3D2.1→2.1/net48；3.1→3.1.6/net48；AM/PDMS→net35；none/空关闭，rehash-all 每新会话一次；改值下次进程生效。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "入口创建不等于登录或Host已核验"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：NoBrowser/NoBuild、端口、构建配置；整合入口另接 Ribbon 数据目录与端口。\n前置条件：匹配仓库 SDK；所需 CLI/Ribbon 后端存在或允许本次构建；本机端口可用。\n算法 / 手段：增量构建 → 启动本机 Ribbon（整合入口）→ 健康核对 → 调用 Manager workbench；不复制发现/启动算法。\n副作用：可构建、启动本机服务/浏览器、创建应用工作目录；此入口本身不登录或启动 AVEVA。\n校验：构建退出码、文件存在、health 实现/版本/状态与自有进程仍存活。\n回执：前台退出码、本机端点及下游操作回执；服务ready不是产品已连接。\n失败 / 恢复：失败不继续启动下游；finally 停自有服务并还原本进程环境。停止服务不撤销已经明确受理的产品动作。\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified\n证据：未登记独立证据；不能推定通过",
        "status": "powershell · workflow",
        "limit": "入口创建不等于登录或Host已核验"
      },
      "body": "输入：NoBrowser/NoBuild、端口、构建配置；整合入口另接 Ribbon 数据目录与端口。\n前置条件：匹配仓库 SDK；所需 CLI/Ribbon 后端存在或允许本次构建；本机端口可用。\n算法 / 手段：增量构建 → 启动本机 Ribbon（整合入口）→ 健康核对 → 调用 Manager workbench；不复制发现/启动算法。\n副作用：可构建、启动本机服务/浏览器、创建应用工作目录；此入口本身不登录或启动 AVEVA。\n校验：构建退出码、文件存在、health 实现/版本/状态与自有进程仍存活。\n回执：前台退出码、本机端点及下游操作回执；服务ready不是产品已连接。\n失败 / 恢复：失败不继续启动下游；finally 停自有服务并还原本进程环境。停止服务不撤销已经明确受理的产品动作。\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified\n证据：未登记独立证据；不能推定通过",
      "status": "powershell · workflow",
      "limit": "入口创建不等于登录或Host已核验",
      "evidence": []
    },
    {
      "id": "script:launch-workflow",
      "entityId": "script:launch-workflow",
      "component": "ScriptContractCard",
      "title": "启动计划、受理与观察",
      "summary": "profileId、expectedRevision、operationId；execute 接冻结计划、本次内存密码、requestId和启动授权。",
      "language": "csharp",
      "role": "workflow",
      "featureIds": [
        "feature:launch"
      ],
      "sections": [
        {
          "id": "script:launch-workflow/input",
          "key": "input",
          "title": "输入",
          "text": "profileId、expectedRevision、operationId；execute 接冻结计划、本次内存密码、requestId和启动授权。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:launch-workflow/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "配置有效、文件/修订/计划期限匹配；有权限；原编号没有冲突事实。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:launch-workflow/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "Plan 冻结入口/INIT/EVARS → 缓存摘要 → 跨进程操作租约 → 同编号去重 → accepted持久化 → 唯一LaunchService.Start → 持久回执。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:launch-workflow/effect",
          "key": "effect",
          "title": "副作用",
          "text": "计划写非秘密缓存；执行创建入口进程；观察读取/更新观察事实，不再次启动。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:launch-workflow/validation",
          "key": "validation",
          "title": "校验",
          "text": "同编号同摘要复用，异摘要冲突；受理持久失败不调用启动；退出入口保留交接候选，不能推定工程关闭。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:launch-workflow/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:launch-workflow/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "中断accepted变unknown；写后回执失败保留不确定性；按原编号Observe，不能自动重放。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R02",
          "originalId": "R02",
          "status": "verified",
          "rule": "产品改变即更新模块；AM 不出现 DESIGN；安装发现与登录成功分别显示。",
          "acceptance": "E3D/AM 切换后模块属于当前产品；无安装只显示未发现；保存无效组合被拒绝。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R03",
          "originalId": "R03",
          "status": "verified",
          "rule": "配置列表同时应用产品、收藏/最近下拉与输入搜索；空结果可清空条件。",
          "acceptance": "两个下拉和关键字取交集；重置后恢复全部；保留当前草稿。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R04",
          "originalId": "R04",
          "status": "verified",
          "rule": "编辑→差异预览→保存→重开；未保存草稿离开需确认。",
          "acceptance": "保存显示修订；新服务重读相同值；取消离开保留草稿；保存失败不显示成功。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R05",
          "originalId": "R05",
          "status": "verified",
          "rule": "过期修订进入冲突状态；保留草稿，允许重新读取并比较。",
          "acceptance": "外部修改后保存被拒绝；界面不悄悄覆盖，草稿仍可查看。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R06",
          "originalId": "R06",
          "status": "verified",
          "rule": "历史先预览再恢复；恢复生成新修订。",
          "acceptance": "选历史→查看差异→确认；取消不写入；恢复后其他配置不变。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R13",
          "originalId": "R13",
          "status": "verified",
          "rule": "WPF 与 CLI 共用启动配置服务；存储与产品目录按实际变化点替换。",
          "acceptance": "同输入经两个适配器得到相同规则/修订；替换存储或目录不改用例；不为每个方法新增接口。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R14",
          "originalId": "R14",
          "status": "verified",
          "rule": "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
          "acceptance": "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R15",
          "originalId": "R15",
          "status": "verified",
          "rule": "恢复重新校验当前环境，只恢复目标配置，产生新历史。",
          "acceptance": "失效路径拒绝；其他配置、旧历史与独立凭据不被回写。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R23",
          "originalId": "R23",
          "status": "verified",
          "rule": "启动初始化读 Current Session；产品系列选择 Host；REHASH 由独立策略和环境值控制。",
          "acceptance": "E3D2.1→2.1/net48；3.1→3.1.6/net48；AM/PDMS→net35；none/空关闭，rehash-all 每新会话一次；改值下次进程生效。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "入口创建不等于登录或Host已核验"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：profileId、expectedRevision、operationId；execute 接冻结计划、本次内存密码、requestId和启动授权。\n前置条件：配置有效、文件/修订/计划期限匹配；有权限；原编号没有冲突事实。\n算法 / 手段：Plan 冻结入口/INIT/EVARS → 缓存摘要 → 跨进程操作租约 → 同编号去重 → accepted持久化 → 唯一LaunchService.Start → 持久回执。\n副作用：计划写非秘密缓存；执行创建入口进程；观察读取/更新观察事实，不再次启动。\n校验：同编号同摘要复用，异摘要冲突；受理持久失败不调用启动；退出入口保留交接候选，不能推定工程关闭。\n回执：operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。\n失败 / 恢复：中断accepted变unknown；写后回执失败保留不确定性；按原编号Observe，不能自动重放。\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified\n证据：未登记独立证据；不能推定通过",
        "status": "csharp · workflow",
        "limit": "入口创建不等于登录或Host已核验"
      },
      "body": "输入：profileId、expectedRevision、operationId；execute 接冻结计划、本次内存密码、requestId和启动授权。\n前置条件：配置有效、文件/修订/计划期限匹配；有权限；原编号没有冲突事实。\n算法 / 手段：Plan 冻结入口/INIT/EVARS → 缓存摘要 → 跨进程操作租约 → 同编号去重 → accepted持久化 → 唯一LaunchService.Start → 持久回执。\n副作用：计划写非秘密缓存；执行创建入口进程；观察读取/更新观察事实，不再次启动。\n校验：同编号同摘要复用，异摘要冲突；受理持久失败不调用启动；退出入口保留交接候选，不能推定工程关闭。\n回执：operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。\n失败 / 恢复：中断accepted变unknown；写后回执失败保留不确定性；按原编号Observe，不能自动重放。\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified\n证据：未登记独立证据；不能推定通过",
      "status": "csharp · workflow",
      "limit": "入口创建不等于登录或Host已核验",
      "evidence": []
    },
    {
      "id": "script:communication-client",
      "entityId": "script:communication-client",
      "component": "ScriptContractCard",
      "title": "通信CLI子进程边界",
      "summary": "已注册通信操作、JSON stdin、requestId；目标操作含PID、启动时间、模块及明确授权。",
      "language": "csharp",
      "role": "transport",
      "featureIds": [
        "feature:sessions",
        "feature:objects",
        "feature:forms",
        "feature:functions"
      ],
      "sections": [
        {
          "id": "script:communication-client/input",
          "key": "input",
          "title": "输入",
          "text": "已注册通信操作、JSON stdin、requestId；目标操作含PID、启动时间、模块及明确授权。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:communication-client/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "明确通信程序路径/身份、兼容协议；管理操作不能借此绕回通信边界。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:communication-client/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "ArgumentList → 单份stdin JSON → 并行读取stdout/stderr → 时间/体积预算 → 核对关联回执。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:communication-client/effect",
          "key": "effect",
          "title": "副作用",
          "text": "创建本次自有通信进程；下游操作可查询或改变宿主，必须按operation判断。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:communication-client/validation",
          "key": "validation",
          "title": "校验",
          "text": "程序指纹、版本、JSON、requestId、退出码与输出预算；PID身份校验不因GUI已选过而省略。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:communication-client/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "原通信结果、分层evidence及mayHaveExecuted；stderr不是可替代业务结果的stdout。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:communication-client/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "终止/回收自有子进程不等于取消宿主动作；发出后无确定回执为unknown，不重发。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R09",
          "originalId": "R09",
          "status": "partial",
          "rule": "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
          "acceptance": "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R08",
          "originalId": "R08",
          "status": "partial",
          "rule": "能力从 CLI 契约和已选源码发现，显示签名、来源与可用性。",
          "acceptance": "CLI 契约已可枚举；PML 自动发现仍需实现；缺协议或未授权显示原因，不能冒充不可发现。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R11",
          "originalId": "R11",
          "status": "partial",
          "rule": "执行模式与权限可见；结果不明有单独状态和核验入口。",
          "acceptance": "关闭执行仍可查资料/看计划；未知结果不显示成功，不出现自动重试按钮。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "发现不等于登录",
        "对象身份不能跨工程永久复用",
        "注册不表示Host已加载",
        "不执行业务回调"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：已注册通信操作、JSON stdin、requestId；目标操作含PID、启动时间、模块及明确授权。\n前置条件：明确通信程序路径/身份、兼容协议；管理操作不能借此绕回通信边界。\n算法 / 手段：ArgumentList → 单份stdin JSON → 并行读取stdout/stderr → 时间/体积预算 → 核对关联回执。\n副作用：创建本次自有通信进程；下游操作可查询或改变宿主，必须按operation判断。\n校验：程序指纹、版本、JSON、requestId、退出码与输出预算；PID身份校验不因GUI已选过而省略。\n回执：原通信结果、分层evidence及mayHaveExecuted；stderr不是可替代业务结果的stdout。\n失败 / 恢复：终止/回收自有子进程不等于取消宿主动作；发出后无确定回执为unknown，不重发。\n规范：R17 verified · R09 partial · R18 verified · R08 partial · R11 partial · R16 verified\n证据：未登记独立证据；不能推定通过",
        "status": "csharp · transport",
        "limit": "发现不等于登录；对象身份不能跨工程永久复用；注册不表示Host已加载；不执行业务回调"
      },
      "body": "输入：已注册通信操作、JSON stdin、requestId；目标操作含PID、启动时间、模块及明确授权。\n前置条件：明确通信程序路径/身份、兼容协议；管理操作不能借此绕回通信边界。\n算法 / 手段：ArgumentList → 单份stdin JSON → 并行读取stdout/stderr → 时间/体积预算 → 核对关联回执。\n副作用：创建本次自有通信进程；下游操作可查询或改变宿主，必须按operation判断。\n校验：程序指纹、版本、JSON、requestId、退出码与输出预算；PID身份校验不因GUI已选过而省略。\n回执：原通信结果、分层evidence及mayHaveExecuted；stderr不是可替代业务结果的stdout。\n失败 / 恢复：终止/回收自有子进程不等于取消宿主动作；发出后无确定回执为unknown，不重发。\n规范：R17 verified · R09 partial · R18 verified · R08 partial · R11 partial · R16 verified\n证据：未登记独立证据；不能推定通过",
      "status": "csharp · transport",
      "limit": "发现不等于登录；对象身份不能跨工程永久复用；注册不表示Host已加载；不执行业务回调",
      "evidence": []
    },
    {
      "id": "script:frozen-copy",
      "entityId": "script:frozen-copy",
      "component": "ScriptContractCard",
      "title": "冻结复制与条件恢复",
      "summary": "源、插件专用注册目标、冻结文件计划和摘要；恢复接原回执。",
      "language": "csharp",
      "role": "algorithm",
      "featureIds": [
        "feature:deployment"
      ],
      "sections": [
        {
          "id": "script:frozen-copy/input",
          "key": "input",
          "title": "输入",
          "text": "源、插件专用注册目标、冻结文件计划和摘要；恢复接原回执。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:frozen-copy/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "路径边界、无链接、不互含、格式及大小预算满足；授权明确。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:frozen-copy/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "枚举冻结 → 全部写前复验 → 备份/暂存/写入 → 结果回读；不另建第二复制引擎。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:frozen-copy/effect",
          "key": "effect",
          "title": "副作用",
          "text": "写目标和本操作备份；不自动删除无关文件、不加载Host。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:frozen-copy/validation",
          "key": "validation",
          "title": "校验",
          "text": "源/目标变化拒绝；检查实际复制与恢复未解决项。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:frozen-copy/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "DeploymentReceipt、应用异常中的部分结果、DeploymentRestoreReceipt。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:frozen-copy/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "仅恢复本次所有权内文件；外部改动不覆盖，RecoveryErrors/Unresolved单列，不承诺无条件回滚。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R10",
          "originalId": "R10",
          "status": "partial",
          "rule": "脚本流程包含写入计划、备份、加载、执行与结果对比。",
          "acceptance": "先看文件差异与执行目标；加载失败有回执；修正后独立验证，不用脚本合集替代工作流。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R19",
          "originalId": "R19",
          "status": "partial",
          "rule": "复制计划冻结源/目标哈希，执行保留备份；与 PML 加载/调试串联。",
          "acceptance": "复制前变更任一端即拒绝；路径不能越界；部署成功与运行成功分开记录。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "复制成功不等于运行时已加载"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：源、插件专用注册目标、冻结文件计划和摘要；恢复接原回执。\n前置条件：路径边界、无链接、不互含、格式及大小预算满足；授权明确。\n算法 / 手段：枚举冻结 → 全部写前复验 → 备份/暂存/写入 → 结果回读；不另建第二复制引擎。\n副作用：写目标和本操作备份；不自动删除无关文件、不加载Host。\n校验：源/目标变化拒绝；检查实际复制与恢复未解决项。\n回执：DeploymentReceipt、应用异常中的部分结果、DeploymentRestoreReceipt。\n失败 / 恢复：仅恢复本次所有权内文件；外部改动不覆盖，RecoveryErrors/Unresolved单列，不承诺无条件回滚。\n规范：R10 partial · R19 partial\n证据：未登记独立证据；不能推定通过",
        "status": "csharp · algorithm",
        "limit": "复制成功不等于运行时已加载"
      },
      "body": "输入：源、插件专用注册目标、冻结文件计划和摘要；恢复接原回执。\n前置条件：路径边界、无链接、不互含、格式及大小预算满足；授权明确。\n算法 / 手段：枚举冻结 → 全部写前复验 → 备份/暂存/写入 → 结果回读；不另建第二复制引擎。\n副作用：写目标和本操作备份；不自动删除无关文件、不加载Host。\n校验：源/目标变化拒绝；检查实际复制与恢复未解决项。\n回执：DeploymentReceipt、应用异常中的部分结果、DeploymentRestoreReceipt。\n失败 / 恢复：仅恢复本次所有权内文件；外部改动不覆盖，RecoveryErrors/Unresolved单列，不承诺无条件回滚。\n规范：R10 partial · R19 partial\n证据：未登记独立证据；不能推定通过",
      "status": "csharp · algorithm",
      "limit": "复制成功不等于运行时已加载",
      "evidence": []
    },
    {
      "id": "script:pml-development",
      "entityId": "script:pml-development",
      "component": "ScriptContractCard",
      "title": "PML部署、重载与验证",
      "summary": "CLI、源目录、注册PMLLIB、schema1清单、PID/ExpectedModel、独立证据目录与AllowExecution。",
      "language": "powershell",
      "role": "workflow",
      "featureIds": [
        "feature:deployment",
        "feature:forms",
        "feature:functions"
      ],
      "sections": [
        {
          "id": "script:pml-development/input",
          "key": "input",
          "title": "输入",
          "text": "CLI、源目录、注册PMLLIB、schema1清单、PID/ExpectedModel、独立证据目录与AllowExecution。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:pml-development/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "明确获准的部署/加载/验证任务；清单字段和期望字符串有效；不复用任意旧证据目录充当新结果。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:pml-development/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "固定发现身份 → 冻结复制 → REHASH ALL → Object/Form重载 → 验证宏 → 读取结果全局数组 → 精确比对expected。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:pml-development/effect",
          "key": "effect",
          "title": "副作用",
          "text": "写部署文件与run.json/rpc.json；重载、宏和窗口可能改变宿主运行态。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:pml-development/validation",
          "key": "validation",
          "title": "校验",
          "text": "复制后对冻结源哈希回读；结果项数、UnparsedCount与每个键/字符串精确一致。该包装器不具有启动协调器的operationId持久去重；清单不是每步重新哈希冻结，不能补造此保证。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:pml-development/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "每步执行前保存状态、原始调用与响应；失败或unknown停止，最后写rpc证据。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:pml-development/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "不自动重发/重跑全流程；先检查已复制文件、原始调用、加载状态及原备份，再明确选择下一动作。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R10",
          "originalId": "R10",
          "status": "partial",
          "rule": "脚本流程包含写入计划、备份、加载、执行与结果对比。",
          "acceptance": "先看文件差异与执行目标；加载失败有回执；修正后独立验证，不用脚本合集替代工作流。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R19",
          "originalId": "R19",
          "status": "partial",
          "rule": "复制计划冻结源/目标哈希，执行保留备份；与 PML 加载/调试串联。",
          "acceptance": "复制前变更任一端即拒绝；路径不能越界；部署成功与运行成功分开记录。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R08",
          "originalId": "R08",
          "status": "partial",
          "rule": "能力从 CLI 契约和已选源码发现，显示签名、来源与可用性。",
          "acceptance": "CLI 契约已可枚举；PML 自动发现仍需实现；缺协议或未授权显示原因，不能冒充不可发现。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R11",
          "originalId": "R11",
          "status": "partial",
          "rule": "执行模式与权限可见；结果不明有单独状态和核验入口。",
          "acceptance": "关闭执行仍可查资料/看计划；未知结果不显示成功，不出现自动重试按钮。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R09",
          "originalId": "R09",
          "status": "partial",
          "rule": "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
          "acceptance": "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "复制成功不等于运行时已加载",
        "注册不表示Host已加载",
        "不执行业务回调"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：CLI、源目录、注册PMLLIB、schema1清单、PID/ExpectedModel、独立证据目录与AllowExecution。\n前置条件：明确获准的部署/加载/验证任务；清单字段和期望字符串有效；不复用任意旧证据目录充当新结果。\n算法 / 手段：固定发现身份 → 冻结复制 → REHASH ALL → Object/Form重载 → 验证宏 → 读取结果全局数组 → 精确比对expected。\n副作用：写部署文件与run.json/rpc.json；重载、宏和窗口可能改变宿主运行态。\n校验：复制后对冻结源哈希回读；结果项数、UnparsedCount与每个键/字符串精确一致。该包装器不具有启动协调器的operationId持久去重；清单不是每步重新哈希冻结，不能补造此保证。\n回执：每步执行前保存状态、原始调用与响应；失败或unknown停止，最后写rpc证据。\n失败 / 恢复：不自动重发/重跑全流程；先检查已复制文件、原始调用、加载状态及原备份，再明确选择下一动作。\n规范：R10 partial · R19 partial · R08 partial · R11 partial · R16 verified · R17 verified · R09 partial · R18 verified\n证据：未登记独立证据；不能推定通过",
        "status": "powershell · workflow",
        "limit": "复制成功不等于运行时已加载；注册不表示Host已加载；不执行业务回调"
      },
      "body": "输入：CLI、源目录、注册PMLLIB、schema1清单、PID/ExpectedModel、独立证据目录与AllowExecution。\n前置条件：明确获准的部署/加载/验证任务；清单字段和期望字符串有效；不复用任意旧证据目录充当新结果。\n算法 / 手段：固定发现身份 → 冻结复制 → REHASH ALL → Object/Form重载 → 验证宏 → 读取结果全局数组 → 精确比对expected。\n副作用：写部署文件与run.json/rpc.json；重载、宏和窗口可能改变宿主运行态。\n校验：复制后对冻结源哈希回读；结果项数、UnparsedCount与每个键/字符串精确一致。该包装器不具有启动协调器的operationId持久去重；清单不是每步重新哈希冻结，不能补造此保证。\n回执：每步执行前保存状态、原始调用与响应；失败或unknown停止，最后写rpc证据。\n失败 / 恢复：不自动重发/重跑全流程；先检查已复制文件、原始调用、加载状态及原备份，再明确选择下一动作。\n规范：R10 partial · R19 partial · R08 partial · R11 partial · R16 verified · R17 verified · R09 partial · R18 verified\n证据：未登记独立证据；不能推定通过",
      "status": "powershell · workflow",
      "limit": "复制成功不等于运行时已加载；注册不表示Host已加载；不执行业务回调",
      "evidence": []
    },
    {
      "id": "script:array-window",
      "entityId": "script:array-window",
      "component": "ScriptContractCard",
      "title": "数组窗口算法",
      "summary": "原生MAXINDEX上界、startIndex、maxItems、maxCharacters与槽位读取委托。",
      "language": "csharp",
      "role": "algorithm",
      "featureIds": [
        "feature:arrays"
      ],
      "sections": [
        {
          "id": "script:array-window/input",
          "key": "input",
          "title": "输入",
          "text": "原生MAXINDEX上界、startIndex、maxItems、maxCharacters与槽位读取委托。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:array-window/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "startIndex≥1，maxItems为1–1000，maxCharacters为1–262144，上界非负。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:array-window/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "按原生索引顺序读取，不按已设置元素数压缩；字符预算不拆行。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:array-window/effect",
          "key": "effect",
          "title": "副作用",
          "text": "算法本身不创建或改写数组；委托仍受宿主协议约束。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:array-window/validation",
          "key": "validation",
          "title": "校验",
          "text": "首行超预算明确失败；其余超预算保留下一索引；空、0、false和unset不相互转换。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:array-window/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "Rows、StartIndex、NextIndex、Truncated、Characters及IndexUpperBound。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:array-window/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "同目标/条件才能接续；换目标/筛选重置页，不拼旧结果。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "稀疏槽位不压缩"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：原生MAXINDEX上界、startIndex、maxItems、maxCharacters与槽位读取委托。\n前置条件：startIndex≥1，maxItems为1–1000，maxCharacters为1–262144，上界非负。\n算法 / 手段：按原生索引顺序读取，不按已设置元素数压缩；字符预算不拆行。\n副作用：算法本身不创建或改写数组；委托仍受宿主协议约束。\n校验：首行超预算明确失败；其余超预算保留下一索引；空、0、false和unset不相互转换。\n回执：Rows、StartIndex、NextIndex、Truncated、Characters及IndexUpperBound。\n失败 / 恢复：同目标/条件才能接续；换目标/筛选重置页，不拼旧结果。\n规范：R18 verified\n证据：未登记独立证据；不能推定通过",
        "status": "csharp · algorithm",
        "limit": "稀疏槽位不压缩"
      },
      "body": "输入：原生MAXINDEX上界、startIndex、maxItems、maxCharacters与槽位读取委托。\n前置条件：startIndex≥1，maxItems为1–1000，maxCharacters为1–262144，上界非负。\n算法 / 手段：按原生索引顺序读取，不按已设置元素数压缩；字符预算不拆行。\n副作用：算法本身不创建或改写数组；委托仍受宿主协议约束。\n校验：首行超预算明确失败；其余超预算保留下一索引；空、0、false和unset不相互转换。\n回执：Rows、StartIndex、NextIndex、Truncated、Characters及IndexUpperBound。\n失败 / 恢复：同目标/条件才能接续；换目标/筛选重置页，不拼旧结果。\n规范：R18 verified\n证据：未登记独立证据；不能推定通过",
      "status": "csharp · algorithm",
      "limit": "稀疏槽位不压缩",
      "evidence": []
    },
    {
      "id": "script:dbref-reader",
      "entityId": "script:dbref-reader",
      "component": "ScriptContractCard",
      "title": "DBREF对象读取与恢复",
      "summary": "size/depth/startStr/name；name支持短名、绝对名或DBREF字符串。",
      "language": "pml",
      "role": "runtime",
      "featureIds": [
        "feature:objects"
      ],
      "sections": [
        {
          "id": "script:dbref-reader/input",
          "key": "input",
          "title": "输入",
          "text": "size/depth/startStr/name；name支持短名、绝对名或DBREF字符串。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:dbref-reader/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "目标PML已加载、当前会话/工程明确；参数与预算还需调用层约束。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:dbref-reader/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "保存CE → 解析目标 → 临时切CE → walker读取扁平数组 → 尝试还原CE。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:dbref-reader/effect",
          "key": "effect",
          "title": "副作用",
          "text": "会临时改变CE；不应表述成纯无状态函数，也不自动获得写工程权限。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:dbref-reader/validation",
          "key": "validation",
          "title": "校验",
          "text": "原PML通过badref/handle处理部分失败；独立通信身份与预算不能省略。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:dbref-reader/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "返回ARRAY；若目标无效或部分错误也可能是空数组，因此必须结合外层成功/错误证据。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:dbref-reader/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "原实现包含正常及读取异常后的CE恢复尝试，但不是通用finally保证；若原生恢复失败须人工核对CE，不能从空结果推断已恢复。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R09",
          "originalId": "R09",
          "status": "partial",
          "rule": "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
          "acceptance": "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R18",
          "originalId": "R18",
          "status": "verified",
          "rule": "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
          "acceptance": "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "对象身份不能跨工程永久复用"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：size/depth/startStr/name；name支持短名、绝对名或DBREF字符串。\n前置条件：目标PML已加载、当前会话/工程明确；参数与预算还需调用层约束。\n算法 / 手段：保存CE → 解析目标 → 临时切CE → walker读取扁平数组 → 尝试还原CE。\n副作用：会临时改变CE；不应表述成纯无状态函数，也不自动获得写工程权限。\n校验：原PML通过badref/handle处理部分失败；独立通信身份与预算不能省略。\n回执：返回ARRAY；若目标无效或部分错误也可能是空数组，因此必须结合外层成功/错误证据。\n失败 / 恢复：原实现包含正常及读取异常后的CE恢复尝试，但不是通用finally保证；若原生恢复失败须人工核对CE，不能从空结果推断已恢复。\n规范：R09 partial · R17 verified · R18 verified\n证据：未登记独立证据；不能推定通过",
        "status": "pml · runtime",
        "limit": "对象身份不能跨工程永久复用"
      },
      "body": "输入：size/depth/startStr/name；name支持短名、绝对名或DBREF字符串。\n前置条件：目标PML已加载、当前会话/工程明确；参数与预算还需调用层约束。\n算法 / 手段：保存CE → 解析目标 → 临时切CE → walker读取扁平数组 → 尝试还原CE。\n副作用：会临时改变CE；不应表述成纯无状态函数，也不自动获得写工程权限。\n校验：原PML通过badref/handle处理部分失败；独立通信身份与预算不能省略。\n回执：返回ARRAY；若目标无效或部分错误也可能是空数组，因此必须结合外层成功/错误证据。\n失败 / 恢复：原实现包含正常及读取异常后的CE恢复尝试，但不是通用finally保证；若原生恢复失败须人工核对CE，不能从空结果推断已恢复。\n规范：R09 partial · R17 verified · R18 verified\n证据：未登记独立证据；不能推定通过",
      "status": "pml · runtime",
      "limit": "对象身份不能跨工程永久复用",
      "evidence": []
    },
    {
      "id": "script:host-bootstrap",
      "entityId": "script:host-bootstrap",
      "component": "ScriptContractCard",
      "title": "Host解析与启动策略",
      "summary": "Current Session/BANNER、PMLUI根、进程环境中的启动策略及可选override。",
      "language": "pml",
      "role": "runtime",
      "featureIds": [
        "feature:launch",
        "feature:sessions"
      ],
      "sections": [
        {
          "id": "script:host-bootstrap/input",
          "key": "input",
          "title": "输入",
          "text": "Current Session/BANNER、PMLUI根、进程环境中的启动策略及可选override。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:host-bootstrap/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "对应profile的Host文件实际存在；安装目录token与安装预览一致。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:host-bootstrap/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "E3D2.1→net48/2.1、E3D3.1系列→net48/3.1.6、AM/PDMS→net35；策略仅none/空或rehash-all，未知值不当作PML执行。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:host-bootstrap/effect",
          "key": "effect",
          "title": "副作用",
          "text": "更新会话描述与解析结果；显式rehash-all可执行REHASH，默认关闭。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:host-bootstrap/validation",
          "key": "validation",
          "title": "校验",
          "text": "不支持映射或缺DLL不得设为成功；策略执行前写开始日志，一旦开始即消耗会话防重试守卫。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:host-bootstrap/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "产品/版本/模块/profile/framework信息及策略报告；路径存在不是Host握手成功。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:host-bootstrap/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "不自动降级加载错误运行时；改策略环境值通常由新进程读取，失败后不能借重试重复执行当前会话动作。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R02",
          "originalId": "R02",
          "status": "verified",
          "rule": "产品改变即更新模块；AM 不出现 DESIGN；安装发现与登录成功分别显示。",
          "acceptance": "E3D/AM 切换后模块属于当前产品；无安装只显示未发现；保存无效组合被拒绝。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R03",
          "originalId": "R03",
          "status": "verified",
          "rule": "配置列表同时应用产品、收藏/最近下拉与输入搜索；空结果可清空条件。",
          "acceptance": "两个下拉和关键字取交集；重置后恢复全部；保留当前草稿。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R04",
          "originalId": "R04",
          "status": "verified",
          "rule": "编辑→差异预览→保存→重开；未保存草稿离开需确认。",
          "acceptance": "保存显示修订；新服务重读相同值；取消离开保留草稿；保存失败不显示成功。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R05",
          "originalId": "R05",
          "status": "verified",
          "rule": "过期修订进入冲突状态；保留草稿，允许重新读取并比较。",
          "acceptance": "外部修改后保存被拒绝；界面不悄悄覆盖，草稿仍可查看。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R06",
          "originalId": "R06",
          "status": "verified",
          "rule": "历史先预览再恢复；恢复生成新修订。",
          "acceptance": "选历史→查看差异→确认；取消不写入；恢复后其他配置不变。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R13",
          "originalId": "R13",
          "status": "verified",
          "rule": "WPF 与 CLI 共用启动配置服务；存储与产品目录按实际变化点替换。",
          "acceptance": "同输入经两个适配器得到相同规则/修订；替换存储或目录不改用例；不为每个方法新增接口。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R14",
          "originalId": "R14",
          "status": "verified",
          "rule": "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
          "acceptance": "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R15",
          "originalId": "R15",
          "status": "verified",
          "rule": "恢复重新校验当前环境，只恢复目标配置，产生新历史。",
          "acceptance": "失效路径拒绝；其他配置、旧历史与独立凭据不被回写。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R16",
          "originalId": "R16",
          "status": "verified",
          "rule": "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
          "acceptance": "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R23",
          "originalId": "R23",
          "status": "verified",
          "rule": "启动初始化读 Current Session；产品系列选择 Host；REHASH 由独立策略和环境值控制。",
          "acceptance": "E3D2.1→2.1/net48；3.1→3.1.6/net48；AM/PDMS→net35；none/空关闭，rehash-all 每新会话一次；改值下次进程生效。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "入口创建不等于登录或Host已核验",
        "发现不等于登录"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：Current Session/BANNER、PMLUI根、进程环境中的启动策略及可选override。\n前置条件：对应profile的Host文件实际存在；安装目录token与安装预览一致。\n算法 / 手段：E3D2.1→net48/2.1、E3D3.1系列→net48/3.1.6、AM/PDMS→net35；策略仅none/空或rehash-all，未知值不当作PML执行。\n副作用：更新会话描述与解析结果；显式rehash-all可执行REHASH，默认关闭。\n校验：不支持映射或缺DLL不得设为成功；策略执行前写开始日志，一旦开始即消耗会话防重试守卫。\n回执：产品/版本/模块/profile/framework信息及策略报告；路径存在不是Host握手成功。\n失败 / 恢复：不自动降级加载错误运行时；改策略环境值通常由新进程读取，失败后不能借重试重复执行当前会话动作。\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified · R17 verified\n证据：未登记独立证据；不能推定通过",
        "status": "pml · runtime",
        "limit": "入口创建不等于登录或Host已核验；发现不等于登录"
      },
      "body": "输入：Current Session/BANNER、PMLUI根、进程环境中的启动策略及可选override。\n前置条件：对应profile的Host文件实际存在；安装目录token与安装预览一致。\n算法 / 手段：E3D2.1→net48/2.1、E3D3.1系列→net48/3.1.6、AM/PDMS→net35；策略仅none/空或rehash-all，未知值不当作PML执行。\n副作用：更新会话描述与解析结果；显式rehash-all可执行REHASH，默认关闭。\n校验：不支持映射或缺DLL不得设为成功；策略执行前写开始日志，一旦开始即消耗会话防重试守卫。\n回执：产品/版本/模块/profile/framework信息及策略报告；路径存在不是Host握手成功。\n失败 / 恢复：不自动降级加载错误运行时；改策略环境值通常由新进程读取，失败后不能借重试重复执行当前会话动作。\n规范：R02 verified · R03 verified · R04 verified · R05 verified · R06 verified · R13 verified · R14 verified · R15 verified · R16 verified · R23 verified · R17 verified\n证据：未登记独立证据；不能推定通过",
      "status": "pml · runtime",
      "limit": "入口创建不等于登录或Host已核验；发现不等于登录",
      "evidence": []
    },
    {
      "id": "script:ribbon-frozen-deployment",
      "entityId": "script:ribbon-frozen-deployment",
      "component": "ScriptContractCard",
      "title": "Ribbon冻结部署与回执映射",
      "summary": "preview是project及选定模块信息；apply严格为planId/planDigest，写授权独立。",
      "language": "csharp",
      "role": "workflow",
      "featureIds": [
        "feature:ribbon"
      ],
      "sections": [
        {
          "id": "script:ribbon-frozen-deployment/input",
          "key": "input",
          "title": "输入",
          "text": "preview是project及选定模块信息；apply严格为planId/planDigest，写授权独立。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ribbon-frozen-deployment/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "已有文档身份、权威来源/托管保护通过、计划期限/历史/目标一致；不能用伪造preferredBinding接管原生文件。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ribbon-frozen-deployment/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "规范化与验证 → 缓存项目/目标/摘要 → apply写锁复验 → 消费计划 → 复用原文件/登记引擎 → 写后哈希回读。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ribbon-frozen-deployment/effect",
          "key": "effect",
          "title": "副作用",
          "text": "file-update仅写UIC；registration-add仅写选定XML及相关工作区记录/绑定。未选XML及登记时UIC保持字节；不执行宏或重启。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ribbon-frozen-deployment/validation",
          "key": "validation",
          "title": "校验",
          "text": "缓存最多64项、五分钟、单规范化输入4MiB、总缓存32MiB；HTTP及Manager另有20MiB输入/输出限制。Manager核对v2、请求编号/摘要、state及activation类型，旧v1错误保留。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ribbon-frozen-deployment/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "workspace/deployment/activation分列；deployed仅磁盘成功，runtimeVerified始终false；不确认写入为RIBBON_RESULT_UNKNOWN。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ribbon-frozen-deployment/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "存活缓存同计划返原回执；过期/后端重启没有持久执行事实，人工核对工作区/目标/备份，不自动重新执行；真实加载另验收。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ],
      "limitations": [
        "首次新文件为v1兼容",
        "限定运行时外观通过，未点击宏"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：preview是project及选定模块信息；apply严格为planId/planDigest，写授权独立。\n前置条件：已有文档身份、权威来源/托管保护通过、计划期限/历史/目标一致；不能用伪造preferredBinding接管原生文件。\n算法 / 手段：规范化与验证 → 缓存项目/目标/摘要 → apply写锁复验 → 消费计划 → 复用原文件/登记引擎 → 写后哈希回读。\n副作用：file-update仅写UIC；registration-add仅写选定XML及相关工作区记录/绑定。未选XML及登记时UIC保持字节；不执行宏或重启。\n校验：缓存最多64项、五分钟、单规范化输入4MiB、总缓存32MiB；HTTP及Manager另有20MiB输入/输出限制。Manager核对v2、请求编号/摘要、state及activation类型，旧v1错误保留。\n回执：workspace/deployment/activation分列；deployed仅磁盘成功，runtimeVerified始终false；不确认写入为RIBBON_RESULT_UNKNOWN。\n失败 / 恢复：存活缓存同计划返原回执；过期/后端重启没有持久执行事实，人工核对工作区/目标/备份，不自动重新执行；真实加载另验收。\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
        "status": "csharp · workflow",
        "limit": "首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块"
      },
      "body": "输入：preview是project及选定模块信息；apply严格为planId/planDigest，写授权独立。\n前置条件：已有文档身份、权威来源/托管保护通过、计划期限/历史/目标一致；不能用伪造preferredBinding接管原生文件。\n算法 / 手段：规范化与验证 → 缓存项目/目标/摘要 → apply写锁复验 → 消费计划 → 复用原文件/登记引擎 → 写后哈希回读。\n副作用：file-update仅写UIC；registration-add仅写选定XML及相关工作区记录/绑定。未选XML及登记时UIC保持字节；不执行宏或重启。\n校验：缓存最多64项、五分钟、单规范化输入4MiB、总缓存32MiB；HTTP及Manager另有20MiB输入/输出限制。Manager核对v2、请求编号/摘要、state及activation类型，旧v1错误保留。\n回执：workspace/deployment/activation分列；deployed仅磁盘成功，runtimeVerified始终false；不确认写入为RIBBON_RESULT_UNKNOWN。\n失败 / 恢复：存活缓存同计划返原回执；过期/后端重启没有持久执行事实，人工核对工作区/目标/备份，不自动重新执行；真实加载另验收。\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "status": "csharp · workflow",
      "limit": "首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "evidence": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ]
    },
    {
      "id": "script:migration-preview",
      "entityId": "script:migration-preview",
      "component": "ScriptContractCard",
      "title": "只读迁移材料解析",
      "summary": "rewrite/next/ribbon和本机绝对source；不自动跟随portable重定向。",
      "language": "csharp",
      "role": "algorithm",
      "featureIds": [
        "feature:migration",
        "feature:database"
      ],
      "sections": [
        {
          "id": "script:migration-preview/input",
          "key": "input",
          "title": "输入",
          "text": "rewrite/next/ribbon和本机绝对source；不自动跟随portable重定向。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:migration-preview/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "明确支持结构；rewrite v3仅允许根内`.control/generations/<GUID-N>.json`及匹配SHA256。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:migration-preview/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "受限枚举 → 原字节摘要 → UTF8/JSON/版本/重复键核查 → 递归净化 → 保留身份与历史材料。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:migration-preview/effect",
          "key": "effect",
          "title": "副作用",
          "text": "只读来源，不保存计划、不写Database、不激活；不完整数据也不发布为活动配置。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:migration-preview/validation",
          "key": "validation",
          "title": "校验",
          "text": "256文件、单文件2MiB、总16MiB、深度64；拒绝链接/越界；敏感字段/凭据引用及可能夹带秘密的已知容器剔除，只报路径。未知普通文本保留不意味着能识别任意恶意隐写。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:migration-preview/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "schema1报告含activate=false、complete、文件SHA256、sourceVersion、净化content与code/file/path诊断；不完整操作退出码2。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:migration-preview/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "不改来源所以无需数据回退；修正格式/预算后另行预览。此报告不是可直接执行的Database切换计划。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R14",
          "originalId": "R14",
          "status": "verified",
          "rule": "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
          "acceptance": "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        }
      ],
      "limitations": [
        "尚未整体替换活动写者",
        "不替代写入切换和恢复"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：rewrite/next/ribbon和本机绝对source；不自动跟随portable重定向。\n前置条件：明确支持结构；rewrite v3仅允许根内`.control/generations/<GUID-N>.json`及匹配SHA256。\n算法 / 手段：受限枚举 → 原字节摘要 → UTF8/JSON/版本/重复键核查 → 递归净化 → 保留身份与历史材料。\n副作用：只读来源，不保存计划、不写Database、不激活；不完整数据也不发布为活动配置。\n校验：256文件、单文件2MiB、总16MiB、深度64；拒绝链接/越界；敏感字段/凭据引用及可能夹带秘密的已知容器剔除，只报路径。未知普通文本保留不意味着能识别任意恶意隐写。\n回执：schema1报告含activate=false、complete、文件SHA256、sourceVersion、净化content与code/file/path诊断；不完整操作退出码2。\n失败 / 恢复：不改来源所以无需数据回退；修正格式/预算后另行预览。此报告不是可直接执行的Database切换计划。\n规范：R14 verified\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储\n\noffline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换",
        "status": "csharp · algorithm",
        "limit": "尚未整体替换活动写者；不替代写入切换和恢复；未整体切换活动存储；没有执行数据库切换"
      },
      "body": "输入：rewrite/next/ribbon和本机绝对source；不自动跟随portable重定向。\n前置条件：明确支持结构；rewrite v3仅允许根内`.control/generations/<GUID-N>.json`及匹配SHA256。\n算法 / 手段：受限枚举 → 原字节摘要 → UTF8/JSON/版本/重复键核查 → 递归净化 → 保留身份与历史材料。\n副作用：只读来源，不保存计划、不写Database、不激活；不完整数据也不发布为活动配置。\n校验：256文件、单文件2MiB、总16MiB、深度64；拒绝链接/越界；敏感字段/凭据引用及可能夹带秘密的已知容器剔除，只报路径。未知普通文本保留不意味着能识别任意恶意隐写。\n回执：schema1报告含activate=false、complete、文件SHA256、sourceVersion、净化content与code/file/path诊断；不完整操作退出码2。\n失败 / 恢复：不改来源所以无需数据回退；修正格式/预算后另行预览。此报告不是可直接执行的Database切换计划。\n规范：R14 verified\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储\n\noffline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换",
      "status": "csharp · algorithm",
      "limit": "尚未整体替换活动写者；不替代写入切换和恢复；未整体切换活动存储；没有执行数据库切换",
      "evidence": [
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        }
      ]
    },
    {
      "id": "script:knowledge-import",
      "entityId": "script:knowledge-import",
      "component": "ScriptContractCard",
      "title": "知识双路导入",
      "summary": "product/version/role/name与选定资料源或SQLite；apply使用缓存计划引用。",
      "language": "csharp",
      "role": "workflow",
      "featureIds": [
        "feature:knowledge"
      ],
      "sections": [
        {
          "id": "script:knowledge-import/input",
          "key": "input",
          "title": "输入",
          "text": "product/version/role/name与选定资料源或SQLite；apply使用缓存计划引用。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:knowledge-import/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "明确写授权、目标名无冲突、源未变、schema受支持；活动写占用/WAL等不能被不一致单文件复制绕过。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:knowledge-import/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "只读预览 → 私有暂存建库或一致备份 → 分层检查 → 重命名发布/登记。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:knowledge-import/effect",
          "key": "effect",
          "title": "副作用",
          "text": "创建产品版本隔离库；不覆盖同名库，不重建个人/工程/经验层。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:knowledge-import/validation",
          "key": "validation",
          "title": "校验",
          "text": "完整性、schema、关系、FTS/MATCH、清单与来源指纹，canEnable决定能否启用。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:knowledge-import/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "库身份、数据库SHA256、路径、检查项和来源证据状态；数据库身份与chunk ID共同定位正文。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:knowledge-import/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "失败清理本次暂存，保留旧库；已启用库变化要重检，不能把旧报告当新鲜证据。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R07",
          "originalId": "R07",
          "status": "planned",
          "rule": "知识入口默认官方 HTML 与源码；个人经验为补充，结果显示来源、产品与版本。",
          "acceptance": "首次使用无需手动登记函数；无官方资料显示定位/导入入口；经验不会覆盖官方来源。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R20",
          "originalId": "R20",
          "status": "planned",
          "rule": "官方 HTML/源码是知识主源；按产品版本建立引用，个人资料独立补充。",
          "acceptance": "保留来源/版本/块 ID；旧索引可辨识；无资料不编造答案；默认官方流程尚待实现。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "官方默认接入仍待完成"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：product/version/role/name与选定资料源或SQLite；apply使用缓存计划引用。\n前置条件：明确写授权、目标名无冲突、源未变、schema受支持；活动写占用/WAL等不能被不一致单文件复制绕过。\n算法 / 手段：只读预览 → 私有暂存建库或一致备份 → 分层检查 → 重命名发布/登记。\n副作用：创建产品版本隔离库；不覆盖同名库，不重建个人/工程/经验层。\n校验：完整性、schema、关系、FTS/MATCH、清单与来源指纹，canEnable决定能否启用。\n回执：库身份、数据库SHA256、路径、检查项和来源证据状态；数据库身份与chunk ID共同定位正文。\n失败 / 恢复：失败清理本次暂存，保留旧库；已启用库变化要重检，不能把旧报告当新鲜证据。\n规范：R07 planned · R20 planned\n证据：未登记独立证据；不能推定通过",
        "status": "csharp · workflow",
        "limit": "官方默认接入仍待完成"
      },
      "body": "输入：product/version/role/name与选定资料源或SQLite；apply使用缓存计划引用。\n前置条件：明确写授权、目标名无冲突、源未变、schema受支持；活动写占用/WAL等不能被不一致单文件复制绕过。\n算法 / 手段：只读预览 → 私有暂存建库或一致备份 → 分层检查 → 重命名发布/登记。\n副作用：创建产品版本隔离库；不覆盖同名库，不重建个人/工程/经验层。\n校验：完整性、schema、关系、FTS/MATCH、清单与来源指纹，canEnable决定能否启用。\n回执：库身份、数据库SHA256、路径、检查项和来源证据状态；数据库身份与chunk ID共同定位正文。\n失败 / 恢复：失败清理本次暂存，保留旧库；已启用库变化要重检，不能把旧报告当新鲜证据。\n规范：R07 planned · R20 planned\n证据：未登记独立证据；不能推定通过",
      "status": "csharp · workflow",
      "limit": "官方默认接入仍待完成",
      "evidence": []
    },
    {
      "id": "script:sdk-acceptance",
      "entityId": "script:sdk-acceptance",
      "component": "ScriptContractCard",
      "title": "SDK选择与按ID验证",
      "summary": "仓库SDK版本、明确TestId/Describe、相应测试入口与独立报告目录。",
      "language": "powershell",
      "role": "build",
      "featureIds": [
        "feature:tests"
      ],
      "sections": [
        {
          "id": "script:sdk-acceptance/input",
          "key": "input",
          "title": "输入",
          "text": "仓库SDK版本、明确TestId/Describe、相应测试入口与独立报告目录。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:sdk-acceptance/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "helper要求host实际含10.0.302，只有10.0.400不能满足精确门禁；Windows/WPF/浏览器等按测试入口声明。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:sdk-acceptance/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "选择已有dotnet host并设置当前进程环境 → 核对测试ID/源码定位与摘要 → 新目录执行 → 解析原始结果/视口证据。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:sdk-acceptance/effect",
          "key": "effect",
          "title": "副作用",
          "text": "构建、启动自有测试进程、写报告/截图；helper不安装SDK、不修改机器级环境。部分入口直接调用dotnet，不能假定全部自动dot-source helper。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:sdk-acceptance/validation",
          "key": "validation",
          "title": "校验",
          "text": "无匹配测试不能算通过；failed/skipped/not-run分别统计；截图需记录请求DIP、实际布局、PNG像素与DPI，图片存在不是验收。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:sdk-acceptance/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "commit/dirty、SDK/系统、执行数量、console/TRX/summary、源码身份及关联截图；历史结果不冒充本轮。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:sdk-acceptance/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "超时停止自有夹具并保留部分证据；环境缺失先修环境，重跑写新报告。首次CLI行为应在调用前设置首次运行/证书抑制环境，避免开发证书等非任务副作用。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R22",
          "originalId": "R22",
          "status": "verified",
          "rule": "代码、JSON、TOML 用同一业务路径测试，逐步保存输入、输出与结论。",
          "acceptance": "解析失败/超时保留部分结果；历史证据与本轮运行区分；报告按新目录保存。",
          "sourceRefs": [
            "source:canonical"
          ]
        },
        {
          "id": "canonical:R24",
          "originalId": "R24",
          "status": "verified",
          "rule": "文档从 Roslyn 生成签名、源码定位、静态调用与实现候选；手写只写业务约束。",
          "acceptance": "缺符号/失效源码哈希/悬空需求引用使构建失败；静态候选不得描述为实机执行；契约元数据可无损展开。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [],
      "limitations": [
        "执行器存在不代表桌面中心完成"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：仓库SDK版本、明确TestId/Describe、相应测试入口与独立报告目录。\n前置条件：helper要求host实际含10.0.302，只有10.0.400不能满足精确门禁；Windows/WPF/浏览器等按测试入口声明。\n算法 / 手段：选择已有dotnet host并设置当前进程环境 → 核对测试ID/源码定位与摘要 → 新目录执行 → 解析原始结果/视口证据。\n副作用：构建、启动自有测试进程、写报告/截图；helper不安装SDK、不修改机器级环境。部分入口直接调用dotnet，不能假定全部自动dot-source helper。\n校验：无匹配测试不能算通过；failed/skipped/not-run分别统计；截图需记录请求DIP、实际布局、PNG像素与DPI，图片存在不是验收。\n回执：commit/dirty、SDK/系统、执行数量、console/TRX/summary、源码身份及关联截图；历史结果不冒充本轮。\n失败 / 恢复：超时停止自有夹具并保留部分证据；环境缺失先修环境，重跑写新报告。首次CLI行为应在调用前设置首次运行/证书抑制环境，避免开发证书等非任务副作用。\n规范：R22 verified · R24 verified\n证据：未登记独立证据；不能推定通过",
        "status": "powershell · build",
        "limit": "执行器存在不代表桌面中心完成"
      },
      "body": "输入：仓库SDK版本、明确TestId/Describe、相应测试入口与独立报告目录。\n前置条件：helper要求host实际含10.0.302，只有10.0.400不能满足精确门禁；Windows/WPF/浏览器等按测试入口声明。\n算法 / 手段：选择已有dotnet host并设置当前进程环境 → 核对测试ID/源码定位与摘要 → 新目录执行 → 解析原始结果/视口证据。\n副作用：构建、启动自有测试进程、写报告/截图；helper不安装SDK、不修改机器级环境。部分入口直接调用dotnet，不能假定全部自动dot-source helper。\n校验：无匹配测试不能算通过；failed/skipped/not-run分别统计；截图需记录请求DIP、实际布局、PNG像素与DPI，图片存在不是验收。\n回执：commit/dirty、SDK/系统、执行数量、console/TRX/summary、源码身份及关联截图；历史结果不冒充本轮。\n失败 / 恢复：超时停止自有夹具并保留部分证据；环境缺失先修环境，重跑写新报告。首次CLI行为应在调用前设置首次运行/证书抑制环境，避免开发证书等非任务副作用。\n规范：R22 verified · R24 verified\n证据：未登记独立证据；不能推定通过",
      "status": "powershell · build",
      "limit": "执行器存在不代表桌面中心完成",
      "evidence": []
    },
    {
      "id": "script:ui-pending-gate",
      "entityId": "script:ui-pending-gate",
      "component": "ScriptContractCard",
      "title": "UI发送前持久门禁",
      "summary": "已确认的v2计划引用，或明确标记的v1首次生成输入；记录非秘密planId/digest/kind/端点，不记录项目口令。",
      "language": "typescript/csharp",
      "role": "workflow",
      "featureIds": [
        "feature:ribbon"
      ],
      "sections": [
        {
          "id": "script:ui-pending-gate/input",
          "key": "input",
          "title": "输入",
          "text": "已确认的v2计划引用，或明确标记的v1首次生成输入；记录非秘密planId/digest/kind/端点，不记录项目口令。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ui-pending-gate/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "没有未决部署记录；状态存储可写可回读；新建v1不冒充服务端冻结计划。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ui-pending-gate/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "消费本地计划 → 发送前持久pending → 发一次apply → 核对回执身份/状态/activation → 可确认结束时清记录。Web使用同源navigator.locks覆盖整个写请求，锁内再次核对localStorage，无可靠锁支持则拒绝发送；WPF使用跨进程FileShare.None租约覆盖整个写请求，并在发送前CreateNew写pending、Flush(true)。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ui-pending-gate/effect",
          "key": "effect",
          "title": "副作用",
          "text": "写本机非秘密门禁记录；复用既有API，不建立第二个部署引擎。读到pending的新页面/进程先暂停写入，不因为页面关闭或服务重启自动解除。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ui-pending-gate/validation",
          "key": "validation",
          "title": "校验",
          "text": "存储失败不得发送；v2回执必须匹配计划编号/摘要、版本、有效状态和activation类型。断连/超时/错身份/损坏回执保留unknown，阻止批次继续或自动重新预览。v1兼容入口发送后任何错误均保守unknown，不把旧引擎可能已保存历史的4xx当成无副作用拒绝；v2明确写前拒绝可以清理本地pending。跨页面竞争保护来自navigator锁而非localStorage自身原子性；人工解除必须核对原计划，不能视为服务器撤销。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ui-pending-gate/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "UI显示原编号、磁盘状态、未核验加载与人工核对出口；pending本身只说明可能未决，不证明服务器已受理或未执行。",
          "sourceRefs": [
            "source:overview"
          ]
        },
        {
          "id": "script:ui-pending-gate/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "重开读取记录后核对工作区历史、文件和备份；仅显式人工核对并二次确认才解除暂停。解除也获取同一Web锁/WPF跨进程租约，并比较提示时原编号与当前持久编号，旧A不能清除新B；当前持久记录优先于旧进程/标签内存编号。旧计划不重放、后续须新预览。上述客户端专项与离线全量已通过，原生预览、HTTP客户端与Web DOM证据仍各按其范围陈述。",
          "sourceRefs": [
            "source:overview"
          ]
        }
      ],
      "requirements": [],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ],
      "limitations": [
        "首次新文件为v1兼容",
        "限定运行时外观通过，未点击宏"
      ],
      "sourceRefs": [
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：已确认的v2计划引用，或明确标记的v1首次生成输入；记录非秘密planId/digest/kind/端点，不记录项目口令。\n前置条件：没有未决部署记录；状态存储可写可回读；新建v1不冒充服务端冻结计划。\n算法 / 手段：消费本地计划 → 发送前持久pending → 发一次apply → 核对回执身份/状态/activation → 可确认结束时清记录。Web使用同源navigator.locks覆盖整个写请求，锁内再次核对localStorage，无可靠锁支持则拒绝发送；WPF使用跨进程FileShare.None租约覆盖整个写请求，并在发送前CreateNew写pending、Flush(true)。\n副作用：写本机非秘密门禁记录；复用既有API，不建立第二个部署引擎。读到pending的新页面/进程先暂停写入，不因为页面关闭或服务重启自动解除。\n校验：存储失败不得发送；v2回执必须匹配计划编号/摘要、版本、有效状态和activation类型。断连/超时/错身份/损坏回执保留unknown，阻止批次继续或自动重新预览。v1兼容入口发送后任何错误均保守unknown，不把旧引擎可能已保存历史的4xx当成无副作用拒绝；v2明确写前拒绝可以清理本地pending。跨页面竞争保护来自navigator锁而非localStorage自身原子性；人工解除必须核对原计划，不能视为服务器撤销。\n回执：UI显示原编号、磁盘状态、未核验加载与人工核对出口；pending本身只说明可能未决，不证明服务器已受理或未执行。\n失败 / 恢复：重开读取记录后核对工作区历史、文件和备份；仅显式人工核对并二次确认才解除暂停。解除也获取同一Web锁/WPF跨进程租约，并比较提示时原编号与当前持久编号，旧A不能清除新B；当前持久记录优先于旧进程/标签内存编号。旧计划不重放、后续须新预览。上述客户端专项与离线全量已通过，原生预览、HTTP客户端与Web DOM证据仍各按其范围陈述。\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
        "status": "typescript/csharp · workflow",
        "limit": "首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块"
      },
      "body": "输入：已确认的v2计划引用，或明确标记的v1首次生成输入；记录非秘密planId/digest/kind/端点，不记录项目口令。\n前置条件：没有未决部署记录；状态存储可写可回读；新建v1不冒充服务端冻结计划。\n算法 / 手段：消费本地计划 → 发送前持久pending → 发一次apply → 核对回执身份/状态/activation → 可确认结束时清记录。Web使用同源navigator.locks覆盖整个写请求，锁内再次核对localStorage，无可靠锁支持则拒绝发送；WPF使用跨进程FileShare.None租约覆盖整个写请求，并在发送前CreateNew写pending、Flush(true)。\n副作用：写本机非秘密门禁记录；复用既有API，不建立第二个部署引擎。读到pending的新页面/进程先暂停写入，不因为页面关闭或服务重启自动解除。\n校验：存储失败不得发送；v2回执必须匹配计划编号/摘要、版本、有效状态和activation类型。断连/超时/错身份/损坏回执保留unknown，阻止批次继续或自动重新预览。v1兼容入口发送后任何错误均保守unknown，不把旧引擎可能已保存历史的4xx当成无副作用拒绝；v2明确写前拒绝可以清理本地pending。跨页面竞争保护来自navigator锁而非localStorage自身原子性；人工解除必须核对原计划，不能视为服务器撤销。\n回执：UI显示原编号、磁盘状态、未核验加载与人工核对出口；pending本身只说明可能未决，不证明服务器已受理或未执行。\n失败 / 恢复：重开读取记录后核对工作区历史、文件和备份；仅显式人工核对并二次确认才解除暂停。解除也获取同一Web锁/WPF跨进程租约，并比较提示时原编号与当前持久编号，旧A不能清除新B；当前持久记录优先于旧进程/标签内存编号。旧计划不重放、后续须新预览。上述客户端专项与离线全量已通过，原生预览、HTTP客户端与Web DOM证据仍各按其范围陈述。\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "status": "typescript/csharp · workflow",
      "limit": "首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "evidence": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ]
    },
    {
      "id": "script:runtime-verification",
      "entityId": "script:runtime-verification",
      "component": "ScriptContractCard",
      "title": "独立运行时验收与收尾",
      "summary": "明确授权的独立测试界面文件、单模块登记、目标产品与冻结备份。",
      "language": "procedure",
      "role": "acceptance",
      "featureIds": [
        "feature:ribbon",
        "feature:sessions"
      ],
      "sections": [
        {
          "id": "script:runtime-verification/input",
          "key": "input",
          "title": "输入",
          "text": "明确授权的独立测试界面文件、单模块登记、目标产品与冻结备份。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "script:runtime-verification/precondition",
          "key": "precondition",
          "title": "前置条件",
          "text": "启动、文件写入与重启分别获准；新会话重新严格核验。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "script:runtime-verification/method",
          "key": "method",
          "title": "算法 / 手段",
          "text": "直接HTTP冻结部署后正常重启，独立核验身份与实际界面，再有条件恢复。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "script:runtime-verification/effect",
          "key": "effect",
          "title": "副作用",
          "text": "改变独立测试文件与指定登记并启动、退出真实产品；未点击宏。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "script:runtime-verification/validation",
          "key": "validation",
          "title": "校验",
          "text": "新旧会话分别核对；实际页签、分组、大小图标与Tooltip限定外观证据。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "script:runtime-verification/receipt",
          "key": "receipt",
          "title": "回执",
          "text": "部署时仍为磁盘成功、运行时未核验；后续外观通过作为独立事实追加。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        },
        {
          "id": "script:runtime-verification/recovery",
          "key": "recovery",
          "title": "失败 / 恢复",
          "text": "正常退出后按冻结摘要恢复唯一测试登记；保留备份，其他产品不外推。",
          "sourceRefs": [
            "source:presentation",
            "source:overview"
          ]
        }
      ],
      "requirements": [
        {
          "id": "canonical:R17",
          "originalId": "R17",
          "status": "verified",
          "rule": "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
          "acceptance": "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
          "sourceRefs": [
            "source:canonical"
          ]
        }
      ],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ],
      "limitations": [
        "发现不等于登录",
        "首次新文件为v1兼容",
        "限定运行时外观通过，未点击宏"
      ],
      "sourceRefs": [
        "source:presentation",
        "source:overview"
      ],
      "props": {},
      "text": {
        "body": "输入：明确授权的独立测试界面文件、单模块登记、目标产品与冻结备份。\n前置条件：启动、文件写入与重启分别获准；新会话重新严格核验。\n算法 / 手段：直接HTTP冻结部署后正常重启，独立核验身份与实际界面，再有条件恢复。\n副作用：改变独立测试文件与指定登记并启动、退出真实产品；未点击宏。\n校验：新旧会话分别核对；实际页签、分组、大小图标与Tooltip限定外观证据。\n回执：部署时仍为磁盘成功、运行时未核验；后续外观通过作为独立事实追加。\n失败 / 恢复：正常退出后按冻结摘要恢复唯一测试登记；保留备份，其他产品不外推。\n规范：R17 verified\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
        "status": "procedure · acceptance",
        "limit": "发现不等于登录；首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块"
      },
      "body": "输入：明确授权的独立测试界面文件、单模块登记、目标产品与冻结备份。\n前置条件：启动、文件写入与重启分别获准；新会话重新严格核验。\n算法 / 手段：直接HTTP冻结部署后正常重启，独立核验身份与实际界面，再有条件恢复。\n副作用：改变独立测试文件与指定登记并启动、退出真实产品；未点击宏。\n校验：新旧会话分别核对；实际页签、分组、大小图标与Tooltip限定外观证据。\n回执：部署时仍为磁盘成功、运行时未核验；后续外观通过作为独立事实追加。\n失败 / 恢复：正常退出后按冻结摘要恢复唯一测试登记；保留备份，其他产品不外推。\n规范：R17 verified\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容\n\nruntime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "status": "procedure · acceptance",
      "limit": "发现不等于登录；首次新文件为v1兼容；限定运行时外观通过，未点击宏；不是UI全程实机验收；首次新建仍为v1兼容；未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "evidence": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        },
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ]
    },
    {
      "id": "verification:migration-offline",
      "entityId": "verification:migration-offline",
      "component": "EvidenceBadge",
      "title": "offline / passed",
      "summary": "三格式只读预览与敏感剔除专测14项通过。",
      "sections": [
        {
          "id": "verification:migration-offline/claim",
          "key": "claim",
          "title": "证据结论",
          "text": "三格式只读预览与敏感剔除专测14项通过。",
          "sourceRefs": [
            "source:evidence-migration"
          ]
        },
        {
          "id": "verification:migration-offline/environment",
          "key": "environment",
          "title": "适用环境",
          "text": "产品：synthetic",
          "sourceRefs": [
            "source:evidence-migration"
          ]
        }
      ],
      "requirements": [],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        }
      ],
      "limitations": [
        "没有执行数据库切换"
      ],
      "sourceRefs": [
        "source:evidence-migration"
      ],
      "props": {},
      "text": {
        "body": "证据结论：三格式只读预览与敏感剔除专测14项通过。\n适用环境：产品：synthetic\n规范：未单独关联规范条目\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换",
        "status": "offline / passed · 2026-10-31 · 产品：synthetic",
        "limit": "没有执行数据库切换"
      },
      "body": "证据结论：三格式只读预览与敏感剔除专测14项通过。\n适用环境：产品：synthetic\n规范：未单独关联规范条目\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n三格式只读预览与敏感剔除专测14项通过。\n未涵盖：没有执行数据库切换",
      "status": "offline / passed · 2026-10-31 · 产品：synthetic",
      "limit": "没有执行数据库切换",
      "evidence": [
        {
          "id": "verification:migration-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "三格式只读预览与敏感剔除专测14项通过。",
          "exclusions": [
            "没有执行数据库切换"
          ],
          "evidenceIds": [
            "evidence:migration"
          ],
          "environmentText": "产品：synthetic"
        }
      ]
    },
    {
      "id": "verification:ribbon-ui",
      "entityId": "verification:ribbon-ui",
      "component": "EvidenceBadge",
      "title": "ui / passed",
      "summary": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
      "sections": [
        {
          "id": "verification:ribbon-ui/claim",
          "key": "claim",
          "title": "证据结论",
          "text": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "sourceRefs": [
            "source:evidence-ribbon-ui"
          ]
        },
        {
          "id": "verification:ribbon-ui/environment",
          "key": "environment",
          "title": "适用环境",
          "text": "产品：synthetic",
          "sourceRefs": [
            "source:evidence-ribbon-ui"
          ]
        }
      ],
      "requirements": [],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        }
      ],
      "limitations": [
        "不是UI全程实机验收",
        "首次新建仍为v1兼容"
      ],
      "sourceRefs": [
        "source:evidence-ribbon-ui"
      ],
      "props": {},
      "text": {
        "body": "证据结论：离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n适用环境：产品：synthetic\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容",
        "status": "ui / passed · 2026-10-31 · 产品：synthetic",
        "limit": "不是UI全程实机验收；首次新建仍为v1兼容"
      },
      "body": "证据结论：离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n适用环境：产品：synthetic\n规范：未单独关联规范条目\n证据：ui / passed · 2026-10-31 · 产品：synthetic\n离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。\n未涵盖：不是UI全程实机验收；首次新建仍为v1兼容",
      "status": "ui / passed · 2026-10-31 · 产品：synthetic",
      "limit": "不是UI全程实机验收；首次新建仍为v1兼容",
      "evidence": [
        {
          "id": "verification:ribbon-ui",
          "scope": "ui",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
          "exclusions": [
            "不是UI全程实机验收",
            "首次新建仍为v1兼容"
          ],
          "evidenceIds": [
            "evidence:ribbon-ui"
          ],
          "environmentText": "产品：synthetic"
        }
      ]
    },
    {
      "id": "verification:ribbon-runtime",
      "entityId": "verification:ribbon-runtime",
      "component": "EvidenceBadge",
      "title": "runtime / passed",
      "summary": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
      "sections": [
        {
          "id": "verification:ribbon-runtime/claim",
          "key": "claim",
          "title": "证据结论",
          "text": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "sourceRefs": [
            "source:evidence-ribbon-runtime"
          ]
        },
        {
          "id": "verification:ribbon-runtime/environment",
          "key": "environment",
          "title": "适用环境",
          "text": "产品：E3D · 版本：2.1 · 模块：DESIGN",
          "sourceRefs": [
            "source:evidence-ribbon-runtime"
          ]
        }
      ],
      "requirements": [],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ],
      "limitations": [
        "未点击宏",
        "直接HTTP部署，不是UI全程",
        "不外推其他产品与模块"
      ],
      "sourceRefs": [
        "source:evidence-ribbon-runtime"
      ],
      "props": {},
      "text": {
        "body": "证据结论：限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n适用环境：产品：E3D · 版本：2.1 · 模块：DESIGN\n规范：未单独关联规范条目\n证据：runtime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
        "status": "runtime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN",
        "limit": "未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块"
      },
      "body": "证据结论：限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n适用环境：产品：E3D · 版本：2.1 · 模块：DESIGN\n规范：未单独关联规范条目\n证据：runtime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN\n限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。\n未涵盖：未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "status": "runtime / passed · 2026-10-31 · 产品：E3D · 版本：2.1 · 模块：DESIGN",
      "limit": "未点击宏；直接HTTP部署，不是UI全程；不外推其他产品与模块",
      "evidence": [
        {
          "id": "verification:ribbon-runtime",
          "scope": "runtime",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "E3D",
            "version": "2.1",
            "module": "DESIGN"
          },
          "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
          "exclusions": [
            "未点击宏",
            "直接HTTP部署，不是UI全程",
            "不外推其他产品与模块"
          ],
          "evidenceIds": [
            "evidence:ribbon-runtime"
          ],
          "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
        }
      ]
    },
    {
      "id": "verification:database-offline",
      "entityId": "verification:database-offline",
      "component": "EvidenceBadge",
      "title": "offline / passed",
      "summary": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
      "sections": [
        {
          "id": "verification:database-offline/claim",
          "key": "claim",
          "title": "证据结论",
          "text": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "sourceRefs": [
            "source:evidence-database"
          ]
        },
        {
          "id": "verification:database-offline/environment",
          "key": "environment",
          "title": "适用环境",
          "text": "产品：synthetic",
          "sourceRefs": [
            "source:evidence-database"
          ]
        }
      ],
      "requirements": [],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        }
      ],
      "limitations": [
        "未整体切换活动存储"
      ],
      "sourceRefs": [
        "source:evidence-database"
      ],
      "props": {},
      "text": {
        "body": "证据结论：独立修订存储及冲突、回滚、格式门禁有隔离测试。\n适用环境：产品：synthetic\n规范：未单独关联规范条目\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储",
        "status": "offline / passed · 2026-10-31 · 产品：synthetic",
        "limit": "未整体切换活动存储"
      },
      "body": "证据结论：独立修订存储及冲突、回滚、格式门禁有隔离测试。\n适用环境：产品：synthetic\n规范：未单独关联规范条目\n证据：offline / passed · 2026-10-31 · 产品：synthetic\n独立修订存储及冲突、回滚、格式门禁有隔离测试。\n未涵盖：未整体切换活动存储",
      "status": "offline / passed · 2026-10-31 · 产品：synthetic",
      "limit": "未整体切换活动存储",
      "evidence": [
        {
          "id": "verification:database-offline",
          "scope": "offline",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "synthetic"
          },
          "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
          "exclusions": [
            "未整体切换活动存储"
          ],
          "evidenceIds": [
            "evidence:database"
          ],
          "environmentText": "产品：synthetic"
        }
      ]
    },
    {
      "id": "verification:caf-static",
      "entityId": "verification:caf-static",
      "component": "EvidenceBadge",
      "title": "static / passed",
      "summary": "真实SDK接口编译与生命周期隔离核查。",
      "sections": [
        {
          "id": "verification:caf-static/claim",
          "key": "claim",
          "title": "证据结论",
          "text": "真实SDK接口编译与生命周期隔离核查。",
          "sourceRefs": [
            "source:evidence-caf"
          ]
        },
        {
          "id": "verification:caf-static/environment",
          "key": "environment",
          "title": "适用环境",
          "text": "产品：CAF",
          "sourceRefs": [
            "source:evidence-caf"
          ]
        }
      ],
      "requirements": [],
      "implementation": null,
      "verifications": [
        {
          "id": "verification:caf-static",
          "scope": "static",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "CAF"
          },
          "claim": "真实SDK接口编译与生命周期隔离核查。",
          "exclusions": [
            "未真实注册宿主",
            "未界面挂载"
          ],
          "evidenceIds": [
            "evidence:caf"
          ],
          "environmentText": "产品：CAF"
        }
      ],
      "limitations": [
        "未真实注册宿主",
        "未界面挂载"
      ],
      "sourceRefs": [
        "source:evidence-caf"
      ],
      "props": {},
      "text": {
        "body": "证据结论：真实SDK接口编译与生命周期隔离核查。\n适用环境：产品：CAF\n规范：未单独关联规范条目\n证据：static / passed · 2026-10-31 · 产品：CAF\n真实SDK接口编译与生命周期隔离核查。\n未涵盖：未真实注册宿主；未界面挂载",
        "status": "static / passed · 2026-10-31 · 产品：CAF",
        "limit": "未真实注册宿主；未界面挂载"
      },
      "body": "证据结论：真实SDK接口编译与生命周期隔离核查。\n适用环境：产品：CAF\n规范：未单独关联规范条目\n证据：static / passed · 2026-10-31 · 产品：CAF\n真实SDK接口编译与生命周期隔离核查。\n未涵盖：未真实注册宿主；未界面挂载",
      "status": "static / passed · 2026-10-31 · 产品：CAF",
      "limit": "未真实注册宿主；未界面挂载",
      "evidence": [
        {
          "id": "verification:caf-static",
          "scope": "static",
          "result": "passed",
          "observedAt": "2026-10-31",
          "environment": {
            "product": "CAF"
          },
          "claim": "真实SDK接口编译与生命周期隔离核查。",
          "exclusions": [
            "未真实注册宿主",
            "未界面挂载"
          ],
          "evidenceIds": [
            "evidence:caf"
          ],
          "environmentText": "产品：CAF"
        }
      ]
    }
  ],
  "flows": [
    {
      "id": "flow:launch",
      "title": "发现到观察",
      "summary": "计划绑定一次动作，未知沿原编号核对。",
      "featureIds": [
        "feature:launch"
      ],
      "steps": [
        {
          "id": "step:launch-discover",
          "title": "解释候选",
          "summary": "保留来源与歧义。",
          "entityId": "feature:launch"
        },
        {
          "id": "step:launch-plan",
          "title": "冻结计划",
          "summary": "修订与文件摘要绑定。",
          "entityId": "script:launch-workflow"
        },
        {
          "id": "step:launch-execute",
          "title": "受理再执行",
          "summary": "持久受理失败不启动。",
          "entityId": "script:launch-workflow"
        },
        {
          "id": "step:launch-observe",
          "title": "观察原编号",
          "summary": "unknown不重放。",
          "entityId": "script:launch-workflow"
        }
      ],
      "connections": [
        {
          "from": "step:launch-discover",
          "to": "step:launch-plan",
          "label": "确定目标",
          "kind": "next"
        },
        {
          "from": "step:launch-plan",
          "to": "step:launch-execute",
          "label": "明确授权",
          "kind": "next"
        },
        {
          "from": "step:launch-execute",
          "to": "step:launch-observe",
          "label": "回执或unknown",
          "kind": "uncertain"
        }
      ],
      "props": {}
    },
    {
      "id": "flow:ribbon",
      "title": "编辑、部署与独立加载核验",
      "summary": "磁盘写入不等于运行时已加载。",
      "featureIds": [
        "feature:ribbon"
      ],
      "steps": [
        {
          "id": "step:ribbon-preview",
          "title": "冻结差异",
          "summary": "绑定文档、目标与期限。",
          "entityId": "script:ribbon-frozen-deployment"
        },
        {
          "id": "step:ribbon-pending",
          "title": "先记录pending",
          "summary": "写前持久，跨页面互斥。",
          "entityId": "script:ui-pending-gate"
        },
        {
          "id": "step:ribbon-apply",
          "title": "仅发送原引用",
          "summary": "写后哈希核验。",
          "entityId": "script:ribbon-frozen-deployment"
        },
        {
          "id": "step:ribbon-check",
          "title": "人工核对",
          "summary": "不确定时暂停，不自动重放。",
          "entityId": "script:ui-pending-gate"
        },
        {
          "id": "step:ribbon-runtime",
          "title": "独立核验加载",
          "summary": "产品范围、实际UI与未执行宏分别记录。",
          "entityId": "script:runtime-verification"
        }
      ],
      "connections": [
        {
          "from": "step:ribbon-preview",
          "to": "step:ribbon-pending",
          "label": "确认",
          "kind": "next"
        },
        {
          "from": "step:ribbon-pending",
          "to": "step:ribbon-apply",
          "label": "记录成功",
          "kind": "next"
        },
        {
          "from": "step:ribbon-apply",
          "to": "step:ribbon-check",
          "label": "unknown",
          "kind": "uncertain"
        },
        {
          "from": "step:ribbon-check",
          "to": "step:ribbon-preview",
          "label": "人工核对后新预览",
          "kind": "recovery"
        },
        {
          "from": "step:ribbon-apply",
          "to": "step:ribbon-runtime",
          "label": "独立授权与证据",
          "kind": "next"
        }
      ],
      "props": {}
    },
    {
      "id": "flow:migration",
      "title": "先只读预览，再考虑切换",
      "summary": "只读阶段已完成；写入切换另属未完成契约。",
      "featureIds": [
        "feature:migration",
        "feature:database"
      ],
      "steps": [
        {
          "id": "step:migration-read",
          "title": "受限读取",
          "summary": "明确来源与预算。",
          "entityId": "script:migration-preview"
        },
        {
          "id": "step:migration-review",
          "title": "净化与审查",
          "summary": "保留历史，剔除秘密，不激活。",
          "entityId": "feature:migration"
        },
        {
          "id": "step:migration-future",
          "title": "未来显式切换",
          "summary": "切换、备份与恢复尚未闭环。",
          "entityId": "feature:database"
        }
      ],
      "connections": [
        {
          "from": "step:migration-read",
          "to": "step:migration-review",
          "label": "材料完整",
          "kind": "next"
        },
        {
          "from": "step:migration-review",
          "to": "step:migration-future",
          "label": "独立待实现",
          "kind": "uncertain"
        }
      ],
      "props": {}
    }
  ],
  "viewBindings": [
    {
      "id": "view:overview",
      "component": "ProjectOverview",
      "entityId": "project:pmltwin",
      "props": {}
    },
    {
      "id": "view:launch",
      "component": "FeatureCard",
      "entityId": "feature:launch",
      "props": {}
    },
    {
      "id": "view:deployment",
      "component": "FeatureCard",
      "entityId": "feature:deployment",
      "props": {}
    },
    {
      "id": "view:sessions",
      "component": "FeatureCard",
      "entityId": "feature:sessions",
      "props": {}
    },
    {
      "id": "view:objects",
      "component": "FeatureCard",
      "entityId": "feature:objects",
      "props": {}
    },
    {
      "id": "view:functions",
      "component": "FeatureCard",
      "entityId": "feature:functions",
      "props": {}
    },
    {
      "id": "view:forms",
      "component": "FeatureCard",
      "entityId": "feature:forms",
      "props": {}
    },
    {
      "id": "view:arrays",
      "component": "FeatureCard",
      "entityId": "feature:arrays",
      "props": {}
    },
    {
      "id": "view:formuse",
      "component": "FeatureCard",
      "entityId": "feature:formuse",
      "props": {}
    },
    {
      "id": "view:knowledge",
      "component": "FeatureCard",
      "entityId": "feature:knowledge",
      "props": {}
    },
    {
      "id": "view:activity",
      "component": "FeatureCard",
      "entityId": "feature:activity",
      "props": {}
    },
    {
      "id": "view:review",
      "component": "FeatureCard",
      "entityId": "feature:review",
      "props": {}
    },
    {
      "id": "view:tests",
      "component": "FeatureCard",
      "entityId": "feature:tests",
      "props": {}
    },
    {
      "id": "view:web",
      "component": "FeatureCard",
      "entityId": "feature:web",
      "props": {}
    },
    {
      "id": "view:ribbon",
      "component": "FeatureCard",
      "entityId": "feature:ribbon",
      "props": {}
    },
    {
      "id": "view:database",
      "component": "FeatureCard",
      "entityId": "feature:database",
      "props": {}
    },
    {
      "id": "view:migration",
      "component": "FeatureCard",
      "entityId": "feature:migration",
      "props": {}
    },
    {
      "id": "view:caf",
      "component": "FeatureCard",
      "entityId": "feature:caf",
      "props": {}
    },
    {
      "id": "view:plugins",
      "component": "FeatureCard",
      "entityId": "feature:plugins",
      "props": {}
    },
    {
      "id": "view:script-workbench-entry",
      "component": "ScriptContractCard",
      "entityId": "script:workbench-entry",
      "props": {}
    },
    {
      "id": "view:script-launch-workflow",
      "component": "ScriptContractCard",
      "entityId": "script:launch-workflow",
      "props": {}
    },
    {
      "id": "view:script-communication-client",
      "component": "ScriptContractCard",
      "entityId": "script:communication-client",
      "props": {}
    },
    {
      "id": "view:script-frozen-copy",
      "component": "ScriptContractCard",
      "entityId": "script:frozen-copy",
      "props": {}
    },
    {
      "id": "view:script-pml-development",
      "component": "ScriptContractCard",
      "entityId": "script:pml-development",
      "props": {}
    },
    {
      "id": "view:script-array-window",
      "component": "ScriptContractCard",
      "entityId": "script:array-window",
      "props": {}
    },
    {
      "id": "view:script-dbref-reader",
      "component": "ScriptContractCard",
      "entityId": "script:dbref-reader",
      "props": {}
    },
    {
      "id": "view:script-host-bootstrap",
      "component": "ScriptContractCard",
      "entityId": "script:host-bootstrap",
      "props": {}
    },
    {
      "id": "view:script-ribbon-frozen-deployment",
      "component": "ScriptContractCard",
      "entityId": "script:ribbon-frozen-deployment",
      "props": {}
    },
    {
      "id": "view:script-migration-preview",
      "component": "ScriptContractCard",
      "entityId": "script:migration-preview",
      "props": {}
    },
    {
      "id": "view:script-knowledge-import",
      "component": "ScriptContractCard",
      "entityId": "script:knowledge-import",
      "props": {}
    },
    {
      "id": "view:script-sdk-acceptance",
      "component": "ScriptContractCard",
      "entityId": "script:sdk-acceptance",
      "props": {}
    },
    {
      "id": "view:script-ui-pending-gate",
      "component": "ScriptContractCard",
      "entityId": "script:ui-pending-gate",
      "props": {}
    },
    {
      "id": "view:script-runtime-verification",
      "component": "ScriptContractCard",
      "entityId": "script:runtime-verification",
      "props": {}
    }
  ],
  "referenceIndex": {
    "sections": [
      {
        "id": "reference:context",
        "title": "规范身份、术语、状态与字段注册表",
        "columns": [
          "field",
          "value"
        ],
        "rows": [
          {
            "field": "schema",
            "value": "phayton.handoff/1"
          },
          {
            "field": "baseline",
            "value": "0923e4a09eb72112373fbab0df4da0ebca51b2a1"
          },
          {
            "field": "date",
            "value": "2026-10-26"
          },
          {
            "field": "terms",
            "value": {
              "BRD": "业务目标与边界",
              "PRD": "产品任务与交互",
              "SRS": "可验证的软件要求",
              "FRD": "触发、规则、结果",
              "SDD": "结构、职责、变化点",
              "SDS": "接口、数据、失败与实现约束"
            }
          },
          {
            "field": "states",
            "value": {
              "verified": "具备实现与验收依据；环境和时点以关联报告为准",
              "partial": "已有部分能力，目标流程未闭合",
              "planned": "待实现；原型可演示，产品未交付"
            }
          },
          {
            "field": "requirementFields",
            "value": [
              "id",
              "layer",
              "goal",
              "priority",
              "status",
              "rule",
              "acceptance",
              "screens",
              "operations",
              "bindings",
              "tests"
            ]
          },
          {
            "field": "bindingFields",
            "value": [
              "id",
              "project",
              "type",
              "member"
            ]
          },
          {
            "field": "testFields",
            "value": [
              "id",
              "file",
              "method"
            ]
          },
          {
            "field": "screenFields",
            "value": [
              "id",
              "title",
              "section",
              "question",
              "answer",
              "requirements"
            ]
          },
          {
            "field": "transitionFields",
            "value": [
              "from",
              "control",
              "label",
              "to",
              "guard",
              "otherwise"
            ]
          }
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:goals",
        "title": "G · 项目目标",
        "columns": [
          "id",
          "title",
          "description"
        ],
        "rows": [
          [
            "G1",
            "开发更便捷",
            "AI 查资料、写 PML、部署、加载、调试并核对结果。"
          ],
          [
            "G2",
            "辅助三维使用",
            "AI 结合对象数据、视图与资料核查问题及隐含关系。"
          ],
          [
            "G3",
            "可持续接手",
            "同一行为由界面、CLI、测试与文档引用；配置可回溯。"
          ]
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:requirements",
        "title": "R · 原始需求",
        "columns": [
          "id",
          "layer",
          "goal",
          "priority",
          "status",
          "rule",
          "acceptance",
          "screens",
          "operations",
          "bindings",
          "tests"
        ],
        "rows": [
          [
            "R01",
            "FE",
            "G3",
            "P0",
            "planned",
            "常用任务从工作台完成；字段、下一步与失败出口可见。",
            "非实现者按 PRD 完成启动配置、检索、查询、历史恢复；不编辑 JSON，不需要解释控件含义；记录步骤、阻塞和误操作。",
            [
              "S01",
              "S02",
              "S04",
              "S08"
            ],
            [],
            [
              "B20"
            ],
            []
          ],
          [
            "R02",
            "FE",
            "G1",
            "P0",
            "verified",
            "产品改变即更新模块；AM 不出现 DESIGN；安装发现与登录成功分别显示。",
            "E3D/AM 切换后模块属于当前产品；无安装只显示未发现；保存无效组合被拒绝。",
            [
              "S01",
              "S02",
              "S24",
              "S25",
              "S26"
            ],
            [
              "launch.modules.list",
              "launch.profile.validate"
            ],
            [
              "B02",
              "B08",
              "B09",
              "B22"
            ],
            [
              "T02"
            ]
          ],
          [
            "R03",
            "FE",
            "G3",
            "P0",
            "verified",
            "配置列表同时应用产品、收藏/最近下拉与输入搜索；空结果可清空条件。",
            "两个下拉和关键字取交集；重置后恢复全部；保留当前草稿。",
            [
              "S01",
              "S02"
            ],
            [
              "launch.profile.list"
            ],
            [
              "B01",
              "B21"
            ],
            [
              "T10"
            ]
          ],
          [
            "R04",
            "FE",
            "G3",
            "P0",
            "verified",
            "编辑→差异预览→保存→重开；未保存草稿离开需确认。",
            "保存显示修订；新服务重读相同值；取消离开保留草稿；保存失败不显示成功。",
            [
              "S03",
              "S04",
              "S05"
            ],
            [
              "launch.profile.preview",
              "launch.profile.save",
              "launch.profile.read"
            ],
            [
              "B03",
              "B20"
            ],
            [
              "T01"
            ]
          ],
          [
            "R05",
            "FE",
            "G3",
            "P0",
            "verified",
            "过期修订进入冲突状态；保留草稿，允许重新读取并比较。",
            "外部修改后保存被拒绝；界面不悄悄覆盖，草稿仍可查看。",
            [
              "S06"
            ],
            [
              "launch.profile.save"
            ],
            [
              "B03",
              "B25"
            ],
            [
              "T03"
            ]
          ],
          [
            "R06",
            "FE",
            "G3",
            "P0",
            "verified",
            "历史先预览再恢复；恢复生成新修订。",
            "选历史→查看差异→确认；取消不写入；恢复后其他配置不变。",
            [
              "S07",
              "S08",
              "S09"
            ],
            [
              "launch.profile.history",
              "launch.profile.restore.preview",
              "launch.profile.restore"
            ],
            [
              "B05",
              "B06",
              "B23"
            ],
            [
              "T04"
            ]
          ],
          [
            "R07",
            "FE",
            "G1",
            "P0",
            "planned",
            "知识入口默认官方 HTML 与源码；个人经验为补充，结果显示来源、产品与版本。",
            "首次使用无需手动登记函数；无官方资料显示定位/导入入口；经验不会覆盖官方来源。",
            [
              "S10",
              "S11"
            ],
            [
              "knowledge.layers.search"
            ],
            [
              "B17"
            ],
            []
          ],
          [
            "R08",
            "FE",
            "G1",
            "P0",
            "partial",
            "能力从 CLI 契约和已选源码发现，显示签名、来源与可用性。",
            "CLI 契约已可枚举；PML 自动发现仍需实现；缺协议或未授权显示原因，不能冒充不可发现。",
            [
              "S12"
            ],
            [
              "pml.functions.list"
            ],
            [
              "B13",
              "B30"
            ],
            [
              "T07"
            ]
          ],
          [
            "R09",
            "FE",
            "G2",
            "P0",
            "partial",
            "查询结果与运行记录分开展示；两表均有下拉条件和输入筛选。",
            "改变查询种类/目标清除旧续页；结果可空；记录按状态/操作及文字筛选，保留关联编号。",
            [
              "S13",
              "S14",
              "S15"
            ],
            [
              "pml.forms.list",
              "pml.gadgets.list",
              "runtime.calls"
            ],
            [
              "B24",
              "B19"
            ],
            [
              "T14"
            ]
          ],
          [
            "R10",
            "FE",
            "G1",
            "P0",
            "partial",
            "脚本流程包含写入计划、备份、加载、执行与结果对比。",
            "先看文件差异与执行目标；加载失败有回执；修正后独立验证，不用脚本合集替代工作流。",
            [
              "S17",
              "S18"
            ],
            [
              "workspace.pmllib.inspect",
              "pml.command.execute"
            ],
            [
              "B16"
            ],
            [
              "T12"
            ]
          ],
          [
            "R11",
            "FE",
            "G2",
            "P0",
            "partial",
            "执行模式与权限可见；结果不明有单独状态和核验入口。",
            "关闭执行仍可查资料/看计划；未知结果不显示成功，不出现自动重试按钮。",
            [
              "S16",
              "S19",
              "S22",
              "S23",
              "S27",
              "S28"
            ],
            [
              "runtime.status"
            ],
            [
              "B18",
              "B19"
            ],
            [
              "T09",
              "T13"
            ]
          ],
          [
            "R12",
            "FE",
            "G3",
            "P1",
            "planned",
            "业务错误定位字段或动作，并保留可恢复上下文。",
            "键盘完成筛选与确认；焦点可见；空/加载/拒绝/冲突/未知状态都有文案和出口；可用性实测另记。",
            [
              "S06",
              "S19",
              "S20"
            ],
            [],
            [
              "B20"
            ],
            []
          ],
          [
            "R13",
            "BE",
            "G3",
            "P0",
            "verified",
            "WPF 与 CLI 共用启动配置服务；存储与产品目录按实际变化点替换。",
            "同输入经两个适配器得到相同规则/修订；替换存储或目录不改用例；不为每个方法新增接口。",
            [
              "S04"
            ],
            [
              "launch.profile.save"
            ],
            [
              "B03",
              "B07",
              "B08",
              "B13"
            ],
            [
              "T01",
              "T02"
            ]
          ],
          [
            "R14",
            "BE",
            "G3",
            "P0",
            "verified",
            "提交使用 expectedRevision、文件锁与历史；未知字段保留。",
            "并发只有一个胜者；外部编辑被检测；旧字段不丢；中断只能恢复到旧态或已提交态。",
            [
              "S05",
              "S06"
            ],
            [
              "launch.profile.update"
            ],
            [
              "B04",
              "B07",
              "B25"
            ],
            [
              "T03",
              "T05"
            ]
          ],
          [
            "R15",
            "BE",
            "G3",
            "P0",
            "verified",
            "恢复重新校验当前环境，只恢复目标配置，产生新历史。",
            "失效路径拒绝；其他配置、旧历史与独立凭据不被回写。",
            [
              "S08",
              "S09"
            ],
            [
              "launch.profile.restore"
            ],
            [
              "B05",
              "B06"
            ],
            [
              "T04"
            ]
          ],
          [
            "R16",
            "BE",
            "G1",
            "P0",
            "verified",
            "CLI 原生分派 JSON；写配置、启动与执行分别授权。",
            "inline/file/stdin 仅选一种；JSON 不能自授权限；禁用独立 MCP；全部 46 项操作可描述。",
            [
              "S12",
              "S16"
            ],
            [
              "builtin.usage",
              "launch.profile.save"
            ],
            [
              "B11",
              "B12",
              "B13"
            ],
            [
              "T06",
              "T07",
              "T08"
            ]
          ],
          [
            "R17",
            "BE",
            "G2",
            "P0",
            "verified",
            "每次目标调用核验 PID、启动时间、模块与 Host 协议。",
            "错误身份/旧协议在分派前拒绝；不得回退为无约束原始命令。",
            [
              "S13",
              "S20",
              "S26",
              "S29"
            ],
            [
              "aveva.session.status",
              "pml.forms.list"
            ],
            [
              "B14",
              "B15"
            ],
            [
              "T09"
            ]
          ],
          [
            "R18",
            "BE",
            "G2",
            "P0",
            "verified",
            "查询受项数/字符预算约束；续页保留边界与原生缺失值。",
            "空字符串、0、false 不互换；未支持属性保留原生原因；条件变化不能拼接旧页。",
            [
              "S13",
              "S14"
            ],
            [
              "pml.array.window",
              "pml.gadget.read"
            ],
            [
              "B15",
              "B24",
              "B31"
            ],
            [
              "T14"
            ]
          ],
          [
            "R19",
            "BE",
            "G1",
            "P0",
            "partial",
            "复制计划冻结源/目标哈希，执行保留备份；与 PML 加载/调试串联。",
            "复制前变更任一端即拒绝；路径不能越界；部署成功与运行成功分开记录。",
            [
              "S17",
              "S18"
            ],
            [
              "workspace.pmllib.inspect",
              "pml.command.execute"
            ],
            [
              "B16",
              "B29"
            ],
            [
              "T12"
            ]
          ],
          [
            "R20",
            "BE",
            "G1",
            "P0",
            "planned",
            "官方 HTML/源码是知识主源；按产品版本建立引用，个人资料独立补充。",
            "保留来源/版本/块 ID；旧索引可辨识；无资料不编造答案；默认官方流程尚待实现。",
            [
              "S10",
              "S11"
            ],
            [
              "knowledge.layers.search"
            ],
            [
              "B17"
            ],
            []
          ],
          [
            "R21",
            "BE",
            "G3",
            "P0",
            "verified",
            "日志关联开始、结束、耗时与目标；未结束操作归为未知。",
            "命名秘密脱敏；日志故障不把已执行改成失败并诱发重试；记录结果与业务结果分别保存。",
            [
              "S15",
              "S19"
            ],
            [
              "runtime.calls",
              "runtime.status"
            ],
            [
              "B18",
              "B19"
            ],
            [
              "T11",
              "T13"
            ]
          ],
          [
            "R22",
            "BE",
            "G3",
            "P0",
            "verified",
            "代码、JSON、TOML 用同一业务路径测试，逐步保存输入、输出与结论。",
            "解析失败/超时保留部分结果；历史证据与本轮运行区分；报告按新目录保存。",
            [
              "S18"
            ],
            [
              "launch.workspace.read",
              "launch.profile.save"
            ],
            [
              "B03",
              "B12"
            ],
            [
              "T07",
              "T08"
            ]
          ],
          [
            "R23",
            "BE",
            "G1",
            "P0",
            "verified",
            "启动初始化读 Current Session；产品系列选择 Host；REHASH 由独立策略和环境值控制。",
            "E3D2.1→2.1/net48；3.1→3.1.6/net48；AM/PDMS→net35；none/空关闭，rehash-all 每新会话一次；改值下次进程生效。",
            [
              "S01",
              "S16"
            ],
            [
              "launch.profile.preview"
            ],
            [
              "B10"
            ],
            []
          ],
          [
            "R24",
            "BE",
            "G3",
            "P1",
            "verified",
            "文档从 Roslyn 生成签名、源码定位、静态调用与实现候选；手写只写业务约束。",
            "缺符号/失效源码哈希/悬空需求引用使构建失败；静态候选不得描述为实机执行；契约元数据可无损展开。",
            [
              "S21"
            ],
            [],
            [
              "B03",
              "B04",
              "B07",
              "B08"
            ],
            [
              "T15"
            ]
          ]
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:bindings",
        "title": "B · 源码绑定",
        "columns": [
          "id",
          "project",
          "type",
          "member"
        ],
        "rows": [
          [
            "B01",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.LaunchProfileService",
            "List"
          ],
          [
            "B02",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.LaunchProfileService",
            "Validate"
          ],
          [
            "B03",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.LaunchProfileService",
            "Save"
          ],
          [
            "B04",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.WorkspaceConfigurationStore",
            "Commit"
          ],
          [
            "B05",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.LaunchProfileService",
            "PreviewRestore"
          ],
          [
            "B06",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.LaunchProfileService",
            "Restore"
          ],
          [
            "B07",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.IConfigurationStore`1",
            "Commit"
          ],
          [
            "B08",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.IProductModuleCatalog",
            "Get"
          ],
          [
            "B09",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.ProductModuleCatalog",
            "Get"
          ],
          [
            "B10",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.LaunchService",
            "Preview"
          ],
          [
            "B11",
            "YuzuhaToolkit.Cli.Compatibility",
            "YuzuhaToolkit.Mcp.NativeCliHost",
            null
          ],
          [
            "B12",
            "PmlTwin.Phayton",
            "PmlTwin.Phayton.OperationExecutor",
            "InvokeAsync"
          ],
          [
            "B13",
            "YuzuhaToolkit.Cli.Compatibility",
            "YuzuhaToolkit.Mcp.NativeOperationCatalog",
            "Create"
          ],
          [
            "B14",
            "YuzuhaToolkit.Mcp.Core",
            "YuzuhaToolkit.Mcp.YuzuhaRpcBridge",
            "SelectSessionAsync"
          ],
          [
            "B15",
            "YuzuhaToolkit.Mcp.Core",
            "YuzuhaToolkit.Mcp.PmlFormQueryRequestSender",
            "SendAsync"
          ],
          [
            "B16",
            "YuzuhaToolkit.Mcp.Core",
            "YuzuhaToolkit.Mcp.WorkspaceTools",
            null
          ],
          [
            "B17",
            "YuzuhaToolkit.Knowledge.Core",
            "YuzuhaToolkit.Knowledge.KnowledgeRepository",
            null
          ],
          [
            "B18",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.RuntimeJournal",
            "Complete"
          ],
          [
            "B19",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.RuntimeReader",
            "Recent"
          ],
          [
            "B20",
            "YuzuhaToolkit.Desktop",
            "YuzuhaToolkit.Desktop.MainWindow",
            "PreviewProfileChanges"
          ],
          [
            "B21",
            "YuzuhaToolkit.Desktop",
            "YuzuhaToolkit.Desktop.MainWindow",
            "LaunchProfileFilterChanged"
          ],
          [
            "B22",
            "YuzuhaToolkit.Desktop",
            "YuzuhaToolkit.Desktop.MainWindow",
            "ProductIdentityChanged"
          ],
          [
            "B23",
            "YuzuhaToolkit.Desktop",
            "YuzuhaToolkit.Desktop.MainWindow",
            "ConfirmRestoreProfile"
          ],
          [
            "B24",
            "YuzuhaToolkit.Desktop",
            "YuzuhaToolkit.Desktop.SessionWorkbench",
            "QueryAsync"
          ],
          [
            "B25",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.ConfigurationConflictException",
            null
          ],
          [
            "B29",
            "YuzuhaToolkit.Workspace",
            "YuzuhaToolkit.Workspace.DirectoryInspector",
            "Scan"
          ],
          [
            "B30",
            "YuzuhaToolkit.Mcp.Core",
            "YuzuhaToolkit.Mcp.PmlFunctionRegistry",
            null
          ],
          [
            "B31",
            "YuzuhaToolkit.PmlHost.Net48",
            "YuzuhaToolkit.PmlHost.PmlCommandRpcService",
            null
          ]
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:tests",
        "title": "T · 验收声明",
        "columns": [
          "id",
          "file",
          "method"
        ],
        "rows": [
          [
            "T01",
            "tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs",
            "SavedProfileIsReadByNewServiceAndDraftDoesNotMutateFile"
          ],
          [
            "T02",
            "tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs",
            "InvalidProductModuleRejectsBeforePersistence"
          ],
          [
            "T03",
            "tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs",
            "ConcurrentEditorsCannotOverwriteTheFirstSavedChange"
          ],
          [
            "T04",
            "tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs",
            "RestoreCreatesNewRevisionAndKeepsOtherProfilesAndOldHistory"
          ],
          [
            "T05",
            "tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs",
            "JsonUpdatePreservesUnknownWorkspaceAndProfileFields"
          ],
          [
            "T06",
            "tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs",
            "PermissionDenialAndUnknownPatchFieldsLeaveFileUntouched"
          ],
          [
            "T07",
            "tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs",
            "EveryFormerToolHasARealNativePositivePathAndRecordedFailureGates"
          ],
          [
            "T08",
            "tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs",
            "FileStdinAndInlineJsonUseTheSameDispatcherAndRejectAmbiguity"
          ],
          [
            "T09",
            "tests/YuzuhaToolkit.Tests/NativeCliCapabilityTests.cs",
            "ExplicitTargetAuthorizationProtocolAndTransportFailuresPreserveTruth"
          ],
          [
            "T10",
            "tests/YuzuhaToolkit.Tests/LaunchLifecycleTests.cs",
            "FiltersFavoritesDefaultsAndHistoryUseSameService"
          ],
          [
            "T11",
            "tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs",
            "JournalPersistsCorrelatedStartAndCompletionWithRedactedNamedSecrets"
          ],
          [
            "T12",
            "tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs",
            "ChangedTargetStopsWholeCopyBeforeAnyWrites"
          ],
          [
            "T13",
            "tests/YuzuhaToolkit.Tests/WorkspaceBehaviorTests.cs",
            "ExitedProcessWithUnfinishedCallIsReportedAsUnknownRatherThanSuccess"
          ],
          [
            "T14",
            "tests/YuzuhaToolkit.Tests/PmlFormQueryTests.cs",
            "FormListsUseNativeManagerAndHonorScanBudget"
          ],
          [
            "T15",
            "tests/handoff-specifications.test.mjs",
            "compact metadata preserves every value"
          ]
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:screens",
        "title": "S · 页面契约",
        "columns": [
          "id",
          "title",
          "section",
          "question",
          "answer",
          "requirements"
        ],
        "rows": [
          [
            "S01",
            "启动配置",
            "launch",
            "从哪个环境开始？",
            "先选产品与安装，再选择模块和工程；不依赖 LaunchDesk。",
            [
              "R02",
              "R03",
              "R23"
            ]
          ],
          [
            "S02",
            "组合筛选",
            "launch",
            "怎样找到需要的配置？",
            "产品、收藏范围与文字取交集；清空条件可回到全部。",
            [
              "R03"
            ]
          ],
          [
            "S03",
            "编辑配置",
            "launch",
            "哪里修改常变配置？",
            "在当前工作台编辑；保存前展示实际差异。",
            [
              "R04"
            ]
          ],
          [
            "S04",
            "保存预览",
            "launch",
            "点击保存会改变什么？",
            "只提交配置与历史；新值在下次启动生效。",
            [
              "R04",
              "R13"
            ]
          ],
          [
            "S05",
            "已保存并重开",
            "launch",
            "怎样确认持久化？",
            "显示新修订；重新读取与保存值一致。",
            [
              "R04",
              "R14"
            ]
          ],
          [
            "S06",
            "并发冲突",
            "launch",
            "别人刚改过怎么办？",
            "保留草稿；重新读取、比较，再选择保存。",
            [
              "R05"
            ]
          ],
          [
            "S07",
            "配置历史",
            "launch",
            "如何找到旧配置？",
            "历史显示时间、原因与修订；先选一条查看差异。",
            [
              "R06"
            ]
          ],
          [
            "S08",
            "恢复预览",
            "launch",
            "恢复会覆盖什么？",
            "仅恢复当前配置；校验当前环境；写入新修订。",
            [
              "R06",
              "R15"
            ]
          ],
          [
            "S09",
            "已恢复",
            "launch",
            "能再回到恢复前吗？",
            "旧历史仍在，恢复记录也是一个新版本。",
            [
              "R06"
            ]
          ],
          [
            "S10",
            "官方资料",
            "knowledge",
            "知识从哪里开始？",
            "默认官方 HTML 与源码；个人经验独立补充。",
            [
              "R07",
              "R20"
            ]
          ],
          [
            "S11",
            "资料检索",
            "knowledge",
            "AI 用了哪份依据？",
            "结果展示来源、产品版本、块 ID；没有资料就明确说明。",
            [
              "R07",
              "R20"
            ]
          ],
          [
            "S12",
            "能力发现",
            "capability",
            "需要手工登记每个函数吗？",
            "CLI 从契约枚举；目标中的 PML 自动发现为下一阶段。",
            [
              "R08",
              "R16"
            ]
          ],
          [
            "S13",
            "查询结果",
            "query",
            "怎样有条件地看模型信息？",
            "先核验目标；按种类与文字筛选；预算内分页。",
            [
              "R09",
              "R17",
              "R18"
            ]
          ],
          [
            "S14",
            "查询筛选结果",
            "query",
            "条件变了还能接旧页吗？",
            "条件变更重新查询，不拼接旧续页；保留空值与原生类型。",
            [
              "R09",
              "R18"
            ]
          ],
          [
            "S15",
            "运行记录",
            "records",
            "怎么找到这次调用？",
            "按状态、操作与文字筛选；通过关联编号回到回执。",
            [
              "R09",
              "R21"
            ]
          ],
          [
            "S16",
            "权限与启动动作",
            "permissions",
            "既要方便又要可控，怎么选？",
            "查资料、写配置、启动与执行分别授权；REHASH 默认关闭。",
            [
              "R11",
              "R16",
              "R23"
            ]
          ],
          [
            "S17",
            "脚本部署计划",
            "scripts",
            "AI 写完代码以后做什么？",
            "看文件差异与备份，再确认复制与加载目标。",
            [
              "R10",
              "R19"
            ]
          ],
          [
            "S18",
            "加载与调试",
            "scripts",
            "结果不对怎么继续？",
            "回读输出与预期，保存失败回执，修改后独立验证。",
            [
              "R10",
              "R22"
            ]
          ],
          [
            "S19",
            "结果不明",
            "records",
            "超时能直接再点一次吗？",
            "先核验模型与回执；可能已执行的命令不自动重试。",
            [
              "R11",
              "R21"
            ]
          ],
          [
            "S20",
            "目标不可用",
            "query",
            "连接不可用还可以做什么？",
            "查资料、看配置和计划；重新发现并核验目标。",
            [
              "R12",
              "R17"
            ]
          ],
          [
            "S21",
            "需求与实现索引",
            "overview",
            "接手时先看什么？",
            "任务→需求→交互→操作→Roslyn 符号→测试，按 ID 跳转。",
            [
              "R24"
            ]
          ],
          [
            "S22",
            "开发授权已设置",
            "permissions",
            "设置授权是否等于已执行？",
            "许可不会自动执行；返回计划，仍需核对目标与差异。",
            [
              "R11",
              "R16"
            ]
          ],
          [
            "S23",
            "执行未授权",
            "scripts",
            "为什么现在不能加载？",
            "PML 执行未授权；可继续预览，或明确调整后续许可。",
            [
              "R10",
              "R11",
              "R16"
            ]
          ],
          [
            "S24",
            "未发现安装",
            "launch",
            "没有找到安装怎么办？",
            "选择产品后可手动定位安装；发现环境不代表已登录。",
            [
              "R02",
              "R12"
            ]
          ],
          [
            "S25",
            "启动预览",
            "launch",
            "即将启动哪个工程？",
            "核对产品、模块、工程、MDB 与许可；保存和启动分别确认。",
            [
              "R02",
              "R04",
              "R16",
              "R23"
            ]
          ],
          [
            "S26",
            "进程已启动",
            "launch",
            "程序启动后就连接了吗？",
            "已创建进程；继续发现并核验 Host，才能查询或执行。",
            [
              "R02",
              "R17"
            ]
          ],
          [
            "S27",
            "启动未授权",
            "launch",
            "为什么没有启动工程？",
            "本次没有创建进程；先明确允许启动，再回到启动计划。",
            [
              "R11",
              "R16"
            ]
          ],
          [
            "S28",
            "启动许可已设置",
            "permissions",
            "允许启动会立即打开工程吗？",
            "不会。回到启动计划核对后，再确认创建进程。",
            [
              "R11",
              "R16"
            ]
          ],
          [
            "S29",
            "目标已核验",
            "launch",
            "现在可以做什么？",
            "核验目标身份与 Host 协议后，可进入查询；执行仍需单独授权。",
            [
              "R09",
              "R17"
            ]
          ],
          [
            "S30",
            "配置写入未授权",
            "launch",
            "为什么文件没有改变？",
            "本次没有提交配置；草稿仍在。明确允许写入后，返回编辑并重新预览。",
            [
              "R04",
              "R12",
              "R16"
            ]
          ]
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:transitions",
        "title": "页面转换与守卫",
        "columns": [
          "from",
          "control",
          "label",
          "to",
          "guard",
          "otherwise"
        ],
        "rows": [
          [
            "S01",
            "filter",
            "应用筛选",
            "S02"
          ],
          [
            "S02",
            "reset",
            "清空条件",
            "S01"
          ],
          [
            "S01",
            "edit",
            "编辑所选配置",
            "S03"
          ],
          [
            "S03",
            "preview",
            "查看保存差异",
            "S04"
          ],
          [
            "S04",
            "save",
            "确认保存",
            "S05",
            "writeAllowed",
            "S30"
          ],
          [
            "S05",
            "reopen",
            "重新读取",
            "S05"
          ],
          [
            "S04",
            "conflict",
            "评审：修订冲突",
            "S06"
          ],
          [
            "S06",
            "compare",
            "重读并保留草稿",
            "S03"
          ],
          [
            "S05",
            "history",
            "查看历史",
            "S07"
          ],
          [
            "S07",
            "restore",
            "预览所选修订",
            "S08"
          ],
          [
            "S08",
            "confirm",
            "确认恢复为新修订",
            "S09",
            "writeAllowed",
            "S30"
          ],
          [
            "S08",
            "cancel",
            "取消恢复",
            "S07"
          ],
          [
            "S10",
            "search",
            "检索官方资料",
            "S11"
          ],
          [
            "S11",
            "discover",
            "查看相关能力",
            "S12"
          ],
          [
            "S12",
            "plan",
            "进入脚本计划",
            "S17"
          ],
          [
            "S13",
            "filter",
            "应用筛选",
            "S14"
          ],
          [
            "S14",
            "reset",
            "清空查询条件",
            "S13"
          ],
          [
            "S13",
            "unavailable",
            "评审：目标失效",
            "S20"
          ],
          [
            "S20",
            "retry",
            "重新发现目标",
            "S13"
          ],
          [
            "S17",
            "apply",
            "确认部署与加载",
            "S18",
            "executionAllowed",
            "S23"
          ],
          [
            "S18",
            "verify",
            "查看运行回执",
            "S15"
          ],
          [
            "S18",
            "timeout",
            "评审：结果未知",
            "S19"
          ],
          [
            "S19",
            "inspect",
            "核验关联回执",
            "S15"
          ],
          [
            "S15",
            "permission",
            "调整后续执行权限",
            "S16"
          ],
          [
            "S16",
            "enable",
            "允许后续执行",
            "S22"
          ],
          [
            "S22",
            "continue",
            "返回脚本计划",
            "S17"
          ],
          [
            "S22",
            "disable",
            "关闭执行",
            "S16"
          ],
          [
            "S23",
            "permission",
            "调整执行许可",
            "S16"
          ],
          [
            "S23",
            "preview",
            "返回只读计划",
            "S17"
          ],
          [
            "S01",
            "missing",
            "评审：缺安装",
            "S24"
          ],
          [
            "S24",
            "locate",
            "定位安装并编辑",
            "S03"
          ],
          [
            "S01",
            "start",
            "查看启动计划",
            "S25"
          ],
          [
            "S05",
            "start",
            "查看启动计划",
            "S25"
          ],
          [
            "S25",
            "launch",
            "确认启动",
            "S26",
            "launchAllowed",
            "S27"
          ],
          [
            "S25",
            "cancel",
            "返回配置",
            "S01"
          ],
          [
            "S26",
            "verify",
            "发现并核验 Host",
            "S29"
          ],
          [
            "S26",
            "unavailable",
            "评审：目标失效",
            "S20"
          ],
          [
            "S27",
            "permission",
            "调整启动许可",
            "S16"
          ],
          [
            "S16",
            "enableLaunch",
            "允许后续启动",
            "S28"
          ],
          [
            "S28",
            "continue",
            "返回启动计划",
            "S25"
          ],
          [
            "S28",
            "disableLaunch",
            "关闭启动许可",
            "S16"
          ],
          [
            "S29",
            "query",
            "进入查询结果",
            "S13"
          ],
          [
            "S16",
            "disableWrite",
            "关闭配置写入",
            "S30"
          ],
          [
            "S30",
            "enableWrite",
            "允许后续配置写入",
            "S03"
          ],
          [
            "S30",
            "permission",
            "查看权限范围",
            "S16"
          ]
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:models",
        "title": "内部模型与决策",
        "columns": [
          "id",
          "name",
          "facts",
          "plain",
          "model",
          "decision"
        ],
        "rows": [
          {
            "id": "M1",
            "name": "配置修订",
            "facts": [
              "共享服务",
              "expectedRevision",
              "保留未知字段",
              "草稿隔离",
              "恢复生成新修订"
            ],
            "plain": "界面保存配置时应调用与命令行相同的服务。命令行保存配置时也应调用这个服务。两者都应提供读取时取得的修订，保存前检查当前文件是否仍是那个修订，如果已经变化则拒绝保存。没有修改的扩展字段不能在保存时被删掉。界面编辑中的值与已经保存的值分开。恢复历史也应检查当前修订，校验字段，只更新选中的配置，并再增加一条修订，不能把整份旧文件覆盖回去。",
            "model": "WPF/CLI→共享服务；草稿隔离。提交(读取修订,draft)→新修订；过期拒绝并保留草稿；未知字段保留。恢复=校验后提交目标配置，生成新修订。",
            "decision": "采用：已有重复入口和真实并发变化点。"
          },
          {
            "id": "M2",
            "name": "执行状态机",
            "facts": [
              "目标核验",
              "分权授权",
              "分派边界",
              "结果不明",
              "不自动重试"
            ],
            "plain": "执行 PML 以前需要核对目标进程身份和模块，也需要检查是否允许执行；允许写配置或启动程序不代表允许执行 PML。只有这些检查通过才能把操作发给 Host。发给 Host 以前失败可明确说没有执行。已经发送后出现超时或连接中断，则不能据此说没有执行，也不能简单显示成功。应保存结果不明状态，让使用者核验回执和模型，然后自行决定下一步，程序不能自动再次发送同一命令。",
            "model": "核验目标+执行授权→分派→成功/失败/未知。分派前拒绝=未执行；分派后超时/断线=可能已执行，先核验，不自动重试。写配置、启动、执行权限独立。",
            "decision": "采用：状态比异常文本更能表达执行边界；不新增通用 Command 总线。"
          },
          {
            "id": "M3",
            "name": "端口与用例",
            "facts": [
              "存储变化点",
              "目录变化点",
              "用例内聚",
              "不逐方法接口化"
            ],
            "plain": "配置可能存于本机文件，也可能在将来使用另一种持久化方式，因此配置存储需要可替换。不同产品和安装版本的模块来源不同，因此模块目录也需要可替换。保存、复制配置、设默认以及恢复仍属于同一个启动配置用例，应放在同一个服务内保持规则一致。不能因为想采用某种设计模式，就把服务里的每一个方法都拆成一个接口、一个命令和一个处理器，增加定位路径和需要理解的概念。",
            "model": "启动用例内聚 B03（共享服务）；可替换 B07（存储）/B08（模块）。无独立变化点就用具体类，不逐方法建接口/Command。",
            "decision": "采用现有两端口；没有第二实现/独立变化压力时继续用具体类。"
          }
        ],
        "sourceRefs": [
          "source:canonical"
        ]
      },
      {
        "id": "reference:source-mappings",
        "title": "来源限定责任映射 · 不升级实现或验收",
        "columns": [
          "source",
          "id",
          "owner",
          "mappingStatus",
          "sourceStatus"
        ],
        "rows": [
          {
            "source": "Archon",
            "id": "BRD-001",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "BRD-002",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "BRD-003",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "BRD-004",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "BRD-005",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "PRD-001",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "PRD-002",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "PRD-003",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "PRD-004",
            "owner": "health",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "PRD-005",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "PRD-006",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "PRD-007",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "SRS-001",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-002",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-003",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-004",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-005",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-006",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-007",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-008",
            "owner": "health",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-009",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-010",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-011",
            "owner": "environment-variables",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-012",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-013",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SRS-014",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "待确认"
          },
          {
            "source": "Archon",
            "id": "FRD-001",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "FRD-002",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "FRD-003",
            "owner": "health",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "FRD-004",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "FRD-005",
            "owner": "environment-variables",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "FRD-006",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "FRD-007",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "FRD-008",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDD-001",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDD-002",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDD-003",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDD-004",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDD-005",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDD-006",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-001",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-002",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-003",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-004",
            "owner": "health",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-005",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-006",
            "owner": "environment-variables",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-007",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-008",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Archon",
            "id": "SDS-009",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "实现观察"
          },
          {
            "source": "Next",
            "id": "PM-01",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已实现并通过验证"
          },
          {
            "source": "Next",
            "id": "PM-02",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已实现并通过验证"
          },
          {
            "source": "Next",
            "id": "PM-03",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已实现并通过验证"
          },
          {
            "source": "Next",
            "id": "PM-04",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已实现并通过验证"
          },
          {
            "source": "Next",
            "id": "PM-05",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已实现并通过验证；当前只查目录"
          },
          {
            "source": "Next",
            "id": "RP-01",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "RP-02",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "RP-03",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "PF-01",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "用户已批准Tomlyn 2.10.1；明确暂不安装"
          },
          {
            "source": "Next",
            "id": "PF-02",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "CR-01",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "基础能力已有；导出边界方案待评审/实现"
          },
          {
            "source": "Next",
            "id": "CR-02",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "共享引用与授权图稿已设计；待评审/实现"
          },
          {
            "source": "Next",
            "id": "CR-03",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "CR-04",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "CR-05",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "CR-06",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "方案已补齐，待评审/实现"
          },
          {
            "source": "Next",
            "id": "CR-07",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "边界已具体化，待评审/落实"
          },
          {
            "source": "Next",
            "id": "VM-01",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "G0/G1环境与10项OS检查通过；16项产品场景仍待实现后分层验证"
          },
          {
            "source": "Next",
            "id": "VM-02",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "G0/G1通过，配置前和Ready快照保留；原VMX哈希例外未掩盖，见[实际记录](xref:remote-project-lab-results)"
          },
          {
            "source": "Next",
            "id": "LC-01",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "只读清单已核对；恢复主机名后AVEVA201签出/归还通过；现有厂商启动链已进入Model，未改许可密钥"
          },
          {
            "source": "Next",
            "id": "UX-01",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "图稿与8张预览已交付，待评审"
          },
          {
            "source": "Next",
            "id": "LC-02",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已查明：克隆改名使0008主机名绑定失配，RMS拒绝加载许可"
          },
          {
            "source": "Next",
            "id": "LC-03",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "首次失败；恢复主机名后签出/归还通过，后续已定位实验器重定向模式并用原生启动进入Model；清理完成"
          },
          {
            "source": "Next",
            "id": "LC-04",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已确认失配；后续按LC-05授权恢复名称，指纹重新匹配"
          },
          {
            "source": "Next",
            "id": "LC-05",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "UID3保留；配置与10项SMB通过，4546及AVEVA201签出通过；应用FNF7，克隆关机"
          },
          {
            "source": "Next",
            "id": "LC-06",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "A/B失败、C/D原生句柄启动成功；并非目录缺失，详见开发对照"
          },
          {
            "source": "Next",
            "id": "DV-01",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "dev/RemoteProjectLab已拆分；机器/用户绑定开关默认关闭，6项开关检查及48项架构测试通过"
          },
          {
            "source": "Next",
            "id": "DV-02",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "调用链与图示已补齐；主题仅构建通过，视觉验收重开，见UX-03"
          },
          {
            "source": "Next",
            "id": "MD-01",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已实现LaunchModel/LaunchWorkspace；原UI不变，草稿版本冲突、快照与记录失败均阻止执行"
          },
          {
            "source": "Next",
            "id": "DV-03",
            "owner": "optional-diagnostics",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "用户已确认双包；LabSettings可配置VM，LaunchModel统一许可参数，默认关闭；真实VM参数回归未执行"
          },
          {
            "source": "Next",
            "id": "FM-01",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "WorkspaceHost为唯一login.launch装配入口；两包比较共用DLL哈希，具体CAF插件API适配未实施"
          },
          {
            "source": "Next",
            "id": "AD-01",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已采用无新依赖的XML清单，工厂与入口受清单约束；启动时装配，VM动作仍需本机授权；TOML解析适配/热卸载未实现"
          },
          {
            "source": "Next",
            "id": "DG-01",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "已发现三份空Error；同包校验/隔离复制通过，历史底层原因因证据丢失仍未确定，原程序未修复"
          },
          {
            "source": "Next",
            "id": "UX-04",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "新主题已实现；实际呈现与功能检查，视觉仍待评审"
          },
          {
            "source": "Next",
            "id": "UX-03",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "2026-10-31新主题已实现并检查，仍待用户视觉评审"
          },
          {
            "source": "Next",
            "id": "DOC-03",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "8幕导入、6变量/11项回写验证及逐页对照已落地，待用户视觉评审"
          },
          {
            "source": "Next",
            "id": "DOC-02",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "八幕HTML与8张FigCraft画板已交付；交互/窄屏/链接校验通过，展示不操作产品或VM"
          },
          {
            "source": "Next",
            "id": "DOC-01",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "文档入口与元信息已整理；理解成本未做计时实测"
          },
          {
            "source": "Next",
            "id": "UX-02",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "验收契约已写；尚未实施WPF"
          },
          {
            "source": "PMLTWIN",
            "id": "G1",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "G2",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "G3",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "R01",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "planned"
          },
          {
            "source": "PMLTWIN",
            "id": "R02",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R03",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R04",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R05",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R06",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R07",
            "owner": "knowledge",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "planned"
          },
          {
            "source": "PMLTWIN",
            "id": "R08",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "partial"
          },
          {
            "source": "PMLTWIN",
            "id": "R09",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "partial"
          },
          {
            "source": "PMLTWIN",
            "id": "R10",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "partial"
          },
          {
            "source": "PMLTWIN",
            "id": "R11",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "partial"
          },
          {
            "source": "PMLTWIN",
            "id": "R12",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "planned"
          },
          {
            "source": "PMLTWIN",
            "id": "R13",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R14",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R15",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R16",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R17",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R18",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R19",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "partial"
          },
          {
            "source": "PMLTWIN",
            "id": "R20",
            "owner": "knowledge",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "planned"
          },
          {
            "source": "PMLTWIN",
            "id": "R21",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R22",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R23",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "R24",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "verified"
          },
          {
            "source": "PMLTWIN",
            "id": "B01",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B02",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B03",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B04",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B05",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B06",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B07",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B08",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B09",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B10",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B11",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B12",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B13",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B14",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B15",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B16",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B17",
            "owner": "knowledge",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B18",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B19",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B20",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B21",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B22",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B23",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B24",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B25",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B29",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B30",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "B31",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T01",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T02",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T03",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T04",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T05",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T06",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T07",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T08",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T09",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T10",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T11",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T12",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T13",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T14",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "T15",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S01",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S02",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S03",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S04",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S05",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S06",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S07",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S08",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S09",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S10",
            "owner": "knowledge",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S11",
            "owner": "knowledge",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S12",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S13",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S14",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S15",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S16",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S17",
            "owner": "tools-deployment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S18",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S19",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S20",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S21",
            "owner": "design-evidence",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S22",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S23",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S24",
            "owner": "environment",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S25",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S26",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S27",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S28",
            "owner": "launch",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S29",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "S30",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "M1",
            "owner": "configuration",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "M2",
            "owner": "communication",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          },
          {
            "source": "PMLTWIN",
            "id": "M3",
            "owner": "composition",
            "mappingStatus": "source-accountability-reviewed; implementation-status-unchanged",
            "sourceStatus": "source-preserved"
          }
        ],
        "sourceRefs": [
          "source:source-mappings"
        ]
      },
      {
        "id": "reference:source-mapping-context",
        "title": "来源映射身份与适用边界",
        "columns": [
          "field",
          "value"
        ],
        "rows": [
          {
            "field": "schema",
            "value": 1
          },
          {
            "field": "policy",
            "value": "来源责任映射；不升级实现或验收状态"
          },
          {
            "field": "counts",
            "value": {
              "Archon": 49,
              "Next": 39,
              "PMLTWIN": 103
            }
          }
        ],
        "sourceRefs": [
          "source:source-mappings"
        ]
      }
    ]
  },
  "coverage": {
    "features": 18,
    "scripts": 14,
    "requirements": 24,
    "flows": 3,
    "sourceMappings": 191
  },
  "taskMap": {
    "schema": "contract-task-map/1",
    "rootId": "project:pmltwin",
    "groups": [
      {
        "id": "task-group:engineering",
        "title": "工程与连接",
        "featureIds": [
          "feature:launch",
          "feature:sessions"
        ]
      },
      {
        "id": "task-group:development",
        "title": "开发与部署",
        "featureIds": [
          "feature:deployment",
          "feature:functions",
          "feature:ribbon"
        ]
      },
      {
        "id": "task-group:query",
        "title": "数据与查询",
        "featureIds": [
          "feature:objects",
          "feature:forms",
          "feature:arrays",
          "feature:formuse"
        ]
      },
      {
        "id": "task-group:knowledge",
        "title": "知识与工具",
        "featureIds": [
          "feature:knowledge",
          "feature:activity",
          "feature:review",
          "feature:tests",
          "feature:web"
        ]
      },
      {
        "id": "task-group:platform",
        "title": "平台与扩展",
        "featureIds": [
          "feature:database",
          "feature:migration",
          "feature:caf",
          "feature:plugins"
        ]
      }
    ],
    "nodes": [
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [],
        "deliverableScope": "entity-contract",
        "limitations": [
          "本图谱不执行登录、部署或宏，不是第二份生效规范",
          "没有执行数据库切换",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块",
          "未整体切换活动存储",
          "未真实注册宿主",
          "未界面挂载"
        ],
        "sourceRefs": [
          "source:canonical",
          "source:capabilities",
          "source:overview",
          "source:presentation",
          "source:evidence",
          "source:source-mappings",
          "source:openspec-proposal",
          "source:openspec-contracts",
          "source:openspec-design",
          "source:openspec-tasks",
          "source:validator",
          "source:render-model",
          "source:evidence-migration",
          "source:evidence-ribbon-ui",
          "source:evidence-ribbon-runtime",
          "source:evidence-database",
          "source:evidence-caf"
        ],
        "requirementIds": [
          "canonical:R01",
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R07",
          "canonical:R08",
          "canonical:R09",
          "canonical:R10",
          "canonical:R11",
          "canonical:R12",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R17",
          "canonical:R18",
          "canonical:R19",
          "canonical:R20",
          "canonical:R21",
          "canonical:R22",
          "canonical:R23",
          "canonical:R24"
        ],
        "requirementStates": [
          {
            "id": "canonical:R01",
            "originalId": "R01",
            "status": "planned"
          },
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R07",
            "originalId": "R07",
            "status": "planned"
          },
          {
            "id": "canonical:R08",
            "originalId": "R08",
            "status": "partial"
          },
          {
            "id": "canonical:R09",
            "originalId": "R09",
            "status": "partial"
          },
          {
            "id": "canonical:R10",
            "originalId": "R10",
            "status": "partial"
          },
          {
            "id": "canonical:R11",
            "originalId": "R11",
            "status": "partial"
          },
          {
            "id": "canonical:R12",
            "originalId": "R12",
            "status": "planned"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          },
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          },
          {
            "id": "canonical:R19",
            "originalId": "R19",
            "status": "partial"
          },
          {
            "id": "canonical:R20",
            "originalId": "R20",
            "status": "planned"
          },
          {
            "id": "canonical:R21",
            "originalId": "R21",
            "status": "verified"
          },
          {
            "id": "canonical:R22",
            "originalId": "R22",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          },
          {
            "id": "canonical:R24",
            "originalId": "R24",
            "status": "verified"
          }
        ],
        "verificationIds": [
          "verification:migration-offline",
          "verification:ribbon-ui",
          "verification:ribbon-runtime",
          "verification:database-offline",
          "verification:caf-static"
        ],
        "verificationStates": [
          {
            "id": "verification:migration-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "三格式只读预览与敏感剔除专测14项通过。",
            "exclusions": [
              "没有执行数据库切换"
            ],
            "evidenceIds": [
              "evidence:migration"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          },
          {
            "id": "verification:database-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
            "exclusions": [
              "未整体切换活动存储"
            ],
            "evidenceIds": [
              "evidence:database"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:caf-static",
            "scope": "static",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "CAF"
            },
            "claim": "真实SDK接口编译与生命周期隔离核查。",
            "exclusions": [
              "未真实注册宿主",
              "未界面挂载"
            ],
            "evidenceIds": [
              "evidence:caf"
            ],
            "environmentText": "产品：CAF"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:launch",
          "flow:ribbon",
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "project:pmltwin",
        "kind": "project",
        "entityId": "project:pmltwin",
        "title": "PMLTWIN",
        "summary": "让发现、配置、受控执行和证据围绕同一任务协作。",
        "entries": [
          {
            "id": "project:pmltwin/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "project:pmltwin/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "project:pmltwin/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "project:pmltwin/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "project:pmltwin/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "project:pmltwin/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:engineering",
        "flowId": null,
        "stepId": null,
        "deliverables": [],
        "deliverableScope": "entity-contract",
        "limitations": [],
        "sourceRefs": [
          "source:render-model"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [
          "flow:launch"
        ],
        "noFlowReason": null,
        "id": "task-group:engineering",
        "kind": "group",
        "entityId": "project:pmltwin",
        "title": "工程与连接",
        "summary": "仅供阅读归类，不表示执行顺序、可用性或交付完成。",
        "entries": [
          {
            "id": "task-group:engineering/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:engineering/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:engineering/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "task-group:engineering/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:engineering/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:engineering/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:engineering",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "候选来源",
          "启动计划与关联回执"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [
          "flow:launch"
        ],
        "noFlowReason": null,
        "id": "feature:launch",
        "kind": "feature",
        "entityId": "feature:launch",
        "title": "工程发现与启动",
        "summary": "冻结一次启动，保存修订并观察原编号。",
        "entries": [
          {
            "id": "feature:launch/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:launch/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:launch/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:launch/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:launch/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:launch/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:engineering",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "身份与协议证据"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "发现不等于登录"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R17"
        ],
        "requirementStates": [
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:sessions",
        "kind": "feature",
        "entityId": "feature:sessions",
        "title": "会话发现与身份核验",
        "summary": "每次调用绑定明确目标身份。",
        "entries": [
          {
            "id": "feature:sessions/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:sessions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:sessions/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:sessions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:sessions/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:sessions",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:sessions/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:sessions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:sessions/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:sessions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:sessions/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:development",
        "flowId": null,
        "stepId": null,
        "deliverables": [],
        "deliverableScope": "entity-contract",
        "limitations": [],
        "sourceRefs": [
          "source:render-model"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "task-group:development",
        "kind": "group",
        "entityId": "project:pmltwin",
        "title": "开发与部署",
        "summary": "仅供阅读归类，不表示执行顺序、可用性或交付完成。",
        "entries": [
          {
            "id": "task-group:development/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:development/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:development/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "task-group:development/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:development/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:development/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:development",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "逐文件差异",
          "备份及恢复回执"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "复制成功不等于运行时已加载"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R10",
          "canonical:R19"
        ],
        "requirementStates": [
          {
            "id": "canonical:R10",
            "originalId": "R10",
            "status": "partial"
          },
          {
            "id": "canonical:R19",
            "originalId": "R19",
            "status": "partial"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "partial",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:deployment",
        "kind": "feature",
        "entityId": "feature:deployment",
        "title": "文件与运行目录部署",
        "summary": "冻结文件差异，备份并有条件恢复。",
        "entries": [
          {
            "id": "feature:deployment/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:deployment/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:deployment/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:deployment/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:deployment/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:deployment/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:development",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "签名列表与调用回执"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "注册不表示Host已加载"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R08",
          "canonical:R11",
          "canonical:R16",
          "canonical:R17"
        ],
        "requirementStates": [
          {
            "id": "canonical:R08",
            "originalId": "R08",
            "status": "partial"
          },
          {
            "id": "canonical:R11",
            "originalId": "R11",
            "status": "partial"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:functions",
        "kind": "feature",
        "entityId": "feature:functions",
        "title": "函数登记、许可与调用",
        "summary": "源码签名、许可和Host加载分别验证。",
        "entries": [
          {
            "id": "feature:functions/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:functions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:functions/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:functions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:functions/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:functions",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:functions/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:functions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:functions/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:functions",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:functions/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:development",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "差异、备份和分层回执"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": {
          "status": "partial",
          "scope": "明确平台切片；未声称整体完成",
          "sourceRefs": [
            "source:overview"
          ]
        },
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "feature:ribbon",
        "kind": "feature",
        "entityId": "feature:ribbon",
        "title": "Ribbon编辑与冻结部署",
        "summary": "编辑历史、磁盘部署和实际加载分开。",
        "entries": [
          {
            "id": "feature:ribbon/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:ribbon",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:ribbon/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:ribbon",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:ribbon/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:ribbon",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:ribbon/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:ribbon",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:ribbon/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:ribbon",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:ribbon/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:query",
        "flowId": null,
        "stepId": null,
        "deliverables": [],
        "deliverableScope": "entity-contract",
        "limitations": [],
        "sourceRefs": [
          "source:render-model"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": null,
        "id": "task-group:query",
        "kind": "group",
        "entityId": "project:pmltwin",
        "title": "数据与查询",
        "summary": "仅供阅读归类，不表示执行顺序、可用性或交付完成。",
        "entries": [
          {
            "id": "task-group:query/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:query/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:query/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "task-group:query/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:query/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:query/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:query",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "原始数据与读取诊断"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "对象身份不能跨工程永久复用"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R09",
          "canonical:R17",
          "canonical:R18"
        ],
        "requirementStates": [
          {
            "id": "canonical:R09",
            "originalId": "R09",
            "status": "partial"
          },
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          },
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:objects",
        "kind": "feature",
        "entityId": "feature:objects",
        "title": "对象与全局数据查询",
        "summary": "读取当前对象、指定元素及全局路径。",
        "entries": [
          {
            "id": "feature:objects/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:objects",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:objects/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:objects",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:objects/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:objects",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:objects/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:objects",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:objects/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:objects",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:objects/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:query",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "元数据、原生缺失值与续页"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "不执行业务回调"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R09",
          "canonical:R17",
          "canonical:R18"
        ],
        "requirementStates": [
          {
            "id": "canonical:R09",
            "originalId": "R09",
            "status": "partial"
          },
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          },
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:forms",
        "kind": "feature",
        "entityId": "feature:forms",
        "title": "原生Form与Gadget查询",
        "summary": "通过既有元数据读取窗体和控件。",
        "entries": [
          {
            "id": "feature:forms/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:forms",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:forms/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:forms",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:forms/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:forms",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:forms/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:forms",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:forms/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:forms",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:forms/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:query",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "原始行、截断和下一索引"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "稀疏槽位不压缩"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R18"
        ],
        "requirementStates": [
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:arrays",
        "kind": "feature",
        "entityId": "feature:arrays",
        "title": "已有数组的有界读取",
        "summary": "按原生索引和字符预算读取窗口。",
        "entries": [
          {
            "id": "feature:arrays/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:arrays",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:arrays/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:arrays",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:arrays/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:arrays",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:arrays/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:arrays",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:arrays/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:arrays",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:arrays/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:query",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "原型事件和限制"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "仍为planned"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R08"
        ],
        "requirementStates": [
          {
            "id": "canonical:R08",
            "originalId": "R08",
            "status": "partial"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "planned",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:formuse",
        "kind": "feature",
        "entityId": "feature:formuse",
        "title": "FormUse跟踪原型",
        "summary": "记录原型支持范围，不冒充完整交付。",
        "entries": [
          {
            "id": "feature:formuse/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:formuse",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:formuse/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:formuse",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:formuse/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:formuse",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:formuse/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:formuse",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:formuse/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:formuse",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:formuse/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:knowledge",
        "flowId": null,
        "stepId": null,
        "deliverables": [],
        "deliverableScope": "entity-contract",
        "limitations": [],
        "sourceRefs": [
          "source:render-model"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": null,
        "id": "task-group:knowledge",
        "kind": "group",
        "entityId": "project:pmltwin",
        "title": "知识与工具",
        "summary": "仅供阅读归类，不表示执行顺序、可用性或交付完成。",
        "entries": [
          {
            "id": "task-group:knowledge/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:knowledge/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:knowledge/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "task-group:knowledge/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:knowledge/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:knowledge/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:knowledge",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "分层检查与来源定位"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "官方默认接入仍待完成"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R07",
          "canonical:R20"
        ],
        "requirementStates": [
          {
            "id": "canonical:R07",
            "originalId": "R07",
            "status": "planned"
          },
          {
            "id": "canonical:R20",
            "originalId": "R20",
            "status": "planned"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "partial",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:knowledge",
        "kind": "feature",
        "entityId": "feature:knowledge",
        "title": "分层知识与经验",
        "summary": "按产品版本保存来源、库身份和正文。",
        "entries": [
          {
            "id": "feature:knowledge/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:knowledge",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:knowledge/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:knowledge",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:knowledge/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:knowledge",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:knowledge/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:knowledge",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:knowledge/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:knowledge",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:knowledge/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:knowledge",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "状态、阶段和保留范围内证据"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "不是所有平台动作的统一协调者"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R09",
          "canonical:R21"
        ],
        "requirementStates": [
          {
            "id": "canonical:R09",
            "originalId": "R09",
            "status": "partial"
          },
          {
            "id": "canonical:R21",
            "originalId": "R21",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:activity",
        "kind": "feature",
        "entityId": "feature:activity",
        "title": "运行记录与日志",
        "summary": "关联本次动作和事实，不确定单列。",
        "entries": [
          {
            "id": "feature:activity/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:activity",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:activity/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:activity",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:activity/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:activity",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:activity/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:activity",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:activity/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:activity",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:activity/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:knowledge",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "批准快照及迁移回执"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "完整GUI仍planned"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R01",
          "canonical:R12"
        ],
        "requirementStates": [
          {
            "id": "canonical:R01",
            "originalId": "R01",
            "status": "planned"
          },
          {
            "id": "canonical:R12",
            "originalId": "R12",
            "status": "planned"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "planned",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:review",
        "kind": "feature",
        "entityId": "feature:review",
        "title": "AI草稿评审与迁移",
        "summary": "面向人工差异审核的候选桌面能力。",
        "entries": [
          {
            "id": "feature:review/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:review",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:review/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:review",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:review/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:review",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:review/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:review",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:review/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:review",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:review/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:knowledge",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "独立运行记录与证据"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "执行器存在不代表桌面中心完成"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R22",
          "canonical:R24"
        ],
        "requirementStates": [
          {
            "id": "canonical:R22",
            "originalId": "R22",
            "status": "verified"
          },
          {
            "id": "canonical:R24",
            "originalId": "R24",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "planned",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:tests",
        "kind": "feature",
        "entityId": "feature:tests",
        "title": "桌面测试执行中心",
        "summary": "复用按ID执行器，桌面中心仍待接线。",
        "entries": [
          {
            "id": "feature:tests/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:tests",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:tests/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:tests",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:tests/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:tests",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:tests/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:tests",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:tests/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:tests",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:tests/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:knowledge",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "目录和原始调用回执"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "远程分发未完成"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R11",
          "canonical:R16"
        ],
        "requirementStates": [
          {
            "id": "canonical:R11",
            "originalId": "R11",
            "status": "partial"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "partial",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:web",
        "kind": "feature",
        "entityId": "feature:web",
        "title": "本机HTML应用管理",
        "summary": "按钮参数绑定既有IPC操作。",
        "entries": [
          {
            "id": "feature:web/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:web",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:web/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:web",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:web/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:web",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:web/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:web",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:web/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:web",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:web/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:platform",
        "flowId": null,
        "stepId": null,
        "deliverables": [],
        "deliverableScope": "entity-contract",
        "limitations": [],
        "sourceRefs": [
          "source:render-model"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "task-group:platform",
        "kind": "group",
        "entityId": "project:pmltwin",
        "title": "平台与扩展",
        "summary": "仅供阅读归类，不表示执行顺序、可用性或交付完成。",
        "entries": [
          {
            "id": "task-group:platform/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:platform/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:platform/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "task-group:platform/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:platform/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "project:pmltwin",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "task-group:platform/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:platform",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "历史、提交或冲突"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "尚未整体替换活动写者",
          "未整体切换活动存储"
        ],
        "sourceRefs": [
          "source:overview",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R14"
        ],
        "requirementStates": [
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          }
        ],
        "verificationIds": [
          "verification:database-offline"
        ],
        "verificationStates": [
          {
            "id": "verification:database-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
            "exclusions": [
              "未整体切换活动存储"
            ],
            "evidenceIds": [
              "evidence:database"
            ],
            "environmentText": "产品：synthetic"
          }
        ],
        "implementation": {
          "status": "partial",
          "scope": "明确平台切片；未声称整体完成",
          "sourceRefs": [
            "source:overview"
          ]
        },
        "flowIds": [
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "feature:database",
        "kind": "feature",
        "entityId": "feature:database",
        "title": "独立应用数据库",
        "summary": "提供隔离范围的文本修订与CAS事务。",
        "entries": [
          {
            "id": "feature:database/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:database/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:database/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:database/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:database/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:database/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:platform",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "摘要、净化材料和诊断"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "不替代写入切换和恢复",
          "没有执行数据库切换"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:migration-offline"
        ],
        "verificationStates": [
          {
            "id": "verification:migration-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "三格式只读预览与敏感剔除专测14项通过。",
            "exclusions": [
              "没有执行数据库切换"
            ],
            "evidenceIds": [
              "evidence:migration"
            ],
            "environmentText": "产品：synthetic"
          }
        ],
        "implementation": {
          "status": "implemented",
          "scope": "只读迁移预览；不包含写入切换",
          "sourceRefs": [
            "source:overview"
          ]
        },
        "flowIds": [
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "feature:migration",
        "kind": "feature",
        "entityId": "feature:migration",
        "title": "只读迁移预览",
        "summary": "保留字段和历史，剔除秘密并说明不支持项。",
        "entries": [
          {
            "id": "feature:migration/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:migration/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:migration/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:migration/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:migration/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:migration/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:platform",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "只读适配描述"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "SDK编译不等于真实宿主注册或UI挂载",
          "未真实注册宿主",
          "未界面挂载"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:caf-static"
        ],
        "verificationStates": [
          {
            "id": "verification:caf-static",
            "scope": "static",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "CAF"
            },
            "claim": "真实SDK接口编译与生命周期隔离核查。",
            "exclusions": [
              "未真实注册宿主",
              "未界面挂载"
            ],
            "evidenceIds": [
              "evidence:caf"
            ],
            "environmentText": "产品：CAF"
          }
        ],
        "implementation": {
          "status": "partial",
          "scope": "明确平台切片；未声称整体完成",
          "sourceRefs": [
            "source:overview"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:caf",
        "kind": "feature",
        "entityId": "feature:caf",
        "title": "CAF薄宿主适配",
        "summary": "验证真实SDK身份，隔离旧运行时。",
        "entries": [
          {
            "id": "feature:caf/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:caf",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:caf/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:caf",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:caf/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:caf",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:caf/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:caf",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:caf/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:caf",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:caf/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": "task-group:platform",
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "状态和独立证据"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "基础包不应依赖可选实验能力"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "partial",
          "scope": "明确平台切片；未声称整体完成",
          "sourceRefs": [
            "source:overview"
          ]
        },
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "feature:plugins",
        "kind": "feature",
        "entityId": "feature:plugins",
        "title": "可选许可与VM插件",
        "summary": "显式插件清单复用既有worker边界。",
        "entries": [
          {
            "id": "feature:plugins/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:plugins",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:plugins/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:plugins",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:plugins/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:plugins",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "feature:plugins/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:plugins",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:plugins/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:plugins",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "feature:plugins/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "前台退出码、本机端点及下游操作回执；服务ready不是产品已连接。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:workbench-entry",
        "kind": "script",
        "entityId": "script:workbench-entry",
        "title": "启动工作台与本地服务编排",
        "summary": "NoBrowser/NoBuild、端口、构建配置；整合入口另接 Ribbon 数据目录与端口。",
        "entries": [
          {
            "id": "script:workbench-entry/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:workbench-entry",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:workbench-entry/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:workbench-entry",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:workbench-entry/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:workbench-entry",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:workbench-entry/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:workbench-entry",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:workbench-entry/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:workbench-entry",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:workbench-entry/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [
          "flow:launch"
        ],
        "noFlowReason": null,
        "id": "script:launch-workflow",
        "kind": "script",
        "entityId": "script:launch-workflow",
        "title": "启动计划、受理与观察",
        "summary": "profileId、expectedRevision、operationId；execute 接冻结计划、本次内存密码、requestId和启动授权。",
        "entries": [
          {
            "id": "script:launch-workflow/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:launch-workflow/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:launch-workflow/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:launch-workflow/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:launch-workflow/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:launch-workflow/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "原通信结果、分层evidence及mayHaveExecuted；stderr不是可替代业务结果的stdout。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "发现不等于登录",
          "对象身份不能跨工程永久复用",
          "注册不表示Host已加载",
          "不执行业务回调"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R17",
          "canonical:R09",
          "canonical:R18",
          "canonical:R08",
          "canonical:R11",
          "canonical:R16"
        ],
        "requirementStates": [
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          },
          {
            "id": "canonical:R09",
            "originalId": "R09",
            "status": "partial"
          },
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          },
          {
            "id": "canonical:R08",
            "originalId": "R08",
            "status": "partial"
          },
          {
            "id": "canonical:R11",
            "originalId": "R11",
            "status": "partial"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:communication-client",
        "kind": "script",
        "entityId": "script:communication-client",
        "title": "通信CLI子进程边界",
        "summary": "已注册通信操作、JSON stdin、requestId；目标操作含PID、启动时间、模块及明确授权。",
        "entries": [
          {
            "id": "script:communication-client/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:communication-client",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:communication-client/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:communication-client",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:communication-client/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:communication-client",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:communication-client/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:communication-client",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:communication-client/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:communication-client",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:communication-client/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "DeploymentReceipt、应用异常中的部分结果、DeploymentRestoreReceipt。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "复制成功不等于运行时已加载"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R10",
          "canonical:R19"
        ],
        "requirementStates": [
          {
            "id": "canonical:R10",
            "originalId": "R10",
            "status": "partial"
          },
          {
            "id": "canonical:R19",
            "originalId": "R19",
            "status": "partial"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:frozen-copy",
        "kind": "script",
        "entityId": "script:frozen-copy",
        "title": "冻结复制与条件恢复",
        "summary": "源、插件专用注册目标、冻结文件计划和摘要；恢复接原回执。",
        "entries": [
          {
            "id": "script:frozen-copy/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:frozen-copy",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:frozen-copy/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:frozen-copy",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:frozen-copy/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:frozen-copy",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:frozen-copy/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:frozen-copy",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:frozen-copy/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:frozen-copy",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:frozen-copy/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "每步执行前保存状态、原始调用与响应；失败或unknown停止，最后写rpc证据。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "复制成功不等于运行时已加载",
          "注册不表示Host已加载",
          "不执行业务回调"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R10",
          "canonical:R19",
          "canonical:R08",
          "canonical:R11",
          "canonical:R16",
          "canonical:R17",
          "canonical:R09",
          "canonical:R18"
        ],
        "requirementStates": [
          {
            "id": "canonical:R10",
            "originalId": "R10",
            "status": "partial"
          },
          {
            "id": "canonical:R19",
            "originalId": "R19",
            "status": "partial"
          },
          {
            "id": "canonical:R08",
            "originalId": "R08",
            "status": "partial"
          },
          {
            "id": "canonical:R11",
            "originalId": "R11",
            "status": "partial"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          },
          {
            "id": "canonical:R09",
            "originalId": "R09",
            "status": "partial"
          },
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:pml-development",
        "kind": "script",
        "entityId": "script:pml-development",
        "title": "PML部署、重载与验证",
        "summary": "CLI、源目录、注册PMLLIB、schema1清单、PID/ExpectedModel、独立证据目录与AllowExecution。",
        "entries": [
          {
            "id": "script:pml-development/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:pml-development",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:pml-development/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:pml-development",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:pml-development/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:pml-development",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:pml-development/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:pml-development",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:pml-development/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:pml-development",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:pml-development/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "Rows、StartIndex、NextIndex、Truncated、Characters及IndexUpperBound。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "稀疏槽位不压缩"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R18"
        ],
        "requirementStates": [
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:array-window",
        "kind": "script",
        "entityId": "script:array-window",
        "title": "数组窗口算法",
        "summary": "原生MAXINDEX上界、startIndex、maxItems、maxCharacters与槽位读取委托。",
        "entries": [
          {
            "id": "script:array-window/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:array-window",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:array-window/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:array-window",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:array-window/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:array-window",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:array-window/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:array-window",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:array-window/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:array-window",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:array-window/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "返回ARRAY；若目标无效或部分错误也可能是空数组，因此必须结合外层成功/错误证据。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "对象身份不能跨工程永久复用"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R09",
          "canonical:R17",
          "canonical:R18"
        ],
        "requirementStates": [
          {
            "id": "canonical:R09",
            "originalId": "R09",
            "status": "partial"
          },
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          },
          {
            "id": "canonical:R18",
            "originalId": "R18",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:dbref-reader",
        "kind": "script",
        "entityId": "script:dbref-reader",
        "title": "DBREF对象读取与恢复",
        "summary": "size/depth/startStr/name；name支持短名、绝对名或DBREF字符串。",
        "entries": [
          {
            "id": "script:dbref-reader/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:dbref-reader",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:dbref-reader/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:dbref-reader",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:dbref-reader/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:dbref-reader",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:dbref-reader/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:dbref-reader",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:dbref-reader/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:dbref-reader",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:dbref-reader/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "产品/版本/模块/profile/framework信息及策略报告；路径存在不是Host握手成功。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验",
          "发现不等于登录"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23",
          "canonical:R17"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          },
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:host-bootstrap",
        "kind": "script",
        "entityId": "script:host-bootstrap",
        "title": "Host解析与启动策略",
        "summary": "Current Session/BANNER、PMLUI根、进程环境中的启动策略及可选override。",
        "entries": [
          {
            "id": "script:host-bootstrap/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:host-bootstrap",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:host-bootstrap/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:host-bootstrap",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:host-bootstrap/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:host-bootstrap",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:host-bootstrap/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:host-bootstrap",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:host-bootstrap/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:host-bootstrap",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:host-bootstrap/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "workspace/deployment/activation分列；deployed仅磁盘成功，runtimeVerified始终false；不确认写入为RIBBON_RESULT_UNKNOWN。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "script:ribbon-frozen-deployment",
        "kind": "script",
        "entityId": "script:ribbon-frozen-deployment",
        "title": "Ribbon冻结部署与回执映射",
        "summary": "preview是project及选定模块信息；apply严格为planId/planDigest，写授权独立。",
        "entries": [
          {
            "id": "script:ribbon-frozen-deployment/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ribbon-frozen-deployment/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ribbon-frozen-deployment/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:ribbon-frozen-deployment/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ribbon-frozen-deployment/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ribbon-frozen-deployment/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "schema1报告含activate=false、complete、文件SHA256、sourceVersion、净化content与code/file/path诊断；不完整操作退出码2。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "尚未整体替换活动写者",
          "不替代写入切换和恢复",
          "未整体切换活动存储",
          "没有执行数据库切换"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R14"
        ],
        "requirementStates": [
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          }
        ],
        "verificationIds": [
          "verification:database-offline",
          "verification:migration-offline"
        ],
        "verificationStates": [
          {
            "id": "verification:database-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
            "exclusions": [
              "未整体切换活动存储"
            ],
            "evidenceIds": [
              "evidence:database"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:migration-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "三格式只读预览与敏感剔除专测14项通过。",
            "exclusions": [
              "没有执行数据库切换"
            ],
            "evidenceIds": [
              "evidence:migration"
            ],
            "environmentText": "产品：synthetic"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "script:migration-preview",
        "kind": "script",
        "entityId": "script:migration-preview",
        "title": "只读迁移材料解析",
        "summary": "rewrite/next/ribbon和本机绝对source；不自动跟随portable重定向。",
        "entries": [
          {
            "id": "script:migration-preview/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:migration-preview/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:migration-preview/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:migration-preview/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:migration-preview/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:migration-preview/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "库身份、数据库SHA256、路径、检查项和来源证据状态；数据库身份与chunk ID共同定位正文。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "官方默认接入仍待完成"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R07",
          "canonical:R20"
        ],
        "requirementStates": [
          {
            "id": "canonical:R07",
            "originalId": "R07",
            "status": "planned"
          },
          {
            "id": "canonical:R20",
            "originalId": "R20",
            "status": "planned"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:knowledge-import",
        "kind": "script",
        "entityId": "script:knowledge-import",
        "title": "知识双路导入",
        "summary": "product/version/role/name与选定资料源或SQLite；apply使用缓存计划引用。",
        "entries": [
          {
            "id": "script:knowledge-import/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:knowledge-import",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:knowledge-import/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:knowledge-import",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:knowledge-import/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:knowledge-import",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:knowledge-import/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:knowledge-import",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:knowledge-import/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:knowledge-import",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:knowledge-import/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "commit/dirty、SDK/系统、执行数量、console/TRX/summary、源码身份及关联截图；历史结果不冒充本轮。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "执行器存在不代表桌面中心完成"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R22",
          "canonical:R24"
        ],
        "requirementStates": [
          {
            "id": "canonical:R22",
            "originalId": "R22",
            "status": "verified"
          },
          {
            "id": "canonical:R24",
            "originalId": "R24",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [],
        "noFlowReason": "此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。",
        "id": "script:sdk-acceptance",
        "kind": "script",
        "entityId": "script:sdk-acceptance",
        "title": "SDK选择与按ID验证",
        "summary": "仓库SDK版本、明确TestId/Describe、相应测试入口与独立报告目录。",
        "entries": [
          {
            "id": "script:sdk-acceptance/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:sdk-acceptance",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:sdk-acceptance/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:sdk-acceptance",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:sdk-acceptance/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:sdk-acceptance",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:sdk-acceptance/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:sdk-acceptance",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:sdk-acceptance/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:sdk-acceptance",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:sdk-acceptance/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "UI显示原编号、磁盘状态、未核验加载与人工核对出口；pending本身只说明可能未决，不证明服务器已受理或未执行。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:overview"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "script:ui-pending-gate",
        "kind": "script",
        "entityId": "script:ui-pending-gate",
        "title": "UI发送前持久门禁",
        "summary": "已确认的v2计划引用，或明确标记的v1首次生成输入；记录非秘密planId/digest/kind/端点，不记录项目口令。",
        "entries": [
          {
            "id": "script:ui-pending-gate/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ui-pending-gate/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ui-pending-gate/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:ui-pending-gate/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ui-pending-gate/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:ui-pending-gate/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": null,
        "stepId": null,
        "deliverables": [
          "部署时仍为磁盘成功、运行时未核验；后续外观通过作为独立事实追加。"
        ],
        "deliverableScope": "entity-contract",
        "limitations": [
          "发现不等于登录",
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:presentation",
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R17"
        ],
        "requirementStates": [
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          }
        ],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "script:runtime-verification",
        "kind": "script",
        "entityId": "script:runtime-verification",
        "title": "独立运行时验收与收尾",
        "summary": "明确授权的独立测试界面文件、单模块登记、目标产品与冻结备份。",
        "entries": [
          {
            "id": "script:runtime-verification/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:runtime-verification/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:runtime-verification/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": null,
              "stepId": null
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "script:runtime-verification/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:runtime-verification/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": null,
              "stepId": null
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "script:runtime-verification/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:launch",
        "stepId": "step:launch-discover",
        "deliverables": [
          "候选来源",
          "启动计划与关联回执"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验"
        ],
        "sourceRefs": [
          "source:capabilities",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": {
          "status": "implemented",
          "scope": "既有目录登记状态；完整平台范围见限制与独立证据",
          "sourceRefs": [
            "source:capabilities"
          ]
        },
        "flowIds": [
          "flow:launch"
        ],
        "noFlowReason": null,
        "id": "step:launch-discover",
        "kind": "step",
        "entityId": "feature:launch",
        "title": "解释候选",
        "summary": "保留来源与歧义。",
        "entries": [
          {
            "id": "step:launch-discover/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": "flow:launch",
              "stepId": "step:launch-discover"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-discover/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": "flow:launch",
              "stepId": "step:launch-discover"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-discover/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": "flow:launch",
              "stepId": "step:launch-discover"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:launch-discover/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": "flow:launch",
              "stepId": "step:launch-discover"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-discover/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:launch",
              "flowId": "flow:launch",
              "stepId": "step:launch-discover"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-discover/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:launch",
        "stepId": "step:launch-plan",
        "deliverables": [
          "operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [
          "flow:launch"
        ],
        "noFlowReason": null,
        "id": "step:launch-plan",
        "kind": "step",
        "entityId": "script:launch-workflow",
        "title": "冻结计划",
        "summary": "修订与文件摘要绑定。",
        "entries": [
          {
            "id": "step:launch-plan/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-plan"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-plan/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-plan"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-plan/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-plan"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:launch-plan/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-plan"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-plan/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-plan"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-plan/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:launch",
        "stepId": "step:launch-execute",
        "deliverables": [
          "operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [
          "flow:launch"
        ],
        "noFlowReason": null,
        "id": "step:launch-execute",
        "kind": "step",
        "entityId": "script:launch-workflow",
        "title": "受理再执行",
        "summary": "持久受理失败不启动。",
        "entries": [
          {
            "id": "step:launch-execute/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-execute"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-execute/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-execute"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-execute/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-execute"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:launch-execute/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-execute"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-execute/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-execute"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-execute/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:launch",
        "stepId": "step:launch-observe",
        "deliverables": [
          "operationId/attemptId/requestId、state/phase/code/message、入口身份、观察与Host未核验。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "入口创建不等于登录或Host已核验"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R02",
          "canonical:R03",
          "canonical:R04",
          "canonical:R05",
          "canonical:R06",
          "canonical:R13",
          "canonical:R14",
          "canonical:R15",
          "canonical:R16",
          "canonical:R23"
        ],
        "requirementStates": [
          {
            "id": "canonical:R02",
            "originalId": "R02",
            "status": "verified"
          },
          {
            "id": "canonical:R03",
            "originalId": "R03",
            "status": "verified"
          },
          {
            "id": "canonical:R04",
            "originalId": "R04",
            "status": "verified"
          },
          {
            "id": "canonical:R05",
            "originalId": "R05",
            "status": "verified"
          },
          {
            "id": "canonical:R06",
            "originalId": "R06",
            "status": "verified"
          },
          {
            "id": "canonical:R13",
            "originalId": "R13",
            "status": "verified"
          },
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          },
          {
            "id": "canonical:R15",
            "originalId": "R15",
            "status": "verified"
          },
          {
            "id": "canonical:R16",
            "originalId": "R16",
            "status": "verified"
          },
          {
            "id": "canonical:R23",
            "originalId": "R23",
            "status": "verified"
          }
        ],
        "verificationIds": [],
        "verificationStates": [],
        "implementation": null,
        "flowIds": [
          "flow:launch"
        ],
        "noFlowReason": null,
        "id": "step:launch-observe",
        "kind": "step",
        "entityId": "script:launch-workflow",
        "title": "观察原编号",
        "summary": "unknown不重放。",
        "entries": [
          {
            "id": "step:launch-observe/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-observe"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-observe/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-observe"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-observe/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-observe"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:launch-observe/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-observe"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-observe/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:launch-workflow",
              "flowId": "flow:launch",
              "stepId": "step:launch-observe"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:launch-observe/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:ribbon",
        "stepId": "step:ribbon-preview",
        "deliverables": [
          "workspace/deployment/activation分列；deployed仅磁盘成功，runtimeVerified始终false；不确认写入为RIBBON_RESULT_UNKNOWN。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "step:ribbon-preview",
        "kind": "step",
        "entityId": "script:ribbon-frozen-deployment",
        "title": "冻结差异",
        "summary": "绑定文档、目标与期限。",
        "entries": [
          {
            "id": "step:ribbon-preview/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-preview"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-preview/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-preview"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-preview/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-preview"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:ribbon-preview/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-preview"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-preview/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-preview"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-preview/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:ribbon",
        "stepId": "step:ribbon-pending",
        "deliverables": [
          "UI显示原编号、磁盘状态、未核验加载与人工核对出口；pending本身只说明可能未决，不证明服务器已受理或未执行。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "step:ribbon-pending",
        "kind": "step",
        "entityId": "script:ui-pending-gate",
        "title": "先记录pending",
        "summary": "写前持久，跨页面互斥。",
        "entries": [
          {
            "id": "step:ribbon-pending/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-pending"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-pending/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-pending"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-pending/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-pending"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:ribbon-pending/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-pending"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-pending/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-pending"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-pending/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:ribbon",
        "stepId": "step:ribbon-apply",
        "deliverables": [
          "workspace/deployment/activation分列；deployed仅磁盘成功，runtimeVerified始终false；不确认写入为RIBBON_RESULT_UNKNOWN。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "step:ribbon-apply",
        "kind": "step",
        "entityId": "script:ribbon-frozen-deployment",
        "title": "仅发送原引用",
        "summary": "写后哈希核验。",
        "entries": [
          {
            "id": "step:ribbon-apply/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-apply"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-apply/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-apply"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-apply/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-apply"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:ribbon-apply/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-apply"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-apply/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:ribbon-frozen-deployment",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-apply"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-apply/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:ribbon",
        "stepId": "step:ribbon-check",
        "deliverables": [
          "UI显示原编号、磁盘状态、未核验加载与人工核对出口；pending本身只说明可能未决，不证明服务器已受理或未执行。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "step:ribbon-check",
        "kind": "step",
        "entityId": "script:ui-pending-gate",
        "title": "人工核对",
        "summary": "不确定时暂停，不自动重放。",
        "entries": [
          {
            "id": "step:ribbon-check/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-check"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-check/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-check"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-check/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-check"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:ribbon-check/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-check"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-check/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:ui-pending-gate",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-check"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-check/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:ribbon",
        "stepId": "step:ribbon-runtime",
        "deliverables": [
          "部署时仍为磁盘成功、运行时未核验；后续外观通过作为独立事实追加。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "发现不等于登录",
          "首次新文件为v1兼容",
          "限定运行时外观通过，未点击宏",
          "不是UI全程实机验收",
          "首次新建仍为v1兼容",
          "未点击宏",
          "直接HTTP部署，不是UI全程",
          "不外推其他产品与模块"
        ],
        "sourceRefs": [
          "source:presentation",
          "source:overview"
        ],
        "requirementIds": [
          "canonical:R17"
        ],
        "requirementStates": [
          {
            "id": "canonical:R17",
            "originalId": "R17",
            "status": "verified"
          }
        ],
        "verificationIds": [
          "verification:ribbon-ui",
          "verification:ribbon-runtime"
        ],
        "verificationStates": [
          {
            "id": "verification:ribbon-ui",
            "scope": "ui",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
            "exclusions": [
              "不是UI全程实机验收",
              "首次新建仍为v1兼容"
            ],
            "evidenceIds": [
              "evidence:ribbon-ui"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:ribbon-runtime",
            "scope": "runtime",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "E3D",
              "version": "2.1",
              "module": "DESIGN"
            },
            "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
            "exclusions": [
              "未点击宏",
              "直接HTTP部署，不是UI全程",
              "不外推其他产品与模块"
            ],
            "evidenceIds": [
              "evidence:ribbon-runtime"
            ],
            "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:ribbon"
        ],
        "noFlowReason": null,
        "id": "step:ribbon-runtime",
        "kind": "step",
        "entityId": "script:runtime-verification",
        "title": "独立核验加载",
        "summary": "产品范围、实际UI与未执行宏分别记录。",
        "entries": [
          {
            "id": "step:ribbon-runtime/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-runtime"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-runtime/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-runtime"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-runtime/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-runtime"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:ribbon-runtime/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-runtime"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-runtime/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:runtime-verification",
              "flowId": "flow:ribbon",
              "stepId": "step:ribbon-runtime"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:ribbon-runtime/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:migration",
        "stepId": "step:migration-read",
        "deliverables": [
          "schema1报告含activate=false、complete、文件SHA256、sourceVersion、净化content与code/file/path诊断；不完整操作退出码2。"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "尚未整体替换活动写者",
          "不替代写入切换和恢复",
          "未整体切换活动存储",
          "没有执行数据库切换"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R14"
        ],
        "requirementStates": [
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          }
        ],
        "verificationIds": [
          "verification:database-offline",
          "verification:migration-offline"
        ],
        "verificationStates": [
          {
            "id": "verification:database-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
            "exclusions": [
              "未整体切换活动存储"
            ],
            "evidenceIds": [
              "evidence:database"
            ],
            "environmentText": "产品：synthetic"
          },
          {
            "id": "verification:migration-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "三格式只读预览与敏感剔除专测14项通过。",
            "exclusions": [
              "没有执行数据库切换"
            ],
            "evidenceIds": [
              "evidence:migration"
            ],
            "environmentText": "产品：synthetic"
          }
        ],
        "implementation": null,
        "flowIds": [
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "step:migration-read",
        "kind": "step",
        "entityId": "script:migration-preview",
        "title": "受限读取",
        "summary": "明确来源与预算。",
        "entries": [
          {
            "id": "step:migration-read/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": "flow:migration",
              "stepId": "step:migration-read"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-read/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": "flow:migration",
              "stepId": "step:migration-read"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-read/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": "flow:migration",
              "stepId": "step:migration-read"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:migration-read/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": "flow:migration",
              "stepId": "step:migration-read"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-read/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "script:migration-preview",
              "flowId": "flow:migration",
              "stepId": "step:migration-read"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-read/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:migration",
        "stepId": "step:migration-review",
        "deliverables": [
          "摘要、净化材料和诊断"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "不替代写入切换和恢复",
          "没有执行数据库切换"
        ],
        "sourceRefs": [
          "source:overview",
          "source:presentation"
        ],
        "requirementIds": [],
        "requirementStates": [],
        "verificationIds": [
          "verification:migration-offline"
        ],
        "verificationStates": [
          {
            "id": "verification:migration-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "三格式只读预览与敏感剔除专测14项通过。",
            "exclusions": [
              "没有执行数据库切换"
            ],
            "evidenceIds": [
              "evidence:migration"
            ],
            "environmentText": "产品：synthetic"
          }
        ],
        "implementation": {
          "status": "implemented",
          "scope": "只读迁移预览；不包含写入切换",
          "sourceRefs": [
            "source:overview"
          ]
        },
        "flowIds": [
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "step:migration-review",
        "kind": "step",
        "entityId": "feature:migration",
        "title": "净化与审查",
        "summary": "保留历史，剔除秘密，不激活。",
        "entries": [
          {
            "id": "step:migration-review/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": "flow:migration",
              "stepId": "step:migration-review"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-review/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": "flow:migration",
              "stepId": "step:migration-review"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-review/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": "flow:migration",
              "stepId": "step:migration-review"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:migration-review/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": "flow:migration",
              "stepId": "step:migration-review"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-review/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:migration",
              "flowId": "flow:migration",
              "stepId": "step:migration-review"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-review/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      },
      {
        "groupId": null,
        "flowId": "flow:migration",
        "stepId": "step:migration-future",
        "deliverables": [
          "历史、提交或冲突"
        ],
        "deliverableScope": "associated-contract",
        "limitations": [
          "尚未整体替换活动写者",
          "未整体切换活动存储"
        ],
        "sourceRefs": [
          "source:overview",
          "source:canonical",
          "source:presentation"
        ],
        "requirementIds": [
          "canonical:R14"
        ],
        "requirementStates": [
          {
            "id": "canonical:R14",
            "originalId": "R14",
            "status": "verified"
          }
        ],
        "verificationIds": [
          "verification:database-offline"
        ],
        "verificationStates": [
          {
            "id": "verification:database-offline",
            "scope": "offline",
            "result": "passed",
            "observedAt": "2026-10-31",
            "environment": {
              "product": "synthetic"
            },
            "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
            "exclusions": [
              "未整体切换活动存储"
            ],
            "evidenceIds": [
              "evidence:database"
            ],
            "environmentText": "产品：synthetic"
          }
        ],
        "implementation": {
          "status": "partial",
          "scope": "明确平台切片；未声称整体完成",
          "sourceRefs": [
            "source:overview"
          ]
        },
        "flowIds": [
          "flow:migration"
        ],
        "noFlowReason": null,
        "id": "step:migration-future",
        "kind": "step",
        "entityId": "feature:database",
        "title": "未来显式切换",
        "summary": "切换、备份与恢复尚未闭环。",
        "entries": [
          {
            "id": "step:migration-future/document/map",
            "kind": "document",
            "label": "任务地图",
            "branch": "map",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": "flow:migration",
              "stepId": "step:migration-future"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-future/document/contract",
            "kind": "document",
            "label": "完整契约",
            "branch": "contract",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": "flow:migration",
              "stepId": "step:migration-future"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-future/document/methods",
            "kind": "document",
            "label": "方法与分析范围",
            "branch": "methods",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": "flow:migration",
              "stepId": "step:migration-future"
            },
            "reason": "仅文档入口；方法可用性及过期状态以同修订内部目录为准。"
          },
          {
            "id": "step:migration-future/document/evidence",
            "kind": "document",
            "label": "证据与来源",
            "branch": "evidence",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": "flow:migration",
              "stepId": "step:migration-future"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-future/document/catalog",
            "kind": "document",
            "label": "完整目录与规范",
            "branch": "catalog",
            "availability": "available",
            "target": {
              "entityId": "feature:database",
              "flowId": "flow:migration",
              "stepId": "step:migration-future"
            },
            "reason": "只读文档导航，不执行工程动作。"
          },
          {
            "id": "step:migration-future/product",
            "kind": "product",
            "label": "实际功能入口",
            "branch": null,
            "availability": "unavailable",
            "target": null,
            "reason": "跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。"
          }
        ]
      }
    ],
    "edges": [
      {
        "id": "task-edge/contains/structure/project:pmltwin/task-group:engineering/0",
        "from": "project:pmltwin",
        "to": "task-group:engineering",
        "kind": "contains",
        "label": "阅读分组",
        "flowId": null,
        "sourceRefs": []
      },
      {
        "id": "task-edge/contains/structure/task-group:engineering/feature:launch/0",
        "from": "task-group:engineering",
        "to": "feature:launch",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:engineering/feature:sessions/0",
        "from": "task-group:engineering",
        "to": "feature:sessions",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/project:pmltwin/task-group:development/0",
        "from": "project:pmltwin",
        "to": "task-group:development",
        "kind": "contains",
        "label": "阅读分组",
        "flowId": null,
        "sourceRefs": []
      },
      {
        "id": "task-edge/contains/structure/task-group:development/feature:deployment/0",
        "from": "task-group:development",
        "to": "feature:deployment",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:development/feature:functions/0",
        "from": "task-group:development",
        "to": "feature:functions",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:development/feature:ribbon/0",
        "from": "task-group:development",
        "to": "feature:ribbon",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/contains/structure/project:pmltwin/task-group:query/0",
        "from": "project:pmltwin",
        "to": "task-group:query",
        "kind": "contains",
        "label": "阅读分组",
        "flowId": null,
        "sourceRefs": []
      },
      {
        "id": "task-edge/contains/structure/task-group:query/feature:objects/0",
        "from": "task-group:query",
        "to": "feature:objects",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:query/feature:forms/0",
        "from": "task-group:query",
        "to": "feature:forms",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:query/feature:arrays/0",
        "from": "task-group:query",
        "to": "feature:arrays",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:query/feature:formuse/0",
        "from": "task-group:query",
        "to": "feature:formuse",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/project:pmltwin/task-group:knowledge/0",
        "from": "project:pmltwin",
        "to": "task-group:knowledge",
        "kind": "contains",
        "label": "阅读分组",
        "flowId": null,
        "sourceRefs": []
      },
      {
        "id": "task-edge/contains/structure/task-group:knowledge/feature:knowledge/0",
        "from": "task-group:knowledge",
        "to": "feature:knowledge",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:knowledge/feature:activity/0",
        "from": "task-group:knowledge",
        "to": "feature:activity",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:knowledge/feature:review/0",
        "from": "task-group:knowledge",
        "to": "feature:review",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:knowledge/feature:tests/0",
        "from": "task-group:knowledge",
        "to": "feature:tests",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:knowledge/feature:web/0",
        "from": "task-group:knowledge",
        "to": "feature:web",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/project:pmltwin/task-group:platform/0",
        "from": "project:pmltwin",
        "to": "task-group:platform",
        "kind": "contains",
        "label": "阅读分组",
        "flowId": null,
        "sourceRefs": []
      },
      {
        "id": "task-edge/contains/structure/task-group:platform/feature:database/0",
        "from": "task-group:platform",
        "to": "feature:database",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:overview",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:platform/feature:migration/0",
        "from": "task-group:platform",
        "to": "feature:migration",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:platform/feature:caf/0",
        "from": "task-group:platform",
        "to": "feature:caf",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/contains/structure/task-group:platform/feature:plugins/0",
        "from": "task-group:platform",
        "to": "feature:plugins",
        "kind": "contains",
        "label": "任务分支",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:launch/script:workbench-entry/0",
        "from": "feature:launch",
        "to": "script:workbench-entry",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:launch/script:launch-workflow/0",
        "from": "feature:launch",
        "to": "script:launch-workflow",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:sessions/script:communication-client/0",
        "from": "feature:sessions",
        "to": "script:communication-client",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:objects/script:communication-client/0",
        "from": "feature:objects",
        "to": "script:communication-client",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:forms/script:communication-client/0",
        "from": "feature:forms",
        "to": "script:communication-client",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:functions/script:communication-client/0",
        "from": "feature:functions",
        "to": "script:communication-client",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:deployment/script:frozen-copy/0",
        "from": "feature:deployment",
        "to": "script:frozen-copy",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:deployment/script:pml-development/0",
        "from": "feature:deployment",
        "to": "script:pml-development",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:forms/script:pml-development/0",
        "from": "feature:forms",
        "to": "script:pml-development",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:functions/script:pml-development/0",
        "from": "feature:functions",
        "to": "script:pml-development",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:arrays/script:array-window/0",
        "from": "feature:arrays",
        "to": "script:array-window",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:objects/script:dbref-reader/0",
        "from": "feature:objects",
        "to": "script:dbref-reader",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:launch/script:host-bootstrap/0",
        "from": "feature:launch",
        "to": "script:host-bootstrap",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:sessions/script:host-bootstrap/0",
        "from": "feature:sessions",
        "to": "script:host-bootstrap",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:ribbon/script:ribbon-frozen-deployment/0",
        "from": "feature:ribbon",
        "to": "script:ribbon-frozen-deployment",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:migration/script:migration-preview/0",
        "from": "feature:migration",
        "to": "script:migration-preview",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:database/script:migration-preview/0",
        "from": "feature:database",
        "to": "script:migration-preview",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:knowledge/script:knowledge-import/0",
        "from": "feature:knowledge",
        "to": "script:knowledge-import",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:tests/script:sdk-acceptance/0",
        "from": "feature:tests",
        "to": "script:sdk-acceptance",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:ribbon/script:ui-pending-gate/0",
        "from": "feature:ribbon",
        "to": "script:ui-pending-gate",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:ribbon/script:runtime-verification/0",
        "from": "feature:ribbon",
        "to": "script:runtime-verification",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:presentation",
          "source:overview"
        ]
      },
      {
        "id": "task-edge/uses/structure/feature:sessions/script:runtime-verification/0",
        "from": "feature:sessions",
        "to": "script:runtime-verification",
        "kind": "uses",
        "label": "关联Script，非执行顺序",
        "flowId": null,
        "sourceRefs": [
          "source:presentation",
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:launch/step:launch-discover/feature:launch/0",
        "from": "step:launch-discover",
        "to": "feature:launch",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:capabilities",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/describes/flow:launch/step:launch-plan/script:launch-workflow/0",
        "from": "step:launch-plan",
        "to": "script:launch-workflow",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:launch/step:launch-execute/script:launch-workflow/0",
        "from": "step:launch-execute",
        "to": "script:launch-workflow",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:launch/step:launch-observe/script:launch-workflow/0",
        "from": "step:launch-observe",
        "to": "script:launch-workflow",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/flow-entry/flow:launch/feature:launch/step:launch-discover/0",
        "from": "feature:launch",
        "to": "step:launch-discover",
        "kind": "flow-entry",
        "label": "进入已登记流程；非执行进度",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/next/flow:launch/step:launch-discover/step:launch-plan/0",
        "from": "step:launch-discover",
        "to": "step:launch-plan",
        "kind": "next",
        "label": "确定目标",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/next/flow:launch/step:launch-plan/step:launch-execute/0",
        "from": "step:launch-plan",
        "to": "step:launch-execute",
        "kind": "next",
        "label": "明确授权",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/uncertain/flow:launch/step:launch-execute/step:launch-observe/0",
        "from": "step:launch-execute",
        "to": "step:launch-observe",
        "kind": "uncertain",
        "label": "回执或unknown",
        "flowId": "flow:launch",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/describes/flow:ribbon/step:ribbon-preview/script:ribbon-frozen-deployment/0",
        "from": "step:ribbon-preview",
        "to": "script:ribbon-frozen-deployment",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:ribbon/step:ribbon-pending/script:ui-pending-gate/0",
        "from": "step:ribbon-pending",
        "to": "script:ui-pending-gate",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:ribbon/step:ribbon-apply/script:ribbon-frozen-deployment/0",
        "from": "step:ribbon-apply",
        "to": "script:ribbon-frozen-deployment",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:ribbon/step:ribbon-check/script:ui-pending-gate/0",
        "from": "step:ribbon-check",
        "to": "script:ui-pending-gate",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:ribbon/step:ribbon-runtime/script:runtime-verification/0",
        "from": "step:ribbon-runtime",
        "to": "script:runtime-verification",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:presentation",
          "source:overview"
        ]
      },
      {
        "id": "task-edge/flow-entry/flow:ribbon/feature:ribbon/step:ribbon-preview/0",
        "from": "feature:ribbon",
        "to": "step:ribbon-preview",
        "kind": "flow-entry",
        "label": "进入已登记流程；非执行进度",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/next/flow:ribbon/step:ribbon-preview/step:ribbon-pending/0",
        "from": "step:ribbon-preview",
        "to": "step:ribbon-pending",
        "kind": "next",
        "label": "确认",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/next/flow:ribbon/step:ribbon-pending/step:ribbon-apply/0",
        "from": "step:ribbon-pending",
        "to": "step:ribbon-apply",
        "kind": "next",
        "label": "记录成功",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/uncertain/flow:ribbon/step:ribbon-apply/step:ribbon-check/0",
        "from": "step:ribbon-apply",
        "to": "step:ribbon-check",
        "kind": "uncertain",
        "label": "unknown",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/recovery/flow:ribbon/step:ribbon-check/step:ribbon-preview/0",
        "from": "step:ribbon-check",
        "to": "step:ribbon-preview",
        "kind": "recovery",
        "label": "人工核对后新预览",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/next/flow:ribbon/step:ribbon-apply/step:ribbon-runtime/0",
        "from": "step:ribbon-apply",
        "to": "step:ribbon-runtime",
        "kind": "next",
        "label": "独立授权与证据",
        "flowId": "flow:ribbon",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/describes/flow:migration/step:migration-read/script:migration-preview/0",
        "from": "step:migration-read",
        "to": "script:migration-preview",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:migration",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:migration/step:migration-review/feature:migration/0",
        "from": "step:migration-review",
        "to": "feature:migration",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:migration",
        "sourceRefs": [
          "source:overview"
        ]
      },
      {
        "id": "task-edge/describes/flow:migration/step:migration-future/feature:database/0",
        "from": "step:migration-future",
        "to": "feature:database",
        "kind": "describes",
        "label": "查看对应契约",
        "flowId": "flow:migration",
        "sourceRefs": [
          "source:overview",
          "source:canonical"
        ]
      },
      {
        "id": "task-edge/flow-entry/flow:migration/feature:migration/step:migration-read/0",
        "from": "feature:migration",
        "to": "step:migration-read",
        "kind": "flow-entry",
        "label": "进入已登记流程；非执行进度",
        "flowId": "flow:migration",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/flow-entry/flow:migration/feature:database/step:migration-read/0",
        "from": "feature:database",
        "to": "step:migration-read",
        "kind": "flow-entry",
        "label": "进入已登记流程；非执行进度",
        "flowId": "flow:migration",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/next/flow:migration/step:migration-read/step:migration-review/0",
        "from": "step:migration-read",
        "to": "step:migration-review",
        "kind": "next",
        "label": "材料完整",
        "flowId": "flow:migration",
        "sourceRefs": [
          "source:presentation"
        ]
      },
      {
        "id": "task-edge/uncertain/flow:migration/step:migration-review/step:migration-future/0",
        "from": "step:migration-review",
        "to": "step:migration-future",
        "kind": "uncertain",
        "label": "独立待实现",
        "flowId": "flow:migration",
        "sourceRefs": [
          "source:presentation"
        ]
      }
    ],
    "flows": [
      {
        "id": "flow:launch",
        "title": "发现到观察",
        "summary": "计划绑定一次动作，未知沿原编号核对。",
        "featureIds": [
          "feature:launch"
        ],
        "stepIds": [
          "step:launch-discover",
          "step:launch-plan",
          "step:launch-execute",
          "step:launch-observe"
        ],
        "connections": [
          {
            "from": "step:launch-discover",
            "to": "step:launch-plan",
            "label": "确定目标",
            "kind": "next"
          },
          {
            "from": "step:launch-plan",
            "to": "step:launch-execute",
            "label": "明确授权",
            "kind": "next"
          },
          {
            "from": "step:launch-execute",
            "to": "step:launch-observe",
            "label": "回执或unknown",
            "kind": "uncertain"
          }
        ]
      },
      {
        "id": "flow:ribbon",
        "title": "编辑、部署与独立加载核验",
        "summary": "磁盘写入不等于运行时已加载。",
        "featureIds": [
          "feature:ribbon"
        ],
        "stepIds": [
          "step:ribbon-preview",
          "step:ribbon-pending",
          "step:ribbon-apply",
          "step:ribbon-check",
          "step:ribbon-runtime"
        ],
        "connections": [
          {
            "from": "step:ribbon-preview",
            "to": "step:ribbon-pending",
            "label": "确认",
            "kind": "next"
          },
          {
            "from": "step:ribbon-pending",
            "to": "step:ribbon-apply",
            "label": "记录成功",
            "kind": "next"
          },
          {
            "from": "step:ribbon-apply",
            "to": "step:ribbon-check",
            "label": "unknown",
            "kind": "uncertain"
          },
          {
            "from": "step:ribbon-check",
            "to": "step:ribbon-preview",
            "label": "人工核对后新预览",
            "kind": "recovery"
          },
          {
            "from": "step:ribbon-apply",
            "to": "step:ribbon-runtime",
            "label": "独立授权与证据",
            "kind": "next"
          }
        ]
      },
      {
        "id": "flow:migration",
        "title": "先只读预览，再考虑切换",
        "summary": "只读阶段已完成；写入切换另属未完成契约。",
        "featureIds": [
          "feature:migration",
          "feature:database"
        ],
        "stepIds": [
          "step:migration-read",
          "step:migration-review",
          "step:migration-future"
        ],
        "connections": [
          {
            "from": "step:migration-read",
            "to": "step:migration-review",
            "label": "材料完整",
            "kind": "next"
          },
          {
            "from": "step:migration-review",
            "to": "step:migration-future",
            "label": "独立待实现",
            "kind": "uncertain"
          }
        ]
      }
    ],
    "coverage": {
      "features": 18,
      "scripts": 14,
      "requirements": 24,
      "flows": 3,
      "sourceMappings": 191
    }
  },
  "verifications": [
    {
      "id": "verification:migration-offline",
      "scope": "offline",
      "result": "passed",
      "observedAt": "2026-10-31",
      "environment": {
        "product": "synthetic"
      },
      "claim": "三格式只读预览与敏感剔除专测14项通过。",
      "exclusions": [
        "没有执行数据库切换"
      ],
      "evidenceIds": [
        "evidence:migration"
      ],
      "environmentText": "产品：synthetic"
    },
    {
      "id": "verification:ribbon-ui",
      "scope": "ui",
      "result": "passed",
      "observedAt": "2026-10-31",
      "environment": {
        "product": "synthetic"
      },
      "claim": "离线53项通过，Web DOM完整apply；原生WPF仅预览，Apply由HTTP客户端验证。",
      "exclusions": [
        "不是UI全程实机验收",
        "首次新建仍为v1兼容"
      ],
      "evidenceIds": [
        "evidence:ribbon-ui"
      ],
      "environmentText": "产品：synthetic"
    },
    {
      "id": "verification:ribbon-runtime",
      "scope": "runtime",
      "result": "passed",
      "observedAt": "2026-10-31",
      "environment": {
        "product": "E3D",
        "version": "2.1",
        "module": "DESIGN"
      },
      "claim": "限定产品正常重启后实际页签、分组、32/16图标与Tooltip通过。",
      "exclusions": [
        "未点击宏",
        "直接HTTP部署，不是UI全程",
        "不外推其他产品与模块"
      ],
      "evidenceIds": [
        "evidence:ribbon-runtime"
      ],
      "environmentText": "产品：E3D · 版本：2.1 · 模块：DESIGN"
    },
    {
      "id": "verification:database-offline",
      "scope": "offline",
      "result": "passed",
      "observedAt": "2026-10-31",
      "environment": {
        "product": "synthetic"
      },
      "claim": "独立修订存储及冲突、回滚、格式门禁有隔离测试。",
      "exclusions": [
        "未整体切换活动存储"
      ],
      "evidenceIds": [
        "evidence:database"
      ],
      "environmentText": "产品：synthetic"
    },
    {
      "id": "verification:caf-static",
      "scope": "static",
      "result": "passed",
      "observedAt": "2026-10-31",
      "environment": {
        "product": "CAF"
      },
      "claim": "真实SDK接口编译与生命周期隔离核查。",
      "exclusions": [
        "未真实注册宿主",
        "未界面挂载"
      ],
      "evidenceIds": [
        "evidence:caf"
      ],
      "environmentText": "产品：CAF"
    }
  ],
  "evidence": [
    {
      "id": "evidence:migration",
      "kind": "report",
      "title": "迁移预览验证",
      "sourceRef": "source:evidence-migration",
      "audience": "public",
      "publication": "approved"
    },
    {
      "id": "evidence:ribbon-ui",
      "kind": "report",
      "title": "Ribbon界面分层验证",
      "sourceRef": "source:evidence-ribbon-ui",
      "audience": "public",
      "publication": "approved"
    },
    {
      "id": "evidence:ribbon-runtime",
      "kind": "report",
      "title": "限定产品运行时外观验证",
      "sourceRef": "source:evidence-ribbon-runtime",
      "audience": "public",
      "publication": "approved"
    },
    {
      "id": "evidence:database",
      "kind": "report",
      "title": "独立修订存储验证",
      "sourceRef": "source:evidence-database",
      "audience": "public",
      "publication": "approved"
    },
    {
      "id": "evidence:caf",
      "kind": "report",
      "title": "CAF接口隔离验证",
      "sourceRef": "source:evidence-caf",
      "audience": "public",
      "publication": "approved"
    }
  ],
  "sources": [
    {
      "id": "source:canonical",
      "title": "既有生效规范",
      "sha256": "ff9bc47548d0d6bd920e1519f4b877a4720061d106fc7694943e69003be82c19",
      "authority": "canonical",
      "audience": "public"
    },
    {
      "id": "source:capabilities",
      "title": "既有Feature与Script目录",
      "sha256": "5438cb4a844055893561efd99a8ee6263d2dfea486f8626c2779cc5197e56cf9",
      "authority": "catalog",
      "audience": "public"
    },
    {
      "id": "source:overview",
      "title": "总体、Feature与内部Script审阅说明",
      "sha256": "bd67b1a0405b05cfe696dfe6f4eddbed8f05a9a224f1d1ce8d86c33c017fc6bb",
      "authority": "reviewed-summary",
      "audience": "public"
    },
    {
      "id": "source:presentation",
      "title": "明确ID映射及公开审阅摘要",
      "sha256": "59e1934aaad0b5e3c6cc5258afa7c3dc4d64029b27127ae45a3b1096f996a51c",
      "authority": "presentation",
      "audience": "public"
    },
    {
      "id": "source:evidence",
      "title": "独立人工审阅的只读证据账本",
      "sha256": "40323c99e3b189b631ee51f58b30aa9263e0e9140d38c1e088e3954c5df91132",
      "authority": "evidence",
      "audience": "public"
    },
    {
      "id": "source:source-mappings",
      "title": "来源限定原ID责任映射",
      "sha256": "e60c5eba64b1bb8172710b7be5956f07a8f67882da558f0b823140fc42e6dc6b",
      "authority": "proposal",
      "audience": "public"
    },
    {
      "id": "source:openspec-proposal",
      "title": "平台候选proposal",
      "sha256": "8cc7005d53bdcc99ab18ef42f432736bfbb1b6c7a7db4ffa63c8983c069f8848",
      "authority": "proposal",
      "audience": "public"
    },
    {
      "id": "source:openspec-contracts",
      "title": "平台候选contracts",
      "sha256": "23f45ae6ebacd16f52c4d83b3b39915156a5fd841315d207059e04028f9e4fd7",
      "authority": "proposal",
      "audience": "public"
    },
    {
      "id": "source:openspec-design",
      "title": "平台候选design",
      "sha256": "ba94e6769cb06b9c9fdca6a69377fd80ccde10ed57872fe22078e74156cccbf0",
      "authority": "proposal",
      "audience": "public"
    },
    {
      "id": "source:openspec-tasks",
      "title": "平台候选tasks",
      "sha256": "6cb436ea3b27295c87b5c006c7884b2bc05ae7dc42f46a3b54dc86f8a127b5e1",
      "authority": "proposal",
      "audience": "public"
    },
    {
      "id": "source:validator",
      "title": "共享导入验证器",
      "sha256": "a053f6ec4ca71c18168899f8f35d285e534e4ee56b9f27a438e7c2dd613f7bbf",
      "authority": "presentation",
      "audience": "public"
    },
    {
      "id": "source:render-model",
      "title": "共同组件事实投影",
      "sha256": "692ad8fa220552a79c55a5e5ff50261c3e851101943cabab08ebc7b996e5fbdc",
      "authority": "presentation",
      "audience": "public"
    },
    {
      "id": "source:evidence-migration",
      "title": "迁移预览验证",
      "sha256": "3c0e42140b5a46e65301098e82794e459a092fb1acdce8ffdc1601a24deb72a1",
      "authority": "evidence",
      "audience": "public"
    },
    {
      "id": "source:evidence-ribbon-ui",
      "title": "Ribbon界面分层验证",
      "sha256": "526b7e7542a1502f9aac70f642e89911503b4faa478bfe8958f150db04ed5567",
      "authority": "evidence",
      "audience": "public"
    },
    {
      "id": "source:evidence-ribbon-runtime",
      "title": "限定产品运行时外观验证",
      "sha256": "9778c9cfde3970cbfb91588678bfc5becc08f4007fb7305c1ca7181d7c680044",
      "authority": "evidence",
      "audience": "public"
    },
    {
      "id": "source:evidence-database",
      "title": "独立修订存储验证",
      "sha256": "4a57b1c75ec338f29896d4acf24a92ffd237a11ea2b64d2f96b08ed22b7b9953",
      "authority": "evidence",
      "audience": "public"
    },
    {
      "id": "source:evidence-caf",
      "title": "CAF接口隔离验证",
      "sha256": "504b9ef46c610669bf1c4e4113b15950abfbd22c7b08f77fa89c7f9088c9814d",
      "authority": "evidence",
      "audience": "public"
    }
  ]
};
