using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

namespace Coplt.Arches;

internal abstract class AStructArcheAccess : AArcheAccess
{
    private readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, Container>> cache = new();

    public MethodInfo EmitAccess(ArcheTypeMeta meta, Type target)
    {
        if (!target.IsValueType) throw new ArgumentException("target must be struct", nameof(target));
        return cache.GetOrCreateValue(target).GetOrCreateValue(meta.Type).Get(this, meta, target);
    }

    internal MethodInfo EmitAccess(Type unit_type, Type target)
    {
        if (!target.IsValueType) throw new ArgumentException("target must be struct", nameof(target));
        return cache.GetOrCreateValue(target).GetOrCreateValue(unit_type).Get(this, unit_type, target);
    }

    protected abstract Type MakeAccessParamType(Type target);

    protected abstract Type MakeAccessLocalType(Type target);

    protected sealed class Container
    {
        private MethodInfo? impl;
        public MethodInfo Get(AStructArcheAccess self, Type unit_type, Type target)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            var meta = ArcheTypes.GetMeta(unit_type);
            return Get(self, meta, target);
        }
        public MethodInfo Get(AStructArcheAccess self, ArcheTypeMeta meta, Type target)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (impl is not null)
                // ReSharper disable once InconsistentlySynchronizedField
                return impl;
            lock (this)
            {
                if (impl is not null) return impl;
                var method = new DynamicMethod($"Coplt.Arches.Accesses.{self.Name}<>{target}",
                    MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(void),
                    [typeof(object), typeof(nint), typeof(int), self.MakeAccessParamType(target)], target, true);
                var ilg = method.GetILGenerator();

                var tar = ilg.DeclareLocal(self.MakeAccessLocalType(target));
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
                            if (!meta.Fields.TryGetValue(ty, out var type_meta)) continue;

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
                            if (!meta.Fields.TryGetValue(ty, out var type_meta)) continue;

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
                            if (!meta.Fields.TryGetValue(ty, out var type_meta)) continue;

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
                        if (!meta.Fields.TryGetValue(ty, out var type_meta)) continue;

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
                        if (!meta.Fields.TryGetValue(type, out var type_meta)) continue;

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
}

internal sealed class StructArcheAccess : AStructArcheAccess
{
    protected override string Name => "StructPointer";

    protected override Type MakeAccessParamType(Type target) => typeof(void*);

    protected override Type MakeAccessLocalType(Type target) => target.MakePointerType();
}

internal sealed class StructRefArcheAccess : AStructArcheAccess
{
    protected override string Name => "StructRef";

    protected override Type MakeAccessParamType(Type target) => target.MakeByRefType();

    protected override Type MakeAccessLocalType(Type target) => target.MakeByRefType();
}
