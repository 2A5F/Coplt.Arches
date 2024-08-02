using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Coplt.Arches;

public record ArcheTypeMeta
{
    public required ArcheTypeUnitMeta[] Units { get; init; }
}

public record ArcheTypeUnitMeta
{
    public required Type Type { get; init; }
    public required Type[] IncludeTypes { get; init; }
    public required TypeMeta TypeMeta { get; init; }
    public required AArcheType ArcheType { get; init; }
    public required FrozenDictionary<Type, FieldMeta> Fields { get; init; }
    public required FrozenDictionary<Type, MethodMeta> Get { get; init; }
    public required FrozenDictionary<Type, MethodMeta> GetRef { get; init; }
    public required MethodInfo AllocateArray { get; init; }
    public required MethodInfo AllocateUninitializedArray { get; init; }
}

public abstract class AArcheType
{
    public ArcheTypeUnitMeta Unit { get; internal set; } = null!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract Array AllocateArray(int len);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract Array AllocateUninitializedArray(int len);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Access<A>(Array array, int index, out A acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void Access<A>(Array array, int index, A* acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheAccess DynamicAccess(Type acc);
}

internal class ArcheType<T> : AArcheType
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Array AllocateArray(int len) => new T[len];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Array AllocateUninitializedArray(int len) => ArcheTypes.AllocateUninitializedArray<T>(len);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override unsafe void Access<A>(Array array, int index, out A acc)
    {
        fixed (A* p = &acc)
        {
            Access(array, index, p);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override unsafe void Access<A>(Array array, int index, A* acc) => 
        ArcheAccesses.StaticAccess<T, A>.Access((T[])array, index, acc);

    #region DynamicAccess

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, ArcheAccess> cache_DynamicAccess = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ArcheAccess DynamicAccess(Type acc) =>
        cache_DynamicAccess.GetValue(acc, static acc =>
        {
            var method = ArcheAccesses.EmitAccess(typeof(T), acc);
            return method.CreateDelegate<ArcheAccess>();
        });

    #endregion
}
