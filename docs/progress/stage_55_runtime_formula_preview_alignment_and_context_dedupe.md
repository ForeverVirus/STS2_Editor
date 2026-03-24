# Stage 55 - Runtime Formula Preview Alignment And Context Dedupe

## 已开发内容
- 修复了 Graph 公式编辑器中 `公式上下文来源` 出现两个“手牌数”的问题。
- 在右侧属性面板接线层把 `cards` 和 `hand_count` 统一归一到 `hand_count`，避免同义上下文重复出现在下拉中。
- 新增运行时 Graph 预览服务 [RuntimeGraphPreviewService.cs](F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeGraphPreviewService.cs)，用于把 Graph 动态值结果写回卡牌的 `DynamicVars.PreviewValue`。
- 为 [RuntimeGraphPatches.cs](F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/Runtime/RuntimeGraphPatches.cs) 增加 `CardModel.UpdateDynamicVarPreview` 的 Harmony postfix，使启用 Graph 的卡牌在游戏内的卡面数值预览不再继续使用原版旧值。
- 统一了编辑器预览和运行时公式上下文：
  - 编辑器中 `hand_count` 预览会在卡牌仍在手牌时自动排除自己。
  - 运行时 `hand_count` 求值也采用同口径，避免“编辑器预览正确但执行结果口径不同”。
- 修正了 `Stage03SmokeTest` 里一条过时的描述断言，使 smoke test 与当前中文描述链兼容。

## 未开发内容
- 还没有做一次新的真机回归来确认“打击”改成公式后，实际战斗中的最终伤害已从原版旧值切换为 Graph 公式结果。
- 还没有把“保存项目后自动刷新运行时 registry”做成统一行为；当前导出包路径已有 refresh，但纯项目保存链仍值得再确认。

## 遇到的问题
- [FieldChoiceProvider.cs](F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/FieldChoiceProvider.cs) 存在编码异常，直接在该文件上做文本级 patch 很不稳定，因此这轮先在 UI 接线层做了规范化和去重。
- 主工程直接 `dotnet build` 会因为游戏进程锁住 `mods/STS2_Editor/STS2_Editor.dll` 而失败，这不是代码编译错误，而是目标 DLL 被 `SlayTheSpire2.exe` 占用。
- 旧的 smoke test 仍在匹配之前乱码阶段留下的中文描述断言，导致描述链已正确时仍误报失败。

## 后续如何解决
- 继续用真机验证 `打击` 这类公式卡，重点确认：
  - 编辑器右侧上下文切换后预览值是否正确。
  - 进战斗后的卡面预览和实际打出伤害是否一致。
- 如果仍发现“项目保存后但未导包时运行时不刷新”的情况，下一阶段把 `SaveProject()` 接上统一的运行时 refresh。
- 后续找机会把 [FieldChoiceProvider.cs](F:/sts2_mod/mod_projects/STS2_editor/Scripts/Editor/UI/FieldChoiceProvider.cs) 的编码彻底清理掉，减少 UI 字段接线层额外兼容逻辑。

## 验证结果
- `dotnet build STS2_Editor.csproj /p:Sts2Dir=F:\sts2_mod\mod_projects\STS2_editor\tools\fake_sts2`
  - 通过，`0 warning / 0 error`
- `dotnet run --project tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- tools\stage03-smoke-run`
  - 通过，`PASS`
- smoke test 中动态公式预览断言结果：
  - 手牌数 `2` 时预览值 `6`
  - 手牌数 `4` 时预览值 `10`
