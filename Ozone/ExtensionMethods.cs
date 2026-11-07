using Microsoft.Playwright;
using System.Text;

namespace Ozone
{
    /// <summary>
    /// Helper extensions for <see cref="ILocator"/> and <see cref="StringBuilder"/>.
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Appends text, preceded by ", " unless the builder is empty.
        /// </summary>
        public static StringBuilder AppendWithComma(this StringBuilder sb, string text)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
            return sb.Append(text);
        }

        /// <summary>
        /// Returns the input value of an input, textarea or select element, otherwise its text content.
        /// </summary>
        /// <param name="locator">Element to read.</param>
        /// <param name="timeoutSeconds">How long to wait for the element.</param>
        public async static Task<string?> Text(this ILocator locator, int timeoutSeconds = 1)
        {
            var tag = await locator.EvaluateAsync<string>("e => e.tagName.toLowerCase()");

            switch (tag)
            {
                case "input":
                case "textarea":
                case "select":
                    return await locator.InputValueAsync(new() { Timeout = timeoutSeconds * 1000 });
                default:
                    return await locator.TextContentAsync(new() { Timeout = timeoutSeconds * 1000 });
            }
        }

        /// <summary>
        /// Checks whether the element's text (see <see cref="Text"/>) contains the given string (ordinal comparison).
        /// </summary>
        public async static Task<bool> TextContains(this ILocator locator, string textToSearch)
        {
            var controltext = await locator.Text();
            if (controltext != null)
            {
                return controltext.Contains(textToSearch);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the value of the named attribute, or null if it is not set.
        /// </summary>
        public async static Task<string?> GetAttribute(this ILocator locator, string attributName) =>
            await locator.GetAttributeAsync(attributName);

    }
}

