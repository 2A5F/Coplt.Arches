using System.Reflection;

namespace Coplt.Arches;

public readonly record struct TypeMeta(Type Type, int Size, int Align, bool IsManaged, bool IsTag);

public readonly record struct FieldMeta(FieldInfo Field, TypeMeta FieldType, TypeMeta Type, int Index);

public readonly record struct MethodMeta(MethodInfo Field, TypeMeta Type, int Index);
