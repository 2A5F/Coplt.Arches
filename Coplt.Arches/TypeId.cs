using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Coplt.Arches;

/// <summary>
/// A unique type id determined at runtime
/// </summary>
public readonly record struct TypeId
{
    private readonly long Id;

    #region Ctor

    public TypeId(long Id)
    {
        this.Id = Id;
    }

    #endregion

    #region TheId

    private static long s_id_inc;

    private static readonly ConcurrentDictionary<long, Type> s_types = new();

    private static class TheId<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        public static readonly long s_id_value = Interlocked.Increment(ref s_id_inc);

        static TheId()
        {
            s_types[s_id_value] = typeof(T);
        }
    }

    #endregion

    #region Create

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

    #endregion

    #region ToType

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Type ToType() => s_types[Id];

    #endregion

    #region ToString

    public override string ToString() => $"TypeId({Id})";

    #endregion
}
