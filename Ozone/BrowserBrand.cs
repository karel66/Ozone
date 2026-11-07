namespace Ozone
{
    /// <summary>
    /// Enumerates supported browsers for Playwright.
    /// </summary>
    public enum BrowserBrand
    {
        /// <summary>Playwright's bundled Chromium.</summary>
        Chromium,
        /// <summary>Installed Google Chrome (Playwright channel <c>chrome</c>).</summary>
        Chrome,
        /// <summary>Installed Microsoft Edge (Playwright channel <c>msedge</c>).</summary>
        Edge,
        /// <summary>Playwright's bundled Firefox.</summary>
        Firefox,
        /// <summary>Playwright's bundled WebKit.</summary>
        Webkit
    }
}
