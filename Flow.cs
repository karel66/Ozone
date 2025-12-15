/*
* Oxygen.Flow.Playwright.Sync library
*/

using Microsoft.Playwright;

namespace Ozone
{
    /// <summary>
    /// Base class for synchronous Playwright UI flows.
    /// </summary>
    public partial class Flow
    {
        protected Flow() { }

        /// <summary>
        /// Creates a synchronous Playwright context and navigates to the given URL.
        /// </summary>
        public async static Task<Context> CreateContext(
            BrowserBrand browserBrand,
            Uri startPageUrl,
            bool headless = true)
        {
            ArgumentNullException.ThrowIfNull(startPageUrl);

            var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            IBrowser browser;

            switch (browserBrand)
            {
                case BrowserBrand.Chromium:
                case BrowserBrand.Chrome:
                case BrowserBrand.Edge:
                    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless
                    });
                    break;

                case BrowserBrand.Firefox:
                    browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless
                    });
                    break;

                case BrowserBrand.Webkit:
                    browser = await playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless
                    });
                    break;

                default:
                    throw new NotSupportedException($"Browser brand {browserBrand} not supported.");
            }

            var page = await browser.NewPageAsync();
            await page.GotoAsync(startPageUrl.ToString());

            return new Context(playwright, browser, page, null, null, null);
        }

        /// <summary>
        /// Disposes browser and Playwright resources synchronously.
        /// </summary>
        public async static void Dispose(Context context)
        {
            try
            {
                if (context.Browser != null)
                {
                    await context.Browser.CloseAsync();
                }
            }
            catch (Exception x)
            {
                LogError("Error closing browser", x);
            }

            try
            {
                context.Playwright?.Dispose();
            }
            catch (Exception x)
            {
                LogError("Error disposing Playwright", x);
            }
        }
    }
}
