using System.Diagnostics.CodeAnalysis;
using Microsoft.Playwright;

namespace Ozone
{
    /// <summary>
    /// Actions, collection filters, assertions and control flow.
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// Default for <c>maxIterations</c> in <see cref="While(Func{Context, Task{bool}}, Func{Context, Task{Context}}, int)"/>.
        /// </summary>
        public const int DefaultMaxIterations = 1000;

        /// <summary>
        /// Runs the body and turns any exception other than <see cref="OzoneException"/>
        /// into a problem, so steps only ever fail with <see cref="OzoneException"/>.
        /// </summary>
        static async Task<Context> Guard(Context context, Func<Task<Context>> body)
        {
            try
            {
                return await body();
            }
            catch(Exception x)
            {
                return context.CreateProblem(x);
            }
        }

        // --- Control flow ----------------------------------------------------

        /// <summary>
        /// Executes the step only if the condition returns true.
        /// </summary>
        public static Func<Context, Task<Context>> If(Func<Context, Task<bool>> condition, Func<Context, Task<Context>> step) =>
            context => Guard(context, async () => await condition(context) ? await step(context) : context);

        /// <summary>
        /// Executes the step while the condition returns true.
        /// </summary>
        /// <param name="condition">Checked before each run of the step.</param>
        /// <param name="step">Step to repeat; each run gets the previous run's result.</param>
        /// <param name="maxIterations">Fails with <see cref="OzoneException"/> if the condition is still true after this many runs.</param>
        public static Func<Context, Task<Context>> While(Func<Context, Task<bool>> condition, Func<Context, Task<Context>> step, int maxIterations = DefaultMaxIterations)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1);

            return context => Guard(context, async () =>
            {
                var current = context;

                for(int i = 0; await condition(current); i++)
                {
                    if(i == maxIterations)
                    {
                        return current.CreateProblem($"{nameof(While)}: condition still true after {maxIterations} iteration(s)");
                    }

                    current = await step(current);
                }

                return current;
            });
        }

        /// <summary>
        /// Executes the step chain while the condition returns true.
        /// </summary>
        /// <inheritdoc cref="While(Func{Context, Task{bool}}, Func{Context, Task{Context}}, int)"/>
        public static Func<Context, Task<Context>> While(Func<Context, Task<bool>> condition, AsyncStep step, int maxIterations = DefaultMaxIterations)
        {
            ArgumentNullException.ThrowIfNull(step);

            // Bind fails if any step in the chain returns no context, so Context is set here.
            return While(condition, async c => (await step.Bind(c)).Context!, maxIterations);
        }

        /// <summary>
        /// Calls <paramref name="success"/> until it returns true or <paramref name="maxAttempts"/>
        /// attempts have failed. An exception counts as a failed attempt. The wait between
        /// attempts starts at 200 ms and grows by 200 ms, up to 2 s.
        /// </summary>
        /// <returns>True if an attempt succeeded.</returns>
        public async static Task<bool> Retry(Func<Task<bool>> success, int maxAttempts = 10)
        {
            ArgumentNullException.ThrowIfNull(success);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

            for(int attempt = 1; ; attempt++)
            {
                try
                {
                    if(await success())
                    {
                        return true;
                    }
                }
                catch(Exception x)
                {
                    LogError($"RETRY [{attempt}] failed", x);
                }

                if(attempt == maxAttempts)
                {
                    LogError($"RETRY FAILED after {maxAttempts} attempt(s)");
                    return false;
                }

                Log($"RETRY [{attempt}]");
                await Task.Delay(RetryDelay(attempt));
            }
        }

        /// <summary>
        /// Runs the step until it succeeds or <paramref name="maxAttempts"/> attempts have failed,
        /// waiting between attempts as <see cref="Retry(Func{Task{bool}}, int)"/> does. Each attempt
        /// starts from the same context. If every attempt fails, the last failure is raised as
        /// <see cref="OzoneException"/>.
        /// </summary>
        public static Func<Context, Task<Context>> Retry(Func<Context, Task<Context>> step, int maxAttempts = 10)
        {
            ArgumentNullException.ThrowIfNull(step);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

            return async context =>
            {
                for(int attempt = 1; ; attempt++)
                {
                    try
                    {
                        return await step(context);
                    }
                    catch(Exception x) when(attempt < maxAttempts)
                    {
                        LogError($"RETRY [{attempt}] failed", x);
                    }
                    catch(Exception x)
                    {
                        LogError($"RETRY FAILED after {maxAttempts} attempt(s)");
                        return context.CreateProblem(x);
                    }

                    await Task.Delay(RetryDelay(attempt));
                }
            };
        }

        static TimeSpan RetryDelay(int attempt) => TimeSpan.FromMilliseconds(Math.Min(200 * attempt, 2000));

        /// <summary>
        /// Provides the step result context for action.
        /// </summary>
        public static Func<Context, Task<Context>> Use(Func<Context, Task<Context>> step, Action<Context> action) =>
            async context =>
            {
                if(action == null)
                {
                    return context.CreateProblem($"{nameof(Use)}: NULL action argument.");
                }

                return await Guard(context, async () =>
                {
                    var result = await step(context);

                    action(result);

                    return result;
                });
            };

        // --- Actions ---------------------------------------------------------

        /// <summary>
        /// Run JavaScript in the page/frame.
        /// </summary>
        public static Func<Context, Task<Context>> Script(string script, params object[] args) =>
            context => Guard(context, async () =>
            {
                if(context.Frame != null)
                {
                    await context.Frame.EvaluateAsync(script, args);
                }
                else
                {
                    await context.Page.EvaluateAsync(script, args);
                }

                return context;
            });

        /// <summary>
        /// Clicks on the context element.
        /// </summary>
        public static Task<Context> Click(Context context) =>
            OnElement(context, nameof(Click), e => e.ClickAsync(new() { Timeout = 60000 }));

        /// <summary>
        /// Mouse click on page element returned by CSS selector.
        /// </summary>
        public static Func<Context, Task<Context>> Click(string selector) =>
            async context => await Click(await Find(selector)(context));

        /// <summary>
        /// Double-click on context element.
        /// </summary>
        public static Task<Context> DblClick(Context context) =>
            OnElement(context, nameof(DblClick), e => e.DblClickAsync());

        /// <summary>
        /// Double-click element by selector.
        /// </summary>
        public static AsyncStep DblClick(string selector) =>
            new AsyncStep(Find(selector)) | DblClick;

        /// <summary>
        /// Sets text box, text area and combo text on page.
        /// </summary>
        public static Func<Context, Task<Context>> SetText(string selector, string text) =>
            async context =>
            {
                var found = await Find(selector)(context);
                return await SetText(text)(found);
            };

        /// <summary>
        /// Sets current context element text. For a <c>select</c> element, chooses the option
        /// whose value or label equals the text.
        /// </summary>
        public static Func<Context, Task<Context>> SetText(string text) =>
            context => OnElement(context, nameof(SetText), async e =>
            {
                if(await e.EvaluateAsync<string>("e => e.tagName.toLowerCase()") == "select")
                {
                    await e.SelectOptionAsync(text);
                }
                else
                {
                    await e.FillAsync(text);
                }
            });

        /// <summary>
        /// Send Enter key to the context element.
        /// </summary>
        public static Task<Context> PressEnter(Context context) =>
            OnElement(context, nameof(PressEnter), e => e.PressAsync("Enter"));

        /// <summary>
        /// Requires a context element, runs the action on it and returns the context.
        /// </summary>
        static async Task<Context> OnElement(Context context, string stepName, Func<ILocator, Task> action)
        {
            if(context.Element == null)
            {
                return context.CreateProblem($"{stepName}: missing context Element");
            }

            return await Guard(context, async () =>
            {
                await action(context.Element);
                return context;
            });
        }

        /// <summary>
        /// Clicks the link and waits until the document it navigates to has loaded and has the
        /// expected title. All the waits together take at most <see cref="Context.FindTimeout"/>.
        /// </summary>
        /// <remarks>
        /// The flow continues where the link's target says the new document will open. The target
        /// comes from the link (the found element or its enclosing <c>a</c>/<c>area</c>), or for a
        /// submit button from its <c>formtarget</c> or its form's <c>target</c>, and failing that
        /// from the page's <c>base</c> element. Any other element (for example a button that
        /// navigates by script) is treated as targeting its own frame.
        /// <list type="bullet">
        /// <item><c>_self</c> or no target: the frame the element is in.</item>
        /// <item><c>_parent</c>: that frame's parent; <c>_top</c>: the top-level page.</item>
        /// <item>A name: the frame or window with that name, if one exists; otherwise a new window.</item>
        /// <item><c>_blank</c>: a new window.</item>
        /// </list>
        /// Windows opened by script (<c>window.open</c>) are not followed.
        /// </remarks>
        /// <returns>
        /// A context for the target page, without Element or Collection. Its Frame is the target
        /// frame, or null when the target is the page's main frame.
        /// </returns>
        public static Func<Context, Task<Context>> FollowLink(string selector, string targetTitle) =>
            async context =>
            {
                var link = await Find(selector)(context);

                return await Guard(context, async () =>
                {
                    var element = link.Element!;
                    var deadline = DateTime.UtcNow + context.FindTimeout;
                    float Remaining() => (float)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);

                    var origin = context.Frame ?? context.Page.MainFrame;
                    string targetName = await element.EvaluateAsync<string>(LinkTargetScript);
                    var existing = await ResolveTarget(context, origin, targetName);

                    IPage page;
                    IFrame frame;

                    if(existing is { } t)
                    {
                        (page, frame) = t;

                        try
                        {
                            await ClickAndWaitForNavigation(element, page, frame, Remaining);
                        }
                        catch(TimeoutException x)
                        {
                            return FollowLinkProblem(context, "could not click the link", x);
                        }
                    }
                    else
                    {
                        try
                        {
                            page = await context.Page.RunAndWaitForPopupAsync(() => element.ClickAsync(), new() { Timeout = Remaining() });
                        }
                        catch(TimeoutException x)
                        {
                            return FollowLinkProblem(context, $"link with target '{targetName}' did not open a new window", x);
                        }

                        frame = page.MainFrame;
                    }

                    var target = context.NextPage(page).NextFrame(frame == page.MainFrame ? null : frame);

                    try
                    {
                        await frame.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = Remaining() });
                    }
                    catch(TimeoutException x)
                    {
                        return FollowLinkProblem(target, "target page did not finish loading", x);
                    }

                    try
                    {
                        await frame.WaitForFunctionAsync(TitleMatchesScript, targetTitle, new() { Timeout = Remaining() });
                    }
                    catch(Exception x) when(x is TimeoutException or PlaywrightException)
                    {
                        return target.CreateProblem($"{nameof(FollowLink)}: expected title '{targetTitle}', actual '{await frame.TitleAsync()}'");
                    }

                    return target;
                });
            };

        [DoesNotReturn]
        static Context FollowLinkProblem(Context context, string message, Exception x)
        {
            message = $"{nameof(FollowLink)}: {message}";
            Log($"Problem created: {message}");
            return context.CreateProblem(new OzoneException(message, x));
        }

        /// <summary>
        /// Clicks the element and, if that starts a navigation of the frame, waits until the
        /// frame has committed the new document, so later waits do not see the old one.
        /// </summary>
        static async Task ClickAndWaitForNavigation(ILocator element, IPage page, IFrame frame, Func<float> remaining)
        {
            var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool started = false;

            void OnRequest(object? _, IRequest r)
            {
                if(r.IsNavigationRequest && r.Frame == frame) started = true;
            }

            void OnFailed(object? _, IRequest r)
            {
                if(r.IsNavigationRequest && r.Frame == frame) committed.TrySetException(
                    new OzoneException($"{nameof(FollowLink)}: navigation to {r.Url} failed: {r.Failure}"));
            }

            void OnNavigated(object? _, IFrame f)
            {
                if(f == frame) committed.TrySetResult();
            }

            page.Request += OnRequest;
            page.RequestFailed += OnFailed;
            page.FrameNavigated += OnNavigated;

            try
            {
                await element.ClickAsync(new() { Timeout = remaining() });

                if(started)
                {
                    var finished = await Task.WhenAny(committed.Task, Task.Delay(TimeSpan.FromMilliseconds(remaining())));

                    if(finished != committed.Task)
                    {
                        throw new OzoneException($"{nameof(FollowLink)}: navigation did not complete in time");
                    }

                    await committed.Task;
                }
            }
            finally
            {
                page.Request -= OnRequest;
                page.RequestFailed -= OnFailed;
                page.FrameNavigated -= OnNavigated;
            }
        }

        /// <summary>
        /// Finds the frame a click with the given target navigates, or null if it opens a new window.
        /// </summary>
        static async Task<(IPage Page, IFrame Frame)?> ResolveTarget(Context context, IFrame origin, string target)
        {
            if(target.Length == 0 || target.Equals("_self", StringComparison.OrdinalIgnoreCase))
            {
                return (context.Page, origin);
            }

            if(target.Equals("_parent", StringComparison.OrdinalIgnoreCase))
            {
                return (context.Page, origin.ParentFrame ?? origin);
            }

            if(target.Equals("_top", StringComparison.OrdinalIgnoreCase))
            {
                return (context.Page, context.Page.MainFrame);
            }

            if(target.Equals("_blank", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // A named target: an existing frame or window with that name, searching this page first.
            var pages = context.Page.Context.Pages.Where(p => p != context.Page && !p.IsClosed).Prepend(context.Page);

            foreach(var page in pages)
            {
                foreach(var frame in page.Frames)
                {
                    try
                    {
                        if(await frame.EvaluateAsync<string>("() => window.name") == target)
                        {
                            return (page, frame);
                        }
                    }
                    catch(PlaywrightException)
                    {
                        // Frame detached or navigating; it can't be the target.
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the target of the element's link, or of the submit button's form, falling back
        /// to the document's base element; an empty string means the element's own frame.
        /// Uses getAttribute so SVG links (whose <c>target</c> property is not a string) work too.
        /// </summary>
        const string LinkTargetScript = """
            e => {
                const base = () => document.querySelector('base[target]')?.getAttribute('target') || '';
                const link = e.closest('a, area');
                if (link) {
                    return link.hasAttribute('href') ? (link.getAttribute('target') || base()) : '';
                }
                const button = e.closest('button, input');
                if (button && button.form && (button.type === 'submit' || button.type === 'image')) {
                    return button.getAttribute('formtarget') || button.form.getAttribute('target') || base();
                }
                return '';
            }
            """;

        /// <summary>
        /// True when the document title equals the expected title, ignoring surrounding and repeated whitespace.
        /// </summary>
        const string TitleMatchesScript =
            "t => document.title.replace(/\\s+/g, ' ').trim() === t.replace(/\\s+/g, ' ').trim()";

        /// <summary>
        /// Finds the <c>iframe</c> (or <c>frame</c>) element by CSS selector and continues inside it:
        /// finders and <see cref="Script"/> then work on the frame's document.
        /// </summary>
        public static Func<Context, Task<Context>> EnterFrame(string selector) =>
            async context =>
            {
                var frameElement = await Find(selector)(context);

                return await Guard(context, async () =>
                {
                    var handle = await frameElement.Element!.ElementHandleAsync();

                    try
                    {
                        var frame = await handle.ContentFrameAsync();

                        return frame == null
                            ? context.CreateProblem($"{nameof(EnterFrame)}: '{selector}' is not a frame")
                            : context.NextFrame(frame);
                    }
                    finally
                    {
                        await handle.DisposeAsync();
                    }
                });
            };

        /// <summary>
        /// Leaves the current frame and continues in its parent frame, or in the page
        /// if the frame is a direct child of the page.
        /// </summary>
        public static async Task<Context> ExitFrame(Context context)
        {
            if(context.Frame == null)
            {
                return context.CreateProblem($"{nameof(ExitFrame)}: not inside a frame");
            }

            var parent = context.Frame.ParentFrame;

            return context.NextFrame(parent == null || parent == context.Page.MainFrame ? null : parent);
        }

        // --- Collection filters ----------------------------------------------

        /// <summary>
        /// Requires a context Collection and selects the element chosen by the filter.
        /// </summary>
        static Func<Context, Task<Context>> CollectionFilter(string stepName, Func<Context, Task<ILocator?>> filter, Func<Context, string> notFound) =>
            async context =>
            {
                if(context.Collection == null)
                {
                    return context.CreateProblem($"{stepName}: missing context Collection");
                }

                return await Guard(context, async () =>
                {
                    var match = await filter(context);

                    return match != null
                        ? context.NextElement(match)
                        : context.CreateProblem($"{stepName}: {notFound(context)}");
                });
            };

        /// <summary>
        /// Returns the first (or last) element of the collection whose text contains the given text.
        /// </summary>
        static async Task<ILocator?> ContainingText(IReadOnlyList<ILocator> collection, string text, bool last)
        {
            ILocator? match = null;

            foreach(var item in collection)
            {
                if(await item.TextContains(text))
                {
                    match = item;

                    if(!last)
                    {
                        break;
                    }
                }
            }

            return match;
        }

        static string ContextItem(Context context, string stepName, string contextKey)
        {
            if(context.Items.TryGetValue(contextKey, out string? text))
            {
                return text;
            }

            context.CreateProblem($"{stepName}: context item '{contextKey}' not found");
            throw new System.Diagnostics.UnreachableException();
        }

        /// <summary>
        /// Selects the first element in the context Collection whose text contains the given text.
        /// </summary>
        public static Func<Context, Task<Context>> FirstContainingText(string text) =>
            CollectionFilter(nameof(FirstContainingText),
                c => ContainingText(c.Collection!, text, last: false),
                c => $"text '{text}' not found in {c.Collection!.Count} item(s)");

        /// <summary>
        /// Selects the last element in the context Collection whose text contains the given text.
        /// </summary>
        public static Func<Context, Task<Context>> LastContainingText(string text) =>
            CollectionFilter(nameof(LastContainingText),
                c => ContainingText(c.Collection!, text, last: true),
                c => $"text '{text}' not found in {c.Collection!.Count} item(s)");

        /// <summary>
        /// Selects the first element in the context Collection whose text contains the value stored in <see cref="Context.Items"/> under the given key.
        /// </summary>
        public static Func<Context, Task<Context>> FirstContainingContextItem(string contextKey) =>
            CollectionFilter(nameof(FirstContainingContextItem),
                c => ContainingText(c.Collection!, ContextItem(c, nameof(FirstContainingContextItem), contextKey), last: false),
                c => $"text of context item '{contextKey}' not found in {c.Collection!.Count} item(s)");

        /// <summary>
        /// Selects the last element in the context Collection whose text contains the value stored in <see cref="Context.Items"/> under the given key.
        /// </summary>
        public static Func<Context, Task<Context>> LastContainingContextItem(string contextKey) =>
            CollectionFilter(nameof(LastContainingContextItem),
                c => ContainingText(c.Collection!, ContextItem(c, nameof(LastContainingContextItem), contextKey), last: true),
                c => $"text of context item '{contextKey}' not found in {c.Collection!.Count} item(s)");

        // --- Assertions ------------------------------------------------------

        /// <summary>
        /// Fails unless the context element's attribute has the expected value.
        /// </summary>
        public static Func<Context, Task<Context>> AssertAttributeValue(string attributeName, string expected)
        {
            ArgumentNullException.ThrowIfNull(attributeName);

            return async context =>
            {
                if(context.Element == null)
                {
                    return context.CreateProblem($"{nameof(AssertAttributeValue)}: missing context Element");
                }

                return await Guard(context, async () =>
                {
                    string? actual = await context.Element.GetAttributeAsync(attributeName);

                    return actual == expected
                        ? context
                        : context.CreateProblem($"{nameof(AssertAttributeValue)}: expected {attributeName}='{expected}', actual {attributeName}='{actual}'");
                });
            };
        }

        /// <summary>
        /// Fails with the given message unless the predicate holds for the context.
        /// </summary>
        public static Func<Context, Task<Context>> Assertion(Predicate<Context> predicate, string errorMessage) =>
            context => Guard(context, () => Task.FromResult(predicate(context) ? context : context.CreateProblem(errorMessage)));

        /// <summary>
        /// Fails with a message built from the context unless the predicate holds.
        /// </summary>
        public static Func<Context, Task<Context>> Assertion(Predicate<Context> predicate, Func<Context, string> errorMessage) =>
            context => Guard(context, () => Task.FromResult(predicate(context) ? context : context.CreateProblem(errorMessage(context))));

        /// <summary>
        /// Fails with the given message unless the async predicate returns true.
        /// </summary>
        public static Func<Context, Task<Context>> AssertAsync(Func<Context, Task<bool>> predicate, string errorMessage) =>
            context => Guard(context, async () => await predicate(context) ? context : context.CreateProblem(errorMessage));

        /// <summary>
        /// Fails with a message built from the context unless the async predicate returns true.
        /// </summary>
        public static Func<Context, Task<Context>> AssertAsync(Func<Context, Task<bool>> predicate, Func<Context, Task<string>> errorMessage) =>
            context => Guard(context, async () => await predicate(context) ? context : context.CreateProblem(await errorMessage(context)));

        // --- Explicit failure ------------------------------------------------

        /// <summary>
        /// Step that always fails with the given problem (see <see cref="Context.CreateProblem(object)"/>).
        /// </summary>
        public static Func<Context, Task<Context>> CreateProblem(object problem) =>
            async context => context.CreateProblem(problem);

        /// <summary>
        /// Step that always fails with the problem produced by the task.
        /// </summary>
        public static Func<Context, Task<Context>> CreateProblem(Task<object> problem) =>
            context => Guard(context, async () => context.CreateProblem(await problem));

        /// <summary>
        /// Step that always fails with a problem built from the context.
        /// </summary>
        public static Func<Context, Task<Context>> CreateProblem(Func<Context, object> problem) =>
            context => Guard(context, () => Task.FromResult(context.CreateProblem(problem(context))));
    }
}
