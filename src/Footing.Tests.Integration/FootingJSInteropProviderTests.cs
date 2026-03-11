using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using Footing.Client.Services;
using Xunit;

namespace Footing.Tests.Integration;

public class FootingJSInteropProviderTests
{
    private readonly Mock<IJSRuntime> _mockJsRuntime = new();
    private readonly FootingJSInteropProvider _provider;

    public FootingJSInteropProviderTests()
    {
        _provider = new FootingJSInteropProvider(_mockJsRuntime.Object);
    }

    [Fact]
    public async Task InitializePopovers_CallsFootingJsFunction()
    {
        _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "Footing.initializePopovers", It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        await _provider.InitializePopovers();

        _mockJsRuntime.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "Footing.initializePopovers", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_ReturnsTrue_WhenUserAccepts()
    {
        _mockJsRuntime.Setup(x => x.InvokeAsync<bool>("confirm",
                It.Is<object[]>(a => a.Length == 1 && (string)a[0] == "Sure?")))
            .ReturnsAsync(true);

        (await _provider.Confirm("Sure?")).Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_ReturnsFalse_WhenUserDenies()
    {
        _mockJsRuntime.Setup(x => x.InvokeAsync<bool>("confirm", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        (await _provider.Confirm("Delete?")).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAs_CallsSaveAsFile()
    {
        _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "Footing.saveAsFile", It.IsAny<object[]>()))
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await _provider.DownloadAs(stream, "test.xlsx", "application/octet-stream");

        _mockJsRuntime.Verify(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "Footing.saveAsFile",
            It.Is<object[]>(a =>
                (string)a[0] == "test.xlsx" &&
                (string)a[1] == "application/octet-stream" &&
                a[2] is string)), Times.Once);
    }

    [Fact]
    public async Task DownloadAs_SendsBase64EncodedContent()
    {
        string? capturedBase64 = null;
        _mockJsRuntime.Setup(x => x.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "Footing.saveAsFile", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => capturedBase64 = (string)args[2])
            .ReturnsAsync(default(Microsoft.JSInterop.Infrastructure.IJSVoidResult)!);

        var data = new byte[] { 72, 101, 108, 108, 111 };
        using var stream = new MemoryStream(data);
        await _provider.DownloadAs(stream, "f.txt", "text/plain");

        capturedBase64.Should().Be(Convert.ToBase64String(data));
    }
}
