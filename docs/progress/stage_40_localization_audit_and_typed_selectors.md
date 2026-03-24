# Stage 40 - Localization Audit And Typed Selectors

## 已开发内容
- 新增 `ModStudioLocalizationCatalog`，把 `ProjectMode / PackageMode / ModeChooser / Basic / Assets / Graph` 的主要 UI 文案和动态值面板文案收口成 key 化目录。
- `FieldChoiceProvider` 已成为统一的字段选择来源：
  - `Basic`：`type / rarity / target_type / usage / layout_type / pool_id / act_ids`
  - `Graph`：`target / props / reward_kind / operator / power_id / dynamic_source_kind / base_override_mode / extra_override_mode / dynamic_var_name / formula_ref / preview_multiplier_key`
- `ModStudioProjectDetailPanel` 的图节点属性编辑器已经按字段类型切分：
  - 数值字段 -> `SpinBox`
  - 布尔字段 -> `CheckBox`
  - 有 choices 的字段 -> `OptionButton`
  - 其他字段 -> `LineEdit`
- `ModStudioBasicEditor` 里的主要可枚举字段已经转成强类型控件。
- 角色的 `starting_deck_ids / starting_relic_ids / starting_potion_ids` 已经改为“可增删列表 + 下拉选择”，避免作者手输 ID 出错。

## 未开发内容
- 仍有少量历史文案还散落在具体 UI 文件的 `Dual(...)` 字面量中，没有完全迁到 `ModStudioLocalizationCatalog`。
- 对超大集合字段，目前还是第一版 choices；后续可以继续升级为更强的搜索选择器/弹窗。

## 遇到的问题
- 历史 UI 文案原本分散在多个控件和页面里，审计时需要边收口边避免影响现有界面。
- 动态值字段加入后，原来“全部用 `LineEdit`”的做法已经不再安全，必须同时补字段类型矩阵。

## 后续如何解决
- 继续把残留文案迁入 `ModStudioLocalizationCatalog`，减少维护分散度。
- 对未来更大的集合字段，补搜索选择弹窗而不是只停留在普通下拉。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过
- `Stage03SmokeTest` 通过

