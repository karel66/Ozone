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
        /// </summary>
        public static Func<Context, Task<Context>> Find(string selector, int index = 0) =>
            async context =>
            {
                if (context.Page == null)
                {
                    return context.CreateProblem($"{nameof(Find)}: Missing Page");
                }

                var locator = context.RootLocatorForSelector(selector);

                int count = await locator.CountAsync();

                if (count == 0)
                {
                    return context.CreateProblem($"{nameof(Find)}: '{selector}' not found");
                }

                int idx = index < 0 ? count - 1 : index;

                if (idx < 0 || idx >= count)
                {
                    return context.CreateProblem($"{nameof(Find)}: index {index} out of range (0..{count - 1})");
                }

                return context.NextElement(locator.Nth(idx));
            };

        /// <summary>
        /// Find last element by selector.
        /// </summary>
        public static Func<Context, Task<Context>> FindLast(string selector) => Find(selector, -1);

        /// <summary>
        /// Find all elements by CSS selector.
        /// </summary>
        public static Func<Context, Task<Context>> FindAll(string selector) =>
            async context =>
            {
                if (context.Page == null)
                {
                    return context.CreateProblem($"{nameof(FindAll)}: Missing Page");
                }

                var locator = context.RootLocatorForSelector(selector);
                int count = await locator.CountAsync();

                if (count <= 0)
                {
                    return context.CreateProblem($"{nameof(FindAll)}: '{selector}' not found");
                }

                var items = Enumerable.Range(0, count)
                    .Select(i => locator.Nth(i))
                    .ToList()
                    .AsReadOnly();

                return context.NextCollection(items);
            };

        /// <summary>
        /// Find element by XPath from page/frame.
        /// </summary>
        public static Func<Context, Task<Context>> FindOnXPath(string xpath, int index = 0) =>
            async context =>
            {
                if (context.Page == null)
                {
                    return context.CreateProblem($"{nameof(FindOnXPath)}: Missing Page");
                }

                var locator = context.RootLocatorForXPath(xpath);
                int count = await locator.CountAsync();

                if (count <= 0)
                {
                    return context.CreateProblem($"{nameof(FindOnXPath)}: '{xpath}' not found");
                }

                int idx = index < 0 ? count - 1 : index;
                if (idx < 0 || idx >= count)
                {
                    return context.CreateProblem($"{nameof(FindOnXPath)}: index {index} out of range (0..{count - 1})");
                }

                return context.NextElement(locator.Nth(idx));
            };

        /// <summary>
        /// Find all elements by XPath from page/frame.
        /// </summary>
        public static Func<Context, Task<Context>> FindAllOnXPath(string xpath) =>
            async context =>
            {
                if (context.Page == null)
                {
                    return context.CreateProblem($"{nameof(FindAllOnXPath)}: Missing Page");
                }

                var locator = context.RootLocatorForXPath(xpath);
                int count = await locator.CountAsync();

                if (count <= 0)
                {
                    return context.CreateProblem($"{nameof(FindAllOnXPath)}: '{xpath}' not found");
                }

                var items = Enumerable.Range(0, count)
                    .Select(i => locator.Nth(i))
                    .ToList()
                    .AsReadOnly();

                return context.NextCollection(items);
            };

        /// <summary>
        /// Check for existence of an element using CSS selector.
        /// </summary>
        public async static Task<bool> Exists(Context context, string selector, float timeoutSeconds = 1.0f)
        {
            try
            {
                var locator = context.RootLocatorForSelector(selector);
                await locator.First.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = (int)(timeoutSeconds * 1000)
                });

                return true;
            }
            catch (TimeoutException)
            {
                Log($"Exists [{selector}] not found");
                return false;
            }
            catch (PlaywrightException)
            {
                Log($"Exists [{selector}] not found");
                return false;
            }
        }

        public async static Task<bool> ExistsOnXPath(Context context, string xpath, float timeoutSeconds = 1.0f)
        {
            try
            {
                var locator = context.RootLocatorForXPath(xpath);
                await locator.First.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = (int)(timeoutSeconds * 1000)
                });

                return true;
            }
            catch (TimeoutException)
            {
                Log($"ExistsOnXPath [{xpath}] not found");
                return false;
            }
            catch (PlaywrightException)
            {
                Log($"ExistsOnXPath [{xpath}] not found");
                return false;
            }
        }

        /// <summary>
        /// Executes the step if element by the selector is found.
        /// </summary>
        public static Func<Context, Task<Context>> IfExists(string selector, Func<Context, Task<Context>>? onTrue = null, Func<Context, Task<Context>>? onFalse = null, float waitSeconds = 0) =>
            async context =>
            {
                bool exists = await Exists(context, selector, waitSeconds);

                if (exists && onTrue != null)
                {
                    return await onTrue(context);
                }

                if (!exists && onFalse != null)
                {
                    return await onFalse(context);
                }

                return context;
            };
    }
}
