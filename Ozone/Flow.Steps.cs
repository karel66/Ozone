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
                if(await e.EvaluateAsync<string>("e => e.tagName") == "SELECT")
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
        /// Clicks the link and waits until the target page has the expected title. A link that
        /// opens a new window (<c>target="_blank"</c>) continues in that window. Waits up to
        /// <see cref="Context.FindTimeout"/> for the title.
        /// </summary>
        /// <returns>A context for the target page, without Element or Collection.</returns>
        public static Func<Context, Task<Context>> FollowLink(string selector, string targetTitle) =>
            async context =>
            {
                var link = await Find(selector)(context);

                return await Guard(context, async () =>
                {
                    var element = link.Element!;
                    Context target;

                    if(await element.GetAttributeAsync("target") == "_blank")
                    {
                        var popup = await context.Page.RunAndWaitForPopupAsync(() => element.ClickAsync());
                        target = context.NextPage(popup);
                    }
                    else
                    {
                        await element.ClickAsync();
                        target = context.EmptyContext();
                    }

                    try
                    {
                        await Assertions.Expect(target.Page).ToHaveTitleAsync(targetTitle,
                            new() { Timeout = (float)target.FindTimeout.TotalMilliseconds });
                    }
                    catch(PlaywrightException)
                    {
                        return target.CreateProblem($"{nameof(FollowLink)}: expected title '{targetTitle}', actual '{await target.Page.TitleAsync()}'");
                    }

                    return target;
                });
            };

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
                    var frame = await handle.ContentFrameAsync();

                    return frame == null
                        ? context.CreateProblem($"{nameof(EnterFrame)}: '{selector}' is not a frame")
                        : context.NextFrame(frame);
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
                var itemText = await item.Text();

                if(itemText != null && itemText.Contains(text))
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

        static string ContextItem(Context context, string stepName, string contextKey) =>
            context.Items.TryGetValue(contextKey, out string? text)
                ? text
                : throw new OzoneException($"{stepName}: context item '{contextKey}' not found");

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
                        : context.CreateProblem($"Expected {attributeName}='{expected}', actual {attributeName}='{actual}'");
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
