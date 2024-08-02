using System.Collections.Frozen;
using System.Collections.Immutable;
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

    public static TypeMeta GetTypeMeta<T>() => new(typeof(T), SizeOf<T>(), AlignOf<T>(), IsManaged<T>(),
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
        ArcheTypeUnitMeta[] groups;
        if (types.Count is 0) groups = [];
        else if (options.SplitManaged)
        {
            var un_managed = types
                .Where(static a => !a.IsTag)
                .Where(static a => !a.IsManaged)
                .OrderByDescending(static a => a.Size)
                .ThenByDescending(static a => a.Align)
                .ThenBy(static a => a.Type.GetHashCode())
                .ToList();
            var managed = types
                .Where(static a => !a.IsTag)
                .Where(static a => a.IsManaged)
                .OrderByDescending(static a => a.Size)
                .ThenByDescending(static a => a.Align)
                .ThenBy(static a => a.Type.GetHashCode())
                .ToList();
            ArcheTypeUnitMeta? g_un_managed = null;
            if (un_managed.Count > 0) g_un_managed = EmitArcheType(un_managed);
            ArcheTypeUnitMeta? g_managed = null;
            if (managed.Count > 0) g_managed = EmitArcheType(managed);
            groups = (g_un_managed, g_managed) switch
            {
                ({ } a, { } b) => [a, b],
                ({ } a, null) => [a],
                (null, { } b) => [b],
                _ => [],
            };
        }
        else
        {
            var ts = types
                .Where(static a => !a.IsTag)
                .OrderByDescending(static a => a.Size)
                .ThenByDescending(static a => a.Align)
                .ThenBy(static a => a.Type.GetHashCode())
                .ToList();
            groups = [EmitArcheType(ts)];
        }

        return new()
        {
            Units = groups
        };
    }

    private static ArcheTypeUnitMeta EmitArcheType(List<TypeMeta> types)
    {
        var container = TypeContainers.EmitGet(types.Count);
        var include_types = types.Select(static t => t.Type).ToArray();
        var type = container.MakeGenericType(include_types);

        var fields = types.Select((t, j) =>
        {
            var field = type.GetField($"_{j}", BindingFlags.Public | BindingFlags.Instance)!;
            return new FieldMeta(field, t, j);
        }).ToFrozenDictionary(static a => a.Type.Type, static a => a);

        var get = types.Select((t, j) =>
        {
            var field = type.GetMethod($"Get{j}", BindingFlags.Public | BindingFlags.Instance)!;
            return new MethodMeta(field, t, j);
        }).ToFrozenDictionary(static a => a.Type.Type, static a => a);

        var get_ref = types.Select((t, j) =>
        {
            var field = type.GetMethod($"GetRef{j}", BindingFlags.Public | BindingFlags.Instance)!;
            return new MethodMeta(field, t, j);
        }).ToFrozenDictionary(static a => a.Type.Type, static a => a);

        var alloc_array = MethodInfo_AllocateArray().MakeGenericMethod(type);
        var alloc_un_init_array = MethodInfo_AllocateUninitializedArray().MakeGenericMethod(type);

        var type_meta = EmitGetTypeMeta(type);

        var impl = (AArcheType)Activator.CreateInstance(typeof(ArcheType<>).MakeGenericType(type))!;

        var unit = impl.Unit = new()
        {
            Type = type,
            IncludeTypes = include_types,
            TypeMeta = type_meta,
            ArcheType = impl,
            Fields = fields,
            Get = get,
            GetRef = get_ref,
            AllocateArray = alloc_array,
            AllocateUninitializedArray = alloc_un_init_array,
        };
        archeTypeUnitMetaCache.Add(unit.Type, unit);
        return unit;
    }

    private static readonly ConditionalWeakTable<Type, ArcheTypeUnitMeta> archeTypeUnitMetaCache = new();

    internal static ArcheTypeUnitMeta GetUnit(Type type) =>
        archeTypeUnitMetaCache.TryGetValue(type, out var val) ? val : null!;

    #endregion
}
