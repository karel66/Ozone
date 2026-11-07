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
        /// Launches a browser, opens a page and navigates to the given URL.
        /// </summary>
        /// <param name="browserBrand">Browser to launch. Chrome and Edge require the branded browser to be installed.</param>
        /// <param name="startPageUrl">URL to navigate to.</param>
        /// <param name="headless">Run the browser without a visible window.</param>
        /// <param name="ignoreHttpsErrors">Disable TLS certificate validation. Use only for test environments with self-signed certificates.</param>
        public async static Task<Context> CreateContext(BrowserBrand browserBrand, Uri startPageUrl, bool headless = true, bool ignoreHttpsErrors = false)
        {
            ArgumentNullException.ThrowIfNull(startPageUrl);

            Log(startPageUrl.ToString());

            var playwright = await Playwright.CreateAsync();
            IBrowser? browser = null;

            try
            {
                var options = new BrowserTypeLaunchOptions
                {
                    Headless = headless
                };

                browser = browserBrand switch
                {
                    BrowserBrand.Chromium => await playwright.Chromium.LaunchAsync(options),
                    BrowserBrand.Chrome => await playwright.Chromium.LaunchAsync(WithChannel(options, "chrome")),
                    BrowserBrand.Edge => await playwright.Chromium.LaunchAsync(WithChannel(options, "msedge")),
                    BrowserBrand.Firefox => await playwright.Firefox.LaunchAsync(options),
                    BrowserBrand.Webkit => await playwright.Webkit.LaunchAsync(options),
                    _ => throw new NotSupportedException($"Browser brand {browserBrand} not supported.")
                };

                if(ignoreHttpsErrors)
                {
                    Log("WARNING: HTTPS certificate errors are ignored.");
                }

                // Closing the browser also closes the page, so page needs no separate cleanup on failure.
                var page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = ignoreHttpsErrors });

                await page.GotoAsync(startPageUrl.ToString());

                return new Context(playwright, browser, page, null, null, null, new());
            }
            catch
            {
                if(browser != null)
                {
                    try { await browser.CloseAsync(); } catch(Exception x) { LogError("Failed to close browser", x); }
                }

                playwright.Dispose();
                throw;
            }
        }

        static BrowserTypeLaunchOptions WithChannel(BrowserTypeLaunchOptions options, string channel) =>
            new(options) { Channel = channel };

    }
}
