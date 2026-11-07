using Microsoft.Playwright;

namespace Ozone
{
    /// <summary>
    /// Relative searches (current element context).
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// Searches child element of the current element.
        /// A negative index counts from the end (-1 is the last match).
        /// </summary>
        public static Func<Context, Task<Context>> RelativeFind(string selector, int index = 0) =>
            async context => await SelectOne(context, nameof(RelativeFind), selector, ChildLocator(context, nameof(RelativeFind), selector), index, FindTimeout);

        /// <summary>
        /// Searches all child elements of the current element.
        /// </summary>
        public static Func<Context, Task<Context>> RelativeFindAll(string selector) =>
            async context => await SelectAll(context, nameof(RelativeFindAll), selector, ChildLocator(context, nameof(RelativeFindAll), selector), FindTimeout);

        /// <summary>
        /// Searches child element of the current element using XPath.
        /// A negative index counts from the end (-1 is the last match).
        /// </summary>
        public static Func<Context, Task<Context>> RelativeFindOnXPath(string xpath, int index = 0) =>
            async context => await SelectOne(context, nameof(RelativeFindOnXPath), xpath, ChildLocator(context, nameof(RelativeFindOnXPath), $"xpath={xpath}"), index, FindTimeout);

        /// <summary>
        /// Searches all child elements of the current element using XPath.
        /// </summary>
        public static Func<Context, Task<Context>> RelativeFindAllOnXPath(string xpath) =>
            async context => await SelectAll(context, nameof(RelativeFindAllOnXPath), xpath, ChildLocator(context, nameof(RelativeFindAllOnXPath), $"xpath={xpath}"), FindTimeout);

        static ILocator ChildLocator(Context context, string stepName, string selector)
        {
            if (context.Element == null)
            {
                context.CreateProblem($"{stepName}: Missing context Element");
            }

            return context.Element.Locator(selector);
        }
    }
}
