using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeDynamicTypeFactory
{
    private static readonly AssemblyBuilder AssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("STS2_Editor.RuntimeGeneratedModels"),
        AssemblyBuilderAccess.Run);

    private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule("RuntimeGeneratedModels");
    private static readonly Dictionary<RuntimeEntityKey, Type> Types = new();
    private static readonly object Sync = new();

    public static Type GetOrCreate(ModStudioEntityKind kind, string entityId)
    {
        if (!RuntimeDynamicContentRegistry.SupportsDynamicRegistration(kind))
        {
            throw new NotSupportedException($"Entity kind '{kind}' does not support dynamic registration.");
        }

        var key = new RuntimeEntityKey(kind, entityId);
        lock (Sync)
        {
            if (Types.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var created = BuildType(kind, entityId);
            Types[key] = created;
            return created;
        }
    }

    private static Type BuildType(ModStudioEntityKind kind, string entityId)
    {
        var typeName = BuildTypeName(entityId);
        var fullName = $"STS2_Editor.RuntimeGenerated.{kind}.{typeName}";

        return kind switch
        {
            ModStudioEntityKind.Card => BuildCardType(fullName),
            ModStudioEntityKind.Relic => BuildRelicType(fullName),
            ModStudioEntityKind.Potion => BuildPotionType(fullName),
            ModStudioEntityKind.Event => BuildEventType(fullName),
            ModStudioEntityKind.Enchantment => BuildEnchantmentType(fullName),
            _ => throw new NotSupportedException($"Entity kind '{kind}' does not support dynamic registration.")
        };
    }

    private static string BuildTypeName(string entityId)
    {
        var segments = entityId
            .Split(['_', '-', ' ', '.', ':', '/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment =>
            {
                var normalized = new string(segment.Where(char.IsLetterOrDigit).ToArray());
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    return string.Empty;
                }

                if (normalized.Length == 1)
                {
                    return normalized.ToUpperInvariant();
                }

                return char.ToUpperInvariant(normalized[0]) + normalized[1..].ToLowerInvariant();
            })
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        return segments.Count == 0
            ? "EdGeneratedEntry"
            : string.Concat(segments);
    }

    private static Type BuildCardType(string fullName)
    {
        var typeBuilder = ModuleBuilder.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(CardModel));
        DefinePublicParameterlessConstructor(
            typeBuilder,
            typeof(CardModel).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(int), typeof(CardType), typeof(CardRarity), typeof(TargetType), typeof(bool)],
                modifiers: null)!,
            il =>
            {
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Ldc_I4, (int)CardType.Skill);
                il.Emit(OpCodes.Ldc_I4, (int)CardRarity.Common);
                il.Emit(OpCodes.Ldc_I4, (int)TargetType.AnyEnemy);
                il.Emit(OpCodes.Ldc_I4_1);
            });
        return typeBuilder.CreateType()!;
    }

    private static Type BuildRelicType(string fullName)
    {
        var typeBuilder = ModuleBuilder.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(RelicModel));
        DefinePublicParameterlessConstructor(
            typeBuilder,
            typeof(RelicModel).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!,
            il => { });
        DefineConstantGetter(typeBuilder, typeof(RelicModel), nameof(RelicModel.Rarity), typeof(RelicRarity), (int)RelicRarity.Common);
        return typeBuilder.CreateType()!;
    }

    private static Type BuildPotionType(string fullName)
    {
        var typeBuilder = ModuleBuilder.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(PotionModel));
        DefinePublicParameterlessConstructor(
            typeBuilder,
            typeof(PotionModel).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!,
            il => { });
        DefineConstantGetter(typeBuilder, typeof(PotionModel), nameof(PotionModel.Rarity), typeof(PotionRarity), (int)PotionRarity.Common);
        DefineConstantGetter(typeBuilder, typeof(PotionModel), nameof(PotionModel.Usage), typeof(PotionUsage), (int)PotionUsage.CombatOnly);
        DefineConstantGetter(typeBuilder, typeof(PotionModel), nameof(PotionModel.TargetType), typeof(TargetType), (int)TargetType.Self);
        return typeBuilder.CreateType()!;
    }

    private static Type BuildEventType(string fullName)
    {
        var typeBuilder = ModuleBuilder.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(EventModel));
        DefinePublicParameterlessConstructor(
            typeBuilder,
            typeof(EventModel).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!,
            il => { });

        var baseMethod = typeof(EventModel).GetMethod(
            "GenerateInitialOptions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var methodBuilder = typeBuilder.DefineMethod(
            baseMethod.Name,
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(IReadOnlyList<EventOption>),
            Type.EmptyTypes);
        var il = methodBuilder.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(Array).GetMethod(nameof(Array.Empty))!.MakeGenericMethod(typeof(EventOption)));
        il.Emit(OpCodes.Ret);
        typeBuilder.DefineMethodOverride(methodBuilder, baseMethod);

        return typeBuilder.CreateType()!;
    }

    private static Type BuildEnchantmentType(string fullName)
    {
        var typeBuilder = ModuleBuilder.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(EnchantmentModel));
        DefinePublicParameterlessConstructor(
            typeBuilder,
            typeof(EnchantmentModel).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!,
            il => { });
        return typeBuilder.CreateType()!;
    }

    private static void DefinePublicParameterlessConstructor(
        TypeBuilder typeBuilder,
        ConstructorInfo baseConstructor,
        Action<ILGenerator> emitBaseArguments)
    {
        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        emitBaseArguments(il);
        il.Emit(OpCodes.Call, baseConstructor);
        il.Emit(OpCodes.Ret);
    }

    private static void DefineConstantGetter(
        TypeBuilder typeBuilder,
        Type baseType,
        string propertyName,
        Type propertyType,
        int enumValue)
    {
        var baseGetter = baseType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetMethod!;
        var getter = typeBuilder.DefineMethod(
            baseGetter.Name,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            propertyType,
            Type.EmptyTypes);
        var il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4, enumValue);
        il.Emit(OpCodes.Ret);
        typeBuilder.DefineMethodOverride(getter, baseGetter);
    }
}
