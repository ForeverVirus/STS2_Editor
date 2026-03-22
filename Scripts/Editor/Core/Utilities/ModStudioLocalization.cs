using System.Globalization;
using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Core.Utilities;

public static class ModStudioLocalization
{
    public const string ChineseLanguageCode = "zh-CN";
    public const string EnglishLanguageCode = "en-US";

    private static readonly string SettingsFilePath = Path.Combine(ModStudioPaths.RootPath, "settings.json");

    private static readonly IReadOnlyDictionary<string, ModStudioLocalizedText> Texts = CreateTexts();

    private static bool _initialized;
    private static ModStudioSettings _settings = new();

    public static event Action? LanguageChanged;

    public static string CurrentLanguageCode { get; private set; } = ChineseLanguageCode;

    public static bool IsChinese => string.Equals(CurrentLanguageCode, ChineseLanguageCode, StringComparison.OrdinalIgnoreCase);

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ModStudioPaths.EnsureAllDirectories();
        _settings = LoadSettings();
        CurrentLanguageCode = NormalizeLanguageCode(_settings.UiLanguageCode);
        if (!string.Equals(_settings.UiLanguageCode, CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            _settings.UiLanguageCode = CurrentLanguageCode;
            SaveSettings(_settings);
        }
    }

    public static bool SetLanguage(string languageCode)
    {
        Initialize();
        var normalized = NormalizeLanguageCode(languageCode);
        if (string.Equals(CurrentLanguageCode, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        CurrentLanguageCode = normalized;
        _settings.UiLanguageCode = normalized;
        SaveSettings(_settings);
        LanguageChanged?.Invoke();
        return true;
    }

    public static string T(string key)
    {
        Initialize();
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (!Texts.TryGetValue(key, out var text))
        {
            return key;
        }

        return IsChinese ? text.ZhCn : text.EnUs;
    }

    public static string F(string key, params object?[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, T(key), args);
    }

    public static string GetEntityKindDisplayName(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Character => T("entity.character"),
            ModStudioEntityKind.Card => T("entity.card"),
            ModStudioEntityKind.Relic => T("entity.relic"),
            ModStudioEntityKind.Potion => T("entity.potion"),
            ModStudioEntityKind.Event => T("entity.event"),
            ModStudioEntityKind.Enchantment => T("entity.enchantment"),
            ModStudioEntityKind.Monster => T("entity.monster"),
            _ => kind.ToString()
        };
    }

    private static ModStudioSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new ModStudioSettings();
            }

            return JsonSerializer.Deserialize<ModStudioSettings>(File.ReadAllText(SettingsFilePath), ModStudioJson.Options)
                ?? new ModStudioSettings();
        }
        catch
        {
            return new ModStudioSettings();
        }
    }

    private static void SaveSettings(ModStudioSettings settings)
    {
        ModStudioJson.Save(SettingsFilePath, settings);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return ChineseLanguageCode;
        }

        var normalized = languageCode.Trim();
        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return ChineseLanguageCode;
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return EnglishLanguageCode;
        }

        return ChineseLanguageCode;
    }

    private static IReadOnlyDictionary<string, ModStudioLocalizedText> CreateTexts()
    {
        var texts = new Dictionary<string, ModStudioLocalizedText>(StringComparer.Ordinal);

        static void Add(IDictionary<string, ModStudioLocalizedText> target, string key, string zhCn, string enUs)
        {
            target[key] = new ModStudioLocalizedText(zhCn, enUs);
        }

        Add(texts, "mod_studio.title", "模组工坊", "Mod Studio");
        Add(texts, "mod_studio.source_of_truth_note", "数据基准始终以运行时 ModelDb 和实时模型为准，`sts2_guides` 仅作辅助参考。", "Runtime truth source is ModelDb and live models. sts2_guides is auxiliary only.");
        Add(texts, "mod_studio.language", "语言", "Language");
        Add(texts, "language.zh", "中文", "Chinese");
        Add(texts, "language.en", "英文", "English");

        Add(texts, "tab.project_mode", "编辑模式", "Project Mode");
        Add(texts, "tab.package_mode", "模组模式", "Package Mode");

        Add(texts, "button.new", "新建", "New");
        Add(texts, "button.duplicate", "复制", "Duplicate");
        Add(texts, "button.delete", "删除", "Delete");
        Add(texts, "button.export_install", "导出并安装", "Export+Install");
        Add(texts, "button.capture_runtime", "抓取运行时", "Capture Runtime");
        Add(texts, "button.apply_event_template_scaffold", "事件模板脚手架", "Event Template Scaffold");
        Add(texts, "button.save_override", "保存覆盖", "Save Override");
        Add(texts, "button.save_graph", "保存 Graph", "Save Graph");
        Add(texts, "button.remove_override", "移除覆盖", "Remove Override");
        Add(texts, "button.import", "导入", "Import");
        Add(texts, "button.refresh", "刷新", "Refresh");
        Add(texts, "button.enable_disable", "启用/禁用", "Enable/Disable");
        Add(texts, "button.move_up", "上移", "Move Up");
        Add(texts, "button.move_down", "下移", "Move Down");
        Add(texts, "button.use_graph_behavior", "使用 Graph 行为", "Use Graph Behavior");
        Add(texts, "button.apply_default_scaffold", "应用默认骨架", "Apply Default Scaffold");

        Add(texts, "label.projects", "项目", "Projects");
        Add(texts, "label.current_project", "当前项目", "Current Project");
        Add(texts, "label.modeldb_browser", "ModelDb 浏览器", "ModelDb Browser");
        Add(texts, "label.override_editor", "覆盖编辑器", "Override Editor");
        Add(texts, "label.graph_id", "Graph ID", "Graph Id");
        Add(texts, "label.quick_presets", "快速模板", "Quick Presets");
        Add(texts, "label.graph_json", "Graph JSON", "Graph JSON");
        Add(texts, "label.graph_validation", "Graph 校验", "Graph Validation");
        Add(texts, "label.node_catalog", "节点目录", "Node Catalog");
        Add(texts, "label.metadata_json", "元数据 JSON", "Metadata JSON");
        Add(texts, "label.notes", "备注", "Notes");
        Add(texts, "label.installed_packages", "已安装模组包", "Installed Packages");
        Add(texts, "label.package_details", "模组包详情", "Package Details");
        Add(texts, "label.package_conflicts", "包冲突", "Package Conflicts");

        Add(texts, "placeholder.projects_intro", "选择一个项目，或新建一个项目后开始编辑覆盖。", "Select or create a project to begin editing overrides.");
        Add(texts, "placeholder.browser_intro", "选择一个运行时对象来查看并编辑它的覆盖快照。", "Select a runtime object to inspect and edit its override snapshot.");
        Add(texts, "placeholder.package_intro", "在这里导入 `.sts2pack`，并管理启用状态与加载顺序。", "Import a .sts2pack and manage enable state or load order here.");
        Add(texts, "placeholder.graph_validation_intro", "选择一个 graph，或套用模板后查看实时校验结果。", "Select a graph or apply a template to see live validation feedback.");
        Add(texts, "placeholder.node_catalog_intro", "当前运行时注册表里的节点定义会显示在这里。", "Node definitions from the active runtime registry will appear here.");
        Add(texts, "placeholder.no_projects", "未找到项目", "No projects found");
        Add(texts, "placeholder.no_projects_hint", "新建一个项目后即可开始制作模组包。", "Create a project to start building a package.");
        Add(texts, "placeholder.no_packages", "未安装任何模组包", "No packages installed");
        Add(texts, "placeholder.no_packages_hint", "导入 `.sts2pack`，或从编辑模式导出一个项目。", "Import or export a .sts2pack to populate package mode.");
        Add(texts, "placeholder.no_entries_title", "没有条目", "No entries");
        Add(texts, "placeholder.no_entries_kind", "没有找到任何 {0} 条目。", "No {0} entries were found.");
        Add(texts, "placeholder.no_selection", "未选择对象", "No selection");
        Add(texts, "placeholder.graph_presets_select", "选择一个运行时对象后，这里会显示可用的 graph 模板。", "Select a runtime object to see graph presets.");
        Add(texts, "placeholder.default_scaffold", "默认骨架", "Default Scaffold");
        Add(texts, "placeholder.default_scaffold_hint", "为当前对象创建一个最小的 entry -> exit graph。", "Create a minimal entry -> exit graph for the selected entity.");
        Add(texts, "placeholder.no_specialized_presets", "暂无专用模板", "No specialized presets");
        Add(texts, "placeholder.no_specialized_presets_hint", "当前实体类型暂时只有默认骨架模板。", "This entity kind currently only has the scaffold preset.");
        Add(texts, "placeholder.select_runtime_object", "选择一个运行时对象来查看它。", "Select a runtime object to inspect it.");
        Add(texts, "placeholder.graph_validation_select", "选择一个运行时对象来查看 graph 校验。", "Select a runtime object to inspect graph validation.");
        Add(texts, "placeholder.graph_disabled", "这个覆盖当前没有启用 graph 行为。开启它，或先套用一个模板后再校验。", "Graph behavior is disabled for this override. Toggle it on or apply a template to validate graph output.");
        Add(texts, "placeholder.node_catalog_select", "选择一个运行时对象来查看当前注册表节点目录。", "Select a runtime object to inspect the registry node catalog.");
        Add(texts, "placeholder.no_project_selected", "当前没有选中项目。", "No project selected.");
        Add(texts, "placeholder.no_package_selected", "当前没有选中模组包。", "No package selected.");
        Add(texts, "placeholder.package_conflicts_intro", "这里会显示当前选中模组包与其他已启用模组包之间的对象级覆盖冲突。后加载的包会整体覆盖前面的包。", "This panel shows object-level override conflicts between the selected package and other enabled packages. Later packages win.");
        Add(texts, "placeholder.no_package_conflicts", "当前选中的模组包没有检测到对象级覆盖冲突。", "No object-level override conflicts were detected for the selected package.");
        Add(texts, "placeholder.package_conflicts_more", "还有 {0} 条冲突未展开显示，请继续调整筛选或顺序。", "{0} more conflicts are hidden. Keep adjusting filters or load order.");
        Add(texts, "placeholder.import_package_title", "导入 STS2 模组包", "Import STS2 Package");
        Add(texts, "list.active", "当前", "Active");
        Add(texts, "list.enabled", "启用", "Enabled");
        Add(texts, "list.session_enabled", "会话启用", "Session On");

        Add(texts, "state.mode", "{0} | 项目 {1} | 模组包 {2} | 当前 {3} | {4}", "{0} | Project {1} | Packages {2} | Selected {3} | {4}");
        Add(texts, "state.ready", "就绪", "Ready");
        Add(texts, "state.none", "无", "none");

        Add(texts, "status.project_opened", "已打开项目“{0}”。", "Opened project '{0}'.");
        Add(texts, "status.project_created", "已创建项目“{0}”。", "Created project '{0}'.");
        Add(texts, "status.project_duplicated", "已复制项目为“{0}”。", "Duplicated project as '{0}'.");
        Add(texts, "status.project_deleted", "已删除项目“{0}”。", "Deleted project '{0}'.");
        Add(texts, "status.project_open_failed", "项目“{0}”无法打开。", "Project '{0}' could not be opened.");
        Add(texts, "status.project_delete_failed", "项目“{0}”无法删除。", "Project '{0}' could not be deleted.");
        Add(texts, "status.select_project_before_duplicate", "请先选择一个项目再执行复制。", "Select a project before duplicating it.");
        Add(texts, "status.select_project_before_delete", "请先选择一个项目再执行删除。", "Select a project before deleting it.");
        Add(texts, "status.select_project_before_export", "请先选择一个项目再执行导出。", "Select a project before exporting it.");
        Add(texts, "status.project_load_failed", "项目加载失败：{0}", "Project load failed: {0}");
        Add(texts, "status.create_project_failed", "创建项目失败：{0}", "Create project failed: {0}");
        Add(texts, "status.duplicate_project_failed", "复制项目失败：{0}", "Duplicate failed: {0}");
        Add(texts, "status.export_failed", "导出失败：{0}", "Export failed: {0}");
        Add(texts, "status.exported_and_installed", "已导出并安装“{0}”：{1}", "Exported and installed '{0}' to {1}.");
        Add(texts, "project.default_name", "新项目 {0}", "New Project {0}");

        Add(texts, "status.capture_runtime_missing_selection", "请先选择一个运行时对象再抓取元数据。", "Select a runtime object before capturing metadata.");
        Add(texts, "status.capture_runtime_done", "已抓取 {0}:{1} 的运行时字段。", "Captured runtime fields for {0}:{1}.");
        Add(texts, "status.select_project_before_event_template", "请先选择或创建一个项目，再应用事件模板脚手架。", "Select or create a project before applying an event template scaffold.");
        Add(texts, "status.event_template_only", "事件模板脚手架当前只支持事件对象。", "The event template scaffold is only available for event entities.");
        Add(texts, "status.event_template_scaffold_applied", "已为事件 {0} 写入模板脚手架元数据。", "Applied event template scaffold metadata for event {0}.");
        Add(texts, "status.event_template_scaffold_failed", "应用事件模板脚手架失败：{0}", "Apply event template scaffold failed: {0}");
        Add(texts, "status.select_project_before_override", "请先选择或创建一个项目再保存覆盖。", "Select or create a project before saving overrides.");
        Add(texts, "status.select_object_before_override", "请先选择一个运行时对象再保存覆盖。", "Select a runtime object before saving an override.");
        Add(texts, "status.saved_override", "已保存 {0}:{1} 的覆盖。", "Saved override for {0}:{1}.");
        Add(texts, "status.save_override_failed", "保存覆盖失败：{0}", "Save override failed: {0}");
        Add(texts, "status.select_project_before_save_graph", "请先选择或创建一个项目再保存 graph。", "Select or create a project before saving graph data.");
        Add(texts, "status.select_object_before_save_graph", "请先选择一个运行时对象再保存 graph。", "Select a runtime object before saving its graph.");
        Add(texts, "status.saved_graph", "已保存 graph“{0}”。", "Saved graph '{0}'.");
        Add(texts, "status.save_graph_failed", "保存 graph 失败：{0}", "Save graph failed: {0}");
        Add(texts, "status.select_project_before_remove_override", "请先选择一个项目再移除覆盖。", "Select a project before removing overrides.");
        Add(texts, "status.select_object_before_remove_override", "请先选择一个运行时对象再移除覆盖。", "Select a runtime object before removing its override.");
        Add(texts, "status.no_override_exists", "{0}:{1} 当前没有覆盖。", "No override existed for {0}:{1}.");
        Add(texts, "status.removed_override", "已移除 {0}:{1} 的覆盖。", "Removed override for {0}:{1}.");

        Add(texts, "status.package_dialog_unavailable", "模组包导入对话框当前不可用。", "Package import dialog is not available.");
        Add(texts, "status.package_imported", "已导入模组包“{0}”。", "Imported package '{0}'.");
        Add(texts, "status.package_import_failed", "导入失败：{0}", "Import failed: {0}");
        Add(texts, "status.package_catalog_refreshed", "已刷新模组包目录。", "Package catalog refreshed.");
        Add(texts, "status.select_package_before_toggle", "请先选择一个模组包再修改它的状态。", "Select a package before changing its state.");
        Add(texts, "status.toggled_package", "已切换模组包“{0}”的状态。", "Toggled package '{0}'.");
        Add(texts, "status.select_package_before_reorder", "请先选择一个模组包再调整顺序。", "Select a package before reordering it.");
        Add(texts, "status.package_order_unchanged", "模组包加载顺序没有变化。", "Package load order did not change.");
        Add(texts, "status.moved_package", "已移动模组包“{0}”。", "Moved package '{0}'.");
        Add(texts, "status.package_load_failed", "模组包加载失败：{0}", "Package load failed: {0}");

        Add(texts, "status.select_project_before_template", "请先选择或创建一个项目再应用 graph 模板。", "Select or create a project before applying a graph template.");
        Add(texts, "status.select_object_before_template", "请先选择一个运行时对象再应用 graph 模板。", "Select a runtime object before applying a graph template.");
        Add(texts, "status.applied_scaffold", "已为 {0}:{1} 应用默认 graph 骨架。", "Applied default graph scaffold for {0}:{1}.");
        Add(texts, "status.applied_template", "已为 {1}:{2} 应用 graph 模板“{0}”。", "Applied graph preset '{0}' for {1}:{2}.");
        Add(texts, "status.apply_template_failed", "应用模板失败：{0}", "Apply template failed: {0}");
        Add(texts, "status.language_switched", "界面语言已切换为{0}。", "UI language switched to {0}.");

        Add(texts, "graph.node_catalog_header", "{0} 节点目录", "Node catalog for {0}");
        Add(texts, "graph.node_catalog_authoritative", "内置节点来自当前运行时注册表，这也是编辑器应遵循的权威节点来源。", "Built-in nodes come from the active runtime registry and are the authoritative UI source.");
        Add(texts, "graph.recommended_presets", "推荐模板：", "Recommended presets:");
        Add(texts, "graph.preset_trigger", "触发器：{0}", "Trigger: {0}");
        Add(texts, "graph.catalog.inputs", "输入：{0}", "inputs: {0}");
        Add(texts, "graph.catalog.outputs", "输出：{0}", "outputs: {0}");
        Add(texts, "graph.catalog.defaults", "默认属性：{0}", "defaults: {0}");
        Add(texts, "graph.validation.parse_failed", "Graph JSON 无法解析。", "Graph JSON could not be parsed.");
        Add(texts, "graph.validation.empty", "Graph JSON 为空。", "Graph JSON is empty.");
        Add(texts, "graph.validation.deserialize_failed", "Graph JSON 无法反序列化。", "Graph JSON could not be deserialized.");
        Add(texts, "graph.validation.validate_failed", "Graph JSON 无法通过校验。", "Graph JSON could not be validated.");
        Add(texts, "graph.validation.no_graph", "当前没有载入 graph。", "No graph loaded.");
        Add(texts, "graph.validation.graph", "Graph：{0}", "Graph: {0}");
        Add(texts, "graph.validation.name", "名称：{0}", "Name: {0}");
        Add(texts, "graph.validation.entity_kind", "实体类型：{0}", "Entity Kind: {0}");
        Add(texts, "graph.validation.entry", "入口节点：{0}", "Entry: {0}");
        Add(texts, "graph.validation.nodes", "节点数：{0}", "Nodes: {0}");
        Add(texts, "graph.validation.connections", "连接数：{0}", "Connections: {0}");
        Add(texts, "graph.validation.valid", "状态：有效", "Status: Valid");
        Add(texts, "graph.validation.invalid", "状态：无效", "Status: Invalid");
        Add(texts, "graph.validation.errors", "错误：", "Errors:");
        Add(texts, "graph.validation.warnings", "警告：", "Warnings:");

        Add(texts, "detail.project_id", "项目 ID：{0}", "Project Id: {0}");
        Add(texts, "detail.name", "名称：{0}", "Name: {0}");
        Add(texts, "detail.author", "作者：{0}", "Author: {0}");
        Add(texts, "detail.description", "描述：{0}", "Description: {0}");
        Add(texts, "detail.target_game", "目标游戏版本：{0}", "Target Game: {0}");
        Add(texts, "detail.editor_version", "编辑器版本：{0}", "Editor Version: {0}");
        Add(texts, "detail.overrides", "覆盖数：{0}", "Overrides: {0}");
        Add(texts, "detail.graphs", "Graphs：{0}", "Graphs: {0}");
        Add(texts, "detail.assets", "资源数：{0}", "Assets: {0}");
        Add(texts, "detail.updated", "更新时间：{0:u}", "Updated: {0:u}");

        Add(texts, "detail.package_key", "包键：{0}", "Package Key: {0}");
        Add(texts, "detail.package_id", "包 ID：{0}", "Package Id: {0}");
        Add(texts, "detail.display_name", "显示名：{0}", "Display Name: {0}");
        Add(texts, "detail.version", "版本：{0}", "Version: {0}");
        Add(texts, "detail.checksum", "校验值：{0}", "Checksum: {0}");
        Add(texts, "detail.load_order", "加载顺序：{0}", "Load Order: {0}");
        Add(texts, "detail.enabled", "已启用：{0}", "Enabled: {0}");
        Add(texts, "detail.session_enabled", "会话启用：{0}", "Session Enabled: {0}");
        Add(texts, "detail.disabled_reason", "禁用原因：{0}", "Disabled Reason: {0}");
        Add(texts, "detail.package_file", "包文件：{0}", "Package File: {0}");
        Add(texts, "detail.override_count", "覆盖条目数：{0}", "Override Count: {0}");
        Add(texts, "detail.graph_count", "Graph 数：{0}", "Graph Count: {0}");
        Add(texts, "detail.asset_count", "资源条目数：{0}", "Asset Count: {0}");
        Add(texts, "detail.package_conflict_count", "冲突对象数：{0}", "Conflicting Objects: {0}");
        Add(texts, "detail.package_conflict_entry", "{0}:{1} | 生效包：{2} | 顺序链：{3}", "{0}:{1} | Winner: {2} | Chain: {3}");

        Add(texts, "detail.source_of_truth", "[数据基准] 运行时 {0}", "[Source Of Truth] runtime {0}");
        Add(texts, "detail.id", "ID：{0}", "Id: {0}");
        Add(texts, "detail.title", "标题：{0}", "Title: {0}");
        Add(texts, "detail.starting_hp", "初始生命：{0}", "Starting HP: {0}");
        Add(texts, "detail.starting_gold", "初始金币：{0}", "Starting Gold: {0}");
        Add(texts, "detail.max_energy", "最大能量：{0}", "Max Energy: {0}");
        Add(texts, "detail.base_orb_slot_count", "基础球槽：{0}", "Base Orb Slot Count: {0}");
        Add(texts, "detail.starting_deck_size", "起始卡组数量：{0}", "Starting Deck Size: {0}");
        Add(texts, "detail.starting_relics", "起始遗物：{0}", "Starting Relics: {0}");
        Add(texts, "detail.starting_potions", "起始药水：{0}", "Starting Potions: {0}");
        Add(texts, "detail.type", "类型：{0}", "Type: {0}");
        Add(texts, "detail.rarity", "稀有度：{0}", "Rarity: {0}");
        Add(texts, "detail.pool", "池：{0}", "Pool: {0}");
        Add(texts, "detail.portrait_path", "卡图路径：{0}", "Portrait Path: {0}");
        Add(texts, "detail.description_text", "描述：{0}", "Description: {0}");
        Add(texts, "detail.usage", "用途：{0}", "Usage: {0}");
        Add(texts, "detail.target_type", "目标类型：{0}", "Target Type: {0}");
        Add(texts, "detail.image_path", "图片路径：{0}", "Image Path: {0}");
        Add(texts, "detail.layout", "布局：{0}", "Layout: {0}");
        Add(texts, "detail.is_shared", "共享事件：{0}", "Is Shared: {0}");
        Add(texts, "detail.initial_description", "初始描述：{0}", "Initial Description: {0}");
        Add(texts, "detail.icon_path", "图标路径：{0}", "Icon Path: {0}");
        Add(texts, "detail.extra_card_text_enabled", "附加卡牌文本启用：{0}", "Extra Card Text Enabled: {0}");
        Add(texts, "detail.show_amount", "显示数值：{0}", "Show Amount: {0}");
        Add(texts, "event_template.default_initial_description", "这是一个 Mod Studio 事件模板起始页。", "This is a Mod Studio event template start page.");
        Add(texts, "event_template.default_option_continue_title", "继续", "Continue");
        Add(texts, "event_template.default_option_continue_description", "前往结束页。", "Go to the ending page.");
        Add(texts, "event_template.default_done_description", "这里是事件模板的结束页。", "This is the event template ending page.");

        Add(texts, "entity.character", "角色", "Character");
        Add(texts, "entity.card", "卡牌", "Card");
        Add(texts, "entity.relic", "遗物", "Relic");
        Add(texts, "entity.potion", "药水", "Potion");
        Add(texts, "entity.event", "事件", "Event");
        Add(texts, "entity.enchantment", "附魔", "Enchantment");
        Add(texts, "entity.monster", "怪物", "Monster");

        Add(texts, "bool.true", "是", "True");
        Add(texts, "bool.false", "否", "False");
        Add(texts, "misc.unknown", "未知", "unknown");
        Add(texts, "misc.unavailable", "<不可用：{0}>", "<unavailable: {0}>");

        Add(texts, "label.asset_binding", "\u8d44\u4ea7\u7ed1\u5b9a", "Asset Binding");
        Add(texts, "label.asset_catalog", "\u539f\u7248\u8d44\u4ea7\u76ee\u5f55", "Runtime Asset Catalog");
        Add(texts, "button.use_runtime_asset", "\u4f7f\u7528\u5f53\u524d\u539f\u7248\u8d44\u4ea7", "Use Runtime Asset");
        Add(texts, "button.apply_asset_path", "\u5e94\u7528\u8def\u5f84", "Apply Path");
        Add(texts, "button.import_external_asset", "\u5bfc\u5165\u5916\u90e8\u56fe\u50cf", "Import External Image");
        Add(texts, "button.clear_asset_override", "\u6e05\u9664\u8d44\u4ea7\u8986\u76d6", "Clear Asset Override");

        Add(texts, "placeholder.asset_binding_intro", "\u5728\u8fd9\u91cc\u7ed1\u5b9a\u539f\u7248\u6216\u5916\u90e8\u7f8e\u672f\u8d44\u6e90\u3002Phase 1 \u652f\u6301\u5361\u724c\u3001\u9057\u7269\u3001\u836f\u6c34\u3001\u4e8b\u4ef6\u9759\u6001\u56fe\u548c\u9644\u9b54\u56fe\u6807\u3002", "Bind original or external art assets here. Phase 1 supports card, relic, potion, event stills, and enchantment icons.");
        Add(texts, "placeholder.asset_binding_select", "\u5148\u9009\u4e2d\u4e00\u4e2a\u53ef\u652f\u6301\u7684\u8fd0\u884c\u65f6\u5bf9\u8c61\uff0c\u8fd9\u91cc\u4f1a\u663e\u793a\u8d44\u4ea7\u7ed1\u5b9a\u4fe1\u606f\u3002", "Select a supported runtime object to inspect and bind assets.");
        Add(texts, "placeholder.asset_binding_unsupported", "{0} \u5728 Phase 1 \u4e0d\u652f\u6301\u8fd9\u7c7b\u8d44\u4ea7\u7ed1\u5b9a\u3002", "{0} does not support this Phase 1 asset binding workflow.");
        Add(texts, "placeholder.asset_path", "\u8f93\u5165 res:// \u8def\u5f84\uff0c\u6216\u901a\u8fc7\u5bfc\u5165\u6309\u94ae\u7ed1\u5b9a\u5916\u90e8\u6587\u4ef6", "Enter a res:// path or import an external file");
        Add(texts, "placeholder.asset_catalog_filter", "\u8fc7\u6ee4\u539f\u7248\u8d44\u4ea7\u8def\u5f84", "Filter runtime asset paths");
        Add(texts, "placeholder.import_asset_title", "\u5bfc\u5165 Mod Studio \u8d44\u4ea7", "Import Mod Studio Asset");
        Add(texts, "placeholder.asset_catalog_unavailable", "\u672a\u63d0\u4f9b\u539f\u7248\u8d44\u4ea7\u5217\u8868", "Runtime catalog unavailable");
        Add(texts, "placeholder.asset_catalog_external_only", "\u5f53\u524d\u5b9e\u4f53\u53ef\u4ee5\u5bfc\u5165\u5916\u90e8\u8d44\u4ea7\uff0c\u4f46\u8fd8\u6ca1\u6709\u53ef\u76f4\u63a5\u5217\u51fa\u7684\u539f\u7248\u8def\u5f84\u3002", "This entity can import external assets, but no direct runtime catalog is available yet.");
        Add(texts, "placeholder.asset_catalog_no_match", "\u5f53\u524d\u8fc7\u6ee4\u6761\u4ef6\u4e0b\u6ca1\u6709\u627e\u5230\u5339\u914d\u7684\u539f\u7248\u8d44\u4ea7\u3002", "No runtime assets matched the current filter.");
        Add(texts, "placeholder.asset_catalog_truncated_title", "\u7ed3\u679c\u5df2\u622a\u65ad", "Results truncated");
        Add(texts, "placeholder.asset_catalog_truncated", "\u4ecd\u6709 {0} \u6761\u7ed3\u679c\u6ca1\u6709\u5c55\u793a\uff0c\u8bf7\u7ee7\u7eed\u8fc7\u6ee4\u3002", "{0} more results are hidden. Keep filtering to narrow the list.");

        Add(texts, "status.select_project_before_asset", "\u8bf7\u5148\u9009\u62e9\u6216\u521b\u5efa\u4e00\u4e2a\u9879\u76ee\uff0c\u518d\u7f16\u8f91\u8d44\u4ea7\u7ed1\u5b9a\u3002", "Select or create a project before editing asset bindings.");
        Add(texts, "status.select_object_before_asset", "\u8bf7\u5148\u9009\u4e2d\u4e00\u4e2a\u8fd0\u884c\u65f6\u5bf9\u8c61\uff0c\u518d\u7f16\u8f91\u8d44\u4ea7\u7ed1\u5b9a\u3002", "Select a runtime object before editing asset bindings.");
        Add(texts, "status.asset_binding_unsupported", "{0} \u6682\u4e0d\u652f\u6301\u8fd9\u7c7b\u8d44\u4ea7\u7ed1\u5b9a\u3002", "{0} does not support this asset binding workflow yet.");
        Add(texts, "status.asset_runtime_unavailable", "\u5f53\u524d\u9009\u4e2d\u5bf9\u8c61\u6ca1\u6709\u53ef\u76f4\u63a5\u590d\u7528\u7684\u539f\u7248\u8d44\u4ea7\u8def\u5f84\u3002", "The selected object does not expose a reusable runtime asset path.");
        Add(texts, "status.asset_path_required", "\u8bf7\u5148\u8f93\u5165\u4e00\u4e2a\u8d44\u4ea7\u8def\u5f84\uff0c\u6216\u4ece\u76ee\u5f55\u4e2d\u9009\u62e9\u4e00\u4e2a\u3002", "Enter an asset path or choose one from the catalog first.");
        Add(texts, "status.asset_dialog_unavailable", "\u8d44\u4ea7\u5bfc\u5165\u5bf9\u8bdd\u6846\u5f53\u524d\u4e0d\u53ef\u7528\u3002", "The asset import dialog is not available.");
        Add(texts, "status.bound_runtime_asset", "\u5df2\u4e3a {0} \u7ed1\u5b9a\u5f53\u524d\u539f\u7248\u8d44\u4ea7\u3002", "Bound the current runtime asset for {0}.");
        Add(texts, "status.bound_manual_asset", "\u5df2\u4e3a {0} \u5e94\u7528\u8d44\u4ea7\u8def\u5f84\u3002", "Applied the asset path for {0}.");
        Add(texts, "status.imported_external_asset", "\u5df2\u5bfc\u5165\u5916\u90e8\u8d44\u4ea7 {0}\uff0c\u5e76\u7ed1\u5b9a\u5230 {1}\u3002", "Imported external asset {0} and bound it to {1}.");
        Add(texts, "status.import_external_asset_failed", "\u5bfc\u5165\u5916\u90e8\u8d44\u4ea7\u5931\u8d25\uff1a{0}", "Import external asset failed: {0}");
        Add(texts, "status.cleared_asset_binding", "\u5df2\u6e05\u9664 {0} \u7684\u8d44\u4ea7\u7ed1\u5b9a\u3002", "Cleared the asset binding for {0}.");
        Add(texts, "status.clear_asset_binding_failed", "\u6e05\u9664\u8d44\u4ea7\u7ed1\u5b9a\u5931\u8d25\uff1a{0}", "Clear asset binding failed: {0}");
        Add(texts, "status.bind_asset_failed", "\u7ed1\u5b9a\u8d44\u4ea7\u5931\u8d25\uff1a{0}", "Bind asset failed: {0}");

        Add(texts, "asset.detail.role", "\u903b\u8f91\u7528\u9014\uff1a{0}", "Role: {0}");
        Add(texts, "asset.detail.metadata_key", "Metadata Key\uff1a{0}", "Metadata Key: {0}");
        Add(texts, "asset.detail.runtime_default", "\u5f53\u524d\u539f\u7248\u8def\u5f84\uff1a{0}", "Runtime Default: {0}");
        Add(texts, "asset.detail.current_binding", "\u5f53\u524d\u7ed1\u5b9a\uff1a{0}", "Current Binding: {0}");
        Add(texts, "asset.detail.resolved_path", "\u9884\u89c8\u89e3\u6790\u8def\u5f84\uff1a{0}", "Resolved Preview Path: {0}");
        Add(texts, "asset.detail.tracked_assets", "\u6258\u7ba1\u8d44\u4ea7\u6570\uff1a{0}", "Tracked Assets: {0}");
        Add(texts, "asset.catalog_click_to_apply", "\u70b9\u51fb\u5373\u53ef\u5e94\u7528\u5230\u5f53\u524d\u8986\u76d6", "Click to apply this path to the current override");
        Add(texts, "asset.role.card_portrait", "\u5361\u9762\u56fe", "Card Portrait");
        Add(texts, "asset.role.relic_icon", "\u9057\u7269\u56fe\u6807", "Relic Icon");
        Add(texts, "asset.role.potion_image", "\u836f\u6c34\u56fe\u50cf", "Potion Image");
        Add(texts, "asset.role.event_portrait", "\u4e8b\u4ef6\u56fe\u50cf", "Event Portrait");
        Add(texts, "asset.role.enchantment_icon", "\u9644\u9b54\u56fe\u6807", "Enchantment Icon");
        Add(texts, "misc.none", "\u65e0", "none");

        // Clean UTF-8 overrides for visible UI text.
        Add(texts, "mod_studio.title", "模组工坊", "Mod Studio");
        Add(texts, "mod_studio.source_of_truth_note", "数据基准始终以运行时 ModelDb 和实际模型为准，`sts2_guides` 仅作辅助参考。", "Runtime truth source is ModelDb and live models. sts2_guides is auxiliary only.");
        Add(texts, "mod_studio.language", "语言", "Language");
        Add(texts, "language.zh", "中文", "Chinese");
        Add(texts, "language.en", "英文", "English");

        Add(texts, "tab.project_mode", "编辑模式", "Project Mode");
        Add(texts, "tab.package_mode", "模组模式", "Package Mode");

        Add(texts, "button.new", "新建", "New");
        Add(texts, "button.duplicate", "复制", "Duplicate");
        Add(texts, "button.delete", "删除", "Delete");
        Add(texts, "button.export_install", "导出并安装", "Export+Install");
        Add(texts, "button.capture_runtime", "抓取运行时", "Capture Runtime");
        Add(texts, "button.save_override", "保存覆盖", "Save Override");
        Add(texts, "button.save_graph", "保存 Graph", "Save Graph");
        Add(texts, "button.remove_override", "移除覆盖", "Remove Override");
        Add(texts, "button.import", "导入", "Import");
        Add(texts, "button.refresh", "刷新", "Refresh");
        Add(texts, "button.enable_disable", "启用/禁用", "Enable/Disable");
        Add(texts, "button.move_up", "上移", "Move Up");
        Add(texts, "button.move_down", "下移", "Move Down");
        Add(texts, "button.use_graph_behavior", "使用 Graph 行为", "Use Graph Behavior");
        Add(texts, "button.apply_default_scaffold", "应用默认骨架", "Apply Default Scaffold");

        Add(texts, "label.projects", "项目", "Projects");
        Add(texts, "label.current_project", "当前项目", "Current Project");
        Add(texts, "label.modeldb_browser", "ModelDb 浏览器", "ModelDb Browser");
        Add(texts, "label.override_editor", "覆盖编辑器", "Override Editor");
        Add(texts, "label.graph_id", "Graph ID", "Graph Id");
        Add(texts, "label.quick_presets", "快速模板", "Quick Presets");
        Add(texts, "label.graph_json", "Graph JSON", "Graph JSON");
        Add(texts, "label.graph_validation", "Graph 校验", "Graph Validation");
        Add(texts, "label.node_catalog", "节点目录", "Node Catalog");
        Add(texts, "label.metadata_json", "元数据 JSON", "Metadata JSON");
        Add(texts, "label.notes", "备注", "Notes");
        Add(texts, "label.installed_packages", "已安装模组包", "Installed Packages");
        Add(texts, "label.package_details", "模组包详情", "Package Details");

        Add(texts, "placeholder.projects_intro", "选择一个项目，或新建一个项目后开始编辑覆盖。", "Select or create a project to begin editing overrides.");
        Add(texts, "placeholder.browser_intro", "选择一个运行时对象来查看并编辑它的覆盖快照。", "Select a runtime object to inspect and edit its override snapshot.");
        Add(texts, "placeholder.package_intro", "在这里导入 `.sts2pack`，并管理启用状态与加载顺序。", "Import a .sts2pack and manage enable state or load order here.");
        Add(texts, "placeholder.graph_validation_intro", "选择一个 graph，或套用模板后查看实时校验结果。", "Select a graph or apply a template to see live validation feedback.");
        Add(texts, "placeholder.node_catalog_intro", "当前运行时注册表里的节点定义会显示在这里。", "Node definitions from the active runtime registry will appear here.");
        Add(texts, "placeholder.no_projects", "未找到项目", "No projects found");
        Add(texts, "placeholder.no_projects_hint", "新建一个项目后即可开始制作模组包。", "Create a project to start building a package.");
        Add(texts, "placeholder.no_packages", "未安装任何模组包", "No packages installed");
        Add(texts, "placeholder.no_packages_hint", "导入 `.sts2pack`，或从编辑模式导出一个项目。", "Import or export a .sts2pack to populate package mode.");
        Add(texts, "placeholder.no_entries_title", "没有条目", "No entries");
        Add(texts, "placeholder.no_entries_kind", "没有找到任何 {0} 条目。", "No {0} entries were found.");
        Add(texts, "placeholder.no_selection", "未选择对象", "No selection");
        Add(texts, "placeholder.graph_presets_select", "选择一个运行时对象后，这里会显示可用的 graph 模板。", "Select a runtime object to see graph presets.");
        Add(texts, "placeholder.default_scaffold", "默认骨架", "Default Scaffold");
        Add(texts, "placeholder.default_scaffold_hint", "为当前对象创建一个最小的 entry -> exit graph。", "Create a minimal entry -> exit graph for the selected entity.");
        Add(texts, "placeholder.no_specialized_presets", "暂无专用模板", "No specialized presets");
        Add(texts, "placeholder.no_specialized_presets_hint", "当前实体类型暂时只有默认骨架模板。", "This entity kind currently only has the scaffold preset.");
        Add(texts, "placeholder.select_runtime_object", "选择一个运行时对象来查看它。", "Select a runtime object to inspect it.");
        Add(texts, "placeholder.graph_validation_select", "选择一个运行时对象来查看 graph 校验。", "Select a runtime object to inspect graph validation.");
        Add(texts, "placeholder.graph_disabled", "这个覆盖当前没有启用 graph 行为。开启它，或先套用一个模板后再校验。", "Graph behavior is disabled for this override. Toggle it on or apply a template to validate graph output.");
        Add(texts, "placeholder.node_catalog_select", "选择一个运行时对象来查看当前注册节点目录。", "Select a runtime object to inspect the registry node catalog.");
        Add(texts, "placeholder.no_project_selected", "当前没有选中项目。", "No project selected.");
        Add(texts, "placeholder.no_package_selected", "当前没有选中模组包。", "No package selected.");
        Add(texts, "placeholder.import_package_title", "导入 STS2 模组包", "Import STS2 Package");

        Add(texts, "list.active", "当前", "Active");
        Add(texts, "list.enabled", "启用", "Enabled");
        Add(texts, "list.session_enabled", "会话启用", "Session On");

        Add(texts, "state.mode", "{0} | 项目 {1} | 模组包 {2} | 当前 {3} | {4}", "{0} | Project {1} | Packages {2} | Selected {3} | {4}");
        Add(texts, "state.ready", "就绪", "Ready");
        Add(texts, "state.none", "无", "none");

        Add(texts, "project.default_name", "新项目 {0}", "New Project {0}");
        Add(texts, "status.project_opened", "已打开项目“{0}”。", "Opened project '{0}'.");
        Add(texts, "status.project_created", "已创建项目“{0}”。", "Created project '{0}'.");
        Add(texts, "status.project_duplicated", "已复制项目为“{0}”。", "Duplicated project as '{0}'.");
        Add(texts, "status.project_deleted", "已删除项目“{0}”。", "Deleted project '{0}'.");
        Add(texts, "status.project_open_failed", "项目“{0}”无法打开。", "Project '{0}' could not be opened.");
        Add(texts, "status.project_delete_failed", "项目“{0}”无法删除。", "Project '{0}' could not be deleted.");
        Add(texts, "status.select_project_before_duplicate", "请先选择一个项目再执行复制。", "Select a project before duplicating it.");
        Add(texts, "status.select_project_before_delete", "请先选择一个项目再执行删除。", "Select a project before deleting it.");
        Add(texts, "status.select_project_before_export", "请先选择一个项目再执行导出。", "Select a project before exporting it.");
        Add(texts, "status.project_load_failed", "项目加载失败：{0}", "Project load failed: {0}");
        Add(texts, "status.create_project_failed", "创建项目失败：{0}", "Create project failed: {0}");
        Add(texts, "status.duplicate_project_failed", "复制项目失败：{0}", "Duplicate failed: {0}");
        Add(texts, "status.export_failed", "导出失败：{0}", "Export failed: {0}");
        Add(texts, "status.exported_and_installed", "已导出并安装“{0}”：{1}", "Exported and installed '{0}' to {1}.");
        Add(texts, "status.language_switched", "界面语言已切换为 {0}。", "UI language switched to {0}.");

        Add(texts, "status.capture_runtime_missing_selection", "请先选择一个运行时对象再抓取元数据。", "Select a runtime object before capturing metadata.");
        Add(texts, "status.capture_runtime_done", "已抓取 {0}:{1} 的运行时字段。", "Captured runtime fields for {0}:{1}.");
        Add(texts, "status.select_project_before_override", "请先选择或创建一个项目再保存覆盖。", "Select or create a project before saving overrides.");
        Add(texts, "status.select_object_before_override", "请先选择一个运行时对象再保存覆盖。", "Select a runtime object before saving an override.");
        Add(texts, "status.saved_override", "已保存 {0}:{1} 的覆盖。", "Saved override for {0}:{1}.");
        Add(texts, "status.save_override_failed", "保存覆盖失败：{0}", "Save override failed: {0}");
        Add(texts, "status.select_project_before_save_graph", "请先选择或创建一个项目再保存 graph。", "Select or create a project before saving graph data.");
        Add(texts, "status.select_object_before_save_graph", "请先选择一个运行时对象再保存 graph。", "Select a runtime object before saving its graph.");
        Add(texts, "status.saved_graph", "已保存 graph“{0}”。", "Saved graph '{0}'.");
        Add(texts, "status.save_graph_failed", "保存 graph 失败：{0}", "Save graph failed: {0}");
        Add(texts, "status.select_project_before_remove_override", "请先选择一个项目再移除覆盖。", "Select a project before removing overrides.");
        Add(texts, "status.select_object_before_remove_override", "请先选择一个运行时对象再移除覆盖。", "Select a runtime object before removing its override.");
        Add(texts, "status.no_override_exists", "{0}:{1} 当前没有覆盖。", "No override existed for {0}:{1}.");
        Add(texts, "status.removed_override", "已移除 {0}:{1} 的覆盖。", "Removed override for {0}:{1}.");

        Add(texts, "status.package_dialog_unavailable", "模组包导入对话框当前不可用。", "Package import dialog is not available.");
        Add(texts, "status.package_imported", "已导入模组包“{0}”。", "Imported package '{0}'.");
        Add(texts, "status.package_import_failed", "导入失败：{0}", "Import failed: {0}");
        Add(texts, "status.package_catalog_refreshed", "已刷新模组包目录。", "Package catalog refreshed.");
        Add(texts, "status.select_package_before_toggle", "请先选择一个模组包再修改它的状态。", "Select a package before changing its state.");
        Add(texts, "status.toggled_package", "已切换模组包“{0}”的状态。", "Toggled package '{0}'.");
        Add(texts, "status.select_package_before_reorder", "请先选择一个模组包再调整顺序。", "Select a package before reordering it.");
        Add(texts, "status.package_order_unchanged", "模组包加载顺序没有变化。", "Package load order did not change.");
        Add(texts, "status.moved_package", "已移动模组包“{0}”。", "Moved package '{0}'.");
        Add(texts, "status.package_load_failed", "模组包加载失败：{0}", "Package load failed: {0}");

        Add(texts, "status.select_project_before_template", "请先选择或创建一个项目再应用 graph 模板。", "Select or create a project before applying a graph template.");
        Add(texts, "status.select_object_before_template", "请先选择一个运行时对象再应用 graph 模板。", "Select a runtime object before applying a graph template.");
        Add(texts, "status.applied_scaffold", "已为 {0}:{1} 应用默认 graph 骨架。", "Applied default graph scaffold for {0}:{1}.");
        Add(texts, "status.applied_template", "已为 {1}:{2} 应用 graph 模板“{0}”。", "Applied graph preset '{0}' for {1}:{2}.");
        Add(texts, "status.apply_template_failed", "应用模板失败：{0}", "Apply template failed: {0}");

        Add(texts, "graph.node_catalog_header", "{0} 节点目录", "Node catalog for {0}");
        Add(texts, "graph.node_catalog_authoritative", "内置节点来自当前运行时注册表，这也是编辑器应遵循的权威节点来源。", "Built-in nodes come from the active runtime registry and are the authoritative UI source.");
        Add(texts, "graph.recommended_presets", "推荐模板：", "Recommended presets:");
        Add(texts, "graph.preset_trigger", "触发器：{0}", "Trigger: {0}");
        Add(texts, "graph.catalog.inputs", "输入：{0}", "inputs: {0}");
        Add(texts, "graph.catalog.outputs", "输出：{0}", "outputs: {0}");
        Add(texts, "graph.catalog.defaults", "默认属性：{0}", "defaults: {0}");
        Add(texts, "graph.validation.parse_failed", "Graph JSON 无法解析。", "Graph JSON could not be parsed.");
        Add(texts, "graph.validation.empty", "Graph JSON 为空。", "Graph JSON is empty.");
        Add(texts, "graph.validation.deserialize_failed", "Graph JSON 无法反序列化。", "Graph JSON could not be deserialized.");
        Add(texts, "graph.validation.validate_failed", "Graph JSON 无法通过校验。", "Graph JSON could not be validated.");
        Add(texts, "graph.validation.no_graph", "当前没有载入 graph。", "No graph loaded.");
        Add(texts, "graph.validation.graph", "Graph：{0}", "Graph: {0}");
        Add(texts, "graph.validation.name", "名称：{0}", "Name: {0}");
        Add(texts, "graph.validation.entity_kind", "实体类型：{0}", "Entity Kind: {0}");
        Add(texts, "graph.validation.entry", "入口节点：{0}", "Entry: {0}");
        Add(texts, "graph.validation.nodes", "节点数：{0}", "Nodes: {0}");
        Add(texts, "graph.validation.connections", "连接数：{0}", "Connections: {0}");
        Add(texts, "graph.validation.valid", "状态：有效", "Status: Valid");
        Add(texts, "graph.validation.invalid", "状态：无效", "Status: Invalid");
        Add(texts, "graph.validation.errors", "错误：", "Errors:");
        Add(texts, "graph.validation.warnings", "警告：", "Warnings:");

        Add(texts, "detail.project_id", "项目 ID：{0}", "Project Id: {0}");
        Add(texts, "detail.name", "名称：{0}", "Name: {0}");
        Add(texts, "detail.author", "作者：{0}", "Author: {0}");
        Add(texts, "detail.description", "描述：{0}", "Description: {0}");
        Add(texts, "detail.target_game", "目标游戏版本：{0}", "Target Game: {0}");
        Add(texts, "detail.editor_version", "编辑器版本：{0}", "Editor Version: {0}");
        Add(texts, "detail.overrides", "覆盖数：{0}", "Overrides: {0}");
        Add(texts, "detail.graphs", "Graphs：{0}", "Graphs: {0}");
        Add(texts, "detail.assets", "资源数：{0}", "Assets: {0}");
        Add(texts, "detail.updated", "更新时间：{0:u}", "Updated: {0:u}");
        Add(texts, "detail.package_key", "包键：{0}", "Package Key: {0}");
        Add(texts, "detail.package_id", "包 ID：{0}", "Package Id: {0}");
        Add(texts, "detail.display_name", "显示名：{0}", "Display Name: {0}");
        Add(texts, "detail.version", "版本：{0}", "Version: {0}");
        Add(texts, "detail.checksum", "校验值：{0}", "Checksum: {0}");
        Add(texts, "detail.load_order", "加载顺序：{0}", "Load Order: {0}");
        Add(texts, "detail.enabled", "已启用：{0}", "Enabled: {0}");
        Add(texts, "detail.session_enabled", "会话启用：{0}", "Session Enabled: {0}");
        Add(texts, "detail.disabled_reason", "禁用原因：{0}", "Disabled Reason: {0}");
        Add(texts, "detail.package_file", "包文件：{0}", "Package File: {0}");
        Add(texts, "detail.override_count", "覆盖条目数：{0}", "Override Count: {0}");
        Add(texts, "detail.graph_count", "Graph 数：{0}", "Graph Count: {0}");
        Add(texts, "detail.asset_count", "资源条目数：{0}", "Asset Count: {0}");
        Add(texts, "detail.source_of_truth", "[数据基准] 运行时 {0}", "[Source Of Truth] runtime {0}");
        Add(texts, "detail.id", "ID：{0}", "Id: {0}");
        Add(texts, "detail.title", "标题：{0}", "Title: {0}");
        Add(texts, "detail.starting_hp", "初始生命：{0}", "Starting HP: {0}");
        Add(texts, "detail.starting_gold", "初始金币：{0}", "Starting Gold: {0}");
        Add(texts, "detail.max_energy", "最大能量：{0}", "Max Energy: {0}");
        Add(texts, "detail.base_orb_slot_count", "基础球槽：{0}", "Base Orb Slot Count: {0}");
        Add(texts, "detail.starting_deck_size", "起始卡组数量：{0}", "Starting Deck Size: {0}");
        Add(texts, "detail.starting_relics", "起始遗物：{0}", "Starting Relics: {0}");
        Add(texts, "detail.starting_potions", "起始药水：{0}", "Starting Potions: {0}");
        Add(texts, "detail.type", "类型：{0}", "Type: {0}");
        Add(texts, "detail.rarity", "稀有度：{0}", "Rarity: {0}");
        Add(texts, "detail.pool", "池：{0}", "Pool: {0}");
        Add(texts, "detail.portrait_path", "卡图路径：{0}", "Portrait Path: {0}");
        Add(texts, "detail.description_text", "描述：{0}", "Description: {0}");
        Add(texts, "detail.usage", "用途：{0}", "Usage: {0}");
        Add(texts, "detail.target_type", "目标类型：{0}", "Target Type: {0}");
        Add(texts, "detail.image_path", "图片路径：{0}", "Image Path: {0}");
        Add(texts, "detail.layout", "布局：{0}", "Layout: {0}");
        Add(texts, "detail.is_shared", "共享事件：{0}", "Is Shared: {0}");
        Add(texts, "detail.initial_description", "初始描述：{0}", "Initial Description: {0}");
        Add(texts, "detail.icon_path", "图标路径：{0}", "Icon Path: {0}");
        Add(texts, "detail.extra_card_text_enabled", "附加卡牌文本启用：{0}", "Extra Card Text Enabled: {0}");
        Add(texts, "detail.show_amount", "显示数值：{0}", "Show Amount: {0}");

        Add(texts, "entity.character", "角色", "Character");
        Add(texts, "entity.card", "卡牌", "Card");
        Add(texts, "entity.relic", "遗物", "Relic");
        Add(texts, "entity.potion", "药水", "Potion");
        Add(texts, "entity.event", "事件", "Event");
        Add(texts, "entity.enchantment", "附魔", "Enchantment");
        Add(texts, "bool.true", "是", "True");
        Add(texts, "bool.false", "否", "False");
        Add(texts, "misc.unknown", "未知", "unknown");
        Add(texts, "misc.unavailable", "<不可用：{0}>", "<unavailable: {0}>");

        Add(texts, "button.new_entry", "\u65b0\u5efa\u6761\u76ee", "New Entry");
        Add(texts, "placeholder.project_only_entry", "\u9879\u76ee\u6682\u5b58\u81ea\u5b9a\u4e49\u6761\u76ee", "Project-staged custom entry");
        Add(texts, "status.select_project_before_new_entry", "\u8bf7\u5148\u9009\u62e9\u6216\u521b\u5efa\u4e00\u4e2a\u9879\u76ee\uff0c\u518d\u65b0\u5efa\u6761\u76ee\u3002", "Select or create a project before creating a new entry.");
        Add(texts, "status.new_entry_unsupported", "{0} \u5728 Phase 1 \u6682\u4e0d\u652f\u6301\u65b0\u5efa\u3002", "{0} does not support creation in Phase 1.");
        Add(texts, "status.created_entry", "\u5df2\u4e3a {0} \u521b\u5efa\u65b0\u6761\u76ee {1}\u3002", "Created new {0} entry {1}.");
        Add(texts, "status.create_entry_failed", "\u65b0\u5efa\u6761\u76ee\u5931\u8d25\uff1a{0}", "Create entry failed: {0}");
        Add(texts, "status.capture_runtime_unavailable_project_entry", "\u9879\u76ee\u6682\u5b58\u7684\u65b0\u6761\u76ee\u8fd8\u6ca1\u6709\u5bf9\u5e94\u7684\u8fd0\u884c\u65f6\u539f\u578b\uff0c\u65e0\u6cd5\u6293\u53d6 Runtime \u5b57\u6bb5\u3002", "Project-staged custom entries do not have a live runtime prototype yet, so runtime capture is unavailable.");
        Add(texts, "detail.energy_cost", "\u80fd\u91cf\u8d39\u7528\uff1a{0}", "Energy Cost: {0}");
        Add(texts, "detail.behavior_source", "\u884c\u4e3a\u6765\u6e90\uff1a{0}", "Behavior Source: {0}");
        Add(texts, "detail.graph_id", "Graph Id\uff1a{0}", "Graph Id: {0}");
        Add(texts, "detail.project_staged_source", "[\u9879\u76ee\u6682\u5b58] {0}", "[Project Staged] {0}");
        Add(texts, "default.card_title", "\u65b0\u5361\u724c {0}", "New Card {0}");
        Add(texts, "default.card_description", "Mod Studio \u65b0\u5361\u724c\u63cf\u8ff0\u3002", "Mod Studio custom card description.");
        Add(texts, "default.relic_title", "\u65b0\u9057\u7269 {0}", "New Relic {0}");
        Add(texts, "default.relic_description", "Mod Studio \u65b0\u9057\u7269\u63cf\u8ff0\u3002", "Mod Studio custom relic description.");
        Add(texts, "default.potion_title", "\u65b0\u836f\u6c34 {0}", "New Potion {0}");
        Add(texts, "default.potion_description", "Mod Studio \u65b0\u836f\u6c34\u63cf\u8ff0\u3002", "Mod Studio custom potion description.");
        Add(texts, "default.event_title", "\u65b0\u4e8b\u4ef6 {0}", "New Event {0}");
        Add(texts, "default.event_initial_description", "\u8fd9\u662f Mod Studio \u65b0\u4e8b\u4ef6\u7684\u8d77\u59cb\u9875\u3002", "This is the start page for a new Mod Studio event.");
        Add(texts, "default.event_continue_description", "\u524d\u5f80\u7ed3\u675f\u9875\u3002", "Move to the ending page.");
        Add(texts, "default.event_done_description", "\u8fd9\u662f Mod Studio \u65b0\u4e8b\u4ef6\u7684\u7ed3\u675f\u9875\u3002", "This is the ending page for a new Mod Studio event.");
        Add(texts, "default.enchantment_title", "\u65b0\u9644\u9b54 {0}", "New Enchantment {0}");
        Add(texts, "default.enchantment_description", "Mod Studio \u65b0\u9644\u9b54\u63cf\u8ff0\u3002", "Mod Studio custom enchantment description.");
        Add(texts, "default.entry_notes", "\u901a\u8fc7 Mod Studio \u65b0\u5efa\u7684\u6761\u76ee\u3002", "Created through Mod Studio.");

        return texts;
    }

    private sealed class ModStudioLocalizedText
    {
        public ModStudioLocalizedText(string zhCn, string enUs)
        {
            ZhCn = zhCn;
            EnUs = enUs;
        }

        public string ZhCn { get; }

        public string EnUs { get; }
    }
}
