# Stage 25 - Native Auto Graph Translation And Description Generation

## 已开发内容
- 将原版转 graph 的入口统一为单一服务 `NativeBehaviorAutoGraphService`。
- 这条服务现在固定采用两层策略：
  - 先走 `reflection-import`
  - 再走 `description-fallback`
- 保留了现有 UI 接线，不需要让 `ProjectMode` 知道背后有几套实现。
- 新的统一服务会把策略、支持到的 step kind、partial 状态和摘要一起回传给编辑器。
- `GraphDescriptionGenerator` 做了收口：
  - 不再把 `flow.sequence` 这种流程节点翻成噪音文案
  - 补了 `all_enemies / all_allies` 的目标描述
- `SaveGraph()` 这条链路保留“自动生成描述”的能力，但不会再覆盖用户已经手填的描述。
- 新增了自动描述缓存语义：
  - 如果当前描述为空，或当前描述仍等于上一次自动生成值，保存 graph 时会刷新自动描述
  - 如果用户已经手动改过描述，后续自动生成不会再强制覆盖
- 新增支持清单文档：
  - [native_auto_graph_support_catalog.md](/F:/sts2_mod/mod_projects/STS2_editor/docs/reference/native_auto_graph_support_catalog.md)
- `Stage03SmokeTest` 新增两类覆盖：
  - graph description generation
  - native translation catalog

## 未开发内容
- 事件自动转 graph 仍未进入“可依赖”的交付状态，目前只在翻译器 catalog 层标记为 partial，不在 UI 中宣传为稳定能力。
- 遗物自动转 graph 仍然受限于当前运行时 dispatcher 已经 patch 的 hook 集合；例如 `BeforeCombatStart` 这类触发点还没有形成完整端到端支持链。
- IL importer 仍然无法可靠重建复杂条件分支的语义，只能识别方法调用序列本身。
- 复杂模式依旧未覆盖：
  - 选牌 / 生成牌 / 牌堆变异
  - orb / summon / forge / transform
  - 随机、多段循环、X 费、CalculatedVar 类实时计算

## 遇到的问题
- 工程里一度同时存在“描述启发式服务”和“IL 解析 importer”两条并行实现，UI 只接了其中一条，后续继续开发会很容易出现语义分叉。
- 旧的描述启发式实现里还混有乱码形式的中文关键字，继续沿用会影响后续维护。
- 自动描述如果每次保存 graph 都覆盖 metadata，会和用户手动写的卡牌/遗物/药水说明冲突。
- 当前 runtime graph dispatcher 只支持一部分 card / potion / relic trigger，因此 support catalog 不能只按“能翻译”判断，还要按“能运行”判断。

## 后续如何解决
- Stage 26 做交付前回归时，继续以 `NativeBehaviorAutoGraphService` 作为唯一公开入口，不再新增第二套 UI 直连实现。
- 后续如果继续扩原版自动转 graph，优先扩这两块：
  - `NativeBehaviorGraphAutoImporter` 的已识别命令集
  - runtime dispatcher 已支持的 trigger 集
- 事件方向不走“整类全自动翻译”，优先做“静态选项树 + 简单叶子分支”的子集。
- 如果后面补更多 runtime hook，再同步更新 support catalog，避免 UI 对用户做出超范围承诺。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- 代码层新增/调整的关键文件：
  - [NativeBehaviorAutoGraphService.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/NativeBehaviorAutoGraphService.cs)
  - [GraphDescriptionGenerator.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Graph/GraphDescriptionGenerator.cs)
  - [NModStudioProjectWindow.cs](/F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/NModStudioProjectWindow.cs)
  - [Program.template](/F:/sts2_mod/mod_projects/STS2_editor/tools/Stage03SmokeTest/Program.template)
- 这一阶段的验证重点已经补进 smoke test；下一阶段会再跑完整 smoke，并做交付判断。
