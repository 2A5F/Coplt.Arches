using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

namespace Coplt.Arches;

public unsafe delegate void ArcheAccess(object obj, nint offset, int index, void* access);

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
                    [typeof(object), typeof(nint), typeof(int), typeof(void*)], target, true);
                var ilg = method.GetILGenerator();

                var tar = ilg.DeclareLocal(target.MakePointerType());
                var arche = ilg.DeclareLocal(typeof(void).MakeByRefType());

                ilg.Emit(OpCodes.Ldarg_3);
                ilg.Emit(OpCodes.Stloc, tar);

                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Conv_I);
                ilg.Emit(OpCodes.Ldarg_1);
                ilg.Emit(OpCodes.Add);
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
                            if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;

                            ilg.Emit(OpCodes.Ldloc, tar);
                            ilg.Emit(OpCodes.Ldarg_0);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Ldarg_2);
                            ilg.Emit(OpCodes.Sizeof, ty);
                            ilg.Emit(OpCodes.Conv_I);
                            ilg.Emit(OpCodes.Mul);
                            ilg.Emit(OpCodes.Add);
                            ilg.Emit(OpCodes.Call,
                                UnsafeRef.MethodInfo_CreateRoRef().MakeGenericMethod(ty));
                            ilg.Emit(OpCodes.Stfld, field);
                            continue;
                        }
                        if (decl == typeof(RwRef<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;

                            ilg.Emit(OpCodes.Ldloc, tar);
                            ilg.Emit(OpCodes.Ldarg_0);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Ldarg_2);
                            ilg.Emit(OpCodes.Sizeof, ty);
                            ilg.Emit(OpCodes.Conv_I);
                            ilg.Emit(OpCodes.Mul);
                            ilg.Emit(OpCodes.Add);
                            ilg.Emit(OpCodes.Call,
                                UnsafeRef.MethodInfo_CreateRwRef().MakeGenericMethod(ty));
                            ilg.Emit(OpCodes.Stfld, field);
                            continue;
                        }
                        if (decl == typeof(Span<>) || decl == typeof(ReadOnlySpan<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (Utils.IsTag(ty)) continue;
                            if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;

                            ilg.Emit(OpCodes.Ldloc, tar);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Ldarg_2);
                            ilg.Emit(OpCodes.Sizeof, ty);
                            ilg.Emit(OpCodes.Conv_I);
                            ilg.Emit(OpCodes.Mul);
                            ilg.Emit(OpCodes.Add);
                            ilg.Emit(OpCodes.Newobj, type.GetConstructor([ty.MakeByRefType()])!);
                            ilg.Emit(OpCodes.Stfld, field);
                            continue;
                        }
                    }
                    else if (type.IsByRef)
                    {
                        var ty = type.GetElementType()!;
                        if (Utils.IsTag(ty)) continue;
                        if (!unit.Fields.TryGetValue(ty, out var type_meta)) continue;

                        ilg.Emit(OpCodes.Ldloc, tar);
                        ilg.Emit(OpCodes.Ldloc, arche);
                        ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                        ilg.Emit(OpCodes.Ldarg_2);
                        ilg.Emit(OpCodes.Sizeof, ty);
                        ilg.Emit(OpCodes.Conv_I);
                        ilg.Emit(OpCodes.Mul);
                        ilg.Emit(OpCodes.Add);
                        ilg.Emit(OpCodes.Stfld, field);
                        continue;
                    }
                    {
                        if (Utils.IsTag(type)) continue;
                        if (!unit.Fields.TryGetValue(type, out var type_meta)) continue;

                        ilg.Emit(OpCodes.Ldloc, tar);
                        ilg.Emit(OpCodes.Ldloc, arche);
                        ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                        ilg.Emit(OpCodes.Ldarg_2);
                        ilg.Emit(OpCodes.Sizeof, type);
                        ilg.Emit(OpCodes.Conv_I);
                        ilg.Emit(OpCodes.Mul);
                        ilg.Emit(OpCodes.Add);
                        Utils.EmitLdInd(ilg, type_meta.Type.Type);
                        ilg.Emit(OpCodes.Stfld, field);
                        continue;
                    }
                }

                ilg.Emit(OpCodes.Ret);
                return method;
            }
        }
    }

    private static readonly ConditionalWeakTable<MethodInfo, ConditionalWeakTable<Type, CallbackContainer>>
        callback_cache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo EmitCallbackAccess(ArcheTypeUnitMeta unit, MethodInfo target)
    {
        return callback_cache.GetOrCreateValue(target).GetOrCreateValue(unit.Type).Get(unit, target);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo EmitCallbackAccess(Type unit_type, MethodInfo target)
    {
        return callback_cache.GetOrCreateValue(target).GetOrCreateValue(unit_type).Get(unit_type, target);
    }

    private sealed class CallbackContainer
    {
        private MethodInfo? impl;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodInfo Get(Type unit_type, MethodInfo target)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            var unit = ArcheTypes.GetUnit(unit_type);
            return Get(unit, target);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodInfo Get(ArcheTypeUnitMeta unit, MethodInfo target)
        {
            // todo
            return null!;
        }
    }

    internal static class StaticAccess<C, A>
    {
        public static readonly MethodInfo Method = EmitAccess(typeof(C), typeof(A));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheAccess Func = Method.CreateDelegate<ArcheAccess>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Access(object obj, nint offset, int index, A* acc) => Func(obj, offset, index, acc);
    }
}
