using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Coplt.Arches;

/// <summary>
/// A unique type id determined at runtime
/// </summary>
public readonly record struct TypeId(int Id)
{
    public readonly int Id = Id;
    
    #region TheId

    private static int s_id_inc;

    private static readonly ConcurrentDictionary<int, WeakReference<Type>> s_types = new();

    private static class TheId<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        public static readonly int s_id_value = Interlocked.Increment(ref s_id_inc);

        static TheId()
        {
            s_types[s_id_value] = new(typeof(T));
            s_type_to_id.Add(typeof(T), s_id_value);
        }
    }

    #endregion

    #region Create

    private static readonly ConditionalWeakTable<Type, object> s_type_to_id = new();

    /// <summary>
    /// Create statically
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeId Of<T>() => new(TheId<T>.s_id_value);

    /// <summary>
    /// Create dynamic by jit
    /// </summary>
    public static TypeId EmitOf(Type type) => (TypeId)MethodInfo_Of().MakeGenericMethod(type).Invoke(null, [])!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo MethodInfo_Of()
    {
        Ldtoken(new MethodRef(typeof(TypeId), nameof(Of)));
        return IL.Return<MethodInfo>();
    }

    /// <summary>
    /// Dynamic get
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeId DynOf(Type type) => (TypeId)s_type_to_id.GetValue(type, static type => EmitOf(type));

    #endregion

    #region ToType

    /// <summary>
    /// Get the type, if the type is unloaded will return null
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Type? ToType() => s_types[Id].TryGetTarget(out var t) ? t : null;

    #endregion

    #region ToString

    public override string ToString() => $"TypeId({Id})";

    #endregion
}
