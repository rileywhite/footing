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
        // Close only the context: it owns the page, so closing it tears down the page
        // too, and closing the context is what actually releases the browser resources.
        // Closing Page first and letting a throw skip Context.CloseAsync() would leak it.
        await Context.CloseAsync();
    }
}
