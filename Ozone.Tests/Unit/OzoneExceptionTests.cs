namespace Ozone.Tests.Unit;

[TestClass]
public sealed class OzoneExceptionTests
{
    [TestMethod]
    public void Constructors_SetMessageAndInnerException()
    {
        var inner = new InvalidOperationException();

        Assert.AreEqual("msg", new OzoneException("msg").Message);

        var withInner = new OzoneException("msg", inner);
        Assert.AreEqual("msg", withInner.Message);
        Assert.AreSame(inner, withInner.InnerException);

        Assert.IsNull(new OzoneException().InnerException);
    }
}
