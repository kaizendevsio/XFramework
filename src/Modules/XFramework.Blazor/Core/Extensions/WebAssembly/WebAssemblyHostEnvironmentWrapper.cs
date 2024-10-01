using Microsoft.Extensions.FileProviders;

namespace XFramework.Blazor.Core.Extensions.WebAssembly;

public class WebAssemblyHostEnvironmentWrapper(IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
    : IHostEnvironment
{
    public string EnvironmentName
    {
        get => webAssemblyHostEnvironment.Environment; 
        set { /* Set operation can be omitted or handled if necessary */ }
    }

    public string ApplicationName
    {
        get => "Blazor WebAssembly App"; // Provide a static or dynamic value if needed
        set { /* Set operation can be omitted or handled if necessary */ }
    }

    public string ContentRootPath
    {
        get => string.Empty; // WebAssembly doesn't have a content root path like server apps.
        set { /* Set operation can be omitted or handled if necessary */ }
    }

    public IFileProvider ContentRootFileProvider { get; set; }
}


public static class WebAssemblyHostEnvironmentExtensions
{
    public static IHostEnvironment ToHostEnvironment(this IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
    {
        return new WebAssemblyHostEnvironmentWrapper(webAssemblyHostEnvironment);
    }
}