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

        Assert.AreEqual("hello", await input.Value());
        Assert.AreEqual("multi\nline", await area.Value());
    }

    [TestMethod]
    public async Task SetText_SelectsOptionInSelect()
    {
        await using var context = await TestContexts.WithHtml(
            """<select id="s"><option value="a">A</option><option value="b">B</option></select>""");

        var select = await Flow.SetText("#s", "b")(context);

        Assert.AreEqual("b", await select.Value());
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

        Assert.AreEqual("apple pie", await (await Flow.FirstContainingText("apple")(items)).Text);
        Assert.AreEqual("apple tart", await (await Flow.LastContainingText("apple")(items)).Text);
    }

    [TestMethod]
    public async Task FirstAndLastContainingContextItem_UseTextFromItems()
    {
        await using var context = await TestContexts.WithHtml(ListHtml);
        context.Items["fruit"] = "apple";
        var items = await Flow.FindAll("li")(context);

        Assert.AreEqual("apple pie", await (await Flow.FirstContainingContextItem("fruit")(items)).Text);
        Assert.AreEqual("apple tart", await (await Flow.LastContainingContextItem("fruit")(items)).Text);
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
        Assert.AreEqual("attr", await input.GetAttribute("data-x"));
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
}
