using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace STS2_Editor.Scripts;

[ModInitializer("Init")]
public class Entry
{
    // 初始化函数
    public static void Init()
    {
        var harmony = new Harmony("sts2.yunxuan.STS2_Editor");
        harmony.PatchAll();

        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
        Log.Debug("Mod initialized!");
    }
}
