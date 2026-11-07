namespace Ozone.Tests.Unit;

[TestClass]
public sealed class ContextTests
{
    [TestMethod]
    public void CreateProblem_WithMessage_ThrowsOzoneExceptionWithThatMessage()
    {
        var context = TestContexts.Detached();

        var x = Assert.ThrowsExactly<OzoneException>(() => context.CreateProblem("something broke"));

        Assert.AreEqual("something broke", x.Message);
        Assert.IsNull(x.InnerException);
    }

    [TestMethod]
    public void CreateProblem_WithNull_ThrowsOzoneExceptionWithPlaceholderMessage()
    {
        var context = TestContexts.Detached();

        var x = Assert.ThrowsExactly<OzoneException>(() => context.CreateProblem((object)null!));

        Assert.AreEqual("Null passed as problem!", x.Message);
    }

    [TestMethod]
    public void CreateProblem_WithException_WrapsItAsInnerException()
    {
        var context = TestContexts.Detached();
        var original = new InvalidOperationException("boom");

        var x = Assert.ThrowsExactly<OzoneException>(() => context.CreateProblem(original));

        Assert.AreEqual("boom", x.Message);
        Assert.AreSame(original, x.InnerException);
    }

    [TestMethod]
    public void CreateProblem_WithOzoneException_RethrowsSameInstance()
    {
        var context = TestContexts.Detached();
        var original = new OzoneException("already wrapped");

        var x = Assert.ThrowsExactly<OzoneException>(() => context.CreateProblem(original));

        Assert.AreSame(original, x);
    }

    [TestMethod]
    public void CreateProblem_WithFactory_UsesFactoryResultAsMessage()
    {
        var context = TestContexts.Detached();
        context.Items["key"] = "value";

        var x = Assert.ThrowsExactly<OzoneException>(() => context.CreateProblem(c => $"item={c.Items["key"]}"));

        Assert.AreEqual("item=value", x.Message);
    }

    [TestMethod]
    public void Use_InvokesActionAndReturnsSameContext()
    {
        var context = TestContexts.Detached();

        var result = context.Use(c => c.Items["seen"] = "yes");

        Assert.AreSame(context, result);
        Assert.AreEqual("yes", context.Items["seen"]);
    }

    [TestMethod]
    public void Use_WhenActionThrows_WrapsExceptionInOzoneException()
    {
        var context = TestContexts.Detached();

        var x = Assert.ThrowsExactly<OzoneException>(() => context.Use(_ => throw new ArgumentException("bad")));

        Assert.IsInstanceOfType<ArgumentException>(x.InnerException);
    }

    [TestMethod]
    public void Use_WithNullAction_ThrowsOzoneException()
    {
        var context = TestContexts.Detached();

        Assert.ThrowsExactly<OzoneException>(() => context.Use(null!));
    }

    [TestMethod]
    public void EmptyContext_ClearsElementAndCollectionButSharesItems()
    {
        var context = TestContexts.Detached(collection: []);
        context.Items["shared"] = "1";

        var empty = context.EmptyContext();

        Assert.IsNull(empty.Element);
        Assert.IsNull(empty.Collection);
        Assert.IsFalse(empty.HasElement);
        Assert.AreSame(context.Items, empty.Items);
    }

    [TestMethod]
    public async Task Text_WithoutElement_ReturnsNull()
    {
        var context = TestContexts.Detached();

        Assert.IsNull(await context.Text);
        Assert.IsNull(await context.Value());
    }
}
