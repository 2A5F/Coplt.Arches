using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Coplt.Arches;

// todo make a sparse bit sets
public readonly struct Bits : IEquatable<Bits>
{
    private readonly BitVector32[] array;
    private readonly int offset;
    private readonly int hash;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Bits(BitVector32[] array, int offset, int hash)
    {
        this.array = array;
        this.offset = offset;
        this.hash = hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bits Create<E>(int min, int max, E items) where E : IEnumerable<int>
    {
        var arr = new BitVector32[(max - min) / 4];
        foreach (var item in items)
        {
            var n = Math.DivRem(item - min, 4, out var i);
            arr[n][i] = true;
        }
        var hash = new HashCode();
#if NETSTANDARD
        foreach (var bits in arr)
        {
            hash.Add(bits.Data);
        }
#else
        hash.AddBytes(MemoryMarshal.AsBytes(arr.AsSpan()));
#endif
        return new(arr, min, hash.ToHashCode());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => hash;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Bits other)
    {
        if (hash != other.hash) return false;
        if (offset != other.offset) return false;
#if NETSTANDARD
        if (array.Length != other.array.Length) return false;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].Data != other.array[i].Data) return false;
        }
        return true;
#else
        return MemoryMarshal.AsBytes(array.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(other.array.AsSpan()));
#endif
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is Bits other && Equals(other);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Bits left, Bits right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Bits left, Bits right) => !left.Equals(right);
}
