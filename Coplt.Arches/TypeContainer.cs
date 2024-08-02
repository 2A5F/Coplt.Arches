using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Serialization;

namespace Coplt.Arches;

public static class TypeContainers
{
    private static readonly ConcurrentDictionary<(int len, int stride, bool structure), Type> cache = new();

    private static readonly AssemblyBuilder asm =
        AssemblyBuilder.DefineDynamicAssembly(new("Coplt.Arches.TypeContainers"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder mod = asm.DefineDynamicModule("Coplt.Arches.TypeContainers");

    public static Type EmitGet(int len, int stride, bool structure)
    {
        if (len <= 0) throw new ArgumentOutOfRangeException();
        return cache.GetOrAdd((len, stride, structure), static input =>
        {
            var (len, stride, structure) = input;
            var arr = FixedArrays.EmitGet(stride);
            var name = $"Coplt.Arches.TypeContainer{(structure ? "V" : "")}{stride}`{len}";
            var typ = mod.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed,
                structure ? typeof(ValueType) : typeof(object));
            var generics = typ.DefineGenericParameters(Enumerable.Range(0, len).Select(static n => $"T{n}").ToArray());
            for (var i = 0; i < len; i++)
            {
                var field = typ.DefineField($"{i}", arr.MakeGenericType(generics[i]), FieldAttributes.Public);
                field.SetCustomAttribute(typeof(JsonIncludeAttribute).GetConstructor([])!, []);
            }
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            return typ.CreateType()!;
        });
    }
}
