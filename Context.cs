using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace Ozone
{
    /// <summary>
    /// Flow context.
    /// </summary>
    public record Context : IAsyncDisposable
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

        internal Context(
            IPlaywright playwright,
            IBrowser browser,
            IPage page,
            IFrame? frame,
            ILocator? element,
            IReadOnlyList<ILocator>? collection,
            ConcurrentDictionary<string, string> items)
        {

            Playwright = playwright;
            Browser = browser;
            Page = page;
            Frame = frame;
            Element = element;
            Collection = collection;
            Items = items ?? new();
        }

        /// <summary>
        /// Indicates that there is an element in the context.
        /// </summary>
        public bool HasElement => Element != null;

        /// <summary>
        /// Current page title (synchronous).
        /// </summary>
        public async Task<string> Title() => await (Page == null ? Task.FromResult(string.Empty) : Page.TitleAsync());


        /// <summary>
        /// Root "scope" to search in (frame or page).
        /// </summary>
        internal IPage EffectivePage => Page;
        internal IFrame EffectiveFrame => Frame ?? Page.MainFrame;

        /// <summary>
        /// Returns current context element text.
        /// </summary>
        public Task<string?> Text => 
            Element==null? Task.FromResult<string?>(null) : Element.Text();

        /// <summary>
        /// Returns current context element value attribute.
        /// </summary>
        public async Task<string?> Value()
        {
            if (Element != null)
            {
                return await Element.EvaluateAsync<string>("e => e.tagName.toLowerCase()") switch
                {
                    "input" or "textarea" or "select" => await Element.InputValueAsync(null),
                    _ => await Element.TextContentAsync(null),
                };
            }
            return null;
        }


        internal ILocator RootLocatorForSelector(string selector)
        {
            if (Frame != null)
            {
                return Frame.Locator(selector);
            }

            return Page.Locator(selector);
        }

        internal ILocator RootLocatorForXPath(string xpath)
        {
            string sel = $"xpath={xpath}";
            if (Frame != null)
            {
                return Frame.Locator(sel);
            }

            return Page.Locator(sel);
        }

        /// <summary>
        /// Returns context without Element or Collection.
        /// </summary>
        public Context EmptyContext() =>
            new(Playwright, Browser, Page, Frame, null, null, Items);

        /// <summary>
        /// Set context Element.
        /// </summary>
        internal Context NextElement(ILocator element) =>
            new(Playwright, Browser, Page, Frame, element, null, Items);

        /// <summary>
        /// Set context Collection.
        /// </summary>
        internal Context NextCollection(IReadOnlyList<ILocator> collection) =>
            new(Playwright, Browser, Page, Frame, null, collection, Items);


        /// <summary>
        /// Set problem.
        /// </summary>
        public Context CreateProblem(object problem)
        {
            problem ??= "Null passed as problem!";
            Flow.Log($"Problem created: {problem}");
            throw new OzoneException(problem.ToString());
        }

        public Context CreateProblem(Func<Context, object> f) => CreateProblem(f(this));


        /// <summary>
        /// Synchronous action bind.
        /// </summary>
        public Context Use(Action<Context> action)
        {
            if (action == null)
            {
                return CreateProblem($"{nameof(Use)}: NULL argument: {nameof(action)}");
            }

            try
            {
                action.Invoke(this);
                return this;
            }
            catch (Exception x)
            {
                return CreateProblem(x);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Browser != null)
            {
                await Browser.CloseAsync();
            }

            Playwright?.Dispose();

            return;
        }

        public static implicit operator string(Context c) => c.ToString();
    }
}