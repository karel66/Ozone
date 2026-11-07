using Microsoft.Playwright;

namespace Ozone
{
    /// <summary>
    /// Shared implementation for all finders, so they wait, index and report problems the same way.
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// Waits for the first match, then selects one element by index.
        /// A negative index counts from the end: -1 is the last match, -2 the one before it.
        /// </summary>
        static async Task<Context> SelectOne(Context context, string stepName, string query, ILocator locator, int index, LocatorWaitForOptions wait)
        {
            int count = await WaitAndCount(locator, wait);

            if(count == 0)
            {
                return context.CreateProblem($"{stepName}: '{query}' not found");
            }

            int idx = index < 0 ? count + index : index;

            if(idx < 0 || idx >= count)
            {
                return context.CreateProblem($"{stepName}: index {index} out of range for '{query}' ({count} match(es))");
            }

            return context.NextElement(locator.Nth(idx));
        }

        /// <summary>
        /// Waits for the first match, then selects all matches as the context collection.
        /// </summary>
        static async Task<Context> SelectAll(Context context, string stepName, string query, ILocator locator, LocatorWaitForOptions wait)
        {
            int count = await WaitAndCount(locator, wait);

            if(count == 0)
            {
                return context.CreateProblem($"{stepName}: '{query}' not found");
            }

            var items = Enumerable.Range(0, count)
                .Select(locator.Nth)
                .ToList()
                .AsReadOnly();

            return context.NextCollection(items);
        }

        /// <summary>
        /// Returns the number of matches, or 0 if none appeared within the wait timeout.
        /// </summary>
        static async Task<int> WaitAndCount(ILocator locator, LocatorWaitForOptions wait)
        {
            try
            {
                await locator.First.WaitForAsync(wait);
            }
            catch(TimeoutException)
            {
                return 0;
            }

            return await locator.CountAsync();
        }

        static LocatorWaitForOptions WaitSeconds(float seconds) => WaitFor(TimeSpan.FromSeconds(seconds));

        static LocatorWaitForOptions WaitFor(TimeSpan timeout) =>
            new() { Timeout = (float)timeout.TotalMilliseconds, State = WaitForSelectorState.Attached };
    }
}
