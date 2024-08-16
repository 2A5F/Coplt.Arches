using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

#if NET8_0_OR_GREATER

namespace Coplt.Arches;

internal abstract class AMethodAccess : AArcheAccess
{
    private readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, ConditionalWeakTable<Type, Container>>>
        cache = new();

    public MethodInfo EmitAccess(ArcheTypeMeta meta, Type interface_type, Type target_type)
    {
        return cache.GetOrCreateValue(interface_type).GetOrCreateValue(target_type).GetOrCreateValue(meta.Type)
            .Get(this, meta, interface_type, target_type);
    }

    internal MethodInfo EmitAccess(Type unit_type, Type interface_type, Type target_type)
    {
        return cache.GetOrCreateValue(interface_type).GetOrCreateValue(target_type).GetOrCreateValue(unit_type)
            .Get(this, unit_type, interface_type, target_type);
    }

    protected abstract Type MakeAccessParamType(Type target);

    protected abstract Type MakeAccessLocalType(Type target);

    protected abstract bool IsRange();

    private sealed class Container
    {
        private MethodInfo? impl;
        public MethodInfo Get(AMethodAccess self, Type unit_type, Type interface_type, Type target_type)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            var meta = ArcheTypes.GetMeta(unit_type);
            return Get(self, meta, interface_type, target_type);
        }

        public MethodInfo Get(AMethodAccess self, ArcheTypeMeta meta, Type interface_type, Type target_type)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            lock (this)
            {
                if (impl is not null) return impl;

                if (!interface_type.IsAssignableFrom(target_type))
                    throw new NotSupportedException(
                        $"The target type {target_type} does not implement the interface {interface_type}");

                var inferface_method = interface_type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => !m.IsSpecialName);
                if (inferface_method is null)
                    throw new NotSupportedException(
                        $"The target interface {interface_type} does not contain the method");

                var interface_map = target_type.GetInterfaceMap(interface_type);
                var target = interface_map.TargetMethods[Array.IndexOf(interface_map.InterfaceMethods, inferface_method)];

                Type[] param_s = self.IsRange()
                    ?
                    [
                        typeof(object), typeof(nint), typeof(int), typeof(uint),
                        self.MakeAccessParamType(target_type)
                    ]
                    : [typeof(object), typeof(nint), typeof(int), self.MakeAccessParamType(target_type)];

                var method = new DynamicMethod(
                    $"Coplt.Arches.Accesses.{self.Name}<>{target_type.Name}<>{interface_type.Name}",
                    MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(void),
                    param_s, target_type,
                    true);
                var ilg = method.GetILGenerator();

                var arche = ilg.DeclareLocal(typeof(void).MakeByRefType());
                var access = ilg.DeclareLocal(self.MakeAccessLocalType(target_type));
                var cursor = ilg.DeclareLocal(typeof(uint));

                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Conv_I);
                ilg.Emit(OpCodes.Ldarg_1);
                ilg.Emit(OpCodes.Add);
                ilg.Emit(OpCodes.Stloc, arche);

                ilg.Emit(OpCodes.Ldarg_S, self.IsRange() ? (byte)4 : (byte)3);
                if (!target_type.IsValueType) ilg.Emit(OpCodes.Ldind_Ref, target_type);
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
                        // ReSharper disable once RedundantJumpStatement
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

internal sealed class MethodPointerAccess : AMethodAccess
{
    protected override string Name => "MethodPointer";

    protected override Type MakeAccessParamType(Type target) => typeof(void*);
    protected override Type MakeAccessLocalType(Type target) => target.IsValueType ? target.MakeByRefType() : target;

    protected override bool IsRange() => false;
}

internal sealed class MethodRefAccess : AMethodAccess
{
    protected override string Name => "MethodRef";

    protected override Type MakeAccessParamType(Type target) => target.MakeByRefType();
    protected override Type MakeAccessLocalType(Type target) => target.IsValueType ? target.MakeByRefType() : target;

    protected override bool IsRange() => false;
}

internal sealed class MethodPointerRangeAccess : AMethodAccess
{
    protected override string Name => "MethodPointerRange";

    protected override Type MakeAccessParamType(Type target) => typeof(void*);
    protected override Type MakeAccessLocalType(Type target) => target.IsValueType ? target.MakeByRefType() : target;

    protected override bool IsRange() => true;
}

internal sealed class MethodRefRangeAccess : AMethodAccess
{
    protected override string Name => "MethodRefRange";

    protected override Type MakeAccessParamType(Type target) => target.MakeByRefType();
    protected override Type MakeAccessLocalType(Type target) => target.IsValueType ? target.MakeByRefType() : target;

    protected override bool IsRange() => true;
}

#endif
