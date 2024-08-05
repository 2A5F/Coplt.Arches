using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Coplt.Arches.TypeSets;

public interface IS
{
    public ImmutableHashSet<TypeMeta> GetTypes();
}

internal readonly struct DynS<T> : IS
{
    // ReSharper disable once StaticMemberInGenericType
    public static ImmutableHashSet<TypeMeta> Types
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        set;
    } = [];
    public ImmutableHashSet<TypeMeta> GetTypes() => Types;
}

public struct S<T> : IS
{
    public ImmutableHashSet<TypeMeta> GetTypes() => Types;
    public S<S<T>, A> A<A>() => default;

    public static ImmutableHashSet<TypeMeta> Types
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = ImmutableHashSet.Create(ArcheTypes.GetTypeMeta<T>());
}

public struct S<B, T> : IS
    where B : struct, IS
{
    public ImmutableHashSet<TypeMeta> GetTypes() => Types;
    public S<S<B, T>, A> A<A>() => default;

    public static ImmutableHashSet<TypeMeta> Types
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = default(B).GetTypes().Add(ArcheTypes.GetTypeMeta<T>());
}
