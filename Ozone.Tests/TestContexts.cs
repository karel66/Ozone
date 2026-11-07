using Microsoft.Playwright;

namespace Ozone.Tests;

/// <summary>
/// Helpers for building <see cref="Context"/> instances in tests.
/// </summary>
internal static class TestContexts
{
    /// <summary>
    /// A context with no browser behind it, for testing logic that never touches Playwright
    /// (precondition checks, control flow, CreateProblem, Items). Do not dispose it.
    /// </summary>
    public static Context Detached(IReadOnlyList<ILocator>? collection = null) =>
        new(null!, null!, null!, null, null, collection, new());

    /// <summary>
    /// Launches headless Chromium and loads the given HTML into the page.
    /// The caller owns the context and must dispose it.
    /// </summary>
    public static async Task<Context> WithHtml(string html)
    {
        var context = await Flow.CreateContext(BrowserBrand.Chromium, new Uri("about:blank"));
        await context.Page.SetContentAsync(html);
        return context;
    }
}
