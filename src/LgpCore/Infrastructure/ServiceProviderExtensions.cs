using System.Collections;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#if !RELEASE //only for Debug builds because of the use of reflection

#pragma warning disable IL2026
#pragma warning disable IL2072
#pragma warning disable IL2075

namespace Infrastructure;

public static class ServiceProviderExtensions
{
  public static string DebugDependencies(this IServiceProvider serviceProvider, bool showLogging = true)
  {
    var sb = new StringBuilder();
    serviceProvider.GetInternalFieldsFromServiceProvider(
      out var serviceDescriptors, 
      out var scopedResolvedServices, 
      out var serviceAccessors,
      out var callSiteCache,
      out var callSiteLocks,
      out var descriptorLookup);
    if (serviceDescriptors != null)
    {
      //show dependencies
      sb.AppendLine($"Registrations: {serviceDescriptors.Count}");
      sb.AppendLine("==============");
      //var serviceCollection = serviceProvider.GetRequiredService<IServiceCollection>();
      //foreach (var sd in serviceCollection)
      var items = serviceDescriptors
        .Select(sd => (sd: sd, friendlyName: sd.ServiceType.FriendlyName()))
        .OrderBy(e => e.friendlyName)
        .ToList();

      foreach (var (sd, friendlyName) in items)
      {
        var type = sd.ServiceType;
        sb.Append(friendlyName);

        if (sd.IsKeyedService)
        {
          sb.Append($" Key:{sd.ServiceKey}");
        }

        var implementationType = sd.IsKeyedService
          ? sd.KeyedImplementationType
          : sd.ImplementationType;

        var implementationInstance = sd.IsKeyedService
          ? sd.KeyedImplementationInstance
          : sd.ImplementationInstance;

        (object? target, System.Reflection.MethodInfo? method)? implementationFactoryInfo = null;
        if (sd.IsKeyedService)
        {
          if (sd.KeyedImplementationFactory != null)
            implementationFactoryInfo = (target: sd.KeyedImplementationFactory?.Target, method: sd.KeyedImplementationFactory?.Method);
        }
        else
        {
          if (sd.ImplementationFactory != null)
            implementationFactoryInfo = (target: sd.ImplementationFactory?.Target, method: sd.ImplementationFactory?.Method);
        }

        if (implementationType != null)
        {
          sb.Append($" ImplType:{implementationType.FriendlyName()}");
        }

        if (implementationInstance != null)
        {
          sb.Append($" ImplInst:{implementationInstance.GetType().FriendlyName()}");
        }
        
        if (implementationFactoryInfo.HasValue)
        {
          sb.Append($" ImplFactory:");
          if (implementationFactoryInfo.Value.target != null) 
            sb.Append($"{implementationFactoryInfo.Value.target?.GetType().FriendlyName()}.");
          sb.Append($"{implementationFactoryInfo.Value.method?.Name}");
        }

        sb.Append($" {sd.Lifetime}");
        sb.AppendLine();
      }

      //Scoped Instances?
      {
        var entries = scopedResolvedServices?
          .ToEnumerable()
          .Select(entry => InstanceInfo.Create(
            GetServiceIdentifierFromServiceCacheKey(entry.Key),
            RuntimeReflectionHelper.GetProp<int?>(entry.Key, "Slot"),
            entry.Value))
          .WhereNotDefault_()
          .Where(info => info.Instance != null)
          .OrderBy(info => info!.ServiceType.FriendlyName())
          .ToList();
        entries?.DebugInstances("Scope Instances", sb);
      }

      //this does not include the resolved instances
      ////Instances
      //{
      //  var entries = serviceAccessors?
      //    .ToEnumerable()
      //    .Select(entry => InstanceInfo.Create(
      //      entry.Key,
      //      null, //no Slot
      //      RuntimeReflectionHelper.GetProp<object?>(RuntimeReflectionHelper.GetProp<object?>(entry.Value, "CallSite"), "Value")))
      //    .WhereNotDefault_()
      //    .Where(info => info.Instance != null)
      //    .OrderBy(info => info!.ServiceType.FriendlyName())
      //    .ToList();

      //  entries?.DebugInstances("Instances", sb);
      //}


      //instances via CallSiteFactory
      if (callSiteCache != null)
      {
        var entries = callSiteCache
          .ToEnumerable()
          .Select(entry => InstanceInfo.Create(
            GetServiceIdentifierFromServiceCacheKey(entry.Key), 
            RuntimeReflectionHelper.GetProp<int?>(entry.Key, "Slot"),
            RuntimeReflectionHelper.GetProp<object?>(entry.Value, "Value")))
          .WhereNotDefault_()
          .Where(info => info.Instance != null)
          .OrderBy(info => info!.ServiceType.FriendlyName())
          .ToList();
        entries.DebugInstances("Instances (including resolved instances)", sb);
      }

      //Configuration
      IConfigurationRoot? configurationRoot = serviceProvider.GetService<IConfiguration>() as IConfigurationRoot;
      if (configurationRoot != null)
      {
        sb.AppendLine();
        sb.AppendLine($"ConfigurationProviders: {configurationRoot.Providers.Count()} Providers");
        sb.AppendLine("==============");
        foreach (IConfigurationProvider provider in configurationRoot.Providers)
        {
          sb.AppendLine($"{provider.GetType().FriendlyName()}");
          //foreach (var childKey in provider.GetChildKeys(Enumerable.Empty<string>(), null))
          //{
          //  sb.AppendLine($"  {childKey}");
          //}
        }
        sb.AppendLine();
        sb.AppendLine($"Configuration:");
        sb.AppendLine("==============");
        sb.AppendLine(configurationRoot.GetDebugView());
      }

      if (showLogging && serviceProvider is ServiceProvider sp)
      {
        sb.AppendLine();
        DebugLoggerProviders(sp, sb);
      }
    }
    else
    {
      sb.AppendLine($"Error: expected Type was ServiceProvider or ServiceProviderEngineScope, but was {serviceProvider.GetType().Name}");
    }

    return sb.ToString();
  }

  internal static void DebugInstances(this List<InstanceInfo>? instanceInfos, string label, StringBuilder sb)
  {
    if (instanceInfos == null)
      return;
    sb.AppendLine();
    sb.AppendLine($"{label}: {instanceInfos.Count}");
    sb.AppendLine("==============");
    foreach (var instanceInfo in instanceInfos)
    {
      sb.Append(instanceInfo.ServiceType.FriendlyName());

      if (instanceInfo.ServiceKey != null)
      {
        sb.Append($" Key:{instanceInfo.ServiceKey}");
      }
      //not really understood what the SLOT is used for :-(
      //if (instanceInfo.Slot != null)
      //{
      //  sb.Append($" Slot:{instanceInfo.Slot}");
      //}

      sb.Append($" -> {instanceInfo.Instance?.GetType()?.FriendlyName()}");

      var interfaceNames = instanceInfo.Instance?.GetType().GetInterfaces()
        .Select(i => i.FriendlyName())
        .ToList() ?? new List<string>();
      if (interfaceNames.Any())
        sb.Append($" -> [{string.Join(", ", interfaceNames)}]");
      sb.AppendLine();
    }

  }

  public static void DebugLoggerProviders(ServiceProvider aServiceProvider, StringBuilder sb)
  {
    var loggerProviders = aServiceProvider.GetServices<ILoggerProvider>();
    sb.AppendLine("Registered LoggerProviders:");
    foreach (var loggerProvider in loggerProviders)
    {
      sb.AppendLine($"  {TypeNameHelper.FriendlyName(loggerProvider)}");
    }

    var loggerFactory = aServiceProvider.GetService<ILoggerFactory>() as LoggerFactory;
    var providerRegistrations = RuntimeReflectionHelper.GetField<ICollection>(typeof(LoggerFactory), loggerFactory, "_providerRegistrations");
    var providerRegistrationType = Type.GetType("Microsoft.Extensions.Logging.LoggerFactory.ProviderRegistration");
    if (providerRegistrations != null && providerRegistrationType != null)
    {
      sb.AppendLine("LoggerFactory.Providers:");
      foreach (var providerRegistration in providerRegistrations)
      {
        var loggerProvider = RuntimeReflectionHelper.GetField<ILoggerProvider>(providerRegistrationType, providerRegistration, "Provider") as ILoggerProvider;
        sb.AppendLine($"  {TypeNameHelper.FriendlyName(loggerProvider)}");
      }
    }
    var filterOptions = RuntimeReflectionHelper.GetField<LoggerFilterOptions>(typeof(LoggerFactory), loggerFactory, "_filterOptions");
    if (filterOptions != null)
    {
      sb.AppendLine($"LoggerFactory.FilterOptions: MinLevel: {filterOptions.MinLevel} CaptureScopes:{filterOptions.CaptureScopes}");

      if (filterOptions.Rules.Any())
      {
        sb.AppendLine("LoggerFactory.FilterOptions.Rules:");
        foreach (var rule in filterOptions.Rules)
        {
          sb.AppendLine($"  ProviderName: '{rule.ProviderName ?? "<all>"}' Category: '{rule.CategoryName}' LogLevel: {rule.LogLevel} CustomFilter: {rule.Filter != null}");
          //this is similar: sb.AppenLine(rule.ToString());
        }
      }
    }
  }


  #region Helper Extensions

  public static IEnumerable<DictionaryEntry> ToEnumerable(this IDictionary dict)
  {
    var enumerator = dict.GetEnumerator();
    while (enumerator.MoveNext())
      yield return enumerator.Entry;
  }

  public static IEnumerable<TSource> WhereNotDefault_<TSource>(this IEnumerable<TSource?> source) where TSource : class
  {
    if (source == null)
      throw new ArgumentNullException(nameof(source));
    return source.Where(value => !EqualityComparer<TSource?>.Default.Equals(value, default)).Select(e => e!);
  }

  public static IEnumerable<TSource> WhereNotDefault_<TSource>(this IEnumerable<Nullable<TSource>> source) where TSource : struct
  {
    if (source == null)
      throw new ArgumentNullException(nameof(source));
    return source
      .Where(value => !EqualityComparer<TSource?>.Default.Equals(value, default))
      .Select(e => e!.Value);
  }

  #endregion

  internal class InstanceInfo
  {
    public InstanceInfo(Type serviceType)
    {
      ServiceType = serviceType;
    }

    public static InstanceInfo? Create(object? serviceIdentifier, int? slot, object? instance)
    {
      if (serviceIdentifier == null)
        return null;
      GetInternalFieldsFromServiceIdentifier(serviceIdentifier, out var serviceType, out var serviceKey);
      return new InstanceInfo(serviceType)
      {
        ServiceKey = serviceKey,
        Slot = slot,
        Instance = instance,
      };
    }

    public Type ServiceType { get; init; }
    public object? ServiceKey { get; set; }
    public int? Slot { get; set; }
    public object? Instance { get; set; }
  }
  private static void GetInternalFieldsFromServiceProvider(this IServiceProvider serviceProvider, 
    out List<ServiceDescriptor>? serviceDescriptors,  
    out IDictionary? scopedResolvedServices,  
    out IDictionary? serviceAccessors,
    out IDictionary? callSiteCache,
    out IDictionary? callSiteLocks,
    out IDictionary? descriptorLookup)
  {
    serviceDescriptors = null;
    scopedResolvedServices = null;
    serviceAccessors = null;
    callSiteCache = null;
    callSiteLocks = null;
    descriptorLookup = null;

    ServiceProvider? rootProvider = null;
    //for ServiceProviderEngineScope
    if (serviceProvider is IServiceScope)
    {
      rootProvider = RuntimeReflectionHelper.TryGetRuntimePropertyInfo(serviceProvider.GetType(), "RootProvider")?.GetValue(serviceProvider) as ServiceProvider;
      scopedResolvedServices = RuntimeReflectionHelper.TryGetRuntimePropertyInfo(serviceProvider.GetType(), "ResolvedServices")?.GetValue(serviceProvider) as IDictionary;
    }
    else
    {
      rootProvider = serviceProvider as ServiceProvider;
    }

    if (rootProvider != null)
    {
      var callSiteFactoryProperty = typeof(ServiceProvider).TryGetRuntimePropertyInfo("CallSiteFactory");
      var callSiteFactory = callSiteFactoryProperty?.GetValue(rootProvider);
      if (callSiteFactory != null)
      {
        var descriptorsField = RuntimeReflectionHelper.TryGetRuntimeFieldInfo(callSiteFactory.GetType(), "_descriptors");
        serviceDescriptors = (descriptorsField?.GetValue(callSiteFactory) as IEnumerable<ServiceDescriptor>)?.ToList();
        callSiteCache = RuntimeReflectionHelper.GetField<IDictionary>(callSiteFactory, "_callSiteCache");
        callSiteLocks = RuntimeReflectionHelper.GetField<IDictionary>(callSiteFactory, "_callSiteLocks");
        descriptorLookup = RuntimeReflectionHelper.GetField<IDictionary>(callSiteFactory, "_descriptorLookup");
      }

      var serviceAccessorsField = rootProvider.GetType().TryGetRuntimeFieldInfo("_serviceAccessors");
      if (serviceAccessorsField != null)
        serviceAccessors = serviceAccessorsField.GetValue(rootProvider) as IDictionary;
    }
  }

  private static PropertyInfo? serviceTypeProperty;
  private static PropertyInfo? serviceKeyProperty;
  private static void GetInternalFieldsFromServiceIdentifier(object serviceIdentifier, out Type serviceType, out object? serviceKey)
  {
    serviceTypeProperty ??= RuntimeReflectionHelper.GetRuntimePropertyInfo(serviceIdentifier.GetType(), "ServiceType");
    serviceKeyProperty ??= RuntimeReflectionHelper.GetRuntimePropertyInfo(serviceIdentifier.GetType(), "ServiceKey");
    serviceType = (Type)serviceTypeProperty.GetValue(serviceIdentifier)!;
    serviceKey = serviceKeyProperty.GetValue(serviceIdentifier);
  }

  private static PropertyInfo? serviceIdentifierProperty;

  private static object? GetServiceIdentifierFromServiceCacheKey(object key)
  {
    serviceIdentifierProperty ??= RuntimeReflectionHelper.GetRuntimePropertyInfo(key.GetType(), "ServiceIdentifier");
    return serviceIdentifierProperty.GetValue(key);
  }
}
#endif