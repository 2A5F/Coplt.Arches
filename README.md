# Coplt.Arches

[![Nuget](https://img.shields.io/nuget/v/Coplt.Arches)](https://www.nuget.org/packages/Coplt.Arches/)

Archetype is a dynamic structure  
This library includes dynamic archetype emit and access method emit  
This is not ECS, but you can use it to implement an archetype based ECS  

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

at.UnsafeAccess(chunk, 3, out Acc acc);
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
ref_acc(chunk, 0, 3, &r);
r.a++;

// Access structures also support ref structures
// Span, ReadOnlySpan will only have a length of 1


// Also supports delegate access (net8+)

delegate void AccCb(
    int a, float b, ref int a1, in int a2, out int a3,
    Span<int> c, ReadOnlySpan<int> d, RoRef<int> e, RwRef<int> f
);

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
```
