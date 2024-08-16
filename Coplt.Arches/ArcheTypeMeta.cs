using System.Collections.Frozen;
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
    #region Meta

    public ArcheTypeMeta Meta { get; internal set; } = null!;

    #endregion

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

    #region Struct Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeAccess<A>(object obj, nint offset, int index, out A acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeAccess<A>(object obj, int index, out A acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeAccess<A>(object obj, nint offset, int index, A* acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract unsafe void UnsafeAccess<A>(object obj, int index, A* acc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheAccess DynamicAccess(Type acc);

    #endregion

#if NET8_0_OR_GREATER

    #region Delegate Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeDelegateAccess<D>(object obj, nint offset, int index, D cb) where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeDelegateAccess<D>(object obj, int index, D cb) where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheCallbackAccess DynamicDelegateAccess(Type delegateType);

    #endregion

    #region Delegate Range Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeDelegateRangeAccess<D>(object obj, nint offset, int start, uint length, D cb)
        where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeDelegateRangeAccess<D>(object obj, int start, uint length, D cb)
        where D : Delegate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheCallbackRangeAccess DynamicDelegateRangeAccess(Type delegateType);

    #endregion

    #region Method Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeMethodAccess<I, T>(object obj, nint offset, int index, ref T target) where T : I;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeMethodAccess<I, T>(object obj, int index, ref T target) where T : I;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheAccess DynamicMethodAccess(Type interface_type, Type target_type);

    #endregion

    #region Method Range Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeMethodRangeAccess<I, T>(object obj, nint offset, int start, uint length, ref T target)
        where T : I;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void UnsafeMethodRangeAccess<I, T>(object obj, int start, uint length, ref T target) where T : I;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract ArcheRangeAccess DynamicMethodRangeAccess(Type interface_type, Type target_type);

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

    #region Struct Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeAccess<A>(object obj, IntPtr offset, int index, out A acc)
    {
        Unsafe.SkipInit(out acc);
        ArcheAccesses.StaticRefAccess<T, A>.Access(obj, offset, index, ref acc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeAccess<A>(object obj, int index, out A acc)
    {
        Unsafe.SkipInit(out acc);
        ArcheAccesses.StaticRefAccess<T, A>.Access(obj, 0, index, ref acc);
    }

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

    #region Delegate Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeDelegateAccess<D>(object obj, nint offset, int index, D cb) =>
        ArcheAccesses.StaticDelegateAccess<T, D>.Access(obj, offset, index, cb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeDelegateAccess<D>(object obj, int index, D cb) =>
        ArcheAccesses.StaticDelegateAccess<T, D>.Access(obj, 0, index, cb);

    #region DynamicAccess

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, ArcheCallbackAccess> cache_DynamicDelegateAccess = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ArcheCallbackAccess DynamicDelegateAccess(Type delegateType) =>
        cache_DynamicDelegateAccess.GetValue(delegateType, static delegateType =>
        {
            var method = ArcheAccesses.EmitDelegateAccess(typeof(T), delegateType);
            return method.CreateDelegate<ArcheCallbackAccess>();
        });

    #endregion

    #endregion

    #region DelegateRange Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeDelegateRangeAccess<D>(object obj, nint offset, int start, uint length, D cb) =>
        ArcheAccesses.StaticDelegateRangeAccess<T, D>.Access(obj, offset, start, length, cb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeDelegateRangeAccess<D>(object obj, int start, uint length, D cb) =>
        ArcheAccesses.StaticDelegateRangeAccess<T, D>.Access(obj, 0, start, length, cb);

    #region DynamicAccess

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, ArcheCallbackRangeAccess> cache_DynamicDelegateRangeAccess =
        new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ArcheCallbackRangeAccess DynamicDelegateRangeAccess(Type delegateType) =>
        cache_DynamicDelegateRangeAccess.GetValue(delegateType, static delegateType =>
        {
            var method = ArcheAccesses.EmitDelegateRangeAccess(typeof(T), delegateType);
            return method.CreateDelegate<ArcheCallbackRangeAccess>();
        });

    #endregion

    #endregion

    #region Method Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeMethodAccess<I, A>(object obj, nint offset, int index, ref A target) =>
        ArcheAccesses.StaticMethodAccess<T, I, A>.Access(obj, offset, index, ref target);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeMethodAccess<I, A>(object obj, int index, ref A target) =>
        ArcheAccesses.StaticMethodAccess<T, I, A>.Access(obj, 0, index, ref target);

    #region DynamicAccess

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, ArcheAccess>>
        cache_DynamicMethodAccess = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ArcheAccess DynamicMethodAccess(Type interface_type, Type target_type) =>
        cache_DynamicMethodAccess.GetOrCreateValue(interface_type).GetValue(target_type, _ =>
        {
            var method = ArcheAccesses.EmitMethodAccess(typeof(T), interface_type, target_type);
            return method.CreateDelegate<ArcheAccess>();
        });

    #endregion

    #endregion

    #region MethodRange Access

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeMethodRangeAccess<I, A>(object obj, nint offset, int start, uint length, ref A target) =>
        ArcheAccesses.StaticMethodRangeAccess<T, I, A>.Access(obj, offset, start, length, ref target);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void UnsafeMethodRangeAccess<I, A>(object obj, int start, uint length, ref A target) =>
        ArcheAccesses.StaticMethodRangeAccess<T, I, A>.Access(obj, 0, start, length, ref target);

    #region DynamicAccess

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, ArcheRangeAccess>>
        cache_DynamicMethodRangeAccess = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ArcheRangeAccess DynamicMethodRangeAccess(Type interface_type, Type target_type) =>
        cache_DynamicMethodRangeAccess.GetOrCreateValue(interface_type).GetValue(target_type, _ =>
        {
            var method = ArcheAccesses.EmitMethodRangeAccess(typeof(T), interface_type, target_type);
            return method.CreateDelegate<ArcheRangeAccess>();
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
