# Stage 36 - Dynamic Value Graph Core

## 已开发内容
- 引入了结构化动态值类型：
  - `DynamicValueDefinition`
  - `DynamicValueSourceKind`
  - `DynamicValueOverrideMode`
  - `DynamicValuePreviewResult`
- `DynamicValueEvaluator` 已接入 Graph 主链路，支持：
  - `Literal`
  - `DynamicVar`
  - `FormulaRef`
- 现有数值型节点已统一通过 `DynamicValueEvaluator.EvaluateRuntimeDecimal(...)` 求值，不再只依赖裸 `amount` 字符串。
- 老图兼容已落地：
  - 旧的 `Properties["amount"]` 会被视为 `Literal`
  - 不会破坏已有 Graph
- `BehaviorGraphNodeDefinition` 已增加 `DynamicValues` 字段，节点级动态值可独立保存。

## 未开发内容
- 还没有支持“任意自由脚本公式”，当前只支持引用原版动态值和原版公式型值。
- `FormulaRef` 仍以“引用原版公式 + 覆盖 base/extra”为主，没有开放任意表达式编辑。
- 部分原版动态变量仍是“尽量识别”，还没有做到 100% 全覆盖。

## 遇到的问题
- 旧图只存了简单字符串属性，无法直接承载动态值结构；因此做了兼容层和迁移逻辑。
- 原版动态值和运行时求值链并不完全等同于编辑器 Graph 节点的执行上下文，所以需要后续的预览上下文补齐。

## 后续如何解决
- Stage 37 继续补动态预览上下文和模板/预览双轨描述。
- 后续在原版自动转 Graph 时，优先保留动态值语义，而不是回退成固定数字。

## 验证结果
- `dotnet build STS2_Editor.csproj` 通过
- `Stage03SmokeTest` 通过

