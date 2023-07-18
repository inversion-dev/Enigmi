namespace Enigmi.Common;

public static class TypeExtensions
{
	public static bool IsAssignableToExtended(this Type typeToCheck, Type baseType)
	{
		typeToCheck = typeToCheck.IsGenericType ? typeToCheck.GetGenericTypeDefinition() : typeToCheck;
		baseType = baseType.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;

		if (typeToCheck.IsAssignableTo(baseType))
		{
			return true;
		}

		if (baseType.IsInterface)
		{
			return typeToCheck.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition().IsAssignableTo(baseType));
		}

		Type? currentType = typeToCheck;

		while (currentType != null)
		{
			if (currentType.IsGenericType && currentType.GetGenericTypeDefinition().IsAssignableTo(baseType))
			{
				return true;
			}
			currentType = currentType.BaseType;
		}
		return false;
	}

	public static Type? GetGenericInterfacesOf(this Type currentType, Type interfaceType)
	{
		if (!currentType.IsClass)
		{
			throw new ArgumentException($"{nameof(currentType)} is not a class", nameof(currentType));
		}

		if (!interfaceType.IsInterface)
		{
			throw new ArgumentException($"{nameof(interfaceType)} is not an interface", nameof(interfaceType));
		}

		var genericInterfaces = currentType.GetInterfaces().Where(x => x.IsGenericType &&
			x.GetGenericTypeDefinition() == interfaceType.GetGenericTypeDefinition()).ToList();

		if (genericInterfaces.Count > 1)
		{
			throw new Exception($"Class {currentType.Name} implements {interfaceType.Name} more than once");
		}

		return genericInterfaces.FirstOrDefault();
	}

	public static Type? GetGenericAbstractClassOf(this Type currentType, Type superType)
	{
		if (!currentType.IsClass)
		{
			throw new ArgumentException($"{nameof(currentType)} is not a class", nameof(currentType));
		}

		if (!superType.IsClass)
		{
			throw new ArgumentException($"{nameof(superType)} is not an class", nameof(superType));
		}

		var baseType = currentType.BaseType;
		while (baseType != null)
		{
			if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == superType.GetGenericTypeDefinition())
				return baseType;

			baseType = baseType.BaseType;
		}

		return null;
	}
}