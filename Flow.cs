using Microsoft.Playwright;

namespace Ozone
{
    /// <summary>
    /// Base class for Playwright UI flows.
    /// </summary>
    public partial class Flow
    {
        static LocatorWaitForOptions FiveSecTimeout = new LocatorWaitForOptions { Timeout = 5000, State = WaitForSelectorState.Attached };

        static LocatorWaitForOptions FindTimeout = new LocatorWaitForOptions { Timeout = 10000, State = WaitForSelectorState.Attached };

        protected Flow() { }

        /// <summary>
        /// Creates a synchronous Playwright context and navigates to the given URL.
        /// </summary>
        public async static Task<Context> CreateContext(BrowserBrand browserBrand, Uri startPageUrl, bool headless = true)
        {
            ArgumentNullException.ThrowIfNull(startPageUrl);

            Log(startPageUrl.ToString());

            var playwright = await Playwright.CreateAsync();

            IBrowser browser;

            var options = new BrowserTypeLaunchOptions
            {
                Headless = headless
            };

            switch (browserBrand)
            {
                case BrowserBrand.Chromium:
                case BrowserBrand.Chrome:
                case BrowserBrand.Edge:
                    browser = await playwright.Chromium.LaunchAsync(options);
                    break;

                case BrowserBrand.Firefox:
                    browser = await playwright.Firefox.LaunchAsync(options);
                    break;

                case BrowserBrand.Webkit:
                    browser = await playwright.Webkit.LaunchAsync(options);
                    break;

                default:
                    throw new NotSupportedException($"Browser brand {browserBrand} not supported.");
            }
            ;

            var page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true });

            await page.GotoAsync(startPageUrl.ToString());

            return new Context(playwright, browser, page, null, null, null, new());
        }

    }
}
