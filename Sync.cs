namespace Ozone
{
    /// <summary>
    /// Helper for running async Playwright operations synchronously.
    /// </summary>
    internal static class Sync
    {
        public static void Run(Func<Task> func) =>
            func().GetAwaiter().GetResult();

        public static T Run<T>(Func<Task<T>> func) =>
            func().GetAwaiter().GetResult();
    }
}
