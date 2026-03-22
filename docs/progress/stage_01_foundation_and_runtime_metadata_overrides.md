# Stage 01 - Foundation And Runtime Metadata Overrides

## Date
- 2026-03-23

## Developed
- `codex_plan.md` 已落地，作为当前总计划基线。
- 主菜单已注入 `Mod Studio` 入口，游戏内可进入 `Project Mode` 和 `Package Mode`。
- 项目制基础能力已完成：
  - 新建、复制、删除项目。
  - 导出并自动安装 `.sts2pack`。
  - 基于运行时 `ModelDb` 的角色、卡牌、遗物、药水、事件、附魔浏览。
  - 针对选中实体抓取运行时字段快照并保存为 override JSON。
  - 支持把实体切换到 `graph` 行为源，并自动生成默认 graph scaffold。
- 模组包基础能力已完成：
  - `.sts2pack` 导出、导入、安装、checksum、加载顺序、启用/禁用、会话持久化。
  - 多包冲突按对象整体覆盖，后加载覆盖前加载。
  - 多人包交集协商后端骨架已完成。
- 运行时 metadata 覆盖已开始生效：
  - 文本覆盖通过 `LocString.GetRawText` patch 接入，当前支持：
    - 角色名称
    - 卡牌标题/描述
    - 遗物标题/描述
    - 药水标题/描述
    - 事件标题/初始描述
    - 附魔标题/描述/额外卡牌文本
  - 角色开局覆盖当前支持：
    - `starting_hp`
    - `starting_gold`
    - `max_energy`
    - `base_orb_slot_count`
    - `starting_deck_ids`
    - `starting_relic_ids`
    - `starting_potion_ids`
  - 美术路径覆盖当前支持：
    - 卡牌 `portrait_path`
    - 遗物 `icon_path`
    - 药水 `image_path`
    - 附魔 `icon_path`
    - 事件 `portrait_path` / `image_path` 的初始立绘加载
  - 对外部绝对路径图片，已支持运行时直接读盘生成 `Texture2D`。

## Not Developed Yet
- 卡牌、遗物、药水、事件、附魔“实际效果逻辑”的运行时重写还未接入真实玩法入口。
- `graph` 目前已经能建模和保存，但还没有接到具体玩法 dispatch 执行。
- 通过 UI 选择和导入外部素材、管理项目内素材、预览素材的工作流还未完成。
- 导出包内托管素材虽然已能打包，但运行时从安装包缓存中提取并统一解析的链路还未完成。
- 新建内容注入 `ModelDb` / 卡池 / 遗物池 / 药水池 / 事件路由的能力还未完成。
- 怪物、怪物技能、附魔以外更多战斗对象的图鉴与编辑尚未开始。
- 角色/怪物 Spine 外部资源替换未开始。
- 事件自定义 Godot 布局未开始。
- 真实游戏内 smoke test 还未完成，目前只有编译与源码级验证。

## Issues Encountered
- `STS2_Proj` 反编译 Godot 工程源码最初被一起编译进 mod 项目，导致与 `sts2.dll` 重复定义冲突。
  - 已通过 `STS2_Editor.csproj` 排除 `STS2_Proj/**` 解决。
- UI worker 初版壳层有若干 Godot API/类型使用问题。
  - 已统一修正并重写为可编译的纯代码 UI。
- 包启用状态存在一个实际 bug：
  - 本地重新启用包时 `SessionEnabled` 不会恢复。
  - 已在 `RuntimePackageBackend.EnablePackage` 中修复。
- 游戏文本系统没有“原始字符串直传”的 `LocString` 构造方式。
  - 已改为 patch `LocString.GetRawText`，用 editor override 直接接管原始本地化文本。
- 部分资源类型返回的是 `CompressedTexture2D`，外部图片运行时无法完全无缝映射。
  - 当前优先完成 `Texture2D` 路径链路，`EnchantmentModel.Icon` 等压缩纹理场景仍需进一步适配。

## Validation
- `dotnet build STS2_Editor.csproj` 通过，当前为 `0 warning / 0 error`。
- 已对反编译源码完成一轮 hook 点核查，确认：
  - 新开局入口集中在 `Player.CreateForNewRun(...)`
  - 起始卡组/遗物/药水人口集中在 `Player` 的起始库存填充方法
  - 文本与大部分立绘/图标都可以通过模型属性或本地化层统一接管

## Next Step
- Stage 02 目标：
  - 把 `graph` 接到实际玩法入口，先覆盖卡牌效果和一部分遗物/药水效果。
  - 建立运行时“metadata -> gameplay patch”分发层，而不只是文本/资源/开局参数。
  - 开始补素材导入 UI、托管素材引用与包内素材运行时提取。
- Stage 02 之后：
  - 新建内容注入 `ModelDb`
  - 怪物/事件扩展
  - 联机实测与回归测试清单
