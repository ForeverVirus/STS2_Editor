# Stage 32 - Graph Canvas Blueprint Rework

## 已开发内容
- 将 Graph 中央区域重构为画布优先的 `GraphEdit` 编辑器，不再使用“节点类型下拉 + 列表化节点内容”的旧交互。
- 新增右键空白处打开的节点搜索弹窗，支持按节点显示名、类型、描述搜索并添加节点。
- 新增画布级交互：
  - 空白处左键拖拽平移画布
  - 鼠标滚轮缩放
  - 自动布局
  - 缩放适配
  - 居中添加节点按钮
- 节点外观重做为更接近蓝图卡片的形式：
  - 标题
  - 节点类型徽标
  - 描述
  - 属性摘要
  - 输入/输出端口行
- 保留并兼容现有右侧节点属性面板、保存、校验、graph 持久化链路。
- 启用 `GraphEdit` 的网格和吸附，关闭内建菜单按钮，让编辑器视觉更集中于画布。

## 未开发内容
- 还没有做到真正 UE/FlowCanvas 那种“数据 pin 语义驱动”的可视化数据流执行；当前仍然主要是流程线 + 属性驱动。
- 还没有做“拖一根线到空白处自动弹出可连接节点”的交互。
- 还没有做小地图、框选、多选、快捷键删除、复制粘贴等高级蓝图体验。
- 节点移动的布局保存目前仍然依赖现有保存链和鼠标释放触发，后续可以进一步挂到更原生的 `GraphEdit`/`GraphElement` 移动信号。

## 遇到的问题
- Windows 下单次 `apply_patch` 过长会触发命令长度限制，需要把 Graph 文件重写拆成多段 patch。
- Godot C# 的 `Environment` 与 `System.Environment` 存在命名冲突，已通过显式限定命名空间解决。
- 现有 Graph 执行器的运行时语义还没有完全消费端口类型，因此本阶段优先解决 UI/UX，而没有同时重构执行模型。

## 后续如何解决
- 下一轮如果继续深挖 Graph，可优先做三件事：
  - 拖线到空白处直接创建候选节点
  - 端口类型映射和合法连线约束
  - 节点移动/选择改挂更原生的 GraphEdit 信号
- 如果要进一步接近 UE 蓝图体验，再补：
  - 小地图
  - 快捷键删除
  - 框选/多选
  - 节点复制粘贴

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 通过，`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果 `PASS`
- 本阶段尚未在真实游戏 UI 内完成手工点击验收，因此画布的最终手感仍需要实际进游戏确认。
