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
        public static Context CreateContext(
            BrowserBrand browserBrand,
            Uri startPageUrl,
            bool headless = true)
        {
            ArgumentNullException.ThrowIfNull(startPageUrl);

            var playwright = Sync.Run(() => Microsoft.Playwright.Playwright.CreateAsync());

            var browser = browserBrand switch
            {
                BrowserBrand.Chromium or BrowserBrand.Chrome or BrowserBrand.Edge =>
                    Sync.Run(() => playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless
                    })),

                BrowserBrand.Firefox =>
                    Sync.Run(() => playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless
                    })),

                BrowserBrand.Webkit =>
                    Sync.Run(() => playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless
                    })),

                _ => throw new NotSupportedException($"Browser brand {browserBrand} not supported.")
            };

            var page = Sync.Run(() => browser.NewPageAsync());
            Sync.Run(() => page.GotoAsync(startPageUrl.ToString()));

            return new Context(playwright, browser, page, null, null, null);
        }

        /// <summary>
        /// Disposes browser and Playwright resources synchronously.
        /// </summary>
        public static void Dispose(Context context)
        {
            try
            {
                if (context.Browser != null)
                {
                    Sync.Run(() => context.Browser.CloseAsync());
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
