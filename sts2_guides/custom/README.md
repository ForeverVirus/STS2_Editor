# 自定义知识库说明

这个目录用来放玩家自己维护的 Slay the Spire 2 知识。

自定义层会优先于内置 `core` 层加载。

如果自定义 JSON 里的 `id` 或 `slug` 和内置条目相同，那么会覆盖内置条目。

英文说明请查看同目录下的 `README_en.md`。

一、推荐先用 JSON，什么时候再用 Markdown

- 如果你要补的是结构化知识，例如卡牌、遗物、药水、能力、敌人、事件、附魔、机制规则，优先写 JSON。
- 如果你要补的是成段策略说明、角色长文攻略、总览说明，再用 Markdown。
- 如果只是想纠正某一条规则，最推荐写到 `game_mechanics.json`。

二、当前支持的 JSON 文件名

- `characters.json`
- `builds.json`
- `cards.json`
- `relics.json`
- `potions.json`
- `powers.json`
- `enemies.json`
- `events.json`
- `enchantments.json`
- `game_mechanics.json`

文件名必须和上面这些内置名称一致，系统才会加载。

三、JSON 怎么写

最简单的做法，是只写你想新增或覆盖的那几条，不需要把整份内置文件复制过来。

例如，`game_mechanics.json` 里的单条规则结构是：

- `id`：规则唯一标识。若想覆盖内置规则，就写和内置相同的 `id`。
- `title`：标题。
- `summary`：规则正文。建议先写客观机制，再写决策约束。
- `source`：建议固定写 `custom`。

推荐写法：

- 先写“游戏里实际怎么结算”。
- 再写“不要把什么当成合法决策理由”。
- 最好中英都写到同一个 `title` 或 `summary` 里，这样中文和英文提问都更容易命中。

例如：

`id = energy-resource`

`title = Unused energy does not carry over by default / 未用完的能量默认不会保留到下一回合`

`summary = 正常机制 + 不要把留能量下回合再用当成合法理由`

四、Markdown 自定义知识怎么写

请先注意一件事：不是任意名字的 Markdown 都会被系统当成知识加载。

当前真正会参与知识加载的 Markdown 主要是下面这些名字：

- 总览：`overview.md` 或 `00_OVERVIEW.md`
- 通用策略：`general_strategy.md` 或 `sts2_knowledge_base.md`
- 角色攻略：使用角色英文名或 slug 的别名，例如 `ironclad.md`、`ironclad_complete_guide.md`、`silent.md`、`defect.md`、`regent.md`、`necrobinder.md`

这个 `README.md` 主要是给人看的说明文档，不建议把它当成实际知识条目来写。

五、Markdown 内容建议

- 只写游戏知识、机制说明、构筑思路、路线选择、事件判断、Boss 应对这类内容。
- 用普通标题、短段落、项目符号即可。
- 一条建议最好写清楚“适用前提”、“结论”、“例外情况”。
- 如果要覆盖某个角色攻略，最简单的方法是新建同名角色 Markdown 文件。

推荐结构：

- 标题
- 这个角色或主题的核心目标
- 前期优先级
- 中后期转型点
- 常见误判
- 关键例外

六、Markdown 约束

自定义 Markdown 会经过校验，下面这些内容会被拒绝：

- 三反引号代码块
- 带协议的网址链接
- 非游戏知识的提示词或指令注入内容
- 命令行、脚本注入、编辑器协议或本地路径协议这类非游戏文本

简单理解就是：

- 可以写普通说明文字
- 不要写提示词
- 不要贴链接
- 不要放代码块

七、大小和维护建议

- 自定义文件不要写得过大，默认单文件上限大约是 256 KB。
- 一条规则只纠正一个误判点，后期更容易维护。
- 如果发现 Agent 总在某个问题上犯同一种错，优先补一条短而强约束的机制规则，不要先写成长篇攻略。

八、覆盖和新增的建议策略

- 要纠错：优先复用内置同名 `id` 或 `slug` 进行覆盖。
- 要补充：新写一个不会冲突的新 `id`。
- 如果一条规则已经有内置版本，但语气太弱，可以在自定义层用相同 `id` 改写成更强的决策约束版本。

九、推荐的工作流

1. 先复盘一次错误日志，找出 Agent 的错误理由。
2. 判断它属于哪一类：
   `game_mechanics.json` 适合规则约束。
   `cards.json` 适合卡牌认知。
   `relics.json` 适合遗物认知。
   `events.json` 适合事件选项。
   角色 Markdown 适合整套打法风格。
3. 先写短条目纠错，再实测。
4. 只有当短条目不够时，再补长文 Markdown。

十、当前这个目录里最值得参考的文件

- `game_mechanics.json`

如果你经常要修正 Agent 的错误决策，建议优先在这里加规则。
