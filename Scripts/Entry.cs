using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2_Editor.Scripts.Editor;

namespace STS2_Editor.Scripts;

[ModInitializer("Init")]
public class Entry
{
    // 初始化函数
    public static void Init()
    {
        ModStudioBootstrap.Initialize();

        var harmony = new Harmony("sts2.yunxuan.STS2_Editor");
        harmony.PatchAll();

        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
        Log.Debug("Mod initialized with Mod Studio bootstrap.");
    }
}
