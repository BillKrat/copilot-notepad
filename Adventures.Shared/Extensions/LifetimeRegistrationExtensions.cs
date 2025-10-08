using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Adventures.Shared.Interfaces;

namespace Adventures.Shared.Extensions;

/// <summary>
/// Scans assemblies for implementations of the marker interfaces
///   ILifetimeTransient, ILifetimeScoped, ILifetimeSingleton
/// and registers them automatically with the matching lifetime.
/// 
/// Updated selection logic:
///  - Prefer an interface named exactly "I" + class name (e.g. WeatherDal -> IWeatherDal).
///  - Otherwise prefer interfaces that are newly introduced on the class (not inherited from its base type).
///  - If multiple remain, pick the first alphabetically for determinism.
///  - If none (besides marker interfaces), self-register.
/// </summary>
public static class LifetimeRegistrationExtensions
{
    private static readonly Type TransientMarker = typeof(ILifetimeTransient);
    private static readonly Type ScopedMarker = typeof(ILifetimeScoped);
    private static readonly Type SingletonMarker = typeof(ILifetimeSingleton);
    private static readonly Type[] Markers = new[] { TransientMarker, ScopedMarker, SingletonMarker };

    public static IServiceCollection AddLifetimeRegistrations(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = ResolveCandidateAssemblies();
        }

        foreach (var assembly in assemblies.Distinct())
        {
            RegisterByMarker(services, assembly, ServiceLifetime.Transient, TransientMarker);
            RegisterByMarker(services, assembly, ServiceLifetime.Scoped, ScopedMarker);
            RegisterByMarker(services, assembly, ServiceLifetime.Singleton, SingletonMarker);
        }

        return services;
    }

    private static void RegisterByMarker(IServiceCollection services, Assembly assembly, ServiceLifetime lifetime, Type marker)
    {
        IEnumerable<Type> candidates;
        try
        {
            candidates = assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition && marker.IsAssignableFrom(t))
                .ToArray();
        }
        catch (ReflectionTypeLoadException ex)
        {
            candidates = ex.Types
                .Where(t => t is not null && t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition && marker.IsAssignableFrom(t))
                .Cast<Type>()
                .ToArray();
        }

        foreach (var impl in candidates)
        {
            var allServiceInterfaces = impl.GetInterfaces()
                .Where(i => !Markers.Contains(i))
                .ToList();

            if (allServiceInterfaces.Count == 0)
            {
                TryAdd(services, impl, impl, lifetime);
                continue;
            }

            // Exclude interfaces implemented by the base type (inheritance chain) to avoid picking generic base contracts like IDal
            var baseType = impl.BaseType;
            var baseInterfaces = baseType != null ? new HashSet<Type>(baseType.GetInterfaces()) : new HashSet<Type>();
            var directInterfaces = allServiceInterfaces.Where(i => !baseInterfaces.Contains(i)).ToList();

            var candidateSet = directInterfaces.Count > 0 ? directInterfaces : allServiceInterfaces;

            // Heuristic: prefer interface named I + ClassName
            var expectedName = "I" + impl.Name;
            var matchingByName = candidateSet.FirstOrDefault(i => i.Name.Equals(expectedName, StringComparison.Ordinal));

            var primary = matchingByName ?? candidateSet.OrderBy(i => i.Name).First();

            TryAdd(services, primary, impl, lifetime);
        }
    }

    private static void TryAdd(IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        // Avoid duplicate exact registrations
        if (services.Any(d => d.ServiceType == serviceType && d.ImplementationType == implementationType))
            return;

        var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime);
        services.Add(descriptor);
    }

    private static Assembly[] ResolveCandidateAssemblies()
    {
        var result = new List<Assembly>();
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToList();
        result.AddRange(loaded);

        var entry = Assembly.GetEntryAssembly();
        if (entry != null)
        {
            foreach (var refName in entry.GetReferencedAssemblies())
            {
                if (loaded.All(a => a.GetName().Name != refName.Name))
                {
                    try
                    {
                        var loadedRef = Assembly.Load(refName);
                        result.Add(loadedRef);
                    }
                    catch
                    {
                        // ignore load failures
                    }
                }
            }
        }

        // Provide a light filter to app specific assemblies to reduce overhead
        return result
            .Where(a =>
                (a.FullName?.StartsWith("Adventures.") ?? false) ||
                (a.FullName?.StartsWith("NotebookAI.") ?? false))
            .Distinct()
            .ToArray();
    }
}
