using Microsoft.Playwright;

namespace Footing.Tests.E2E;

/// <summary>
/// Owns one browser context and its page, disposing both deterministically.
/// </summary>
public sealed class PageSession : IAsyncDisposable
{
    public IBrowserContext Context { get; }
    public IPage Page { get; }

    public PageSession(IBrowserContext context, IPage page)
    {
        Context = context;
        Page = page;
    }

    public async ValueTask DisposeAsync()
    {
        await Page.CloseAsync();
        await Context.CloseAsync();
    }
}
