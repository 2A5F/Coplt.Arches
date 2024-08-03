using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Coplt.Arches;

public static partial class ArcheTypes
{
    #region SizeOf

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int SizeOf<T>() => sizeof(T);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EmitSizeOf(Type type) =>
        (int)MethodInfo_SizeOf().MakeGenericMethod(type).Invoke(null, [])!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo MethodInfo_SizeOf()
    {
        Ldtoken(new MethodRef(typeof(ArcheTypes), nameof(SizeOf)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }

    #endregion

    #region AlignOf

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int AlignOf<T>() => sizeof(AlignOfHelper<T>) - sizeof(T);

    private struct AlignOfHelper<T>
    {
        public byte dummy;
        public T data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EmitAlignOf(Type type) =>
        (int)MethodInfo_AlignOf().MakeGenericMethod(type).Invoke(null, [])!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo MethodInfo_AlignOf()
    {
        Ldtoken(new MethodRef(typeof(ArcheTypes), nameof(AlignOf)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }

    #endregion

    #region IsManaged

    public static bool IsManaged<T>() => RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EmitIsManaged(Type type) =>
        (bool)MethodInfo_IsManaged().MakeGenericMethod(type).Invoke(null, [])!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo MethodInfo_IsManaged()
    {
        Ldtoken(new MethodRef(typeof(ArcheTypes), nameof(IsManaged)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }

    #endregion

    #region TypeMeta

    public static TypeMeta GetTypeMeta<T>() => new(typeof(T), TypeId.Of<T>(), SizeOf<T>(), AlignOf<T>(), IsManaged<T>(),
        !typeof(T).IsPrimitive && SizeOf<T>() == 1 &&
        typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic).Length is 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeMeta EmitGetTypeMeta(Type type) =>
        (TypeMeta)MethodInfo_GetTypeMeta().MakeGenericMethod(type).Invoke(null, [])!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo MethodInfo_GetTypeMeta()
    {
        Ldtoken(new MethodRef(typeof(ArcheTypes), nameof(GetTypeMeta)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }

    #endregion

    #region Aligned

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Aligned(int addr, int align) => (addr + (align - 1)) & ~(align - 1);

    #endregion

    #region AllocArray

    public static T[] AllocateArray<T>(int length) => new T[length];

#if NETSTANDARD
    public static T[] AllocateUninitializedArray<T>(int length) => new T[length];
#else
    public static T[] AllocateUninitializedArray<T>(int length) => GC.AllocateUninitializedArray<T>(length);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo MethodInfo_AllocateArray()
    {
        Ldtoken(new MethodRef(typeof(ArcheTypes), nameof(AllocateArray)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo MethodInfo_AllocateUninitializedArray()
    {
        Ldtoken(new MethodRef(typeof(ArcheTypes), nameof(AllocateUninitializedArray)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }

    #endregion

    #region ArcheTypeMeta

    public static ArcheTypeMeta EmitArcheType(IEnumerable<Type> types, in ArcheTypeOptions options)
        => EmitArcheType(types.Select(EmitGetTypeMeta).ToImmutableHashSet(), in options);

    public static ArcheTypeMeta EmitArcheType(ImmutableHashSet<TypeMeta> types, in ArcheTypeOptions options)
    {
        var ts = SortType(types);
        return EmitArcheTypeSorted(ts, options);
    }

    public static ArcheTypeMeta EmitArcheTypeSorted(List<TypeMeta> types, in ArcheTypeOptions options) =>
        EmitArcheType(types, options);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TypeMeta> SortType(ImmutableHashSet<TypeMeta> types) => types
        .Where(static a => !a.IsTag)
        .OrderByDescending(static a => a.Size)
        .ThenByDescending(static a => a.Align)
        .ThenBy(static a => a.Type.Name)
        .ThenBy(static a => a.Type.GetHashCode())
        .ToList();

    private static ArcheTypeMeta EmitArcheType(List<TypeMeta> types, in ArcheTypeOptions options)
    {
        var stride = options.Stride;
        if (stride <= 0)
        {
            var sum_size = types.Sum(static t => t.Size);
            if (options.PageSize < sum_size) throw new ArgumentException("The page is too small to fit any archetype");
            stride = options.PageSize / sum_size;
        }

        var container = TypeContainers.EmitGet(types.Count, stride, options.GenerateStructure);
        var include_types = types.Select(static t => t.Type).ToArray();
        var type = container.MakeGenericType(include_types);

        var fields = types.Select((t, j) =>
        {
            var field = type.GetField($"{j}", BindingFlags.Public | BindingFlags.Instance)!;
            return new FieldMeta(field, EmitGetTypeMeta(field.FieldType), t, j);
        }).ToFrozenDictionary(static a => a.Type.Type, static a => a);


        var type_meta = EmitGetTypeMeta(type);

        var min_id = types.Min(static t => t.Id.Id);
        var max_id = types.Max(static t => t.Id.Id);
        var bits = Bits.Create(min_id, max_id, types.Select(static t => t.Id.Id));

        var impl = (AArcheType)Activator.CreateInstance(typeof(ArcheType<>).MakeGenericType(type))!;
        impl.Bits = bits;

        var meta = new ArcheTypeMeta
        {
            Type = type,
            IncludeTypes = include_types,
            TypeMeta = type_meta,
            Stride = stride,
            ArcheType = impl,
            Fields = fields,
        };
        archeTypeMetaCache.Add(meta.Type, meta);
        return meta;
    }

    private static readonly ConditionalWeakTable<Type, ArcheTypeMeta> archeTypeMetaCache = new();

    internal static ArcheTypeMeta GetMeta(Type type) =>
        archeTypeMetaCache.TryGetValue(type, out var val) ? val : null!;

    #endregion
}
