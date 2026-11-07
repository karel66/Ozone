using Microsoft.Playwright;

namespace Ozone.Tests.Unit;

/// <summary>
/// Control-flow, assertion and precondition behavior of <see cref="Flow"/> steps
/// that can be exercised without a browser.
/// </summary>
[TestClass]
public sealed class FlowLogicTests
{
    static readonly Func<Context, Task<Context>> Mark =
        c => { c.Items["marked"] = "yes"; return Task.FromResult(c); };

    // --- If / While -------------------------------------------------------

    [TestMethod]
    public async Task If_WhenConditionTrue_RunsStep()
    {
        var context = TestContexts.Detached();

        await Flow.If(_ => Task.FromResult(true), Mark)(context);

        Assert.IsTrue(context.Items.ContainsKey("marked"));
    }

    [TestMethod]
    public async Task If_WhenConditionFalse_ReturnsContextUnchanged()
    {
        var context = TestContexts.Detached();

        var result = await Flow.If(_ => Task.FromResult(false), Mark)(context);

        Assert.AreSame(context, result);
        Assert.IsFalse(context.Items.ContainsKey("marked"));
    }

    [TestMethod]
    public async Task While_RepeatsStepUntilConditionFalse()
    {
        var context = TestContexts.Detached();
        int runs = 0;

        await Flow.While(_ => Task.FromResult(runs < 3), c => { runs++; return Task.FromResult(c); })(context);

        Assert.AreEqual(3, runs);
    }

    [TestMethod]
    public async Task While_WithAsyncStep_RepeatsChainUntilConditionFalse()
    {
        var context = TestContexts.Detached();
        int runs = 0;
        var chain = new AsyncStep(c => { runs++; return Task.FromResult(c); });

        await Flow.While(_ => Task.FromResult(runs < 4), chain)(context);

        Assert.AreEqual(4, runs);
    }

    // --- Retry ------------------------------------------------------------

    [TestMethod]
    public async Task Retry_WhenFirstAttemptSucceeds_ReturnsTrueImmediately()
    {
        int attempts = 0;

        bool ok = await Flow.Retry(() => { attempts++; return Task.FromResult(true); });

        Assert.IsTrue(ok);
        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task Retry_WhenLaterAttemptSucceeds_ReturnsTrue()
    {
        int attempts = 0;

        bool ok = await Flow.Retry(() => Task.FromResult(++attempts == 2), maxAttempts: 3);

        Assert.IsTrue(ok);
        Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public async Task Retry_WhenAllAttemptsFailOrThrow_ReturnsFalse()
    {
        int attempts = 0;

        bool ok = await Flow.Retry(() =>
        {
            attempts++;
            return attempts == 1 ? throw new InvalidOperationException() : Task.FromResult(false);
        }, maxAttempts: 2);

        Assert.IsFalse(ok);
        Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public async Task Retry_WithZeroAttempts_StillTriesOnce()
    {
        int attempts = 0;

        await Flow.Retry(() => { attempts++; return Task.FromResult(true); }, maxAttempts: 0);

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task Retry_WithNullDelegate_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Flow.Retry(null!));
    }

    // --- Use --------------------------------------------------------------

    [TestMethod]
    public async Task Use_PassesStepResultToAction()
    {
        var context = TestContexts.Detached();
        var stepResult = context.EmptyContext();
        Context? seen = null;

        var result = await Flow.Use(_ => Task.FromResult(stepResult), c => seen = c)(context);

        Assert.AreSame(stepResult, seen);
        Assert.AreSame(stepResult, result);
    }

    [TestMethod]
    public async Task Use_WithNullAction_ThrowsOzoneException()
    {
        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.Use(Mark, null!)(TestContexts.Detached()));
    }

    // --- Assertions -------------------------------------------------------

    [TestMethod]
    public async Task Assertion_WhenPredicateHolds_ReturnsContext()
    {
        var context = TestContexts.Detached();

        Assert.AreSame(context, await Flow.Assertion(_ => true, "unused")(context));
        Assert.AreSame(context, await Flow.Assertion(_ => true, _ => "unused")(context));
        Assert.AreSame(context, await Flow.AssertAsync(_ => Task.FromResult(true), "unused")(context));
        Assert.AreSame(context, await Flow.AssertAsync(_ => Task.FromResult(true), _ => Task.FromResult("unused"))(context));
    }

    [TestMethod]
    public async Task Assertion_WhenPredicateFails_ThrowsWithMessage()
    {
        var context = TestContexts.Detached();

        var x1 = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.Assertion(_ => false, "fixed")(context));
        var x2 = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.Assertion(_ => false, _ => "computed")(context));
        var x3 = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.AssertAsync(_ => Task.FromResult(false), "async fixed")(context));
        var x4 = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.AssertAsync(_ => Task.FromResult(false), _ => Task.FromResult("async computed"))(context));

        Assert.AreEqual("fixed", x1.Message);
        Assert.AreEqual("computed", x2.Message);
        Assert.AreEqual("async fixed", x3.Message);
        Assert.AreEqual("async computed", x4.Message);
    }

    [TestMethod]
    public async Task CreateProblemSteps_AlwaysThrow()
    {
        var context = TestContexts.Detached();

        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.CreateProblem("a")(context));
        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.CreateProblem(Task.FromResult<object>("b"))(context));
        await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.CreateProblem(_ => "c")(context));
    }

    // --- Preconditions: steps that need an Element or Collection ----------

    public static IEnumerable<object[]> StepsRequiringElement =>
    [
        ["Click", (Func<Context, Task<Context>>)Flow.Click],
        ["PressEnter", (Func<Context, Task<Context>>)Flow.PressEnter],
        ["SetText", Flow.SetText("x")],
        ["AssertAttributeValue", Flow.AssertAttributeValue("id", "x")],
        ["RelativeFind", Flow.RelativeFind("a")],
        ["RelativeFindAll", Flow.RelativeFindAll("a")],
        ["RelativeFindOnXPath", Flow.RelativeFindOnXPath("a")],
        ["RelativeFindAllOnXPath", Flow.RelativeFindAllOnXPath("a")],
    ];

    [TestMethod]
    [DynamicData(nameof(StepsRequiringElement))]
    public async Task StepRequiringElement_WithoutElement_ThrowsOzoneException(string name, Func<Context, Task<Context>> step)
    {
        await Assert.ThrowsExactlyAsync<OzoneException>(() => step(TestContexts.Detached()), name);
    }

    public static IEnumerable<object[]> StepsRequiringCollection =>
    [
        ["FirstContainingText", Flow.FirstContainingText("a")],
        ["LastContainingText", Flow.LastContainingText("a")],
        ["FirstContainingContextItem", Flow.FirstContainingContextItem("k")],
        ["LastContainingContextItem", Flow.LastContainingContextItem("k")],
    ];

    [TestMethod]
    [DynamicData(nameof(StepsRequiringCollection))]
    public async Task StepRequiringCollection_WithoutCollection_ThrowsOzoneException(string name, Func<Context, Task<Context>> step)
    {
        await Assert.ThrowsExactlyAsync<OzoneException>(() => step(TestContexts.Detached()), name);
    }

    [TestMethod]
    public async Task FirstContainingContextItem_WithMissingKey_ThrowsOzoneException()
    {
        var context = TestContexts.Detached(collection: new List<ILocator>());

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.FirstContainingContextItem("missing")(context));

        StringAssert.Contains(x.Message, "'missing'");
    }

    [TestMethod]
    public async Task LastContainingContextItem_WithMissingKey_ThrowsOzoneException()
    {
        var context = TestContexts.Detached(collection: new List<ILocator>());

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() => Flow.LastContainingContextItem("missing")(context));

        StringAssert.Contains(x.Message, "'missing'");
    }

    [TestMethod]
    public async Task CreateContext_WithNullUrl_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Flow.CreateContext(BrowserBrand.Chromium, null!));
    }
}
