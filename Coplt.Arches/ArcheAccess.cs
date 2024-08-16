using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Coplt.Arches.Internal;

namespace Coplt.Arches;

public unsafe delegate void ArcheAccess(object obj, nint offset, int index, void* access);
public unsafe delegate void ArcheRangeAccess(object obj, nint offset, int start, uint length, void* access);
public delegate void ArcheAccess<T>(object obj, nint offset, int index, ref T access);
public delegate void ArcheRangeAccess<T>(object obj, nint offset, int start, uint length, ref T access);
public delegate void ArcheCallbackAccess(object obj, nint offset, int index, Delegate access);
public delegate void ArcheCallbackRangeAccess(object obj, nint offset, int start, uint length, Delegate access);

public static class ArcheAccesses
{
    #region Struct

    private static readonly StructArcheAccess struct_arche_access = new();

    private static readonly StructRefArcheAccess struct_ref_arche_access = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo EmitAccess(ArcheTypeMeta meta, Type target) =>
        struct_arche_access.EmitAccess(meta, target);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo EmitAccess(Type unit_type, Type target) =>
        struct_arche_access.EmitAccess(unit_type, target);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo EmitRefAccess(ArcheTypeMeta meta, Type target) =>
        struct_ref_arche_access.EmitAccess(meta, target);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo EmitRefAccess(Type unit_type, Type target) =>
        struct_ref_arche_access.EmitAccess(unit_type, target);

    internal static class StaticAccess<C, A>
    {
        public static readonly MethodInfo Method = EmitAccess(typeof(C), typeof(A));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheAccess Func = Method.CreateDelegate<ArcheAccess>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Access(object obj, nint offset, int index, A* acc) => Func(obj, offset, index, acc);
    }

    internal static class StaticRefAccess<C, A>
    {
        public static readonly MethodInfo Method = EmitRefAccess(typeof(C), typeof(A));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheAccess<A> Func = Method.CreateDelegate<ArcheAccess<A>>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Access(object obj, nint offset, int index, ref A acc) =>
            Func(obj, offset, index, ref acc);
    }

    #endregion

#if NET8_0_OR_GREATER

    #region Delegate

    private static readonly DelegateAccess delegate_access = new();

    public static MethodInfo EmitDelegateAccess(ArcheTypeMeta meta, Type delegateType) =>
        delegate_access.EmitAccess(meta, delegateType);

    internal static MethodInfo EmitDelegateAccess(Type unit_type, Type delegateType) =>
        delegate_access.EmitAccess(unit_type, delegateType);

    internal static class StaticDelegateAccess<C, D> where D : Delegate
    {
        public static readonly MethodInfo Method =
            delegate_access.EmitAccess(typeof(C), typeof(D));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheCallbackAccess Func = Method.CreateDelegate<ArcheCallbackAccess>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Access(object obj, nint offset, int index, D acc) => Func(obj, offset, index, acc);
    }

    #endregion

    #region Delegate Range

    private static readonly DelegateRangeAccess delegate_range_access = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo EmitDelegateRangeAccess(ArcheTypeMeta meta, Type delegateType) =>
        delegate_range_access.EmitAccess(meta, delegateType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo EmitDelegateRangeAccess(Type unit_type, Type delegateType) =>
        delegate_range_access.EmitAccess(unit_type, delegateType);

    // private sealed class CallbackRangeContainer
    // {
    //     private MethodInfo? impl;
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public MethodInfo Get(Type unit_type, MethodInfo target, Type delegateType)
    //     {
    //         // ReSharper disable once InconsistentlySynchronizedField
    //         if (impl is not null)
    //             // ReSharper disable once InconsistentlySynchronizedField
    //             return impl;
    //         var meta = ArcheTypes.GetMeta(unit_type);
    //         return Get(meta, target, delegateType);
    //     }
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public MethodInfo Get(ArcheTypeMeta meta, MethodInfo target, Type delegateType)
    //     {
    //         // ReSharper disable once InconsistentlySynchronizedField
    //         if (impl is not null)
    //             // ReSharper disable once InconsistentlySynchronizedField
    //             return impl;
    //         lock (this)
    //         {
    //             if (impl is not null) return impl;
    //
    //             var method = new DynamicMethod($"Coplt.Arches.Accesses.Callback<>{target}",
    //                 MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(void),
    //                 [typeof(object), typeof(nint), typeof(int), typeof(uint), typeof(Delegate)], delegateType, true);
    //             var ilg = method.GetILGenerator();
    //
    //             var arche = ilg.DeclareLocal(typeof(void).MakeByRefType());
    //             var access = ilg.DeclareLocal(delegateType);
    //             var cursor = ilg.DeclareLocal(typeof(uint));
    //
    //             ilg.Emit(OpCodes.Ldarg_0);
    //             ilg.Emit(OpCodes.Conv_I);
    //             ilg.Emit(OpCodes.Ldarg_1);
    //             ilg.Emit(OpCodes.Add);
    //             ilg.Emit(OpCodes.Stloc, arche);
    //
    //             ilg.Emit(OpCodes.Ldarg_S, (byte)4);
    //             ilg.Emit(OpCodes.Castclass, delegateType);
    //             ilg.Emit(OpCodes.Stloc, access);
    //
    //             var loop = ilg.DefineLabel();
    //             var loop_body = ilg.DefineLabel();
    //
    //             ilg.Emit(OpCodes.Ldarg_2);
    //             ilg.Emit(OpCodes.Ldc_I4_1);
    //             ilg.Emit(OpCodes.Sub);
    //             ilg.Emit(OpCodes.Starg_S, (byte)2);
    //
    //             ilg.Emit(OpCodes.Ldarg_3);
    //             ilg.Emit(OpCodes.Ldarg_2);
    //             ilg.Emit(OpCodes.Add);
    //             ilg.Emit(OpCodes.Stloc, cursor);
    //
    //             ilg.MarkLabel(loop);
    //             ilg.Emit(OpCodes.Ldloc, cursor);
    //             ilg.Emit(OpCodes.Conv_I4);
    //             ilg.Emit(OpCodes.Ldarg_2);
    //             ilg.Emit(OpCodes.Sub);
    //             ilg.Emit(OpCodes.Brtrue, loop_body);
    //             ilg.Emit(OpCodes.Ret);
    //
    //             ilg.MarkLabel(loop_body);
    //
    //             ilg.Emit(OpCodes.Ldloc, access);
    //             foreach (var parameter in target.GetParameters())
    //             {
    //                 var type = parameter.ParameterType;
    //
    //                 if (type.IsGenericType)
    //                 {
    //                     var decl = type.GetGenericTypeDefinition();
    //                     if (decl == typeof(RoRef<>))
    //                     {
    //                         var ty = type.GetGenericArguments()[0];
    //                         if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;
    //
    //                         ilg.Emit(OpCodes.Ldarg_0);
    //                         ilg.Emit(OpCodes.Ldloc, arche);
    //                         ilg.Emit(OpCodes.Ldflda, type_meta.Field);
    //                         ilg.Emit(OpCodes.Ldloc, cursor);
    //                         ilg.Emit(OpCodes.Sizeof, ty);
    //                         ilg.Emit(OpCodes.Conv_I);
    //                         ilg.Emit(OpCodes.Mul);
    //                         ilg.Emit(OpCodes.Add);
    //                         ilg.Emit(OpCodes.Call,
    //                             UnsafeRef.MethodInfo_CreateRoRef().MakeGenericMethod(ty));
    //                         continue;
    //                     }
    //                     if (decl == typeof(RwRef<>))
    //                     {
    //                         var ty = type.GetGenericArguments()[0];
    //                         if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;
    //
    //                         ilg.Emit(OpCodes.Ldarg_0);
    //                         ilg.Emit(OpCodes.Ldloc, arche);
    //                         ilg.Emit(OpCodes.Ldflda, type_meta.Field);
    //                         ilg.Emit(OpCodes.Ldloc, cursor);
    //                         ilg.Emit(OpCodes.Sizeof, ty);
    //                         ilg.Emit(OpCodes.Conv_I);
    //                         ilg.Emit(OpCodes.Mul);
    //                         ilg.Emit(OpCodes.Add);
    //                         ilg.Emit(OpCodes.Call,
    //                             UnsafeRef.MethodInfo_CreateRwRef().MakeGenericMethod(ty));
    //                         continue;
    //                     }
    //                     if (decl == typeof(Span<>) || decl == typeof(ReadOnlySpan<>))
    //                     {
    //                         var ty = type.GetGenericArguments()[0];
    //                         if (Utils.IsTag(ty)) goto def_v;
    //                         if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;
    //
    //                         ilg.Emit(OpCodes.Ldloc, arche);
    //                         ilg.Emit(OpCodes.Ldflda, type_meta.Field);
    //                         ilg.Emit(OpCodes.Ldloc, cursor);
    //                         ilg.Emit(OpCodes.Sizeof, ty);
    //                         ilg.Emit(OpCodes.Conv_I);
    //                         ilg.Emit(OpCodes.Mul);
    //                         ilg.Emit(OpCodes.Add);
    //                         ilg.Emit(OpCodes.Newobj, type.GetConstructor([ty.MakeByRefType()])!);
    //                         continue;
    //                     }
    //                 }
    //                 else if (type.IsByRef)
    //                 {
    //                     var ty = type.GetElementType()!;
    //                     if (Utils.IsTag(ty)) goto def_v;
    //                     if (!meta.Fields.TryGetValue(ty, out var type_meta)) goto def_v;
    //
    //                     ilg.Emit(OpCodes.Ldloc, arche);
    //                     ilg.Emit(OpCodes.Ldflda, type_meta.Field);
    //                     ilg.Emit(OpCodes.Ldloc, cursor);
    //                     ilg.Emit(OpCodes.Sizeof, ty);
    //                     ilg.Emit(OpCodes.Conv_I);
    //                     ilg.Emit(OpCodes.Mul);
    //                     ilg.Emit(OpCodes.Add);
    //                     continue;
    //                 }
    //
    //                 {
    //                     if (Utils.IsTag(type)) goto def_v;
    //                     if (!meta.Fields.TryGetValue(type, out var type_meta)) goto def_v;
    //
    //                     ilg.Emit(OpCodes.Ldloc, arche);
    //                     ilg.Emit(OpCodes.Ldflda, type_meta.Field);
    //                     ilg.Emit(OpCodes.Ldloc, cursor);
    //                     ilg.Emit(OpCodes.Sizeof, type);
    //                     ilg.Emit(OpCodes.Conv_I);
    //                     ilg.Emit(OpCodes.Mul);
    //                     ilg.Emit(OpCodes.Add);
    //                     Utils.EmitLdInd(ilg, type_meta.Type.Type);
    //                     continue;
    //                 }
    //
    //                 def_v:
    //                 {
    //                     var loc = ilg.DeclareLocal(type);
    //                     ilg.Emit(OpCodes.Ldloc, loc);
    //                     continue;
    //                 }
    //             }
    //             ilg.EmitCall(OpCodes.Callvirt, target, null);
    //
    //             if (target.ReturnType != typeof(void))
    //             {
    //                 ilg.Emit(OpCodes.Pop);
    //             }
    //
    //             ilg.Emit(OpCodes.Ldloc, cursor);
    //             ilg.Emit(OpCodes.Ldc_I4_1);
    //             ilg.Emit(OpCodes.Sub);
    //             ilg.Emit(OpCodes.Stloc, cursor);
    //             ilg.Emit(OpCodes.Br, loop);
    //
    //
    //             return method;
    //         }
    //     }
    // }

    internal static class StaticDelegateRangeAccess<C, D> where D : Delegate
    {
        public static readonly MethodInfo Method =
            EmitDelegateRangeAccess(typeof(C), typeof(D));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheCallbackRangeAccess Func = Method.CreateDelegate<ArcheCallbackRangeAccess>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Access(object obj, nint offset, int start, uint length, D acc) =>
            Func(obj, offset, start, length, acc);
    }

    #endregion

    #region Method

    private static readonly MethodPointerAccess method_pointer_access = new();
    private static readonly MethodRefAccess method_ref_access = new();

    public static MethodInfo EmitMethodAccess(ArcheTypeMeta meta, Type interface_type, Type target_type) =>
        method_pointer_access.EmitAccess(meta, interface_type, target_type);

    internal static MethodInfo EmitMethodAccess(Type unit_type, Type interface_type, Type target_type) =>
        method_pointer_access.EmitAccess(unit_type, interface_type, target_type);

    internal static class StaticMethodAccess<C, I, T> where T : I
    {
        public static readonly MethodInfo Method =
            method_ref_access.EmitAccess(typeof(C), typeof(I), typeof(T));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheAccess<T> Func = Method.CreateDelegate<ArcheAccess<T>>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Access(object obj, nint offset, int index, ref T acc) =>
            Func(obj, offset, index, ref acc);
    }

    #endregion

    #region MethodRange

    private static readonly MethodPointerRangeAccess method_pointer_range_access = new();
    private static readonly MethodRefRangeAccess method_ref_range_access = new();

    public static MethodInfo EmitMethodRangeAccess(ArcheTypeMeta meta, Type interface_type, Type target_type) =>
        method_pointer_range_access.EmitAccess(meta, interface_type, target_type);

    internal static MethodInfo EmitMethodRangeAccess(Type unit_type, Type interface_type, Type target_type) =>
        method_pointer_range_access.EmitAccess(unit_type, interface_type, target_type);

    internal static class StaticMethodRangeAccess<C, I, T> where T : I
    {
        public static readonly MethodInfo Method =
            method_ref_range_access.EmitAccess(typeof(C), typeof(I), typeof(T));
        // ReSharper disable once StaticMemberInGenericType
        public static readonly ArcheRangeAccess<T> Func = Method.CreateDelegate<ArcheRangeAccess<T>>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Access(object obj, nint offset, int start, uint length, ref T acc) =>
            Func(obj, offset, start, length, ref acc);
    }

    #endregion

#endif
}
