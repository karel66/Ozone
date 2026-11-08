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
        static Task<Context> SelectOne(Context context, string stepName, string query, ILocator locator, int index, LocatorWaitForOptions wait) =>
            Guard(context, async () =>
        {
            int count = await WaitAndCount(locator, wait);

            if(count == 0)
            {
                return context.CreateProblem($"{stepName}: {Sensitive(query)} not found");
            }

            int idx = index < 0 ? count + index : index;

            if(idx < 0 || idx >= count)
            {
                return context.CreateProblem($"{stepName}: index {index} out of range for {Sensitive(query)} ({count} match(es))");
            }

            return context.NextElement(locator.Nth(idx));
        });

        /// <summary>
        /// Waits for the first match, then selects all matches as the context collection.
        /// </summary>
        static Task<Context> SelectAll(Context context, string stepName, string query, ILocator locator, LocatorWaitForOptions wait) =>
            Guard(context, async () =>
        {
            int count = await WaitAndCount(locator, wait);

            if(count == 0)
            {
                return context.CreateProblem($"{stepName}: {Sensitive(query)} not found");
            }

            var items = Enumerable.Range(0, count)
                .Select(locator.Nth)
                .ToList()
                .AsReadOnly();

            return context.NextCollection(items);
        });

        /// <summary>
        /// Returns the number of matches, or 0 if none appeared within the wait timeout.
        /// A timeout of zero checks once without waiting (Playwright itself reads 0 as "wait forever").
        /// Callers validate waits with <see cref="CheckWait"/>, so the timeout is never negative.
        /// </summary>
        static async Task<int> WaitAndCount(ILocator locator, LocatorWaitForOptions wait)
        {
            if(wait.Timeout == 0)
            {
                return await locator.CountAsync();
            }

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

        /// <summary>
        /// Rejects a wait that is negative, NaN or infinite. Zero is allowed and means "check once".
        /// </summary>
        static void CheckWait(float seconds, string paramName)
        {
            if(!float.IsFinite(seconds) || seconds < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, seconds, "Wait must be zero (check once) or a positive number of seconds.");
            }
        }

        static LocatorWaitForOptions WaitFor(TimeSpan timeout) =>
            new() { Timeout = (float)timeout.TotalMilliseconds, State = WaitForSelectorState.Attached };
    }
}
