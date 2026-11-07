using Microsoft.Playwright;

namespace Ozone.Tests;

/// <summary>
/// Helpers for building <see cref="Context"/> instances in tests.
/// </summary>
[TestClass]
public sealed class TestContexts
{
    /// <summary>
    /// Find timeout for browser tests. Inline HTML is ready at once, so "not found" cases
    /// need not wait out the 10 s default; elements added later in tests appear well within it.
    /// </summary>
    public static readonly TimeSpan FindTimeout = TimeSpan.FromSeconds(1);

    static readonly SemaphoreSlim BrowserLock = new(1, 1);
    static IPlaywright? _playwright;
    static IBrowser? _browser;

    /// <summary>
    /// A context with no browser behind it, for testing logic that never touches Playwright
    /// (precondition checks, control flow, CreateProblem, Items). Do not dispose it.
    /// </summary>
    internal static Context Detached(IReadOnlyList<ILocator>? collection = null) =>
        new(null!, null!, null!, null, null, collection, new());

    /// <summary>
    /// Opens a page with the given HTML in its own browser context, on a headless Chromium
    /// shared by all tests (launching one browser per test dominates the suite's run time).
    /// The context uses a <see cref="FindTimeout"/> find timeout. The caller must dispose it;
    /// that closes only its browser context, not the shared browser.
    /// Tests of browser launch and shutdown should call <see cref="Flow.CreateContext"/> instead.
    /// </summary>
    internal static async Task<Context> WithHtml(string html)
    {
        var browser = await SharedBrowser();
        var browserContext = await browser.NewContextAsync();

        try
        {
            var page = await browserContext.NewPageAsync();
            await page.SetContentAsync(html);
            return new Context(_playwright!, browser, page, null, null, null, new(), FindTimeout, ownsBrowser: false);
        }
        catch
        {
            await browserContext.CloseAsync();
            throw;
        }
    }

    static async Task<IBrowser> SharedBrowser()
    {
        await BrowserLock.WaitAsync();

        try
        {
            if(_browser == null)
            {
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
            }

            return _browser;
        }
        finally
        {
            BrowserLock.Release();
        }
    }

    [AssemblyCleanup]
    public static async Task CloseSharedBrowser()
    {
        if(_browser != null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}
