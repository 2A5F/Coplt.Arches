using System.Reflection;
using System.Runtime.CompilerServices;

namespace Coplt.Arches;

internal static class Utils
{
#if NETSTANDARD
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static D CreateDelegate<D>(this MethodInfo self) where D : Delegate => (D)self.CreateDelegate(typeof(D));
#endif
}
