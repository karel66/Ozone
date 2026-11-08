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
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> is negative, NaN or infinite.</exception>
        public static Func<Context, Task<Context>> FindOnXPath(string xpath, int index = 0, float timeoutSeconds = 1.0f)
        {
            CheckWait(timeoutSeconds, nameof(timeoutSeconds));
            return async context => await SelectOne(context, nameof(FindOnXPath), xpath, context.RootLocatorForXPath(xpath), index, WaitSeconds(timeoutSeconds));
        }

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
        /// A wait of zero checks once without waiting.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> is negative, NaN or infinite.</exception>
        /// <exception cref="OzoneException">The search failed, for example because the selector is invalid.</exception>
        public async static Task<Context?> Exists(Context context, string selector, float timeoutSeconds)
        {
            CheckWait(timeoutSeconds, nameof(timeoutSeconds));
            var locator = context.RootLocatorForSelector(selector);

            if(await CountOrProblem(context, locator, timeoutSeconds) > 0)
            {
                return context.NextElement(locator.First);
            }

            Log($"Selector {Sensitive(selector)} does not exist.");
            return null;
        }

        /// <summary>
        /// Checks whether an element exists by XPath.
        /// A wait of zero checks once without waiting.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> is negative, NaN or infinite.</exception>
        /// <exception cref="OzoneException">The search failed, for example because the XPath is invalid.</exception>
        public async static Task<bool> ExistsOnXPath(Context context, string xpath, float timeoutSeconds = 1.0f)
        {
            CheckWait(timeoutSeconds, nameof(timeoutSeconds));

            if(await CountOrProblem(context, context.RootLocatorForXPath(xpath), timeoutSeconds) > 0)
            {
                return true;
            }

            Log($"XPath {Sensitive(xpath)} not found");
            return false;
        }

        static async Task<int> CountOrProblem(Context context, ILocator locator, float timeoutSeconds)
        {
            try
            {
                return await WaitAndCount(locator, WaitSeconds(timeoutSeconds));
            }
            catch(Exception x)
            {
                context.CreateProblem(x); // always throws OzoneException
                return 0;
            }
        }

        /// <summary>
        /// Executes the step if element by the selector is found.
        /// A wait of zero checks once without waiting.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="waitSeconds"/> is negative, NaN or infinite.</exception>
        public static Func<Context, Task<Context>> IfExists(string selector, Func<Context, Task<Context>>? onTrue = null, Func<Context, Task<Context>>? onFalse = null, float waitSeconds = 1)
        {
            CheckWait(waitSeconds, nameof(waitSeconds));

            return context => Guard(context, async () =>
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
            });
        }

        /// <summary>
        /// Chain gate: stops an <see cref="AsyncStep"/> chain unless an element matches the CSS
        /// selector in the page or current frame, waiting up to <paramref name="waitSeconds"/> for one.
        /// </summary>
        /// <remarks>
        /// When nothing matches, the rest of the chain the gate is linked into is skipped, and the
        /// result of <see cref="AsyncStep.Bind"/> has <see cref="AsyncStep.Stopped"/> set and the context
        /// the gate received. The context passes through unchanged when an element matches. Inside
        /// <see cref="While(Func{Context, Task{bool}}, AsyncStep, int)"/> a stop ends the loop.
        /// <code>
        /// ctx | (Flow.BreakIfNotExists("#next") | Flow.Find("#next") | Flow.Click)
        /// </code>
        /// Built on <see cref="AsyncStep.BreakUnless(string, Func{Context, Task{bool}})"/>, which makes gates for other conditions.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="waitSeconds"/> is negative, NaN or infinite.</exception>
        public static AsyncStep BreakIfNotExists(string selector, float waitSeconds = 1)
        {
            CheckWait(waitSeconds, nameof(waitSeconds));

            return AsyncStep.BreakUnless(nameof(BreakIfNotExists),
                async context => await Exists(context, selector, waitSeconds) != null);
        }
    }
}
