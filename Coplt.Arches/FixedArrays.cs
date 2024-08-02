using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Coplt.Arches;

public static class FixedArrays
{
    private static readonly ConcurrentDictionary<int, Type> cache = new();

    private static readonly AssemblyBuilder asm =
        AssemblyBuilder.DefineDynamicAssembly(new("Coplt.Arches.FixedArrays"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder mod = asm.DefineDynamicModule("Coplt.Arches.FixedArrays");

    public static Type EmitGet(int len)
    {
        if (len <= 0) throw new ArgumentOutOfRangeException();
        return cache.GetOrAdd(len, static len =>
        {
            var name = $"Coplt.Arches.FixedArrays{len}`1";
            var typ = mod.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed, typeof(ValueType));
            var generic = typ.DefineGenericParameters("T")[0];
#if NET8_0
            typ.SetCustomAttribute(
                new CustomAttributeBuilder(typeof(InlineArrayAttribute).GetConstructor([typeof(int)])!, [len]));
            var field = typ.DefineField($"_", generic, FieldAttributes.Private);
            field.SetCustomAttribute(typeof(JsonIncludeAttribute).GetConstructor([])!, []);
#else
            for (var i = 0; i < len; i++)
            {
                var field = typ.DefineField($"{i}", generic, FieldAttributes.Public);
                field.SetCustomAttribute(typeof(JsonIncludeAttribute).GetConstructor([])!, []);
            }
#endif
            {
                var span_type = typeof(Span<>).MakeGenericType(generic);
                var as_span_prop = typ.DefineProperty($"Span", PropertyAttributes.None, CallingConventions.Standard,
                    span_type, []);
                as_span_prop.SetCustomAttribute(typeof(UnscopedRefAttribute).GetConstructor([])!, []);
                var as_span_get = typ.DefineMethod("get_Span", MethodAttributes.Public, CallingConventions.Standard,
                    span_type, []);
                as_span_prop.SetGetMethod(as_span_get);
                var ilg = as_span_get.GetILGenerator();
                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Ldc_I4, len);
                ilg.Emit(OpCodes.Newobj,
                    TypeBuilder.GetConstructor(span_type, typeof(Span<>).GetConstructor([typeof(void*), typeof(int)])!));
                ilg.Emit(OpCodes.Ret);
            }

            // ReSharper disable once RedundantSuppressNullableWarningExpression
            return typ.CreateType()!;
        });
    }
}

#if NET8_0
[InlineArray(10)]
public struct Foo<T>
{
    private T value;

    [UnscopedRef]
    public ref T Get1
    {
        get => ref this[1];
    }
}

#endif
