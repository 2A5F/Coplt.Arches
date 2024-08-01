using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

namespace Coplt.Arches;

public unsafe delegate void DynamicArcheAccess(Array arr, int index, void* access);

public unsafe delegate void ArcheAccess<in C>(C[] arr, int index, void* access);

public static class ArcheAccesses
{
    private static readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, Container>> cache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo EmitAccess(ArcheTypeUnitMeta unit, Type target)
    {
        if (!target.IsValueType) throw new ArgumentException("target must be struct", nameof(target));
        return cache.GetOrCreateValue(target).GetOrCreateValue(unit.Type).Get(unit, target);
    }

    private sealed class Container
    {
        private MethodInfo? impl;
        public MethodInfo Get(ArcheTypeUnitMeta unit, Type target)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            lock (this)
            {
                if (impl is not null) return impl;
                var method = new DynamicMethod($"Coplt.Arches.Accesses.<>{target}",
                    MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(void),
                    [unit.Type.MakeArrayType(), typeof(int), typeof(void*)], target, true);
                var ilg = method.GetILGenerator();

                var tar = ilg.DeclareLocal(target.MakePointerType());
                var arche = ilg.DeclareLocal(unit.Type.MakeByRefType());

                ilg.Emit(OpCodes.Ldarg_2);
                ilg.Emit(OpCodes.Stloc, tar);

                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Ldarg_1);
                ilg.Emit(OpCodes.Ldelema, unit.Type);
                ilg.Emit(OpCodes.Stloc, arche);

                foreach (var field in target.GetFields())
                {
                    var type = field.FieldType;
                    if (type.IsGenericType)
                    {
                        var decl = type.GetGenericTypeDefinition();
                        if (decl == typeof(RoRef<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (!unit.Fields.TryGetValue(ty, out var type_meta))
                                throw new ArgumentException($"{type} dose not in the archetype", nameof(target));

                            ilg.Emit(OpCodes.Ldloc, tar);
                            ilg.Emit(OpCodes.Ldarg_0);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Call,
                                UnsafeArrayRef.MethodInfo_CreateRoRef().MakeGenericMethod(unit.Type, ty));
                            ilg.Emit(OpCodes.Stfld, field);
                            continue;
                        }
                        else if (decl == typeof(RwRef<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (!unit.Fields.TryGetValue(ty, out var type_meta))
                                throw new ArgumentException($"{type} dose not in the archetype", nameof(target));

                            ilg.Emit(OpCodes.Ldloc, tar);
                            ilg.Emit(OpCodes.Ldarg_0);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Call,
                                UnsafeArrayRef.MethodInfo_CreateRwRef().MakeGenericMethod(unit.Type, ty));
                            ilg.Emit(OpCodes.Stfld, field);
                            continue;
                        }
                        else if (decl == typeof(Span<>) || decl == typeof(ReadOnlySpan<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (!unit.Fields.TryGetValue(ty, out var type_meta))
                                throw new ArgumentException($"{type} dose not in the archetype", nameof(target));

                            ilg.Emit(OpCodes.Ldloc, tar);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Newobj, type.GetConstructor([ty.MakeByRefType()])!);
                            ilg.Emit(OpCodes.Stfld, field);
                            continue;
                        }
                    }
                    else if (type.IsByRef)
                    {
                        var ty = type.GetElementType()!;
                        if (!unit.Fields.TryGetValue(ty, out var type_meta))
                            throw new ArgumentException($"{type} dose not in the archetype", nameof(target));

                        ilg.Emit(OpCodes.Ldloc, tar);
                        ilg.Emit(OpCodes.Ldloc, arche);
                        ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                        ilg.Emit(OpCodes.Stfld, field);
                        continue;
                    }
                    {
                        if (!unit.Fields.TryGetValue(type, out var type_meta))
                            throw new ArgumentException($"{type} dose not in the archetype", nameof(target));

                        ilg.Emit(OpCodes.Ldloc, tar);
                        ilg.Emit(OpCodes.Ldloc, arche);
                        ilg.Emit(OpCodes.Ldfld, type_meta.Field);
                        ilg.Emit(OpCodes.Stfld, field);
                    }
                }

                ilg.Emit(OpCodes.Ret);
                return method;
            }
        }
    }
    
    internal static class StaticAccess<C, A>
    {
        private static ArcheAccess<C>? Func;

#if NETSTANDARD
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArcheAccess<C> Get(ArcheTypeUnitMeta unit) =>
            Func ??= (ArcheAccess<C>)EmitAccess(unit, typeof(A)).CreateDelegate(typeof(ArcheAccess<C>));
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArcheAccess<C> Get(ArcheTypeUnitMeta unit) =>
            Func ??= EmitAccess(unit, typeof(A)).CreateDelegate<ArcheAccess<C>>();
#endif
    }
}
