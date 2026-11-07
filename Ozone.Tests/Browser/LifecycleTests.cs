using Microsoft.Playwright;

namespace Ozone.Tests.Browser;

/// <summary>
/// Creating and disposing contexts.
/// </summary>
[TestClass]
[TestCategory("Browser")]
public sealed class LifecycleTests
{
    [TestMethod]
    public async Task CreateContext_NavigatesToStartUrl()
    {
        await using var context = await Flow.CreateContext(
            BrowserBrand.Chromium, new Uri("data:text/html,<title>Start</title>"));

        Assert.AreEqual("Start", await context.Title());
        Assert.IsTrue(context.Browser.IsConnected);
        Assert.IsFalse(context.HasElement);
        Assert.IsEmpty(context.Items);
    }

    [TestMethod]
    public async Task CreateContext_WhenNavigationFails_Throws()
    {
        // Port 1 is never listening, so navigation fails after the browser has started;
        // CreateContext must clean up and rethrow.
        await Assert.ThrowsAsync<PlaywrightException>(() =>
            Flow.CreateContext(BrowserBrand.Chromium, new Uri("http://127.0.0.1:1/")));
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesBrowserAndIsIdempotent()
    {
        var context = await TestContexts.WithHtml("<p>x</p>");

        await context.DisposeAsync();
        await context.DisposeAsync();

        Assert.IsFalse(context.Browser.IsConnected);
        Assert.IsTrue(context.Page.IsClosed);
    }

    [TestMethod]
    public async Task DerivedContexts_ShareItemsWithOrigin()
    {
        await using var context = await TestContexts.WithHtml("<p id='p'>x</p>");

        var derived = await Flow.Find("#p")(context);
        derived.Items["k"] = "v";

        Assert.AreEqual("v", context.Items["k"]);
        Assert.AreSame(context.Page, derived.Page);
    }
}
