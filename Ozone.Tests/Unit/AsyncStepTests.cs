namespace Ozone.Tests.Unit;

[TestClass]
public sealed class AsyncStepTests
{
    internal static Func<Context, Task<Context>> Append(string value) =>
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
    public async Task BreakUnless_WhenTrue_RunsRestOfChain()
    {
        var context = TestContexts.Detached();

        var last = await (context | (new AsyncStep(Append("a"))
            | AsyncStep.BreakUnless("Gate", _ => Task.FromResult(true)) | Append("b")));

        Assert.AreEqual("ab", context.Items["trail"]);
        Assert.AreSame(context, last.Context);
    }

    [TestMethod]
    public async Task BreakUnless_WhenFalse_SkipsRestOfChainAndReturnsGateInput()
    {
        var context = TestContexts.Detached();
        var gate = AsyncStep.BreakUnless("Gate", _ => Task.FromResult(false));

        var last = await (context | (new AsyncStep(Append("a")) | gate | Append("b")));

        Assert.AreEqual("a", context.Items["trail"]);
        Assert.AreSame(gate, last);
        Assert.AreSame(context, last.Context);
    }

    [TestMethod]
    public async Task BreakUnless_WhenPredicateThrows_FailsWithOzoneException()
    {
        var context = TestContexts.Detached();

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() =>
            context | (AsyncStep.BreakUnless("Gate", _ => throw new InvalidOperationException("boom")) | Append("b")));

        Assert.IsInstanceOfType<InvalidOperationException>(x.InnerException);
        Assert.IsFalse(context.Items.ContainsKey("trail"));
    }

    [TestMethod]
    public async Task BreakUnless_WhenFalse_SetsStopped_AndFinishedChainIsNotStopped()
    {
        var context = TestContexts.Detached();
        bool open = false;
        var chain = new AsyncStep(Append("a")) | AsyncStep.BreakUnless("Gate", _ => Task.FromResult(open)) | Append("b");

        Assert.IsTrue((await (context | chain)).Stopped);

        open = true;
        var last = await (context | chain);

        Assert.IsFalse(last.Stopped);
        Assert.AreEqual("aab", context.Items["trail"]);
    }

    [TestMethod]
    public async Task BreakUnless_InLinkedSubChain_StopsOnlyThatSubChain()
    {
        var context = TestContexts.Detached();

        var last = await (context | (new AsyncStep(Append("a"))
            | (AsyncStep.BreakUnless("Gate", _ => Task.FromResult(false)) | Append("b"))
            | Append("c")));

        Assert.AreEqual("ac", context.Items["trail"]);
        Assert.IsFalse(last.Stopped);
    }

    [TestMethod]
    public async Task BreakUnless_InSubChain_PassesGateInputToNextOuterStep()
    {
        var context = TestContexts.Detached();
        var replacement = context.EmptyContext();
        Context? seen = null;

        await (context | (new AsyncStep(_ => Task.FromResult(replacement))
            | (AsyncStep.BreakUnless("Gate", _ => Task.FromResult(false)) | Append("b"))
            | (c => { seen = c; return Task.FromResult(c); })));

        Assert.AreSame(replacement, seen);
    }

    [TestMethod]
    public async Task While_WithChain_EndsLoopWhenGateStops()
    {
        var context = TestContexts.Detached();
        int runs = 0;
        var chain = new AsyncStep(c => { runs++; return Task.FromResult(c); })
            | AsyncStep.BreakUnless("Gate", _ => Task.FromResult(runs < 3))
            | Append("x");

        await Flow.While(_ => Task.FromResult(true), chain)(context);

        Assert.AreEqual(3, runs);
        Assert.AreEqual("xx", context.Items["trail"]);
    }

    [TestMethod]
    public async Task While_WithChain_GateInSubChainDoesNotEndLoop()
    {
        var context = TestContexts.Detached();
        int runs = 0;
        var chain = new AsyncStep(c => { runs++; return Task.FromResult(c); })
            | (AsyncStep.BreakUnless("Gate", _ => Task.FromResult(false)) | Append("never"));

        await Flow.While(_ => Task.FromResult(runs < 3), chain)(context);

        Assert.AreEqual(3, runs);
        Assert.IsFalse(context.Items.ContainsKey("trail"));
    }

    [TestMethod]
    public void BreakUnless_TracesGivenNameAndRedactsCapturedStrings()
    {
        string selector = "#secret";
        var trace = AsyncStep.BreakUnless("HasThing", _ => Task.FromResult(selector.Length > 0)).MethodTrace();

        Assert.AreEqual("HasThing(selector=***)", trace);
    }

    [TestMethod]
    public void BreakUnless_RejectsMissingArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => AsyncStep.BreakUnless("Gate", null!));
        Assert.ThrowsExactly<ArgumentException>(() => AsyncStep.BreakUnless(" ", _ => Task.FromResult(true)));
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
    public void MethodTrace_ForStepFactory_ShowsFactoryNameAndRedactsStrings()
    {
        // The order of captured fields is chosen by the compiler, so check each argument separately.
        var find = new AsyncStep(Flow.Find("#id", 2)).MethodTrace()!;
        StringAssert.StartsWith(find, "Find(");
        StringAssert.Contains(find, "selector=***");
        StringAssert.Contains(find, "index=2");

        var xpath = new AsyncStep(Flow.FindOnXPath("//a")).MethodTrace()!;
        StringAssert.StartsWith(xpath, "FindOnXPath(");
        StringAssert.Contains(xpath, "xpath=***");
        StringAssert.Contains(xpath, "timeoutSeconds=1");
    }

    [TestMethod]
    public async Task Bind_WhenIntermediateStepReturnsNull_FailsWithClearMessage()
    {
        var context = TestContexts.Detached();

        var x = await Assert.ThrowsExactlyAsync<OzoneException>(() =>
            context | (new AsyncStep(_ => Task.FromResult<Context>(null!)) | Append("b")));

        StringAssert.Contains(x.Message, "step returned no context");
        Assert.IsFalse(context.Items.ContainsKey("trail"));
    }

    [TestMethod]
    public async Task Bind_WithNullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new AsyncStep(Append("a")).Bind(null!));
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
    public void MethodTrace_ByDefault_RedactsAllStrings()
    {
        Flow.TraceSensitiveValues = false;

        var trace = new AsyncStep(Flow.SetText("#pwd", "hunter2")).MethodTrace();

        Assert.AreEqual("SetText(selector=***, text=***)", trace);
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

    static async Task<string> CaptureConsole(Func<Task> action)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            await action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }

    [TestMethod]
    public async Task Bind_NestedChain_TracesOnlyItsSteps()
    {
        var context = TestContexts.Detached();

        string log = await CaptureConsole(async () =>
            await (context | (new AsyncStep(AsyncStepTests.Append("a")) | (new AsyncStep(AsyncStepTests.Append("b")) | AsyncStepTests.Append("c")))));

        Assert.DoesNotContain("SubChain", log);
        Assert.DoesNotContain("AsyncStep", log);
        Assert.AreEqual(3, log.Split("Append(").Length - 1, log);
    }

    [TestMethod]
    public void CreateProblem_WithForeignException_ByDefault_RedactsQuotedTextAndDropsCallLog()
    {
        Flow.TraceSensitiveValues = false;
        var original = new TimeoutException("Timeout 1000ms exceeded waiting for Locator(\"#password\") and 'x'.\nCall log:\n  - <input id=\"password\" value=secret>");

        var x = Assert.ThrowsExactly<OzoneException>(() => TestContexts.Detached().CreateProblem(original));

        Assert.AreEqual("Timeout 1000ms exceeded waiting for Locator(***) and ***.", x.Message);
        Assert.AreSame(original, x.InnerException);
    }

    [TestMethod]
    public void CreateProblem_WithForeignException_WhenSensitiveTracingEnabled_KeepsFullMessage()
    {
        Flow.TraceSensitiveValues = true;
        var original = new TimeoutException("waiting for Locator(\"#password\")\nCall log:");

        var x = Assert.ThrowsExactly<OzoneException>(() => TestContexts.Detached().CreateProblem(original));

        Assert.AreEqual(original.Message, x.Message);
    }

    [TestMethod]
    public async Task LogError_ByDefault_RedactsExceptionMessage()
    {
        Flow.TraceSensitiveValues = false;

        string log = await CaptureConsole(() =>
        {
            Flow.LogError("failed", new InvalidOperationException("bad selector \"#secret\"\nmore"));
            return Task.CompletedTask;
        });

        StringAssert.Contains(log, "InvalidOperationException: bad selector ***");
        Assert.DoesNotContain("#secret", log);
        Assert.DoesNotContain("more", log);
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
