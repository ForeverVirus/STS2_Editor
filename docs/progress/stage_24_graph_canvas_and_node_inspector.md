# Stage 24 - Graph Canvas And Node Inspector

## 已开发内容
- `Graph` 中栏画布的端口映射已重做：
  - 不再依赖把语义端口 id 当整数解析。
  - 现在为每个节点维护 `portId <-> slotIndex` 映射，可兼容 `next / in / true / false / out` 这类语义端口。
- `GraphNode` 的 slot 绑定已修正到真实子节点索引，避免端口落在错误的 UI child 上。
- `GraphEdit` 的连线与断线逻辑已改成真正按语义端口保存：
  - 画布事件使用 slot index
  - 保存到 `BehaviorGraphConnectionDefinition` 时转换回 `FromPortId / ToPortId`
- 右侧 `Graph` 节点检视器现在已真正写回当前选中节点：
  - 节点显示名可编辑
  - 节点描述可编辑
  - 节点属性字典可编辑
  - 修改后会同步刷新中栏节点标题和描述摘要
- `Graph` 保存时会先导出画布布局到 `BehaviorGraphDefinition.Metadata`，避免节点坐标丢失。
- `Graph` 右侧详情区新增图级摘要：
  - Graph Id
  - 当前模式（Native / Graph）
  - 节点数
  - 连线数
  - 自动描述结果
- `Graph` 校验结果不再错误写进 `Basic` 只读区，现在会显示在右侧 `Graph` 信息区。
- `GraphEnabled` 打开但当前没有 graph 时，会自动创建默认 scaffold，而不是让用户面对空状态。
- 保存 graph 时已接入 `GraphDescriptionGenerator`：
  - 当实体描述字段为空时，自动把可生成的 graph 描述写入元数据。
  - 若用户已在 `Basic` 中手动填写描述，则不会覆盖。
- graph 保存时已做 graph id 变更清理：
  - 如果用户改了 Graph Id，会移除旧 key，避免项目内残留悬空 graph 记录。

## 未开发内容
- `Graph` 右键菜单、节点搜索弹出层、小地图这类更完整的蓝图交互还没做。
- `Import` 目前仍是默认 scaffold 起步，还没有完成“从游戏内已有对象 / 当前项目对象挑选并复制 graph”的 UI 选择器。
- 还没有完成从原版对象逻辑自动抽 graph 的正式接入，当前只完成了 graph 编辑器与描述联动的基础设施。

## 遇到的问题
- 旧版画布会把语义端口 id 强行 `int.Parse()`，这对模板里的 `next / in / true / false` 端口会直接失效。
- 右侧节点检视器此前只是显示内容，没有真正修改回 `BoundGraph`，导致用户改完看起来生效，保存后却没有写入。
- 程序化刷新右侧节点详情时会触发 `TextChanged`，如果不做 suppress，会把“单纯选中节点”误判为编辑。

## 后续如何解决
- 下一阶段会先评估能否从反编译源码抽出常见原版行为，优先覆盖卡牌的伤害 / 格挡 / 抽牌 / 能量等简单模式。
- 如果原版行为可抽取，会把 `Import` 扩展成两类来源：
  - 当前项目已有 graph
  - 游戏原版对象自动翻译 graph
- 后续还会继续把 graph 右侧属性编辑器做成更清晰的分组式表单，而不是纯 key/value 行列表。

## 验证结果
- `dotnet build F:\sts2_mod\mod_projects\STS2_editor\STS2_Editor.csproj`
  - 结果：`0 warning / 0 error`
- `dotnet run --project F:\sts2_mod\mod_projects\STS2_editor\tools\Stage03SmokeTest\Stage03SmokeTest.csproj -- F:\sts2_mod\mod_projects\STS2_editor\tools\stage03-smoke-run`
  - 结果：`PASS`
