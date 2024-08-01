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

    private ref struct Acc
    {
        public int v;
        public ref int a;
        public Span<int> b;
        public ReadOnlySpan<int> c;
        public RoRef<int> d;
        public RwRef<int> e;
    }

    private record struct Acc2
    {
        public int a;
        public RwRef<Vector128<float>> v;
        public RoRef<Foo> foo;
    }

    [Test]
    public unsafe void Test1()
    {
        var arch = ArcheTypes.EmitArcheType(
        [
            typeof(int), typeof(float), typeof(Foo), typeof(object), typeof(Vector128<float>), typeof(Tag),
            typeof(bool), typeof(byte)
        ], new ArcheTypeOptions());

        var atu = arch.Units[0];
        var at = atu.ArcheType;

        Console.WriteLine(atu.TypeMeta);
        Console.WriteLine();
        Console.WriteLine(string.Join("\n", arch.Units[0].Fields.Values.OrderBy(a => a.Index)));
        Console.WriteLine();
        Console.WriteLine(string.Join("\n", arch.Units[0].GetRef.Values.OrderBy(a => a.Index)));

        Console.WriteLine();
        var arr = at.AllocateArray(16);
        Console.WriteLine(arr);

        Console.WriteLine();
        var acc = ArcheAccesses.EmitAccess(arch.Units[0], typeof(Acc));
        Console.WriteLine(acc);

        Console.WriteLine();
        Acc r = default;
        acc.Invoke(null, [arr, 3, (nuint)(void*)&r]);
        Console.WriteLine(r.a);
        r.a = 1;
        Console.WriteLine(r.a);
        Assert.That(r.a, Is.EqualTo(1));
        
        Console.WriteLine();
        at.Access(arr, 3, out Acc2 acc2);
        Console.WriteLine(acc2);
        acc2.v.V = Vector128.Create(1f, 2, 3, 4);
        Console.WriteLine(acc2);
        Assert.That(acc2.a, Is.EqualTo(1));
        
        Console.WriteLine();
        at.Access(arr, 3, out (int a, float b, Vector128<float> c) acc3);
        Console.WriteLine(acc3);
    }
}
