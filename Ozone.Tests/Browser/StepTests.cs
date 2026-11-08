namespace Ozone.Tests.Browser;

/// <summary>
/// User actions, collection filters and element helpers against real HTML in headless Chromium.
/// </summary>
[TestClass]
[TestCategory("Browser")]
public sealed class StepTests
{
    [TestMethod]
    public async Task Click_RunsElementClickHandler()
    {
        await using var context = await TestContexts.WithHtml(
            """<button id="b" onclick="document.title='clicked'">Go</button>""");

        await Flow.Click("#b")(context);

        Assert.AreEqual("clicked", await context.Title());
    }

    [TestMethod]
    public async Task DblClick_RunsDoubleClickHandler()
    {
        await using var context = await TestContexts.WithHtml(
            """<div id="d" ondblclick="document.title='double'">x</div>""");

        await (context | Flow.DblClick("#d"));

        Assert.AreEqual("double", await context.Title());
    }

    [TestMethod]
    public async Task SetText_FillsInputAndTextarea()
    {
        await using var context = await TestContexts.WithHtml("""<input id="i"><textarea id="t"></textarea>""");

        var input = await Flow.SetText("#i", "hello")(context);
        var area = await Flow.SetText("#t", "multi\nline")(context);

        Assert.AreEqual("hello", await input.TextAsync());
        Assert.AreEqual("multi\nline", await area.TextAsync());
    }

    [TestMethod]
    public async Task SetText_SelectsOptionInSelect()
    {
        await using var context = await TestContexts.WithHtml(
            """<select id="s"><option value="a">A</option><option value="b">B</option></select>""");

        var select = await Flow.SetText("#s", "b")(context);

        Assert.AreEqual("b", await select.TextAsync());
    }

    [TestMethod]
    public async Task PressEnter_SendsEnterKey()
    {
        await using var context = await TestContexts.WithHtml(
            """<input id="i" onkeydown="if (event.key === 'Enter') document.title = 'entered'">""");

        await (context | (new AsyncStep(Flow.Find("#i")) | Flow.PressEnter));

        Assert.AreEqual("entered", await context.Title());
    }

    [TestMethod]
    public async Task Script_EvaluatesJavaScriptInPage()
    {
        await using var context = await TestContexts.WithHtml("<p>x</p>");

        await Flow.Script("t => document.title = t", "from script")(context);

        Assert.AreEqual("from script", await context.Title());
    }

    [TestMethod]
    public async Task FollowLink_WhenTitleMatches_Succeeds()
    {
        await using var context = await TestContexts.WithHtml(
            """<a id="l" href="#" onclick="document.title='Next page'">next</a>""");

        await Flow.FollowLink("#l", "Next page")(context);
    }

    [TestMethod]
    public async Task FollowLink_WhenTitleDiffers_ThrowsOzoneException()
    {
        await using var context = await TestContexts.WithHtml(
            """<a id="l" href="#" onclick="document.title='Other'">next</a>""");

        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FollowLink("#l", "Next page")(context));
    }

    [TestMethod]
    public async Task AssertAttributeValue_ComparesAttribute()
    {
        await using var context = await TestContexts.WithHtml("""<a id="l" href="/target">x</a>""");
        var link = await Flow.Find("#l")(context);

        await Flow.AssertAttributeValue("href", "/target")(link);
        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.AssertAttributeValue("href", "/other")(link));
    }

    // --- Collection filters ----------------------------------------------

    const string ListHtml = """<ul><li>apple pie</li><li>banana</li><li>apple tart</li></ul>""";

    [TestMethod]
    public async Task FirstAndLastContainingText_PickMatchingItems()
    {
        await using var context = await TestContexts.WithHtml(ListHtml);
        var items = await Flow.FindAll("li")(context);

        Assert.AreEqual("apple pie", await (await Flow.FirstContainingText("apple")(items)).TextAsync());
        Assert.AreEqual("apple tart", await (await Flow.LastContainingText("apple")(items)).TextAsync());
    }

    [TestMethod]
    public async Task FirstAndLastContainingContextItem_UseTextFromItems()
    {
        await using var context = await TestContexts.WithHtml(ListHtml);
        context.Items["fruit"] = "apple";
        var items = await Flow.FindAll("li")(context);

        Assert.AreEqual("apple pie", await (await Flow.FirstContainingContextItem("fruit")(items)).TextAsync());
        Assert.AreEqual("apple tart", await (await Flow.LastContainingContextItem("fruit")(items)).TextAsync());
    }

    [TestMethod]
    public async Task ContainingTextFilters_WhenNoItemMatches_ThrowOzoneException()
    {
        await using var context = await TestContexts.WithHtml(ListHtml);
        var items = await Flow.FindAll("li")(context);

        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FirstContainingText("cherry")(items));
        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.LastContainingText("cherry")(items));
    }

    // --- Element text helpers --------------------------------------------

    [TestMethod]
    public async Task TextExtensions_ReadInputValueOrTextContent()
    {
        await using var context = await TestContexts.WithHtml(
            """<input id="i" value="typed" data-x="attr"><div id="d">content here</div>""");

        var input = context.Page.Locator("#i");
        var div = context.Page.Locator("#d");

        Assert.AreEqual("typed", await input.Text());
        Assert.AreEqual("content here", await div.Text());
        Assert.IsTrue(await div.TextContains("here"));
        Assert.IsFalse(await div.TextContains("absent"));
    }

    // --- Chaining ---------------------------------------------------------

    [TestMethod]
    public async Task AsyncStepChain_RunsMultiStepFlow()
    {
        await using var context = await TestContexts.WithHtml("""
            <input id="q">
            <button id="go" onclick="document.title = 'searched ' + document.getElementById('q').value">Go</button>
            """);

        await (context | (new AsyncStep(Flow.SetText("#q", "ozone")) | Flow.Click("#go")));

        Assert.AreEqual("searched ozone", await context.Title());
    }

    // --- Step 5 fixes -----------------------------------------------------

    [TestMethod]
    public async Task DblClick_SendsOnlyTheClicksOfOneDoubleClick()
    {
        await using var context = await TestContexts.WithHtml(
            """<div id="d" onclick="window.clicks = (window.clicks || 0) + 1" ondblclick="window.dbl = (window.dbl || 0) + 1">x</div>""");

        await (context | Flow.DblClick("#d"));

        Assert.AreEqual(2, await context.Page.EvaluateAsync<int>("() => window.clicks"));
        Assert.AreEqual(1, await context.Page.EvaluateAsync<int>("() => window.dbl"));
    }

    [TestMethod]
    public async Task SetText_SelectsOptionByLabel()
    {
        await using var context = await TestContexts.WithHtml(
            """<select id="s"><option value="a">Apple</option><option value="b">Banana</option></select>""");

        var select = await Flow.SetText("#s", "Banana")(context);

        Assert.AreEqual("b", await select.TextAsync());
    }

    [TestMethod]
    public async Task Script_WhenScriptThrows_WrapsErrorInOzoneException()
    {
        await using var context = await TestContexts.WithHtml("<p>x</p>");

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.Script("() => { throw new Error('boom'); }")(context));

        Assert.IsInstanceOfType<Microsoft.Playwright.PlaywrightException>(x.InnerException);
    }

    [TestMethod]
    public async Task AssertAttributeValue_WhenElementIsGone_WrapsTimeoutInOzoneException()
    {
        await using var context = await TestContexts.WithHtml("""<a id="l" href="/x">x</a>""");
        var link = await Flow.Find("#l")(context);
        await context.Page.EvaluateAsync("() => document.getElementById('l').remove()");
        context.Page.SetDefaultTimeout(300);

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.AssertAttributeValue("href", "/x")(link));

        Assert.IsInstanceOfType<TimeoutException>(x.InnerException);
    }

    // --- FollowLink -------------------------------------------------------

    /// <summary>Serves http://ozone.test/{name} as a page titled after the name; slow-* pages load slowly.</summary>
    static Task ServeTestSite(Context context) =>
        context.Page.Context.RouteAsync("http://ozone.test/**", async route =>
        {
            string name = route.Request.Url.Split('/')[^1];

            if(name == "slow.png")
            {
                await Task.Delay(500);
                await route.FulfillAsync(new() { ContentType = "image/png", Body = "" });
                return;
            }

            // Pages named slow-* hold back their load event with a slow image.
            string image = name.StartsWith("slow") ? "<img src='http://ozone.test/slow.png'>" : "";

            await route.FulfillAsync(new()
            {
                ContentType = "text/html",
                Body = $"<title>{name}</title><p id='p'>arrived at {name}</p>{image}",
            });
        });

    [TestMethod]
    public async Task FollowLink_NavigatesAndReturnsContextOnTargetPage()
    {
        await using var context = await TestContexts.WithHtml("""<a id="l" href="http://ozone.test/next">next</a>""");
        await ServeTestSite(context);

        var target = await Flow.FollowLink("#l", "next")(context);

        Assert.IsNull(target.Element);
        Assert.AreEqual("arrived at next", await (await Flow.Find("#p")(target)).TextAsync());
    }

    [TestMethod]
    public async Task FollowLink_WhenLinkOpensNewWindow_ContinuesInIt()
    {
        await using var context = await TestContexts.WithHtml("""<title>start</title><a id="l" href="http://ozone.test/popup" target="_blank">next</a>""");
        await ServeTestSite(context);

        var target = await Flow.FollowLink("#l", "popup")(context);

        Assert.AreNotSame(context.Page, target.Page);
        Assert.AreEqual("popup", await target.Title());
        Assert.AreEqual("start", await context.Title());
    }

    [TestMethod]
    public async Task FollowLink_WaitsForTitleToChange()
    {
        await using var context = await TestContexts.WithHtml(
            """<a id="l" href="#" onclick="setTimeout(() => document.title = 'Later', 300)">next</a>""");

        await Flow.FollowLink("#l", "Later")(context);
    }

    [TestMethod]
    public async Task FollowLink_WhenTitleDiffers_ReportsActualTitle()
    {
        await using var context = await TestContexts.WithHtml(
            """<a id="l" href="#" onclick="document.title='Other'">next</a>""");

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FollowLink("#l", "Next page")(context));

        StringAssert.Contains(x.Message, "expected title 'Next page', actual 'Other'");
    }

    // --- Frames -----------------------------------------------------------

    const string FrameHtml = """
        <p id="where">page</p>
        <iframe id="outer" srcdoc="<p id='where'>outer</p><iframe id='inner' srcdoc=&quot;<p id='where'>inner</p>&quot;></iframe>"></iframe>
        """;

    static async Task<string?> WhereAmI(Context context) => await (await Flow.Find("#where")(context)).TextAsync();

    [TestMethod]
    public async Task EnterFrame_FindsInsideFrameAndExitFrameReturnsToPage()
    {
        await using var context = await TestContexts.WithHtml(FrameHtml);

        var outer = await Flow.EnterFrame("#outer")(context);
        var back = await Flow.ExitFrame(outer);

        Assert.AreEqual("outer", await WhereAmI(outer));
        Assert.IsNull(back.Frame);
        Assert.AreEqual("page", await WhereAmI(back));
    }

    [TestMethod]
    public async Task EnterFrame_SupportsNestedFrames()
    {
        await using var context = await TestContexts.WithHtml(FrameHtml);

        var outer = await Flow.EnterFrame("#outer")(context);
        var inner = await Flow.EnterFrame("#inner")(outer);
        var backToOuter = await Flow.ExitFrame(inner);

        Assert.AreEqual("inner", await WhereAmI(inner));
        Assert.AreEqual("outer", await WhereAmI(backToOuter));
    }

    [TestMethod]
    public async Task EnterFrame_OnNonFrameElement_ThrowsOzoneException()
    {
        await using var context = await TestContexts.WithHtml(FrameHtml);

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.EnterFrame("#where")(context));

        StringAssert.Contains(x.Message, "is not a frame");
    }

    // --- Review fixes -------------------------------------------------------

    [TestMethod]
    public async Task FollowLink_WhenTargetHasSameTitle_WaitsForPageToLoad()
    {
        await using var context = await TestContexts.WithHtml(
            """<title>slow-same</title><a id="l" href="http://ozone.test/slow-same">next</a>""");
        await ServeTestSite(context);

        var target = await Flow.FollowLink("#l", "slow-same")(context);

        Assert.AreEqual("http://ozone.test/slow-same", target.Page.Url);
        Assert.AreEqual("complete", await target.Page.EvaluateAsync<string>("() => document.readyState"));
    }

    [TestMethod]
    [DataRow("""<a id="l" href="http://ozone.test/w" target="_BLANK">next</a>""", "#l", DisplayName = "upper-case _BLANK")]
    [DataRow("""<a id="l" href="http://ozone.test/w" target="help">next</a>""", "#l", DisplayName = "named target")]
    [DataRow("""<base target="_blank"><a id="l" href="http://ozone.test/w">next</a>""", "#l", DisplayName = "base target")]
    [DataRow("""<a href="http://ozone.test/w" target="_blank"><span id="s">next</span></a>""", "#s", DisplayName = "child of link")]
    public async Task FollowLink_DetectsLinksOpeningNewWindow(string html, string selector)
    {
        await using var context = await TestContexts.WithHtml("<title>start</title>" + html);
        await ServeTestSite(context);

        var target = await Flow.FollowLink(selector, "w")(context);

        Assert.AreNotSame(context.Page, target.Page);
        Assert.AreEqual("start", await context.Title());
    }

    [TestMethod]
    public async Task FollowLink_FromInsideFrame_ContinuesAtTopLevel()
    {
        await using var context = await TestContexts.WithHtml(
            """<iframe id="f" srcdoc="<a id='l' href='http://ozone.test/top' target='_top'>top</a>"></iframe>""");
        await ServeTestSite(context);
        var inFrame = await Flow.EnterFrame("#f")(context);

        var target = await Flow.FollowLink("#l", "top")(inFrame);

        Assert.IsNull(target.Frame);
        Assert.AreEqual("arrived at top", await (await Flow.Find("#p")(target)).TextAsync());
    }

    [TestMethod]
    public async Task AssertAttributeValue_WhenValueDiffers_PrefixesStepName()
    {
        await using var context = await TestContexts.WithHtml("""<a id="l" href="/target">x</a>""");
        var link = await Flow.Find("#l")(context);

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.AssertAttributeValue("href", "/other")(link));

        Assert.AreEqual("AssertAttributeValue: expected href='/other', actual href='/target'", x.Message);
    }
}
