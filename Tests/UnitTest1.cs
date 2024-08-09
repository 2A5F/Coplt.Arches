using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Coplt.Arches;
using Coplt.Arches.Internal;

namespace Tests;

public class Tests
{
    [SetUp]
    public void Setup() { }

    private struct Foo
    {
        public int a;
        public int b;
    }

    private struct Tag;

#if NET8_0
    private ref struct RefAcc
    {
        public int v;
        public ref int a;
        public Span<int> b;
        public ReadOnlySpan<int> c;
        public RoRef<int> d;
        public RwRef<int> e;
    }
#endif

    private record struct Acc
    {
        public int a;
        public RwRef<Vector128<float>> v;
        public RoRef<Foo> foo;
    }

#if NET8_0_OR_GREATER
    private delegate void AccCb(
        int a, float b, ref int a1, in int a2, out int a3,
        Span<int> c, ReadOnlySpan<int> d,
        RoRef<int> e, RwRef<int> f
    );
#endif

    [Test]
    public unsafe void Test1()
    {
        var arch = ArcheTypes.EmitArcheType(
        [
            typeof(int), typeof(float), typeof(Foo), typeof(object), typeof(Vector128<float>), typeof(Tag),
            typeof(bool), typeof(byte)
        ], new ArcheTypeOptions() { });

        var at = arch.ArcheType;

        Console.WriteLine(arch.ArcheType);
        Console.WriteLine();
        Console.WriteLine(string.Join("\n", arch.Fields.Values.OrderBy(a => a.Index)));

        Console.WriteLine();
        var obj = at.Create();
        var obj_addr = Unsafe.As<object, nint>(ref obj);
        Console.WriteLine(obj);
        Console.WriteLine(obj_addr);

#if NET8_0
        Console.WriteLine();
        var ref_acc = at.DynamicAccess(typeof(RefAcc));
        Console.WriteLine(ref_acc);

        Console.WriteLine();
        RefAcc r = default;
        ref_acc(obj, 0, 3, &r);
        var r_addr = (nint)(void*)&r;
        var ra_addr = (nint)(void*)&r.a;
        Console.WriteLine(r_addr);
        Console.WriteLine(ra_addr);
        Console.WriteLine(r.a);
        r.a = 123;
        Console.WriteLine(r.a);
        Assert.That(r.a, Is.EqualTo(123));
#endif

        Console.WriteLine();
        at.UnsafeAccess(obj, 3, out Acc acc);
        Console.WriteLine(acc);

        acc.v.V = Vector128.Create(1f, 2, 3, 4);
        Console.WriteLine(acc);
        Assert.That(acc.v.V, Is.EqualTo(Vector128.Create(1f, 2, 3, 4)));

        Console.WriteLine();
        at.UnsafeAccess(obj, 3, out (int a, float b, Vector128<float> c) acc3);
        Console.WriteLine(acc3);

        Console.WriteLine();
        Console.WriteLine(at.IsSupersetOf(TypeSet.Of<int, float>()));
        Console.WriteLine(at.IsSubsetOf(TypeSet.Of<int, float>()));
        Console.WriteLine(at.IsOverlap(TypeSet.Of<int, float>()));
        Assert.That(at.IsSupersetOf(TypeSet.Of<int, float>()), Is.True);

#if NET8_0_OR_GREATER
        Console.WriteLine();
        at.UnsafeCallbackAccess<AccCb>(obj, 3,
            (
                int a, float b, ref int a1, in int a2, out int a3,
                Span<int> c, ReadOnlySpan<int> d, RoRef<int> e, RwRef<int> f
            ) =>
            {
                Console.WriteLine($"{a}, {b}");
                a3 = a2;
            }
        );
#endif
    }
}
