using System;
using System.Reflection;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void SimAssemblyHasNoEngineOrGameDependency()
    {
        Assembly sim = Assembly.Load("Sim");
        foreach (AssemblyName dependency in sim.GetReferencedAssemblies())
        {
            Assert.False(dependency.Name!.Contains("Godot", StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual("RtsGame", dependency.Name);
        }
    }

    [Fact]
    public void SimTargetsDotNetEight()
    {
        Assembly sim = Assembly.Load("Sim");
        var target = sim.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
        Assert.NotNull(target);
        Assert.Equal(".NETCoreApp,Version=v8.0", target.FrameworkName);
    }
}
