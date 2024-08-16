using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

#if NET8_0_OR_GREATER

namespace Coplt.Arches;

internal abstract class ADelegateAccess : AArcheAccess
{
    private readonly ConditionalWeakTable<MethodInfo, ConditionalWeakTable<Type, Container>>
        cache = new();

    public MethodInfo EmitAccess(ArcheTypeMeta meta, Type delegateType)
    {
        var target = delegateType.GetMethod("Invoke");
        if (target == null) throw new ArgumentException("The target type does not contain an Invoke method");
        return cache.GetOrCreateValue(target).GetOrCreateValue(meta.Type).Get(this, meta, target, delegateType);
    }

    internal MethodInfo EmitAccess(Type unit_type, Type delegateType)
    {
        var target = delegateType.GetMethod("Invoke");
        if (target == null) throw new ArgumentException("The target type does not contain an Invoke method");
        return cache.GetOrCreateValue(target).GetOrCreateValue(unit_type).Get(this, unit_type, target, delegateType);
    }

    protected abstract Type MakeAccessParamType(Type target);

    protected abstract Type MakeAccessLocalType(Type target);

    protected abstract bool IsRange();

    private sealed class Container
    {
        private MethodInfo? impl;
        public MethodInfo Get(ADelegateAccess self, Type unit_type, MethodInfo target, Type delegateType)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            var meta = ArcheTypes.GetMeta(unit_type);
            return Get(self, meta, target, delegateType);
        }

        public MethodInfo Get(ADelegateAccess self, ArcheTypeMeta meta, MethodInfo target, Type delegateType)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            lock (this)
            {
                if (impl is not null) return impl;

                Type[] param_s = self.IsRange()
                    ?
                    [
                        typeof(object), typeof(nint), typeof(int), typeof(uint),
                        self.MakeAccessParamType(delegateType)
                    ]
                    : [typeof(object), typeof(nint), typeof(int), self.MakeAccessParamType(delegateType)];

                var method = new DynamicMethod($"Coplt.Arches.Accesses.{self.Name}<>{target}",
                    MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(void),
                    param_s, delegateType,
                    true);
                var ilg = method.GetILGenerator();

                var arche = ilg.DeclareLocal(typeof(void).MakeByRefType());
                var access = ilg.DeclareLocal(delegateType);
                var cursor = ilg.DeclareLocal(typeof(uint));

                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Conv_I);
                ilg.Emit(OpCodes.Ldarg_1);
                ilg.Emit(OpCodes.Add);
                ilg.Emit(OpCodes.Stloc, arche);

                ilg.Emit(OpCodes.Ldarg_S, self.IsRange() ? (byte)4 : (byte)3);
                ilg.Emit(OpCodes.Castclass, self.MakeAccessLocalType(delegateType));
                ilg.Emit(OpCodes.Stloc, access);

                var loop = ilg.DefineLabel();
                var loop_body = ilg.DefineLabel();

                if (self.IsRange())
                {
                    ilg.Emit(OpCodes.Ldarg_2);
                    ilg.Emit(OpCodes.Ldc_I4_1);
                    ilg.Emit(OpCodes.Sub);
                    ilg.Emit(OpCodes.Starg_S, (byte)2);

                    ilg.Emit(OpCodes.Ldarg_3);
                    ilg.Emit(OpCodes.Ldarg_2);
                    ilg.Emit(OpCodes.Add);
                    ilg.Emit(OpCodes.Stloc, cursor);

                    ilg.MarkLabel(loop);
                    ilg.Emit(OpCodes.Ldloc, cursor);
                    ilg.Emit(OpCodes.Conv_I4);
                    ilg.Emit(OpCodes.Ldarg_2);
                    ilg.Emit(OpCodes.Sub);
                    ilg.Emit(OpCodes.Brtrue, loop_body);
                    ilg.Emit(OpCodes.Ret);

                    ilg.MarkLabel(loop_body);
                }
                else
                {
                    ilg.Emit(OpCodes.Ldarg_2);
                    ilg.Emit(OpCodes.Stloc, cursor);
                }

                ilg.Emit(OpCodes.Ldloc, access);
                foreach (var parameter in target.GetParameters())
                {
                    var type = parameter.ParameterType;

                    if (type.IsGenericType)
                    {
                        var decl = type.GetGenericTypeDefinition();
                        if (decl == typeof(RoRef<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;

                            ilg.Emit(OpCodes.Ldarg_0);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Ldloc, cursor);
                            ilg.Emit(OpCodes.Sizeof, ty);
                            ilg.Emit(OpCodes.Conv_I);
                            ilg.Emit(OpCodes.Mul);
                            ilg.Emit(OpCodes.Add);
                            ilg.Emit(OpCodes.Call,
                                UnsafeRef.MethodInfo_CreateRoRef().MakeGenericMethod(ty));
                            continue;
                        }
                        if (decl == typeof(RwRef<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;

                            ilg.Emit(OpCodes.Ldarg_0);
                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Ldloc, cursor);
                            ilg.Emit(OpCodes.Sizeof, ty);
                            ilg.Emit(OpCodes.Conv_I);
                            ilg.Emit(OpCodes.Mul);
                            ilg.Emit(OpCodes.Add);
                            ilg.Emit(OpCodes.Call,
                                UnsafeRef.MethodInfo_CreateRwRef().MakeGenericMethod(ty));
                            continue;
                        }
                        if (decl == typeof(Span<>) || decl == typeof(ReadOnlySpan<>))
                        {
                            var ty = type.GetGenericArguments()[0];
                            if (Utils.IsTag(ty)) goto def_v;
                            if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;

                            ilg.Emit(OpCodes.Ldloc, arche);
                            ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                            ilg.Emit(OpCodes.Ldloc, cursor);
                            ilg.Emit(OpCodes.Sizeof, ty);
                            ilg.Emit(OpCodes.Conv_I);
                            ilg.Emit(OpCodes.Mul);
                            ilg.Emit(OpCodes.Add);
                            ilg.Emit(OpCodes.Newobj, type.GetConstructor([ty.MakeByRefType()])!);
                            continue;
                        }
                    }
                    else if (type.IsByRef)
                    {
                        var ty = type.GetElementType()!;
                        if (Utils.IsTag(ty)) goto def_v;
                        if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;

                        ilg.Emit(OpCodes.Ldloc, arche);
                        ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                        ilg.Emit(OpCodes.Ldloc, cursor);
                        ilg.Emit(OpCodes.Sizeof, ty);
                        ilg.Emit(OpCodes.Conv_I);
                        ilg.Emit(OpCodes.Mul);
                        ilg.Emit(OpCodes.Add);
                        continue;
                    }

                    {
                        if (Utils.IsTag(type)) goto def_v;
                        if (!meta.Fields.TryGetValue(type, out var type_meta)) goto def_v;

                        ilg.Emit(OpCodes.Ldloc, arche);
                        ilg.Emit(OpCodes.Ldflda, type_meta.Field);
                        ilg.Emit(OpCodes.Ldloc, cursor);
                        ilg.Emit(OpCodes.Sizeof, type);
                        ilg.Emit(OpCodes.Conv_I);
                        ilg.Emit(OpCodes.Mul);
                        ilg.Emit(OpCodes.Add);
                        Utils.EmitLdInd(ilg, type_meta.Type.Type);
                        continue;
                    }

                    def_v:
                    {
                        var loc = ilg.DeclareLocal(type);
                        ilg.Emit(OpCodes.Ldloc, loc);
                        continue;
                    }
                }
                ilg.EmitCall(OpCodes.Callvirt, target, null);

                if (target.ReturnType != typeof(void))
                {
                    ilg.Emit(OpCodes.Pop);
                }

                if (self.IsRange())
                {
                    ilg.Emit(OpCodes.Ldloc, cursor);
                    ilg.Emit(OpCodes.Ldc_I4_1);
                    ilg.Emit(OpCodes.Sub);
                    ilg.Emit(OpCodes.Stloc, cursor);
                    ilg.Emit(OpCodes.Br, loop);
                }
                else
                {
                    ilg.Emit(OpCodes.Ret);
                }

                return method;
            }
        }
    }
}

internal sealed class DelegateAccess : ADelegateAccess
{
    protected override string Name => "Delegate";

    protected override Type MakeAccessParamType(Type target) => typeof(Delegate);
    protected override Type MakeAccessLocalType(Type target) => target;

    protected override bool IsRange() => false;
}

internal sealed class DelegateRangeAccess : ADelegateAccess
{
    protected override string Name => "DelegateRange";

    protected override Type MakeAccessParamType(Type target) => typeof(Delegate);
    protected override Type MakeAccessLocalType(Type target) => target;

    protected override bool IsRange() => true;
}

#endif
