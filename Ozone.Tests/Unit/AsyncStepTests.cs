namespace Ozone.Tests.Unit;

[TestClass]
public sealed class AsyncStepTests
{
    static Func<Context, Task<Context>> Append(string value) =>
        context =>
        {
            context.Items.AddOrUpdate("trail", value, (_, old) => old + value);
            return Task.FromResult(context);
        };

    [TestMethod]
    public async Task Bind_RunsLinkedStepsInOrder()
    {
        var context = TestContexts.Detached();

        var last = await (context | (new AsyncStep(Append("a")) | Append("b") | Append("c")));

        Assert.AreEqual("abc", context.Items["trail"]);
        Assert.AreSame(context, last.Context);
    }

    [TestMethod]
    public async Task Bind_PassesEachStepResultToTheNext()
    {
        var context = TestContexts.Detached();
        var replacement = context.EmptyContext();
        Context? seenBySecond = null;

        await (context | (new AsyncStep(_ => Task.FromResult(replacement))
            | (c => { seenBySecond = c; return Task.FromResult(c); })));

        Assert.AreSame(replacement, seenBySecond);
    }

    [TestMethod]
    public async Task Bind_StopsChainWhenStepThrows()
    {
        var context = TestContexts.Detached();

        await Assert.ThrowsExactlyAsync<OzoneException>(() =>
            context | (new AsyncStep(Append("a")) | Flow.CreateProblem("stop") | Append("b")));

        Assert.AreEqual("a", context.Items["trail"]);
    }

    [TestMethod]
    public async Task Link_CombinesTwoChains()
    {
        var context = TestContexts.Detached();
        var first = new AsyncStep(Append("1")) | Append("2");
        var second = new AsyncStep(Append("3")) | Append("4");

        await (context | (first | second));

        Assert.AreEqual("1234", context.Items["trail"]);
    }

    [TestMethod]
    public void MethodTrace_ForMethodGroup_ShowsMethodName()
    {
        Assert.AreEqual("Click()", new AsyncStep(Flow.Click).MethodTrace());
    }

    [TestMethod]
    public void MethodTrace_ForStepFactory_ShowsFactoryNameAndLocatorArguments()
    {
        // The order of captured fields is chosen by the compiler, so check each argument separately.
        var find = new AsyncStep(Flow.Find("#id", 2)).MethodTrace()!;
        StringAssert.StartsWith(find, "Find(");
        StringAssert.Contains(find, "selector=\"#id\"");
        StringAssert.Contains(find, "index=2");

        var xpath = new AsyncStep(Flow.FindOnXPath("//a")).MethodTrace()!;
        StringAssert.StartsWith(xpath, "FindOnXPath(");
        StringAssert.Contains(xpath, "xpath=\"//a\"");
        StringAssert.Contains(xpath, "timeoutSeconds=1");
    }
}

/// <summary>
/// These tests toggle the global <see cref="Flow.TraceSensitiveValues"/> switch,
/// so they must not run in parallel with each other.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class AsyncStepTraceRedactionTests
{
    [TestCleanup]
    public void ResetSwitch() => Flow.TraceSensitiveValues = false;

    [TestMethod]
    public void MethodTrace_ByDefault_RedactsNonLocatorStrings()
    {
        Flow.TraceSensitiveValues = false;

        var trace = new AsyncStep(Flow.SetText("#pwd", "hunter2")).MethodTrace();

        Assert.AreEqual("SetText(selector=\"#pwd\", text=***)", trace);
    }

    [TestMethod]
    public void MethodTrace_ByDefault_RedactsArrayContents()
    {
        Flow.TraceSensitiveValues = false;

        var trace = new AsyncStep(Flow.Script("x => x", "secret", "token")).MethodTrace();

        Assert.IsNotNull(trace);
        StringAssert.Contains(trace, "args=[2 item(s)]");
        Assert.DoesNotContain("secret", trace);
    }

    [TestMethod]
    public void MethodTrace_WhenSensitiveTracingEnabled_ShowsAllValues()
    {
        Flow.TraceSensitiveValues = true;

        Assert.AreEqual(
            "SetText(selector=\"#pwd\", text=\"hunter2\")",
            new AsyncStep(Flow.SetText("#pwd", "hunter2")).MethodTrace());

        StringAssert.Contains(
            new AsyncStep(Flow.Script("x => x", "secret", "token")).MethodTrace(),
            "args=[\"secret\", \"token\"]");
    }
}
