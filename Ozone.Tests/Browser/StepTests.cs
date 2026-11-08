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

    /// <summary>
    /// Serves http://ozone.test/{name} (query ignored) as a page titled after the name.
    /// slow-* pages hold back their load event with a slow image, slowdoc* pages are sent
    /// late, and hang* pages don't finish loading before the test gives up.
    /// </summary>
    static Task ServeTestSite(Context context) =>
        context.Page.Context.RouteAsync("http://ozone.test/**", async route =>
        {
            string name = new Uri(route.Request.Url).AbsolutePath.TrimStart('/');

            try
            {
                if(name is "slow.png" or "hang.png")
                {
                    await Task.Delay(name == "slow.png" ? 500 : 5000);
                    await route.FulfillAsync(new() { ContentType = "image/png", Body = "" });
                    return;
                }

                if(name.StartsWith("slowdoc"))
                {
                    await Task.Delay(500);
                }
            }
            catch(Microsoft.Playwright.PlaywrightException)
            {
                return; // The test finished and closed the page first.
            }

            string image = name.StartsWith("slow-") ? "<img src='http://ozone.test/slow.png'>"
                : name.StartsWith("hang") ? "<img src='http://ozone.test/hang.png'>"
                : "";

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

        StringAssert.Contains(x.Message, "expected title ***, actual ***");
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

        Assert.AreEqual("AssertAttributeValue: expected href=***, actual href=***", x.Message);
    }

    // --- FollowLink targets -------------------------------------------------

    [TestMethod]
    public async Task FollowLink_WithTargetNamingAnIframe_ContinuesInThatIframe()
    {
        await using var context = await TestContexts.WithHtml(
            """<title>start</title><iframe name="content"></iframe><a id="l" href="http://ozone.test/inframe" target="content">next</a>""");
        await ServeTestSite(context);

        var target = await Flow.FollowLink("#l", "inframe")(context);

        Assert.AreSame(context.Page, target.Page);
        Assert.IsNotNull(target.Frame);
        Assert.AreEqual("arrived at inframe", await (await Flow.Find("#p")(target)).TextAsync());
        Assert.AreEqual("start", await context.Title());
    }

    [TestMethod]
    public async Task FollowLink_WithTargetNamingAnOpenWindow_ReusesThatWindow()
    {
        await using var context = await TestContexts.WithHtml("""
            <title>start</title>
            <a id="first" href="http://ozone.test/first" target="help">first</a>
            <a id="second" href="http://ozone.test/second" target="help">second</a>
            """);
        await ServeTestSite(context);

        var first = await Flow.FollowLink("#first", "first")(context);
        var second = await Flow.FollowLink("#second", "second")(context);

        Assert.AreNotSame(context.Page, first.Page);
        Assert.AreSame(first.Page, second.Page);
        Assert.IsNull(second.Frame);
    }

    [TestMethod]
    public async Task FollowLink_WithoutTargetInsideFrame_ContinuesInThatFrame()
    {
        await using var context = await TestContexts.WithHtml(
            """<title>start</title><iframe id="f" srcdoc="<a id='l' href='http://ozone.test/framed'>next</a>"></iframe>""");
        await ServeTestSite(context);
        var inFrame = await Flow.EnterFrame("#f")(context);

        var target = await Flow.FollowLink("#l", "framed")(inFrame);

        Assert.AreSame(inFrame.Frame, target.Frame);
        Assert.AreEqual("arrived at framed", await (await Flow.Find("#p")(target)).TextAsync());
        Assert.AreEqual("start", await context.Title());
    }

    [TestMethod]
    public async Task FollowLink_WithParentTargetFromNestedFrame_ContinuesInParentFrame()
    {
        await using var context = await TestContexts.WithHtml("""
            <title>start</title>
            <iframe id="outer" srcdoc="<iframe id='inner' srcdoc=&quot;<a id='l' href='http://ozone.test/parent' target='_parent'>up</a>&quot;></iframe>"></iframe>
            """);
        await ServeTestSite(context);
        var outer = await Flow.EnterFrame("#outer")(context);
        var inner = await Flow.EnterFrame("#inner")(outer);

        var target = await Flow.FollowLink("#l", "parent")(inner);

        Assert.AreSame(outer.Frame, target.Frame);
        Assert.AreEqual("arrived at parent", await (await Flow.Find("#p")(target)).TextAsync());
        Assert.AreEqual("start", await context.Title());
    }

    [TestMethod]
    public async Task FollowLink_OnButtonWithBaseTargetBlank_StaysInPage()
    {
        await using var context = await TestContexts.WithHtml(
            """<base target="_blank"><button id="b" type="button" onclick="document.title='Done'">go</button>""");

        var target = await Flow.FollowLink("#b", "Done")(context);

        Assert.AreSame(context.Page, target.Page);
    }

    [TestMethod]
    public async Task FollowLink_OnSubmitButtonOfFormWithTargetBlank_ContinuesInNewWindow()
    {
        await using var context = await TestContexts.WithHtml(
            """<title>start</title><form action="http://ozone.test/formed" target="_blank"><button id="b">go</button></form>""");
        await ServeTestSite(context);

        var target = await Flow.FollowLink("#b", "formed")(context);

        Assert.AreNotSame(context.Page, target.Page);
        Assert.AreEqual("start", await context.Title());
    }

    [TestMethod]
    public async Task FollowLink_WhenSameTitledPageResponseIsSlow_WaitsForNewDocument()
    {
        await using var context = await TestContexts.WithHtml(
            """<title>slowdoc</title><a id="l" href="http://ozone.test/slowdoc">next</a>""");
        await ServeTestSite(context);

        var target = await Flow.FollowLink("#l", "slowdoc")(context);

        Assert.AreEqual("http://ozone.test/slowdoc", target.Page.Url);
        Assert.AreEqual("arrived at slowdoc", await (await Flow.Find("#p")(target)).TextAsync());
    }

    [TestMethod]
    [DataRow("", DisplayName = "same window")]
    [DataRow(" target=\"_blank\"", DisplayName = "new window")]
    public async Task FollowLink_OnSvgLink_Navigates(string targetAttribute)
    {
        await using var context = await TestContexts.WithHtml(
            $"""<title>start</title><svg width="200" height="50"><a id="l" href="http://ozone.test/svg"{targetAttribute}><text x="5" y="30">go</text></a></svg>""");
        await ServeTestSite(context);

        var target = await Flow.FollowLink("#l", "svg")(context);

        Assert.AreEqual("arrived at svg", await (await Flow.Find("#p")(target)).TextAsync());
        Assert.AreEqual(targetAttribute.Length == 0, ReferenceEquals(context.Page, target.Page));
    }

    [TestMethod]
    public async Task FollowLink_WhenNoWindowOpens_FailsWithStepPrefix()
    {
        await using var context = await TestContexts.WithHtml(
            """<a id="l" href="http://ozone.test/never" target="_blank" onclick="event.preventDefault()">next</a>""");

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FollowLink("#l", "never")(context));

        StringAssert.StartsWith(x.Message, "FollowLink: link with target '_blank' did not open a new window");
        Assert.IsInstanceOfType<TimeoutException>(x.InnerException);
    }

    [TestMethod]
    public async Task FollowLink_WhenPageNeverLoads_FailsWithStepPrefixWithinFindTimeout()
    {
        await using var context = await TestContexts.WithHtml("""<a id="l" href="http://ozone.test/hang">next</a>""");
        await ServeTestSite(context);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FollowLink("#l", "hang")(context));

        StringAssert.StartsWith(x.Message, "FollowLink: target page did not finish loading");
        Assert.IsLessThan(context.FindTimeout + TimeSpan.FromMilliseconds(700), watch.Elapsed);
    }
}
