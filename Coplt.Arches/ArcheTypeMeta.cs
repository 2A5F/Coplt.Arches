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
    public required int Stride { get; init; }
    public required AArcheType ArcheType { get; init; }
    public required FrozenDictionary<Type, FieldMeta> Fields { get; init; }
}

public abstract class AArcheType
{
    public ArcheTypeUnitMeta Unit { get; internal set; } = null!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract object Create();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract Array AllocateArray(int len);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract Array AllocateUninitializedArray(int len);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void UnsafeAccess<A>(object obj, nint offset, int index, out A acc)
    {
        fixed (A* p = &acc)
        {
            UnsafeAccess(obj, offset, index, p);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void UnsafeAccess<A>(object obj, int index, out A acc)
    {
        fixed (A* p = &acc)
        {
            UnsafeAccess(obj, index, p);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeAccess<A>(object obj, nint offset, int index, A* acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeAccess<A>(object obj, int index, A* acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheAccess DynamicAccess(Type acc);
}

internal class ArcheType<T> : AArcheType
    where T : new()
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override object Create() => new T();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Array AllocateArray(int len) => new T[len];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Array AllocateUninitializedArray(int len) => ArcheTypes.AllocateUninitializedArray<T>(len);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override unsafe void UnsafeAccess<A>(object obj, nint offset, int index, A* acc) =>
        ArcheAccesses.StaticAccess<T, A>.Access(obj, offset, index, acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override unsafe void UnsafeAccess<A>(object obj, int index, A* acc) =>
        ArcheAccesses.StaticAccess<T, A>.Access(obj, 0, index, acc);

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
