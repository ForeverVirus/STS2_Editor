using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Timeline;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeModelIdSerializationCacheBridge
{
    private static readonly Type CacheType = typeof(ModelIdSerializationCache);
    private static readonly FieldInfo CategoryMapField = CacheType.GetField("_categoryNameToNetIdMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo CategoryListField = CacheType.GetField("_netIdToCategoryNameMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo EntryMapField = CacheType.GetField("_entryNameToNetIdMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo EntryListField = CacheType.GetField("_netIdToEntryNameMap", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo CategoryBitSizeProperty = CacheType.GetProperty(nameof(ModelIdSerializationCache.CategoryIdBitSize), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo EntryBitSizeProperty = CacheType.GetProperty(nameof(ModelIdSerializationCache.EntryIdBitSize), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo HashProperty = CacheType.GetProperty(nameof(ModelIdSerializationCache.Hash), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

    public static void EnsureMapped(ModelId modelId)
    {
        var categoryMap = (Dictionary<string, int>)CategoryMapField.GetValue(null)!;
        var categoryList = (List<string>)CategoryListField.GetValue(null)!;
        var entryMap = (Dictionary<string, int>)EntryMapField.GetValue(null)!;
        var entryList = (List<string>)EntryListField.GetValue(null)!;

        var changed = false;
        if (!categoryMap.ContainsKey(modelId.Category))
        {
            categoryMap[modelId.Category] = categoryList.Count;
            categoryList.Add(modelId.Category);
            changed = true;
        }

        if (!entryMap.ContainsKey(modelId.Entry))
        {
            entryMap[modelId.Entry] = entryList.Count;
            entryList.Add(modelId.Entry);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        RecomputeDerivedValues(categoryList, entryList);
    }

    private static void RecomputeDerivedValues(IReadOnlyList<string> categories, IReadOnlyList<string> entries)
    {
        CategoryBitSizeProperty.SetValue(null, ComputeBitSize(categories.Count));
        EntryBitSizeProperty.SetValue(null, ComputeBitSize(entries.Count));
        HashProperty.SetValue(null, ComputeHash(categories, entries));
    }

    private static int ComputeBitSize(int count)
    {
        return count <= 1 ? 0 : Mathf.CeilToInt(Math.Log2(count));
    }

    private static uint ComputeHash(IReadOnlyList<string> categories, IReadOnlyList<string> entries)
    {
        var hash = 2166136261u;

        foreach (var category in categories)
        {
            hash = AppendString(hash, category);
        }

        foreach (var entry in entries)
        {
            hash = AppendString(hash, entry);
        }

        foreach (var epochId in EpochModel.AllEpochIds)
        {
            hash = AppendString(hash, epochId);
        }

        hash = AppendInt32(hash, categories.Count);
        hash = AppendInt32(hash, entries.Count);
        hash = AppendInt32(hash, EpochModel.AllEpochIds.Count);

        return hash;
    }

    private static uint AppendString(uint hash, string value)
    {
        unchecked
        {
            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            hash ^= 0;
            hash *= 16777619u;
            return hash;
        }
    }

    private static uint AppendInt32(uint hash, int value)
    {
        unchecked
        {
            hash ^= (byte)(value & 0xFF);
            hash *= 16777619u;
            hash ^= (byte)((value >> 8) & 0xFF);
            hash *= 16777619u;
            hash ^= (byte)((value >> 16) & 0xFF);
            hash *= 16777619u;
            hash ^= (byte)((value >> 24) & 0xFF);
            hash *= 16777619u;
            return hash;
        }
    }
}
