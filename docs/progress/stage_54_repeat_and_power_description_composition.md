# Stage 54 - Repeat And Power Description Composition

## 已开发内容
- 重做了 Graph 描述生成的两条关键链路：
  - `combat.repeat + 后续动作节点` 现在会组合成单条描述，不再把 `repeat` 语义丢失。
  - `combat.apply_power` 在可解析到原版 power 的情况下，会优先使用 power 的 `smartDescription` 来生成描述，而不是退化成泛化的 “apply power xN”。
- 新增了共享辅助层：
  - `GraphDescriptionSupport`
  - 负责主路径节点排序、power smartDescription 模板替换、preview 替换、目标文本格式化。
- `DynamicValueEvaluator.GetSourceToken(...)` 现在对 `Repeat` 做了特殊处理：
  - 原版动态变量 `Repeat` 会保留为 `{Repeat}`
  - 不再统一退化成 `{Repeat:diff()}`
- `GraphDescriptionTemplateGenerator` 已重写为按主路径顺序组合描述：
  - 线性图会按 entry 主路径生成模板
  - `repeat + damage` 会输出合并后的模板
  - `apply_power` 会尽量输出 power smartDescription 模板
- `GraphDescriptionGenerator` 已重写为按主路径顺序生成预览描述：
  - 预览描述不再简单按节点列表逐个拼句子
  - `repeat + damage` 会输出合并后的预览文本
  - `apply_power` 会尽量输出 power smartDescription 的预览文本

## 未开发内容
- 还没有对所有 `apply_power` 相关卡牌做逐卡真机回归。
- 对含复杂 `Amount:formatter(...)` 且同时存在“绝对值/增量覆盖”的 power smartDescription，当前主要先覆盖常见场景，并不承诺所有 formatter 都百分百保真。
- 当前 smoke harness 里的旧描述断言仍需继续收口调整。

## 遇到的问题
- `Basic` 保存逻辑优先使用 `TemplateDescription`，而旧模板生成器没有覆盖 `combat.repeat` 和 `combat.apply_power` 的组合语义，导致：
  - `七星` 保存后丢失 `{Repeat}`
  - `书页风暴` 之类 apply-power 卡牌只能生成 generic 文本
- 原版 `Repeat` token 的写法与普通 `Damage/Block/Cards` 不同，不能继续统一走 `:diff()` 规则。
- power 语义如果直接自己拼一句“施加某能力 xN”，会和官方卡牌描述风格偏差很大。

## 后续如何解决
- 下一轮继续按真机回归结果补具体语义缺口：
  - 逐卡扫描仍然空 graph 或描述不准确的卡牌
  - 优先处理 `apply_power + 特殊 formatter`、`repeat + 多目标`、`damage + 后续状态引用`
- 收口 smoke harness 的旧断言，改成新的组合描述语义断言。

## 验证结果
- `dotnet build STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `Stage03SmokeTest`
  - 动态值语义断言已更新并通过
  - 仍有一条旧描述断言需要继续收口
