namespace Ozone.Tests.Browser;

/// <summary>
/// Absolute and relative finders against real HTML in headless Chromium.
/// </summary>
[TestClass]
[TestCategory("Browser")]
public sealed class FinderTests
{
    const string Html = """
        <div id="list">
          <ul>
            <li class="item">alpha</li>
            <li class="item">beta</li>
            <li class="item">gamma beta</li>
          </ul>
        </div>
        <div id="other"><span class="item">outside</span></div>
        <p>Plain paragraph text</p>
        """;

    static async Task<string?> TextOf(Task<Context> step) => await (await step).Text;

    [TestMethod]
    public async Task Find_ByDefault_ReturnsFirstMatch()
    {
        await using var context = await TestContexts.WithHtml(Html);

        Assert.AreEqual("alpha", await TextOf(Flow.Find("li.item")(context)));
    }

    [TestMethod]
    public async Task Find_WithIndex_ReturnsThatMatch()
    {
        await using var context = await TestContexts.WithHtml(Html);

        Assert.AreEqual("beta", await TextOf(Flow.Find("li.item", 1)(context)));
    }

    [TestMethod]
    public async Task FindLast_ReturnsLastMatch()
    {
        await using var context = await TestContexts.WithHtml(Html);

        Assert.AreEqual("gamma beta", await TextOf(Flow.FindLast("li.item")(context)));
    }

    [TestMethod]
    public async Task Find_WithIndexOutOfRange_ThrowsOzoneException()
    {
        await using var context = await TestContexts.WithHtml(Html);

        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.Find("li.item", 5)(context));
    }

    [TestMethod]
    public async Task Find_WhenNothingMatches_ThrowsOzoneException()
    {
        await using var context = await TestContexts.WithHtml(Html);

        // Waits for the 10 s find timeout before failing.
        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.Find("#missing")(context));

        StringAssert.Contains(x.Message, "'#missing' not found");
    }

    [TestMethod]
    public async Task FindAll_ReturnsAllMatchesAsCollection()
    {
        await using var context = await TestContexts.WithHtml(Html);

        var result = await Flow.FindAll(".item")(context);

        Assert.IsNull(result.Element);
        Assert.IsNotNull(result.Collection);
        Assert.HasCount(4, result.Collection);
    }

    [TestMethod]
    public async Task FindOnXPath_ReturnsMatchAtIndex()
    {
        await using var context = await TestContexts.WithHtml(Html);

        Assert.AreEqual("alpha", await TextOf(Flow.FindOnXPath("//li")(context)));
        Assert.AreEqual("beta", await TextOf(Flow.FindOnXPath("//li", 1)(context)));
        Assert.AreEqual("gamma beta", await TextOf(Flow.FindOnXPath("//li", -1)(context)));
    }

    [TestMethod]
    public async Task FindAllOnXPath_ReturnsAllMatches()
    {
        await using var context = await TestContexts.WithHtml(Html);

        var result = await Flow.FindAllOnXPath("//li")(context);

        Assert.HasCount(3, result.Collection!);
    }

    [TestMethod]
    public async Task FindAllOnXPath_WhenNothingMatches_ThrowsOzoneException()
    {
        await using var context = await TestContexts.WithHtml(Html);

        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FindAllOnXPath("//table")(context));
    }

    [TestMethod]
    public async Task FindByText_ReturnsElementContainingText()
    {
        await using var context = await TestContexts.WithHtml(Html);

        Assert.AreEqual("Plain paragraph text", await TextOf(Flow.FindByText("paragraph")(context)));
    }

    [TestMethod]
    public async Task RelativeFind_SearchesOnlyInsideCurrentElement()
    {
        await using var context = await TestContexts.WithHtml(Html);
        var list = await Flow.Find("#other")(context);

        Assert.AreEqual("outside", await TextOf(Flow.RelativeFind(".item")(list)));
        Assert.AreEqual("outside", await TextOf(Flow.RelativeFind(".item", -1)(list)));
    }

    [TestMethod]
    public async Task RelativeFind_WhenNothingMatches_ThrowsOzoneException()
    {
        await using var context = await TestContexts.WithHtml(Html);
        var other = await Flow.Find("#other")(context);

        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.RelativeFind("li")(other));
    }

    [TestMethod]
    public async Task RelativeFindAll_ReturnsChildrenOfCurrentElement()
    {
        await using var context = await TestContexts.WithHtml(Html);
        var list = await Flow.Find("#list")(context);

        var result = await Flow.RelativeFindAll(".item")(list);

        Assert.HasCount(3, result.Collection!);
    }

    [TestMethod]
    public async Task RelativeFindOnXPath_ReturnsChildAtIndex()
    {
        await using var context = await TestContexts.WithHtml(Html);
        var list = await Flow.Find("#list")(context);

        Assert.AreEqual("beta", await TextOf(Flow.RelativeFindOnXPath(".//li", 1)(list)));
    }

    [TestMethod]
    public async Task RelativeFindAllOnXPath_ReturnsChildren()
    {
        await using var context = await TestContexts.WithHtml(Html);
        var list = await Flow.Find("#list")(context);

        var result = await Flow.RelativeFindAllOnXPath(".//li")(list);

        Assert.HasCount(3, result.Collection!);
    }

    [TestMethod]
    public async Task Exists_ReturnsContextWhenFoundAndNullOtherwise()
    {
        await using var context = await TestContexts.WithHtml(Html);

        var found = await Flow.Exists(context, "#list", 1);
        var missing = await Flow.Exists(context, "#missing", 0.2f);

        Assert.IsNotNull(found);
        Assert.IsTrue(found.HasElement);
        Assert.IsNull(missing);
    }

    [TestMethod]
    public async Task ExistsOnXPath_ReportsPresence()
    {
        await using var context = await TestContexts.WithHtml(Html);

        Assert.IsTrue(await Flow.ExistsOnXPath(context, "//ul"));
        Assert.IsFalse(await Flow.ExistsOnXPath(context, "//table", 0.2f));
    }

    [TestMethod]
    public async Task IfExists_RunsMatchingBranch()
    {
        await using var context = await TestContexts.WithHtml(Html);
        Func<Context, Task<Context>> Set(string value) => c => { c.Items["branch"] = value; return Task.FromResult(c); };

        await Flow.IfExists("#list", Set("true"), Set("false"))(context);
        Assert.AreEqual("true", context.Items["branch"]);

        await Flow.IfExists("#missing", Set("true"), Set("false"), waitSeconds: 0.2f)(context);
        Assert.AreEqual("false", context.Items["branch"]);
    }

    [TestMethod]
    public async Task IfExists_WhenFound_PassesFoundElementToOnTrue()
    {
        await using var context = await TestContexts.WithHtml(Html);
        string? seen = null;

        await Flow.IfExists("li.item", async c => { seen = await c.Text; return c; })(context);

        Assert.AreEqual("alpha", seen);
    }

    // --- Known issues from the code review; remove [Ignore] once fixed. ---

    [TestMethod]
    [Ignore("Known issue: FindAll lets Playwright's TimeoutException escape instead of OzoneException.")]
    public async Task FindAll_WhenNothingMatches_ThrowsOzoneException()
    {
        await using var context = await TestContexts.WithHtml(Html);

        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FindAll("#missing")(context));
    }

    [TestMethod]
    [Ignore("Known issue: Find treats any negative index as 'last', FindOnXPath counts from the end.")]
    public async Task Find_WithNegativeIndex_CountsFromEndLikeFindOnXPath()
    {
        await using var context = await TestContexts.WithHtml(Html);

        Assert.AreEqual("beta", await TextOf(Flow.Find("li.item", -2)(context)));
    }
}
