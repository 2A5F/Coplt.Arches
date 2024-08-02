using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Coplt.Arches;

internal static class Utils
{
#if NETSTANDARD
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static D CreateDelegate<D>(this MethodInfo self) where D : Delegate => (D)self.CreateDelegate(typeof(D));
#endif

    public static void EmitLdInd(ILGenerator ilg, Type type)
    {
        if (type == typeof(int))
        {
            ilg.Emit(OpCodes.Ldind_I4);
            return;
        }
        if (type == typeof(uint))
        {
            ilg.Emit(OpCodes.Ldind_U4);
            return;
        }
        if (type == typeof(long) || type == typeof(ulong))
        {
            ilg.Emit(OpCodes.Ldind_I8);
            return;
        }
        if (type == typeof(short))
        {
            ilg.Emit(OpCodes.Ldind_I2);
            return;
        }
        if (type == typeof(ushort))
        {
            ilg.Emit(OpCodes.Ldind_U2);
            return;
        }
        if (type == typeof(byte))
        {
            ilg.Emit(OpCodes.Ldind_U1);
            return;
        }
        if (type == typeof(sbyte))
        {
            ilg.Emit(OpCodes.Ldind_I1);
            return;
        }
        if (type == typeof(float))
        {
            ilg.Emit(OpCodes.Ldind_R4);
            return;
        }
        if (type == typeof(double))
        {
            ilg.Emit(OpCodes.Ldind_R8);
            return;
        }
        if (type == typeof(nint) || type == typeof(nuint) || type.IsByRef || type.IsPointer)
        {
            ilg.Emit(OpCodes.Ldind_I);
            return;
        }
        if (type.IsValueType)
        {
            ilg.Emit(OpCodes.Ldobj, type);
            return;
        }
        else
        {
            ilg.Emit(OpCodes.Ldind_Ref);
            return;
        }
    }

    public static bool IsTag(Type type)
    {
        if (type is { IsByRef: false, IsByRefLike: false })
        {
            var field_type_meta = ArcheTypes.EmitGetTypeMeta(type);
            if (field_type_meta.IsTag) return true;
        }
        return false;
    }
}
