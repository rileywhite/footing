using Bunit;
using FluentAssertions;
using Footing.Client.Components;
using Xunit;

namespace Footing.Tests.Functional.Components;

public class LoadingStateTests : BunitContext
{
    [Fact]
    public void RendersLoadingText()
    {
        var cut = Render<LoadingState>();
        cut.Markup.Should().Contain("Loading...");
    }
}
