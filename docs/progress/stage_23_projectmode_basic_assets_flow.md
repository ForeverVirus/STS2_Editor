# Stage 23 - ProjectMode Basic / Assets Flow

## 已开发内容
- `ProjectMode` 的 `Basic` 编辑流已改成“只修改基础字段”，不再把整份 `EntityOverrideEnvelope.Metadata` 整体覆盖。
- `Basic` 现在按实体类型做字段白名单过滤：
  - 角色：`title / starting_hp / starting_gold / max_energy / base_orb_slot_count / starting_deck_ids / starting_relic_ids / starting_potion_ids`
  - 卡牌：`title / description / pool_id / type / rarity / target_type / energy_cost / energy_cost_x / canonical_star_cost / star_cost_x / can_be_generated_in_combat`
  - 遗物 / 药水 / 事件 / 附魔 也都切到了各自的基础字段集合。
- `Basic` 的保存逻辑现在只写回这些基础字段，保留素材绑定、事件模板扩展字段、graph 相关字段，不会再误删。
- `Basic` 的还原逻辑已独立成 `RevertBasic()`：
  - 原版实体：只清除基础字段覆盖，恢复到游戏原始值。
  - 项目内新建实体：恢复到 `CreateDefaultMetadata()` 的模板默认值。
- 右侧 `Basic` 只读信息已切回“原版运行时元数据”视角：
  - 原版实体显示游戏真实基础信息。
  - 项目新建实体显示默认模板信息。
- `Assets` 的“当前生效预览”和“候选预览”已真正拆开：
  - 进入条目时，当前与候选都显示当前实际绑定。
  - 在右侧挑选素材时，只更新候选位，不再把当前位一起改掉。
- `Assets` 的游戏内素材和项目素材列表改成使用 `ItemList` metadata 传递真实 path / assetId：
  - 修复了搜索过滤后索引错位导致选中错误素材的问题。
  - 修复了运行时素材列表只拿显示文本、丢失真实路径的问题。
- 外部素材导入后，不再直接 `LoadEntity()` 把候选状态冲掉：
  - 现在会刷新右侧导入列表。
  - 中栏会继续保持“当前 vs 新导入候选”的对比预览。
- 资源覆盖清除后，会尝试清理空 envelope，避免原版实体残留空壳 override。

## 未开发内容
- `Basic` 目前还是通用字段表单，还没有做每类实体更强的专用编辑器组件。
- `Assets` 目前的中栏仍是贴图级预览，尚未升级成卡牌/遗物/药水的完整实体预览组件。
- 外部素材导入仍是“单图片文件导入”，还没有补目录导入、批量导入或尺寸校验 UI。
- `EntityBrowserPanel` 的 `Modified` 筛选仍较粗糙，尚未改成严格基于 envelope 状态判断。

## 遇到的问题
- 旧版 `SaveBasic()` 直接用 `BasicEditor` 的字段结果替换整个 `envelope.Metadata`，会把 asset binding、event 模板页配置等非基础字段全部抹掉。
- 旧版 `Assets` 预览把“当前绑定”和“候选选择”混在同一个状态变量里，导致用户一点击素材，界面就像已经生效了一样。
- 项目素材列表在搜索后仍按过滤前索引取值，会错绑素材。

## 后续如何解决
- 下一阶段会把 `Basic` 表单进一步按实体类型做结构化组件拆分，例如卡牌池/角色池改成明确的选择器，而不是普通字符串输入。
- `Assets` 会继续往实体级预览演进，让卡牌/遗物/药水在中栏显示更接近真实游戏 UI 的对比效果。
- 浏览器筛选会补一层显式的“runtime modified / project new / graph enabled / asset overridden”状态索引。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 结果：`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果：`PASS`
