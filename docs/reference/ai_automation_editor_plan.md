# AI 自动化编辑助手实施计划

## Summary

- 在 Project Mode 顶部菜单增加 `AI` 菜单，提供 `AI 助手` 和 `AI 设置`。
- `AI 助手` 未配置时先弹配置窗，已配置时打开底部居中的聊天窗。
- 聊天窗关闭后不清 session；`/new` 才重开新会话；上下文接近上限或服务端返回 context overflow 时自动滚动新 session。
- AI 只通过结构化 `edit_plan` 改当前项目，先预览，后应用，不自动保存磁盘。
- v1 支持修改 basic、资源绑定、graph、graph 节点属性，并支持新建对象、增删 graph 节点和连接。

## Frozen Decisions

- 协议固定走 OpenAI-compatible `chat/completions`。
- 配置字段固定为 `Base URL`、`API Key`、`Model`。
- API Key 本地明文保存到 `settings.json`。
- session 只在当前编辑器运行期保留，不跨重启。
- 默认编辑范围是整个当前项目，不限制当前选中对象。
- v1 的资源编辑只覆盖现有 runtime/imported asset 切换，不做外部文件导入。
- v1 不支持删除顶层工程对象。

## Response Protocol

AI 响应只能返回一个 JSON 对象，顶层 `type` 只能是：

- `reply`
- `query`
- `edit_plan`

`query` 允许的查询类型：

- `get_current_selection`
- `list_project_entities`
- `get_entity_snapshot`
- `get_graph_snapshot`
- `list_node_types`
- `get_node_schema`
- `list_asset_choices`

`edit_plan` 允许的操作类型：

- `create_entity`
- `set_basic_fields`
- `set_behavior_mode`
- `set_asset_binding`
- `clear_asset_binding`
- `ensure_graph`
- `set_graph_meta`
- `set_graph_entry`
- `add_graph_node`
- `update_graph_node`
- `remove_graph_node`
- `connect_graph_nodes`
- `disconnect_graph_nodes`

## Execution Order

1. 扩展设置模型与存储，落 AI 基础配置。
2. 增加 `AI` 菜单、配置窗、聊天窗 UI。
3. 增加 AI session、客户端、协议解析器。
4. 增加本地上下文查询服务。
5. 增加 project clone 预览器和原子 apply 执行器。
6. 把 AI 流程接入 `NModStudioProjectWindow`。
7. 补 smoke 和阶段日志，直到可交付。

## Apply Rules

- 所有 `edit_plan` 先在 project clone 上执行。
- 预览零错误才允许点击“应用”。
- 应用前再次对当前 live project 重跑预览；只有成功才替换 live project。
- 应用后刷新 browser、basic、asset、graph、detail panel，并标记 dirty。

## Session Rules

- 关闭聊天窗只隐藏 UI，不销毁 session。
- `/new` 清空当前会话和待应用预览，保留配置。
- 本地按字符预算估算上下文长度，默认阈值 48000 字符。
- 超阈值时用本地摘要开启新会话。
- 服务端返回上下文溢出时自动滚动一次并重试一次。

## Acceptance

- 菜单里能打开 AI 配置和 AI 聊天窗。
- 没配置时点击 `AI 助手` 会先弹配置。
- 聊天窗关闭后重开仍保留消息。
- `/new` 能清空会话。
- AI 可以生成可预览的修改方案并应用到项目。
- 能覆盖 basic、资源绑定、graph、graph 节点编辑，以及新建对象。
- 构建通过，现有 smoke 不回归。
