using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

namespace Coplt.Arches;

public unsafe delegate void ArcheAccess(Array arr, int index, void* access);

public static class ArcheAccesses
{
    private static readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, Container>> cache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo EmitAccess(ArcheTypeUnitMeta unit, Type target)
    {
        if (!target.IsValueType) throw new ArgumentException("target must be struct", nameof(target));
        return cache.GetOrCreateValue(target).GetOrCreateValue(unit.Type).Get(unit, target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo EmitAccess(Type unit_type, Type target)
    {
        if (!target.IsValueType) throw new ArgumentException("target must be struct", nameof(target));
        return cache.GetOrCreateValue(target).GetOrCreateValue(unit_type).Get(unit_type, target);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class Container
    {
        private MethodInfo? impl;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodInfo Get(Type unit_type, Type target)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            var unit = ArcheTypes.GetUnit(unit_type);
            return Get(unit, target);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                    [typeof(Array), typeof(int), typeof(void*)], target, true);
                var ilg = method.GetILGenerator();

                var tar = ilg.DeclareLocal(target.MakePointerType());
                var arche = ilg.DeclareLocal(unit.Type.MakeByRefType());

                ilg.Emit(OpCodes.Ldarg_2);
                ilg.Emit(OpCodes.Stloc, tar);

                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Castclass, unit.Type.MakeArrayType());
                ilg.Emit(OpCodes.Ldarg_1);
                ilg.Emit(OpCodes.Ldelema, unit.Type);
                ilg.Emit(OpCodes.Stloc, arche);

                foreach (var field in target.GetFields())
                {
                    var type = field.FieldType;
                    if (type is { IsByRef: false, IsByRefLike: false })
                    {
                        var field_type_meta = ArcheTypes.EmitGetTypeMeta(type);
                        if (field_type_meta.IsTag) continue;
                    }
                    if (type.IsGenericType)
                    {
                        var decl = type.GetGenericTypeDefinition();
                        if (decl == typeof(RoRef<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;

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
                            if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;

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
                            if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;

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
                        if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;
                        
                        ilg.Emit(OpCodes.Ldloc, tar);
                        ilg.Emit(OpCodes.Ldloc, arche);
                        ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                        ilg.Emit(OpCodes.Stfld, field);
                        continue;
                    }
                    {
                        if (!unit.Fields.TryGetValue(type, out var type_meta)) continue;
                        
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
        public static readonly MethodInfo Method = EmitAccess(typeof(C), typeof(A));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheAccess Func = Method.CreateDelegate<ArcheAccess>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Access(C[] array, int index, A* acc) => Func(array, index, acc);
    }

    // private sealed class CallbackContainer
    // {
    //     private MethodInfo? impl;
    //     public MethodInfo Get(ArcheTypeUnitMeta unit, Type target)
    //     {
    //         return null!;
    //     }
    // }
}
