using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Aom.App.Tests;

public static class TestAssemblyBootstrapper
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += ResolveAomCore;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAomCoreFromAppDomain;
    }

    private static Assembly? ResolveAomCore(AssemblyLoadContext loadContext, AssemblyName assemblyName)
    {
        if (!string.Equals(assemblyName.Name, "Aom.Core", StringComparison.Ordinal))
        {
            return null;
        }

        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Aom.Core.dll");
        return File.Exists(assemblyPath)
            ? loadContext.LoadFromAssemblyPath(assemblyPath)
            : null;
    }

    private static Assembly? ResolveAomCoreFromAppDomain(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);
        if (!string.Equals(assemblyName.Name, "Aom.Core", StringComparison.Ordinal))
        {
            return null;
        }

        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Aom.Core.dll");
        return File.Exists(assemblyPath)
            ? Assembly.LoadFrom(assemblyPath)
            : null;
    }
}