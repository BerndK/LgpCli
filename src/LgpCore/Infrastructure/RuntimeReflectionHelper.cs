using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Infrastructure;

public static class RuntimeReflectionHelper
{
  public const BindingFlags DefaultLookup = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
  public static PropertyInfo? TryGetRuntimePropertyInfo(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties|DynamicallyAccessedMemberTypes.NonPublicProperties)]
#endif
    this Type type, 
    string name,
    BindingFlags bindingFlags = DefaultLookup)
  {
    return type.GetProperty(name, bindingFlags);
  }

  public static PropertyInfo GetRuntimePropertyInfo(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties|DynamicallyAccessedMemberTypes.NonPublicProperties)]
#endif
    this Type type, 
    string name,
    BindingFlags bindingFlags = DefaultLookup)
  {
    return type.GetProperty(name, bindingFlags)
           ?? throw new InvalidOperationException($"Not able to get property '{name}' from {type.FullName}");
  }

  public static T? GetProp<T>(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
#endif
    Type type,
    object? instance, 
    string name, object?[]? index = null)
  {
    if (instance == null)
      return default;
    var info = TryGetRuntimePropertyInfo(type, name);
    var value = info?.GetValue(instance, index);
    return value is T ? (T)value : default;
  }

  public static T? GetProp<T>(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
#endif
    string sType,
    object? instance, string name, object?[]? index = null)
  {
    if (instance == null)
      return default;
    var type = Type.GetType(sType);
    if (type == null)
      return default(T);

    var info = TryGetRuntimePropertyInfo(type, name);
    var value = info?.GetValue(instance, index);
    return value is T ? (T)value : default;
  }

#if NET5_0_OR_GREATER
  [RequiresUnreferencedCode("This functionality is not compatible with trimming. Use other overloads instead (providing the types directly or by constant string (does not work for private types))", Url = "https://site/trimming-and-method")]
#endif
  public static T? GetProp<T>(object? instance, string name, object?[]? index = null)
  {
    if (instance == null)
      return default;
    
    var info = TryGetRuntimePropertyInfo(instance.GetType(), name);
    var value = info?.GetValue(instance, index);
    return value is T ? (T)value : default;
  }

  public static FieldInfo? TryGetRuntimeFieldInfo(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields|DynamicallyAccessedMemberTypes.NonPublicFields)]
#endif
    this Type type, 
    string name,
    BindingFlags bindingFlags = DefaultLookup)
  {
    return type.GetField(name, bindingFlags);
  }

  public static FieldInfo GetRuntimeFieldInfo(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields|DynamicallyAccessedMemberTypes.NonPublicFields)]
#endif
  this Type type, 
    string name,
    BindingFlags bindingFlags = DefaultLookup)
  {
    return type.GetField(name, bindingFlags)
           ?? throw new InvalidOperationException($"Not able to get field '{name}' from {type.FullName}");
  }

  public static T? GetField<T>(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
#endif
    Type type,
    object? instance, string name)
  {
    if (instance == null)
      return default;
    var info = TryGetRuntimeFieldInfo(type, name);
    var value = info?.GetValue(instance);
    return value is T ? (T) value : default;
  }

  public static T? GetField<T>(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
#endif
    string sType,
    object? instance, 
    string name)
  {
    if (instance == null)
      return default;
    var type = Type.GetType(sType);
    if (type == null)
      return default(T);
    var info = TryGetRuntimeFieldInfo(type, name);
    var value = info?.GetValue(instance);
    return value is T ? (T) value : default;
  }

#if NET5_0_OR_GREATER
  [RequiresUnreferencedCode("This functionality is not compatible with trimming. Use other overloads instead (providing the types directly or by constant string (does not work for private types))", Url = "https://site/trimming-and-method")]
#endif

  public static T? GetField<T>(object? instance, string name)
  {
    if (instance == null)
      return default;
    var info = TryGetRuntimeFieldInfo(instance.GetType(), name);
    var value = info?.GetValue(instance);
    return value is T ? (T) value : default;
  }

  public static MethodInfo GetRuntimeMethodInfo(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]

#endif
  this Type type, 
    string name,
    BindingFlags bindingFlags = DefaultLookup)
  {
    return type.GetMethod(name, bindingFlags)
           ?? throw new InvalidOperationException($"Not able to get method '{name}' from {type.FullName}");
  }

  public static MethodInfo? TryGetRuntimeMethodInfo(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
#endif
  this Type type, 
    string name,
    BindingFlags bindingFlags = DefaultLookup)
  {
    return type.GetMethod(name, bindingFlags);
  }

  public static T? GetMethod<T>(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
#endif
    Type type,
    object? instance, string name, params object?[]? parameters)
  {
    if (instance == null)
      return default;
    var info = TryGetRuntimeMethodInfo(type, name);
    var method = info?.Invoke(instance, parameters);
    return method is T ? (T)method : default;
  }

  public static T? GetMethod<T>(
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
#endif
    string sType,
    object? instance, string name, params object?[]? parameters)
  {
    if (instance == null)
      return default;
    var type = Type.GetType(sType);
    if (type == null)
      return default(T);
    var info = TryGetRuntimeMethodInfo(type, name);
    var method = info?.Invoke(instance, parameters);
    return method is T ? (T)method : default;
  }

#if NET5_0_OR_GREATER
  [RequiresUnreferencedCode("This functionality is not compatible with trimming. Use other overloads instead (providing the types directly or by constant string (does not work for private types))", Url = "https://site/trimming-and-method")]
#endif
  public static T? GetMethod<T>(object? instance, string name, params object?[]? parameters)
  {
    if (instance == null)
      return default;
    var info = TryGetRuntimeMethodInfo(instance.GetType(), name);
    var method = info?.Invoke(instance, parameters);
    return method is T ? (T)method : default;
  }
}