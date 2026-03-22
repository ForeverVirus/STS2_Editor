using System.Collections;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class RuntimeDynamicContentRegistry
{
    private readonly Dictionary<RuntimeEntityKey, AbstractModel> _registered = new();
    private readonly HashSet<RuntimeEntityKey> _activeCustomEntries = new();
    private readonly Dictionary<RuntimeEntityKey, string> _runtimeToEditorEntityIds = new();
    private readonly Dictionary<ModStudioEntityKind, HashSet<string>> _vanillaIds = new();
    private bool _vanillaIdsCaptured;

    public IReadOnlyCollection<RuntimeEntityKey> ActiveCustomEntries => _activeCustomEntries;

    public void Synchronize(RuntimeOverrideResolutionResult resolution)
    {
        EnsureVanillaIdsCaptured();

        _activeCustomEntries.Clear();
        foreach (var pair in resolution.Overrides
                     .OrderBy(pair => (int)pair.Key.Kind)
                     .ThenBy(pair => pair.Key.EntityId, StringComparer.Ordinal))
        {
            if (!SupportsDynamicRegistration(pair.Key.Kind) || IsVanilla(pair.Key.Kind, pair.Key.EntityId))
            {
                continue;
            }

            EnsureRegistered(pair.Key.Kind, pair.Key.EntityId);
            _activeCustomEntries.Add(pair.Key);
        }
    }

    public bool TryGetRegisteredModel<TModel>(ModStudioEntityKind kind, string entityId, out TModel? model)
        where TModel : AbstractModel
    {
        model = null;
        var resolvedEntityId = ResolveEditorEntityId(kind, entityId);
        if (_registered.TryGetValue(new RuntimeEntityKey(kind, resolvedEntityId), out var raw) && raw is TModel typed)
        {
            model = typed;
            return true;
        }

        return false;
    }

    public string ResolveEditorEntityId(ModStudioEntityKind kind, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return entityId;
        }

        return _runtimeToEditorEntityIds.TryGetValue(new RuntimeEntityKey(kind, entityId), out var resolved)
            ? resolved
            : entityId;
    }

    public IEnumerable<CardModel> GetActiveCardsForPool(string poolId)
    {
        foreach (var entry in _activeCustomEntries.Where(entry => entry.Kind == ModStudioEntityKind.Card))
        {
            if (!ModStudioBootstrap.RuntimeRegistry.TryGetOverride(ModStudioEntityKind.Card, entry.EntityId, out var envelope) ||
                envelope == null ||
                !RuntimeOverrideMetadata.TryGetMetadata(ModStudioEntityKind.Card, entry.EntityId, "pool_id", out var currentPoolId) ||
                !string.Equals(currentPoolId, poolId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryGetRegisteredModel<CardModel>(entry.Kind, entry.EntityId, out var card) && card != null)
            {
                yield return card;
            }
        }
    }

    public IEnumerable<RelicModel> GetActiveRelicsForPool(string poolId)
    {
        foreach (var entry in _activeCustomEntries.Where(entry => entry.Kind == ModStudioEntityKind.Relic))
        {
            if (!RuntimeOverrideMetadata.TryGetMetadata(ModStudioEntityKind.Relic, entry.EntityId, "pool_id", out var currentPoolId) ||
                !string.Equals(currentPoolId, poolId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryGetRegisteredModel<RelicModel>(entry.Kind, entry.EntityId, out var relic) && relic != null)
            {
                yield return relic;
            }
        }
    }

    public IEnumerable<PotionModel> GetActivePotionsForPool(string poolId)
    {
        foreach (var entry in _activeCustomEntries.Where(entry => entry.Kind == ModStudioEntityKind.Potion))
        {
            if (!RuntimeOverrideMetadata.TryGetMetadata(ModStudioEntityKind.Potion, entry.EntityId, "pool_id", out var currentPoolId) ||
                !string.Equals(currentPoolId, poolId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryGetRegisteredModel<PotionModel>(entry.Kind, entry.EntityId, out var potion) && potion != null)
            {
                yield return potion;
            }
        }
    }

    public IEnumerable<EventModel> GetActiveEvents(string? actId = null, bool sharedOnly = false)
    {
        foreach (var entry in _activeCustomEntries.Where(entry => entry.Kind == ModStudioEntityKind.Event))
        {
            var isShared = RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Event, entry.EntityId, "is_shared", out var sharedValue) && sharedValue;
            if (sharedOnly && !isShared)
            {
                continue;
            }

            if (!sharedOnly && !IsEventAllowedForAct(entry.EntityId, actId))
            {
                continue;
            }

            if (TryGetRegisteredModel<EventModel>(entry.Kind, entry.EntityId, out var evt) && evt != null)
            {
                yield return evt;
            }
        }
    }

    public IEnumerable<EnchantmentModel> GetActiveEnchantments()
    {
        foreach (var entry in _activeCustomEntries.Where(entry => entry.Kind == ModStudioEntityKind.Enchantment))
        {
            if (TryGetRegisteredModel<EnchantmentModel>(entry.Kind, entry.EntityId, out var enchantment) && enchantment != null)
            {
                yield return enchantment;
            }
        }
    }

    public bool IsCustomActive(ModStudioEntityKind kind, string entityId)
    {
        return _activeCustomEntries.Contains(new RuntimeEntityKey(kind, ResolveEditorEntityId(kind, entityId)));
    }

    public bool IsVanilla(ModStudioEntityKind kind, string entityId)
    {
        EnsureVanillaIdsCaptured();
        return _vanillaIds.TryGetValue(kind, out var ids) && ids.Contains(entityId);
    }

    public static bool SupportsDynamicRegistration(ModStudioEntityKind kind)
    {
        return kind is ModStudioEntityKind.Card
            or ModStudioEntityKind.Relic
            or ModStudioEntityKind.Potion
            or ModStudioEntityKind.Event
            or ModStudioEntityKind.Enchantment;
    }

    private void EnsureRegistered(ModStudioEntityKind kind, string entityId)
    {
        var key = new RuntimeEntityKey(kind, entityId);
        if (_registered.ContainsKey(key))
        {
            return;
        }

        var type = RuntimeDynamicTypeFactory.GetOrCreate(kind, entityId);
        try
        {
            RuntimeModelIdSerializationCacheBridge.EnsureMapped(ModelDb.GetId(type));
            ModelDb.Inject(type);
            var modelId = ModelDb.GetId(type);
            var model = (AbstractModel)typeof(ModelDb)
                .GetMethod(nameof(ModelDb.GetById), [typeof(ModelId)])!
                .MakeGenericMethod(GetModelType(kind))
                .Invoke(null, [modelId])!;
            model.InitId(model.Id);
            _registered[key] = model;
            _runtimeToEditorEntityIds[new RuntimeEntityKey(kind, entityId)] = entityId;
            _runtimeToEditorEntityIds[new RuntimeEntityKey(kind, model.Id.Entry)] = entityId;
            Log.Info($"Mod Studio registered dynamic {kind} '{entityId}' as runtime type '{type.FullName}'.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Mod Studio failed to register dynamic {kind} '{entityId}': {ex.Message}");
        }
    }

    private void EnsureVanillaIdsCaptured()
    {
        if (_vanillaIdsCaptured)
        {
            return;
        }

        _vanillaIdsCaptured = true;
        foreach (var kind in Enum.GetValues<ModStudioEntityKind>())
        {
            _vanillaIds[kind] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var type in ModelDb.AllAbstractModelSubtypes)
        {
            if (!TryGetSupportedKind(type, out var kind))
            {
                continue;
            }

            _vanillaIds[kind].Add(ModelDb.GetId(type).Entry);
        }
    }

    private static bool TryGetSupportedKind(Type type, out ModStudioEntityKind kind)
    {
        kind = type switch
        {
            _ when type.IsSubclassOf(typeof(CardModel)) => ModStudioEntityKind.Card,
            _ when type.IsSubclassOf(typeof(RelicModel)) => ModStudioEntityKind.Relic,
            _ when type.IsSubclassOf(typeof(PotionModel)) => ModStudioEntityKind.Potion,
            _ when type.IsSubclassOf(typeof(EventModel)) => ModStudioEntityKind.Event,
            _ when type.IsSubclassOf(typeof(EnchantmentModel)) => ModStudioEntityKind.Enchantment,
            _ => default
        };

        return kind is ModStudioEntityKind.Card
            or ModStudioEntityKind.Relic
            or ModStudioEntityKind.Potion
            or ModStudioEntityKind.Event
            or ModStudioEntityKind.Enchantment;
    }

    private static Type GetModelType(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Card => typeof(CardModel),
            ModStudioEntityKind.Relic => typeof(RelicModel),
            ModStudioEntityKind.Potion => typeof(PotionModel),
            ModStudioEntityKind.Event => typeof(EventModel),
            ModStudioEntityKind.Enchantment => typeof(EnchantmentModel),
            _ => throw new NotSupportedException($"Entity kind '{kind}' does not support dynamic registration.")
        };
    }

    private static bool IsEventAllowedForAct(string eventId, string? actId)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Event, eventId, "is_shared", out var isShared) && isShared)
        {
            return true;
        }

        var actIds = RuntimeOverrideMetadata.GetIdList(ModStudioEntityKind.Event, eventId, "act_ids");
        if (actIds.Count == 0)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(actId) &&
               actIds.Contains(actId, StringComparer.OrdinalIgnoreCase);
    }
}
