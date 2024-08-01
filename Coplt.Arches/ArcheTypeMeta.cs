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
    public abstract DynamicArcheAccess DynamicAccess(Type acc);
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
    public override unsafe void Access<A>(Array array, int index, A* acc)
    {
        var f = ArcheAccesses.StaticAccess<T, A>.Get(Unit);
        f((T[])array, index, acc);
    }

    #region DynamicAccess

    private readonly ConditionalWeakTable<Type, DynamicArcheAccessContainer> cache_DynamicAccess = new();

    private class DynamicArcheAccessContainer
    {
        private DynamicArcheAccess? Delegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe DynamicArcheAccess Get(ArcheTypeUnitMeta unit, Type acc)
        {
            if (Delegate is not null) return Delegate;
            var f = ArcheAccesses.EmitAccess(unit, acc);
            ArcheAccess<T> d;
#if NETSTANDARD
            d = (ArcheAccess<T>)f.CreateDelegate(typeof(ArcheAccess<T>));
#else
            d = f.CreateDelegate<ArcheAccess<T>>();
#endif
            return (arr, index, access) => d((T[])arr, index, access);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override DynamicArcheAccess DynamicAccess(Type acc) =>
        cache_DynamicAccess.GetOrCreateValue(acc).Get(Unit, acc);

    #endregion
}
