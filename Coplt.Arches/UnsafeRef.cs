using System.Reflection;
using System.Runtime.CompilerServices;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Coplt.Arches.Internal;

// ReSharper disable EntityNameCapturedOnly.Global
public readonly record struct UnsafeRef(object Object, nint Offset)
{
    // This depends on the core clr's implementation:
    //   object data、type handle、object header are allocated continuously,
    //   and the object pointer points to the type hande,
    // other implementations may fail,
    // such as if the implementation is a two-level indirect pointer.
    // But if the implementation does not move objects,
    // such as Mono's Boehm GC, it can also work normally.
    // 
    //                  object head     - nint
    // object ref ->    type hande      - nint          - offset : 0
    //                  fields          ...             - offset : 8
    //                  ...
    // target ref ->    T               - sizeof(T)     - offset : 8 + field offset
    // 
    
    /// <summary>
    /// Create an unsafe reference from an object and an offset within the object
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint CalcOffset<T>(object obj, ref T addr)
    {
        Ldarg(nameof(addr));
        Ldarg(nameof(obj));
        Conv_I();
        Sub();
        return IL.Return<nint>();
    }

    /// <summary>
    /// Restore the reference within the object based on the object and the offset within the object
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetRef<T>(object obj, nint offset)
    {
        Ldarg(nameof(obj));
        Conv_I();
        Ldarg(nameof(offset));
        Add();
        Ret();
        throw IL.Unreachable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnsafeRef Create<T>(object obj, ref T addr) => new(obj, CalcOffset(obj, ref addr));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>() => ref GetRef<T>(Object, Offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RoRef<T> CreateRoRef<T>(object obj, ref T addr) => new(Create(obj, ref addr));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RwRef<T> CreateRwRef<T>(object obj, ref T addr) => new(Create(obj, ref addr));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo MethodInfo_CreateRoRef()
    {
        Ldtoken(new MethodRef(typeof(UnsafeRef), nameof(CreateRoRef)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo MethodInfo_CreateRwRef()
    {
        Ldtoken(new MethodRef(typeof(UnsafeRef), nameof(CreateRwRef)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }
}
