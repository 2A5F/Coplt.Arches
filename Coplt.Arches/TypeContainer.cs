using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Serialization;

namespace Coplt.Arches;

public static class TypeContainers
{
    private static readonly ConcurrentDictionary<int, Type> cache = new();

    private static readonly AssemblyBuilder asm =
        AssemblyBuilder.DefineDynamicAssembly(new("Coplt.Arches.TypeContainers"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder mod = asm.DefineDynamicModule("Coplt.Arches.TypeContainers");

    public static Type EmitGet(int len)
    {
        if (len <= 0) throw new ArgumentOutOfRangeException();
        return cache.GetOrAdd(len, static len =>
        {
            var name = $"Coplt.Arches.TypeContainer`{len}";
            var typ = mod.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed, typeof(ValueType));
            var generics = typ.DefineGenericParameters(Enumerable.Range(0, len).Select(static n => $"T{n}").ToArray());
            for (var i = 0; i < len; i++)
            {
                var field = typ.DefineField($"_{i}", generics[i], FieldAttributes.Public);
                field.SetCustomAttribute(typeof(JsonIncludeAttribute).GetConstructor([])!, []);

                {
                    var method = typ.DefineMethod($"GetRef{i}", MethodAttributes.Public, CallingConventions.HasThis, generics[i].MakeByRefType(), []);
                    method.SetImplementationFlags(MethodImplAttributes.AggressiveInlining);
                    var ilg = method.GetILGenerator();
                    ilg.Emit(OpCodes.Ldarg_0);
                    ilg.Emit(OpCodes.Ldflda);
                    ilg.Emit(OpCodes.Ret);
                }

                {
                    var method = typ.DefineMethod($"Get{i}", MethodAttributes.Public, CallingConventions.HasThis, generics[i], []);
                    method.SetImplementationFlags(MethodImplAttributes.AggressiveInlining);
                    var ilg = method.GetILGenerator();
                    ilg.Emit(OpCodes.Ldarg_0);
                    ilg.Emit(OpCodes.Ldfld);
                    ilg.Emit(OpCodes.Ret);
                }
            }
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            return typ.CreateType()!;
        });
    }
}
