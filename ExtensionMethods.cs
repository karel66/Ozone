using Microsoft.Playwright;
using System.Text;

namespace Ozone
{
    public static class ExtensionMethods
    {
        public static StringBuilder AppendWithComma(this StringBuilder sb, string text)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
            return sb.Append(text);
        }

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

        public async static Task<string?> GetAttribute(this ILocator locator, string attributName) =>
            await locator.GetAttributeAsync(attributName);

    }
}

