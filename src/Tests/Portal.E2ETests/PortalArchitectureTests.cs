using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using XFramework.Portal.Composition;

namespace Portal.E2ETests;

[TestFixture]
public sealed class PortalArchitectureTests
{
    [Test]
    public void SharedProject_RemainsIndependentFromPortalHostAndBackendModules()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharedProject = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal.Shared",
            "XFramework.Portal.Shared.csproj");

        File.Exists(sharedProject).Should().BeTrue();

        var references = ResolveProjectReferences(sharedProject);
        var portalProject = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "XFramework.Portal",
            "XFramework.Portal.csproj");
        var modulesRoot = Path.Combine(repositoryRoot.FullName, "src", "Modules") + Path.DirectorySeparatorChar;

        references.Should().NotContain(portalProject);
        references.Should().NotContain(reference =>
            reference.StartsWith(modulesRoot, StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void FeatureProjects_DoNotReferencePortalHostOrOtherFeatureProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var presentationRoot = Path.Combine(repositoryRoot.FullName, "src", "Presentation");
        var featureProjects = Directory.GetFiles(
            presentationRoot,
            "XFramework.Portal.Features.*.csproj",
            SearchOption.AllDirectories);

        foreach (var featureProject in featureProjects)
        {
            var references = ResolveProjectReferences(featureProject);
            var portalProject = Path.Combine(
                presentationRoot,
                "XFramework.Portal",
                "XFramework.Portal.csproj");

            references.Should().NotContain(portalProject);
            references.Should().NotContain(reference =>
                Path.GetFileNameWithoutExtension(reference)
                    .StartsWith("XFramework.Portal.Features.", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Test]
    public void PortalHost_RegistersSharedProjectAndFeatureAssemblyDiscovery()
    {
        var repositoryRoot = FindRepositoryRoot();
        var portalRoot = Path.Combine(repositoryRoot.FullName, "src", "Presentation", "XFramework.Portal");
        var project = File.ReadAllText(Path.Combine(portalRoot, "XFramework.Portal.csproj"));
        var program = File.ReadAllText(Path.Combine(portalRoot, "Program.cs"));
        var routes = File.ReadAllText(Path.Combine(portalRoot, "Components", "Routes.razor"));

        project.Should().Contain("XFramework.Portal.Shared.csproj");
        program.Should().Contain("AddAdditionalAssemblies(PortalFeatureAssemblies.All)");
        routes.Should().Contain("AdditionalAssemblies=\"PortalFeatureAssemblies.All\"");
    }

    [Test]
    public void RegisteredFeatureAssemblies_ExposeUniqueRoutes()
    {
        var routes = PortalFeatureAssemblies.All
            .SelectMany(assembly => assembly.ExportedTypes)
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>()
                .Select(attribute => attribute.Template))
            .ToList();

        routes.Should().Contain("/storage/files");
        routes.Should().OnlyHaveUniqueItems();
    }

    private static IReadOnlyList<string> ResolveProjectReferences(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Select(reference => Path.GetFullPath(reference, Path.GetDirectoryName(projectPath)!))
            .ToList();

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate the XFramework repository root.");
    }
}
