using System.Collections;
using System.Collections.Frozen;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Coplt.Arches;

public record ArcheTypeMeta
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
    #region Create And Alloc

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract object Create();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeCreate(ref byte target);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract Array AllocateArray(int len);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract Array AllocateUninitializedArray(int len);

    #endregion

    #region Access

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

    #endregion

#if NET8_0_OR_GREATER

    #region Callback Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeCallbackAccess<D>(object obj, nint offset, int index, D cb) where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeCallbackAccess<D>(object obj, int index, D cb) where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheCallbackAccess DynamicCallbackAccess(Type delegateType);

    #endregion

    #region Callback Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeCallbackRangeAccess<D>(object obj, nint offset, int start, uint length, D cb)
        where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeCallbackRangeAccess<D>(object obj, int start, uint length, D cb)
        where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheCallbackRangeAccess DynamicCallbackRangeAccess(Type delegateType);

    #endregion

#endif

    #region TypeSet

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract bool IsOverlap<Set>(Set set) where Set : struct, IS;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract bool IsSupersetOf<Set>(Set set) where Set : struct, IS;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract bool IsSubsetOf<Set>(Set set) where Set : struct, IS;

    #region TypeSet Arche Api

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract bool IsOverlap(AArcheType other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract bool IsSupersetOf(AArcheType other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract bool IsSubsetOf(AArcheType other);

    #endregion

    #region TypeSet Arche

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal abstract bool IsOverlapInv<U>(ArcheType<U> other) where U : new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal abstract bool IsSupersetOfInv<U>(ArcheType<U> other) where U : new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal abstract bool IsSubsetOfInv<U>(ArcheType<U> other) where U : new();

    #endregion

    #endregion
}

internal class ArcheType<T> : AArcheType
    where T : new()
{
    #region Create And Alloc

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override object Create() => new T();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeCreate(ref byte target)
    {
        Unsafe.As<byte, T>(ref target) = new T();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Array AllocateArray(int len) => new T[len];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Array AllocateUninitializedArray(int len) => ArcheTypes.AllocateUninitializedArray<T>(len);

    #endregion

    #region Access

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

    #endregion

#if NET8_0_OR_GREATER

    #region Callback Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeCallbackAccess<D>(object obj, nint offset, int index, D cb) =>
        ArcheAccesses.StaticCallbackAccess<T, D>.Access(obj, offset, index, cb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeCallbackAccess<D>(object obj, int index, D cb) =>
        ArcheAccesses.StaticCallbackAccess<T, D>.Access(obj, 0, index, cb);

    #region DynamicAccess

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, ArcheCallbackAccess> cache_DynamicCallbackAccess = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ArcheCallbackAccess DynamicCallbackAccess(Type delegateType) =>
        cache_DynamicCallbackAccess.GetValue(delegateType, static delegateType =>
        {
            var method = ArcheAccesses.EmitCallbackAccess(typeof(T), delegateType);
            return method.CreateDelegate<ArcheCallbackAccess>();
        });

    #endregion

    #endregion

    #region CallbackRange Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeCallbackRangeAccess<D>(object obj, nint offset, int start, uint length, D cb) =>
        ArcheAccesses.StaticCallbackRangeAccess<T, D>.Access(obj, offset, start, length, cb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeCallbackRangeAccess<D>(object obj, int start, uint length, D cb) =>
        ArcheAccesses.StaticCallbackRangeAccess<T, D>.Access(obj, 0, start, length, cb);

    #region DynamicAccess

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, ArcheCallbackRangeAccess> cache_DynamicCallbackRangeAccess = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ArcheCallbackRangeAccess DynamicCallbackRangeAccess(Type delegateType) =>
        cache_DynamicCallbackRangeAccess.GetValue(delegateType, static delegateType =>
        {
            var method = ArcheAccesses.EmitCallbackRangeAccess(typeof(T), delegateType);
            return method.CreateDelegate<ArcheCallbackRangeAccess>();
        });

    #endregion

    #endregion

#endif

    #region TypeSet

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsOverlap<Set>(Set set) => TypeIsOverlap<DynS<T>, Set>.IsOverlap;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsSupersetOf<Set>(Set set) => TypeSetIsSupersetOf<DynS<T>, Set>.IsSupersetOf;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsSubsetOf<Set>(Set set) => TypeSetIsSubsetOf<DynS<T>, Set>.IsSubsetOf;

    #region TypeSet Arche Api

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsOverlap(AArcheType other) => other.IsOverlapInv(this);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsSupersetOf(AArcheType other) => other.IsSupersetOfInv(this);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsSubsetOf(AArcheType other) => other.IsSubsetOfInv(this);

    #endregion

    #region TypeSet Arche

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal override bool IsOverlapInv<U>(ArcheType<U> other) => TypeIsOverlap<DynS<U>, DynS<T>>.IsOverlap;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal override bool IsSupersetOfInv<U>(ArcheType<U> other) => TypeSetIsSupersetOf<DynS<U>, DynS<T>>.IsSupersetOf;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal override bool IsSubsetOfInv<U>(ArcheType<U> other) => TypeSetIsSubsetOf<DynS<U>, DynS<T>>.IsSubsetOf;

    #endregion

    #endregion
}
