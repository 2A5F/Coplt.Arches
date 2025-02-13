# Coplt.Arches

[![Nuget](https://img.shields.io/nuget/v/Coplt.Arches)](https://www.nuget.org/packages/Coplt.Arches/)

This library includes dynamic archetype emit and access method emit  
This is not ECS, but you can use it to implement an archetype based ECS  

### Memory Layout

```csharp
// default chunk size is 16kb
// N is stride or chunk capacity, automatically calculated based on chunk size and content size
// or manually specify the stride and then automatically calculate the chunk size

struct Chunk
{
    InlineArrayN<A> a;
    InlineArrayN<B> b;
    InlineArrayN<C> c;
    ...
}

Chunk
[
  [A, A, A, ...]
  [B, B, B, ...]
  [C, C, C, ...]
  ...
]
```

### Example

```csharp
struct Foo
{
    public int a;
    public int b;
}

struct Tag;


ArcheTypeMeta arch = ArcheTypes.EmitArcheType(
[
    typeof(int), 
    typeof(float), 
    typeof(object), // You can use any type, including managed types
    typeof(Foo), 
    typeof(Vector128<float>),
    // All 1 byte size empty structures are considered tags 
    // And do not actually occupy space
    typeof(Tag), 
    typeof(bool), 
    typeof(byte),
], new ArcheTypeOptions());

// AArcheType contains some dynamically generated utility methods, 
// such as create chunk instance、 generating access bindings

AArcheType at = arch.ArcheType;

object chunk = at.Create();


record struct Acc
{
    public int a;
    public RwRef<Vector128<float>> v;
    public RoRef<Foo> foo;
}

// Automatically generate access bindings
// The access structure can be on the heap, which means it can be passed in linq

at.UnsafeAccess(chunk, index: 3, out Acc acc);
Console.WriteLine(acc);


ref struct RefAcc
{
    public int v;
    public ref int a;
    public Span<int> b;
    public ReadOnlySpan<int> c;
    public RoRef<int> d;
    public RwRef<int> e;
}
ArcheAccess ref_acc = at.DynamicAccess(typeof(Acc));
RefAcc r = default;
ref_acc(chunk, offset: 0, index: 3, &r);
r.a++;

// Access structures also support ref structures
// Span, ReadOnlySpan will only have a length of 1


// Also supports delegate access (net8+)

delegate void AccCb(
    int a, float b, ref int a1, in int a2, out int a3,
    Span<int> c, ReadOnlySpan<int> d, RoRef<int> e, RwRef<int> f
);

at.UnsafeDelegateAccess<AccCb>(obj, index: 3,
    (
        int a, float b, ref int a1, in int a2, out int a3,
        Span<int> c, ReadOnlySpan<int> d, RoRef<int> e, RwRef<int> f
    ) =>
    {
        Console.WriteLine($"{a}, {b}");
        a3 = a2;
    }
);

// Support range delegate access, call delegates in reverse order

at.UnsafeDelegateRangeAccess<AccCb>(obj, start: 3, length: 3,
    (
        int a, float b, ref int a1, in int a2, out int a3,
        Span<int> c, ReadOnlySpan<int> d, RoRef<int> e, RwRef<int> f
    ) =>
    {
        // Calling order:
        // index 5
        // index 4
        // index 3
        Console.WriteLine($"{a}, {b}");
        a3 = a2;
    }
);


// Support method access. 
// Need to provide an interface containing only one method to specify the method. 
// This access can support inlining.

interface IAcc
{
    public void A(
        int a, float b, ref int a1, in int a2, out int a3,
        Span<int> c, ReadOnlySpan<int> d,
        RoRef<int> e, RwRef<int> f
    );
}

struct SAcc : IAcc
{
    public int a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void A(int a, float b, ref int a1, in int a2, out int a3, Span<int> c, ReadOnlySpan<int> d, RoRef<int> e,
        RwRef<int> f)
    {
        Console.WriteLine($"{a}, {b}");
        a3 = a2;
        this.a += a;
    }
}

var s_acc = new SAcc();
at.UnsafeMethodAccess<IAcc, SAcc>(obj, 3, ref s_acc);
Console.WriteLine(s_acc.a);

var s_acc_2 = new SAcc();
at.UnsafeMethodRangeAccess<IAcc, SAcc>(obj, 3, 3, ref s_acc_2);
Console.WriteLine(s_acc_2.a);
```
