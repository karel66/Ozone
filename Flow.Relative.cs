/*
* Oxygen.Flow.Playwright.Sync library
*/

namespace Ozone
{
    /// <summary>
    /// Relative searches (current element context).
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// Searches child element of the current element.
        /// </summary>
        public static FlowStep RelativeFind(string selector, int index = 0) =>
            context =>
            {
                if (context.Element == null)
                {
                    return context.CreateProblem($"{nameof(RelativeFind)}: Missing context Element");
                }

                var locator = context.Element.Locator(selector);
                int count = Sync.Run(() => locator.CountAsync());

                if (count <= 0)
                {
                    return context.CreateProblem($"{nameof(RelativeFind)}: '{selector}' not found");
                }

                int idx = index < 0 ? count - 1 : index;
                if (idx < 0 || idx >= count)
                {
                    return context.CreateProblem($"{nameof(RelativeFind)}: index {index} out of range (0..{count - 1})");
                }

                return context.NextElement(locator.Nth(idx));
            };

        /// <summary>
        /// Searches all child elements of the current element.
        /// </summary>
        public static FlowStep RelativeFindAll(string selector) =>
            context =>
            {
                if (context.Element == null)
                {
                    return context.CreateProblem($"{nameof(RelativeFindAll)}: Missing context Element");
                }

                var locator = context.Element.Locator(selector);
                int count = Sync.Run(() => locator.CountAsync());

                if (count <= 0)
                {
                    return context.CreateProblem($"{nameof(RelativeFindAll)}: '{selector}' not found");
                }

                var items = Enumerable.Range(0, count)
                    .Select(i => locator.Nth(i))
                    .ToList()
                    .AsReadOnly();

                return context.NextCollection(items);
            };

        /// <summary>
        /// Searches child element of the current element using XPath.
        /// </summary>
        public static FlowStep RelativeFindOnXPath(string xpath, int index = 0) =>
            context =>
            {
                if (context.Element == null)
                {
                    return context.CreateProblem($"{nameof(RelativeFindOnXPath)}: Missing context Element");
                }

                var locator = context.Element.Locator($"xpath={xpath}");
                int count = Sync.Run(() => locator.CountAsync());

                if (count <= 0)
                {
                    return context.CreateProblem($"{nameof(RelativeFindOnXPath)}: '{xpath}' not found");
                }

                int idx = index < 0 ? count - 1 : index;
                if (idx < 0 || idx >= count)
                {
                    return context.CreateProblem($"{nameof(RelativeFindOnXPath)}: index {index} out of range (0..{count - 1})");
                }

                return context.NextElement(locator.Nth(idx));
            };

        /// <summary>
        /// Searches all child elements of the current element using XPath.
        /// </summary>
        public static FlowStep RelativeFindAllOnXPath(string xpath) =>
            context =>
            {
                if (context.Element == null)
                {
                    return context.CreateProblem($"{nameof(RelativeFindAllOnXPath)}: Missing context Element");
                }

                var locator = context.Element.Locator($"xpath={xpath}");
                int count = Sync.Run(() => locator.CountAsync());

                if (count <= 0)
                {
                    return context.CreateProblem($"{nameof(RelativeFindAllOnXPath)}: '{xpath}' not found");
                }

                var items = Enumerable.Range(0, count)
                    .Select(i => locator.Nth(i))
                    .ToList()
                    .AsReadOnly();

                return context.NextCollection(items);
            };
    }
}
