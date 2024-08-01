using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Coplt.Arches.Internal;

public readonly record struct UnsafeArrayRef(Array Array, nint Offset)
{
#if !NETSTANDARD
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint CalcOffset<C, V>(C[] array, ref V addr) =>
        Unsafe.ByteOffset(ref MemoryMarshal.GetArrayDataReference((Array)array), ref Unsafe.As<V, byte>(ref addr));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetRef<T>(Array array, nint offset) =>
        ref Unsafe.As<byte, T>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), offset));
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint CalcOffset<C, V>(C[] array, ref V addr) =>
        Unsafe.ByteOffset(ref Unsafe.As<byte[]>(array)[0], ref Unsafe.As<V, byte>(ref addr));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T GetRef<T>(Array array, nint offset) =>
        ref Unsafe.As<byte, T>(ref Unsafe.Add(ref Unsafe.As<byte[]>(array)[0], offset));
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnsafeArrayRef Create<C, V>(C[] array, ref V addr) => new(array, CalcOffset(array, ref addr));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>() => ref GetRef<T>(Array, Offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RoRef<V> CreateRoRef<C, V>(C[] array, ref V addr) => new(Create(array, ref addr));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RwRef<V> CreateRwRef<C, V>(C[] array, ref V addr) => new(Create(array, ref addr));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo MethodInfo_CreateRoRef()
    {
        Ldtoken(new MethodRef(typeof(UnsafeArrayRef), nameof(CreateRoRef)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo MethodInfo_CreateRwRef()
    {
        Ldtoken(new MethodRef(typeof(UnsafeArrayRef), nameof(CreateRwRef)));
        Call(new MethodRef(typeof(MethodBase), nameof(MethodBase.GetMethodFromHandle), typeof(RuntimeMethodHandle)));
        Castclass<MethodInfo>();
        return IL.Return<MethodInfo>();
    }
}
