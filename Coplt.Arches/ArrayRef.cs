using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

namespace Coplt.Arches;

public readonly record struct RoRef<T>
{
    private readonly UnsafeArrayRef inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RoRef(UnsafeArrayRef inner)
    {
        this.inner = inner;
    }

    public ref readonly T V
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref inner.GetRef<T>();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"{V}";
}

public readonly record struct RwRef<T>
{
    private readonly UnsafeArrayRef inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RwRef(UnsafeArrayRef inner)
    {
        this.inner = inner;
    }

    public ref T V
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref inner.GetRef<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"{V}";
}
