using Microsoft.Playwright;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace Ozone
{
    /// <summary>
    /// Flow context.
    /// </summary>
    public class Context : IAsyncDisposable
    {
        /// <summary>
        /// Playwright root object.
        /// </summary>
        public IPlaywright Playwright { get; }

        /// <summary>
        /// Browser instance.
        /// </summary>
        public IBrowser Browser { get; }

        /// <summary>
        /// Current page.
        /// </summary>
        public IPage Page { get; }

        /// <summary>
        /// Current frame (optional). If null, Page is used.
        /// </summary>
        public IFrame? Frame { get; }

        /// <summary>
        /// Current element.
        /// </summary>
        public ILocator? Element { get; }

        /// <summary>
        /// Collection of elements.
        /// </summary>
        public IReadOnlyList<ILocator>? Collection { get; }

        /// <summary>
        /// Dictionary for passing data items between AsyncSteps
        /// </summary>
        public ConcurrentDictionary<string, string> Items { get; }

        /// <summary>
        /// Default time finders wait for the first match to appear.
        /// </summary>
        public static readonly TimeSpan DefaultFindTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// How long finders wait for the first match before reporting it as not found.
        /// Carried over to every context derived from this one. See <see cref="WithFindTimeout"/>.
        /// </summary>
        public TimeSpan FindTimeout { get; }

        internal Context(
            IPlaywright playwright,
            IBrowser browser,
            IPage page,
            IFrame? frame,
            ILocator? element,
            IReadOnlyList<ILocator>? collection,
            ConcurrentDictionary<string, string> items,
            TimeSpan? findTimeout = null,
            bool ownsBrowser = true)
        {

            Playwright = playwright;
            Browser = browser;
            Page = page;
            Frame = frame;
            Element = element;
            Collection = collection;
            Items = items ?? new();
            FindTimeout = findTimeout ?? DefaultFindTimeout;
            OwnsBrowser = ownsBrowser;
        }

        /// <summary>
        /// Indicates that there is an element in the context.
        /// </summary>
        public bool HasElement => Element != null;

        /// <summary>
        /// Current page title.
        /// </summary>
        public Task<string> Title() => Page.TitleAsync();

        /// <summary>
        /// Returns the context element's text: the input value of an input, textarea or select
        /// element, otherwise its text content. Null if there is no context element.
        /// </summary>
        public Task<string?> TextAsync() =>
            Element == null ? Task.FromResult<string?>(null) : Element.Text();

        /// <summary>
        /// Returns the context element's text.
        /// </summary>
        [Obsolete("Use TextAsync().")]
        public Task<string?> Text => TextAsync();

        /// <summary>
        /// Returns the context element's text.
        /// </summary>
        [Obsolete("Use TextAsync(); it returns the same value.")]
        public Task<string?> Value() => TextAsync();


        internal ILocator RootLocatorForSelector(string selector)
        {
            if(Frame != null)
            {
                return Frame.Locator(selector);
            }

            return Page.Locator(selector);
        }

        internal ILocator RootLocatorForText(string text) =>
            Frame != null ? Frame.GetByText(text) : Page.GetByText(text);

        internal ILocator RootLocatorForXPath(string xpath)
        {
            string sel = $"xpath={xpath}";
            if(Frame != null)
            {
                return Frame.Locator(sel);
            }

            return Page.Locator(sel);
        }

        /// <summary>
        /// Returns context without Element or Collection.
        /// </summary>
        public Context EmptyContext() =>
            new(Playwright, Browser, Page, Frame, null, null, Items, FindTimeout, OwnsBrowser);

        /// <summary>
        /// Returns this context with a different finder timeout. Steps run on the returned
        /// context, and contexts derived from it, use the new timeout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The timeout is zero or negative.</exception>
        public Context WithFindTimeout(TimeSpan timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
            return new(Playwright, Browser, Page, Frame, Element, Collection, Items, timeout, OwnsBrowser);
        }

        /// <summary>
        /// Set context Element.
        /// </summary>
        internal Context NextElement(ILocator element) =>
            new(Playwright, Browser, Page, Frame, element, null, Items, FindTimeout, OwnsBrowser);

        /// <summary>
        /// Set context Frame.
        /// </summary>
        internal Context NextFrame(IFrame? frame) =>
            new(Playwright, Browser, Page, frame, null, null, Items, FindTimeout, OwnsBrowser);

        /// <summary>
        /// Continue on another page (e.g. a popup window), in its main frame.
        /// </summary>
        internal Context NextPage(IPage page) =>
            new(Playwright, Browser, page, null, null, null, Items, FindTimeout, OwnsBrowser);

        /// <summary>
        /// Set context Collection.
        /// </summary>
        internal Context NextCollection(IReadOnlyList<ILocator> collection) =>
            new(Playwright, Browser, Page, Frame, null, collection, Items, FindTimeout, OwnsBrowser);


        /// <summary>
        /// Logs the problem and throws <see cref="OzoneException"/>. Never returns;
        /// the <see cref="Context"/> return type only allows use in expressions such as
        /// <c>return context.CreateProblem(...)</c>.
        /// </summary>
        /// <remarks>
        /// An <see cref="OzoneException"/> is rethrown unchanged; any other exception
        /// becomes the inner exception so its type and stack trace are kept.
        /// </remarks>
        [DoesNotReturn]
        public Context CreateProblem(object problem)
        {
            problem ??= "Null passed as problem!";

            switch(problem)
            {
                case OzoneException ozone:
                    ExceptionDispatchInfo.Throw(ozone);
                    break;

                case Exception x:
                    Flow.Log($"Problem created: {x.GetType().Name}: {x.Message}");
                    throw new OzoneException(x.Message, x);
            }

            Flow.Log($"Problem created: {problem}");
            throw new OzoneException(problem.ToString());
        }

        /// <inheritdoc cref="CreateProblem(object)"/>
        [DoesNotReturn]
        public Context CreateProblem(Func<Context, object> f) => CreateProblem(f(this));


        /// <summary>
        /// Synchronous action bind.
        /// </summary>
        public Context Use(Action<Context> action)
        {
            if(action == null)
            {
                return CreateProblem($"{nameof(Use)}: NULL argument: {nameof(action)}");
            }

            try
            {
                action.Invoke(this);
                return this;
            }
            catch(Exception x)
            {
                return CreateProblem(x);
            }
        }

        /// <summary>
        /// True when disposing this context closes the browser and Playwright (contexts from
        /// <see cref="Flow.CreateContext"/>). False when the browser is shared, e.g. by tests;
        /// disposing then closes only this page's browser context.
        /// </summary>
        internal bool OwnsBrowser { get; }

        bool _disposed = false;

        /// <summary>
        /// Closes the page and browser and shuts down Playwright.
        /// All contexts derived from the same CreateContext call share these resources,
        /// so disposing any one of them disposes them for all.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if(_disposed)
            {
                return;
            }

            _disposed = true;
            Trace.TraceInformation("Context: DisposeAsync()");

            if(!OwnsBrowser)
            {
                await Page.Context.CloseAsync();
                return;
            }

            try
            {
                await Page.CloseAsync();
            }
            finally
            {
                try
                {
                    await Browser.CloseAsync();
                }
                finally
                {
                    Playwright.Dispose();
                }
            }
        }
    }
}
