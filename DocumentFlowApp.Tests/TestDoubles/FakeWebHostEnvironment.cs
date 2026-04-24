using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace DocumentFlowApp.Tests.TestDoubles;

internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "DocumentFlowApp.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string WebRootPath { get; set; } = Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
