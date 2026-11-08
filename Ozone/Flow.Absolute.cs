using Microsoft.Playwright;

namespace Ozone
{
    /// <summary>
    /// Global searches (page or current frame).
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// Find element by CSS selector from page/frame.
        /// A negative index counts from the end (-1 is the last match).
        /// </summary>
        public static Func<Context, Task<Context>> Find(string selector, int index = 0) =>
            async context => await SelectOne(context, nameof(Find), selector, context.RootLocatorForSelector(selector), index, WaitFor(context.FindTimeout));

        /// <summary>
        /// Find last element by selector.
        /// </summary>
        public static Func<Context, Task<Context>> FindLast(string selector) => Find(selector, -1);

        /// <summary>
        /// Find all elements by CSS selector.
        /// </summary>
        public static Func<Context, Task<Context>> FindAll(string selector) =>
            async context => await SelectAll(context, nameof(FindAll), selector, context.RootLocatorForSelector(selector), WaitFor(context.FindTimeout));

        /// <summary>
        /// Find element by XPath from page/frame.
        /// A negative index counts from the end (-1 is the last match).
        /// </summary>
        public static Func<Context, Task<Context>> FindOnXPath(string xpath, int index = 0, float timeoutSeconds = 1.0f) =>
            async context => await SelectOne(context, nameof(FindOnXPath), xpath, context.RootLocatorForXPath(xpath), index, WaitSeconds(timeoutSeconds));

        /// <summary>
        /// Find the first element containing the given text from page/frame.
        /// </summary>
        public static Func<Context, Task<Context>> FindByText(string text) =>
            async context => await SelectOne(context, nameof(FindByText), text, context.RootLocatorForText(text), 0, WaitFor(context.FindTimeout));

        /// <summary>
        /// Find all elements by XPath from page/frame.
        /// </summary>
        public static Func<Context, Task<Context>> FindAllOnXPath(string xpath) =>
            async context => await SelectAll(context, nameof(FindAllOnXPath), xpath, context.RootLocatorForXPath(xpath), WaitFor(context.FindTimeout));

        /// <summary>
        /// Find an element using CSS selector, otherwise returns null.
        /// </summary>
        public async static Task<Context?> Exists(Context context, string selector, float timeoutSeconds)
        {
            var locator = context.RootLocatorForSelector(selector);

            if(await WaitAndCount(locator, WaitSeconds(timeoutSeconds)) > 0)
            {
                return context.NextElement(locator.First);
            }

            Log($"Selector {selector} does not exist.");
            return null;
        }

        /// <summary>
        /// Checks whether an element exists by XPath.
        /// </summary>
        public async static Task<bool> ExistsOnXPath(Context context, string xpath, float timeoutSeconds = 1.0f)
        {
            if(await WaitAndCount(context.RootLocatorForXPath(xpath), WaitSeconds(timeoutSeconds)) > 0)
            {
                return true;
            }

            Log($"{xpath} not found");
            return false;
        }

        /// <summary>
        /// Executes the step if element by the selector is found.
        /// </summary>
        public static Func<Context, Task<Context>> IfExists(string selector, Func<Context, Task<Context>>? onTrue = null, Func<Context, Task<Context>>? onFalse = null, float waitSeconds = 1) =>
            async context =>
            {
                var c = await Exists(context, selector, waitSeconds);

                if(c != null && onTrue != null)
                {
                    return await onTrue(c);
                }

                if(c == null && onFalse != null)
                {
                    return await onFalse(context);
                }

                return context;
            };

        /// <summary>
        /// Chain gate: stops an <see cref="AsyncStep"/> chain unless an element matches the CSS
        /// selector in the page or current frame, waiting up to <paramref name="waitSeconds"/> for one.
        /// </summary>
        /// <remarks>
        /// When nothing matches, the steps after the gate are skipped and the chain's result is the
        /// context the gate received. The context passes through unchanged when an element matches.
        /// The chain is not modified, so it can be run again (for example by
        /// <see cref="While(Func{Context, Task{bool}}, AsyncStep, int)"/>) and the check is repeated.
        /// <code>
        /// ctx | (Flow.BreakIfNotExists("#next") | Flow.Find("#next") | Flow.Click)
        /// </code>
        /// </remarks>
        public static AsyncStep BreakIfNotExists(string selector, float waitSeconds = 1) =>
            new(nameof(BreakIfNotExists), async context =>
            {
                try
                {
                    return await Exists(context, selector, waitSeconds) != null;
                }
                catch(Exception x)
                {
                    context.CreateProblem(x);
                    throw; // not reached: CreateProblem always throws
                }
            });
    }
}
