using System.Runtime.CompilerServices;

namespace Coplt.Arches;

public static partial class TypeSet
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeSet<Set> ToSet<Set>(this Set set) where Set : struct, IS => default;
}

public struct TypeSet<Set> where Set : struct, IS
{
    internal static List<TypeMeta> SortedTypes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = ArcheTypes.SortType(default(Set).GetTypes());
}

public struct TypeSetOverlapInfo<A, B> where A : struct, IS where B : struct, IS
{
    public static bool IsOverlap
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = default(A).GetTypes().Overlaps(default(B).GetTypes());
}

public struct TypeSetSubSetInfo<A, B> where A : struct, IS where B : struct, IS
{
    public static bool IsSubsetOf
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = default(A).GetTypes().IsSubsetOf(default(B).GetTypes());
}
