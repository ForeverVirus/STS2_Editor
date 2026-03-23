# Stage 35 - Graph Import, Description Sync, And Property Dropdowns

## 已开发内容
- 修复了 `Graph -> 导入` 选择器的导入源定位逻辑。
  - 现在导入候选会携带明确的源对象，而不是确认后再仅靠 `EntityId` 反查。
  - 同时排除了“当前正在编辑的同一张卡”作为默认导入候选，避免看起来像“导入了但没变化”。
- 打通了 graph 修改后的描述同步链路。
  - `Auto Description` 现在会随着节点属性变化实时更新。
  - `graph.Description` 会在“未手动覆盖”的前提下自动跟随最新 graph 描述。
  - `Basic` 页里的 `description / initial_description` 字段会在安全条件下实时同步成新的自动描述，避免仍显示旧的 `{Damage:diff()}` 文本。
- 补上了节点级自动描述缓存。
  - 现在节点属性变更后，节点详情里的描述也会一起刷新，不再只更新右上角摘要。
- 右侧 `Graph` 详细信息面板已重做一轮本地化和控件类型。
  - `target` 改成下拉选项。
  - `props` 改成下拉选项。
  - `amount` 改成数字输入框。
  - 属性标签改成中文显示名，不再直接裸露英文 key。
  - `Graph ID / Graph Name / Graph Description / Node Display Name / Node Description / Node Properties` 等字段增加了清晰的中文标题。
- Graph 右侧主信息改成优先显示编辑器自己的本地化摘要。
  - 不再默认把原始 auto-import 英文诊断摘要直接塞进主信息区。
- `GraphDescriptionGenerator` 已支持按当前编辑器语言生成描述。
  - 中文模式下自动描述改为中文。
  - smoke test 也已同步兼容双语描述输出。

## 未开发内容
- 还没有做“导入成功后自动将视角聚焦到新 graph 布局中心”的额外体验优化。
- 右侧属性编辑器目前只把高频易错字段做成强类型控件：
  - `target`
  - `props`
  - `amount`
  - 其他字段如 `power_id` 仍然是文本输入。
- `Graph` 仍不是完整 UE Blueprint 的数据 pin 类型系统。
  - 当前仍然是“流程线 + 右侧属性编辑器”的 Phase 1 方案。
- 这轮没有在真实游戏里逐项点击复验：
  - 导入其它卡牌后画布是否立刻切成目标卡 graph
  - 改 `amount` 后 `Basic` 描述是否立刻可见地刷新
  - `target / props` 下拉在游戏内是否操作顺手

## 遇到的问题
- 这轮改动把 `GraphDescriptionGenerator` 从纯英文改成了跟随当前语言，导致原有 `Stage03SmokeTest` 的英文断言失败。
- `NModStudioProjectWindow` 里新增的 graph 文本派生逻辑一开始漏掉了：
  - 节点自动描述缓存 helper
  - `TryGetValue` 的局部变量初始化
- `ModStudioProjectDetailPanel` 原有结构更偏“简单表单”，继续补丁式追加很难干净支持：
  - 本地化标签
  - 下拉/数字输入
  - 图级字段标题
  - 所以本轮把这个右栏文件整体重写了。

## 后续如何解决
- 下一轮优先做真实游戏内实机复验，重点确认 3 条链：
  - Graph 导入其它卡是否真正刷新成目标图
  - 改节点数值后 `Basic` 描述是否即时更新
  - 右侧下拉控件是否在游戏内可正常操作和保存
- 如果 `power_id` 等字段在实机里也经常填错，再补第二批强类型编辑器：
  - `power_id` 搜索下拉
  - `reward_kind` 下拉
  - 常见布尔字段 checkbox 化
- 如果导入后的图虽然已切换，但用户仍不容易感知，会补：
  - 导入完成后的缩放适配
  - 视角跳转到 entry/主要动作节点
  - 导入成功 toast

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过
  - `0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 通过
  - `Result: PASS`
  - 自动描述输出已切换为中文：
    - `造成 8 点伤害，目标 当前目标。 获得 5 点格挡，目标 自身。`
