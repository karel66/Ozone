/*
* Oxygen.Flow.Playwright.Sync library
*/

using Microsoft.Playwright;

namespace Ozone
{
    /// <summary>
    /// Common UI testing steps for synchronous Playwright.
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// Run JavaScript in the page/frame.
        /// </summary>
        public static FlowStep Script(string script, params object[] args) =>
            context =>
            {
                if (context.Frame != null)
                {
                    Sync.Run(() => context.Frame.EvaluateAsync(script, args));
                }
                else
                {
                    Sync.Run(() => context.Page.EvaluateAsync(script, args));
                }
                return context;
            };


        /// <summary>
        /// Locates and switches to iframe by selector.
        /// </summary>
        public static FlowStep SwitchToFrame(string iframeSelector) =>
            context =>
            {
                var frameLocator = context.Page.FrameLocator(iframeSelector);
                // FrameLocator does not return ILocator, so we need to get the frame itself.
                // Use FrameLocator.First.Locator(":root") to get the root locator of the frame.
                var locator = frameLocator.Locator(":root");
                return context.NextElement(locator);
            };



        /// <summary>
        /// Switch back to main page (clear frame).
        /// </summary>
        public static Context SwitchToDefault(Context context) =>
            context.WithoutFrame();

        /// <summary>
        /// Executes the step only if the condition returns true.
        /// </summary>
        public static FlowStep If(Func<Context, bool> condition, FlowStep step) =>
            context => condition(context) ? step(context) : context;

        /// <summary>
        /// Executes the step while the condition returns true.
        /// </summary>
        public static FlowStep While(Func<Context, bool> condition, FlowStep step) =>
            context =>
            {
                var current = context;
                while (condition(current))
                {
                    current = step(current);
                    if (current.HasProblem)
                    {
                        break;
                    }
                }
                return current;
            };

        /// <summary>
        /// Retries until success or the given number of attempts has failed.
        /// </summary>
        public static bool Retry(Func<bool> success, int maxAttempts = 10)
        {
            ArgumentNullException.ThrowIfNull(success);

            int delay = 0;

            if (maxAttempts < 1)
            {
                maxAttempts = 1;
            }

            if (maxAttempts > 10)
            {
                maxAttempts = 10;
            }

            for (int i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    if (success())
                    {
                        return true;
                    }
                }
                catch (Exception x)
                {
                    LogError(x.Message);
                    Log($"RETRY [{i}]");
                    delay += 200;
                    System.Threading.Thread.Sleep(delay);
                }
            }

            return false;
        }

        /// <summary>
        /// Provides the step result context for action.
        /// </summary>
        public static FlowStep Use(FlowStep step, Action<Context> action) =>
            context =>
            {
                if (context.HasProblem)
                {
                    return context;
                }

                if (action == null)
                {
                    return context.CreateProblem($"{nameof(Use)}: NULL action argument.");
                }

                var result = step(context);
                if (!result.HasProblem)
                {
                    result.Use(action);
                }

                return result;
            };

        /// <summary>
        /// Provides the step result element for action.
        /// </summary>
        public static FlowStep Use(FlowStep step, Action<ILocator> action) =>
            context =>
            {
                if (context.HasProblem)
                {
                    return context;
                }

                if (action == null)
                {
                    return context.CreateProblem($"{nameof(Use)}: NULL action argument.");
                }

                var result = step(context);
                if (!result.HasProblem && result.Element != null)
                {
                    action(result.Element);
                }

                return result;
            };

        /// <summary>
        /// Provides current context for action.
        /// </summary>
        public static FlowStep Use(Action<Context> action) =>
            context =>
            {
                if (context.HasProblem)
                {
                    return context;
                }

                if (action == null)
                {
                    return context.CreateProblem($"{nameof(Use)}: NULL action argument.");
                }

                return context.Use(action);
            };

        /// <summary>
        /// Provides current context element for the action.
        /// </summary>
        public static FlowStep UseElement(Action<ILocator> action) =>
            context =>
            {
                if (context.HasProblem)
                {
                    return context;
                }

                if (action == null)
                {
                    return context.CreateProblem($"{nameof(UseElement)}: NULL action argument.");
                }

                if (context.Element != null)
                {
                    action(context.Element);
                }

                return context;
            };

        /// <summary>
        /// Clicks on the context element.
        /// </summary>
        public static Context Click(Context context)
        {
            if (context.Element == null)
            {
                return context.CreateProblem(LogError($"{nameof(Click)}: NULL element!"));
            }

            try
            {
                Sync.Run(() => context.Element.ClickAsync());
                return context;
            }
            catch (Exception x)
            {
                return context.CreateProblem(x);
            }
        }

        /// <summary>
        /// Click on page element returned by given flow step.
        /// </summary>
        public static FlowStep Click(FlowStep step) =>
            context => context | step | Click;

        /// <summary>
        /// Mouse click on page element returned by CSS selector.
        /// </summary>
        public static FlowStep Click(string selector) =>
            context =>
                context
                | Find(selector)
                | Click;

        /// <summary>
        /// Double-click on context element.
        /// </summary>
        public static Context DblClick(Context context)
        {
            var c = Click(context);
            if (c.HasProblem)
            {
                return c;
            }

            try
            {
                Sync.Run(() => c.Element.DblClickAsync());
                return c;
            }
            catch (Exception x)
            {
                return c.CreateProblem(x);
            }
        }

        /// <summary>
        /// Double-click element by selector.
        /// </summary>
        public static FlowStep DblClick(string selector) =>
            context => context | Find(selector) | DblClick;

        /// <summary>
        /// Sets text box, text area and combo text on page.
        /// </summary>
        public static FlowStep SetText(string selector, string text) =>
            context => context | Find(selector) | SetText(text);

        /// <summary>
        /// Sets current context element text.
        /// </summary>
        public static FlowStep SetText(string text) =>
            context =>
            {
                if (context.Element == null)
                {
                    return context.CreateProblem($"{nameof(SetText)}: missing context Element");
                }

                try
                {
                    Sync.Run(() => context.Element.FillAsync(text));
                    return context;
                }
                catch (Exception x)
                {
                    return context.CreateProblem(x);
                }
            };

        /// <summary>
        /// Send Enter key to the context element.
        /// </summary>
        public static Context PressEnter(Context context)
        {
            if (context.Element == null)
            {
                return context.CreateProblem("PressEnter: Missing context element");
            }

            try
            {
                Sync.Run(() => context.Element.PressAsync("Enter"));
                return context;
            }
            catch (Exception x)
            {
                return context.CreateProblem(x);
            }
        }

        /// <summary>
        /// Clicks the hyperlink and checks target window title.
        /// </summary>
        public static FlowStep FollowLink(string selector, string targetTitle) =>
            context =>
            {
                var c = context | Click(selector);
                if (c.HasProblem)
                {
                    return c;
                }

                Sync.Run(() => c.Page.WaitForLoadStateAsync(LoadState.NetworkIdle));

                string actual = Sync.Run(() => c.Page.TitleAsync());

                if (string.Equals(actual, targetTitle, StringComparison.InvariantCulture))
                {
                    return c;
                }

                return c.CreateProblem($"Expected title '{targetTitle}', actual '{actual}'");
            };

        public static FlowStep AssertAttributeValue(string attributeName, string expected) =>
            context =>
            {
                ArgumentNullException.ThrowIfNull(attributeName);

                if (!context.HasElement)
                {
                    return context.CreateProblem($"AssertAttributeValue {attributeName}='{expected}': Missing context element.");
                }

                string actual = Sync.Run(() => context.Element.GetAttributeAsync(attributeName));

                return actual == expected ?
                    context : context.CreateProblem($"Expected {attributeName}='{expected}', actual {attributeName}='{actual}'");
            };

        public static FlowStep Assertion(Predicate<Context> predicate, string errorMessage) =>
            context => predicate(context) ? context : context.CreateProblem(errorMessage);

        public static FlowStep Assertion(Predicate<Context> predicate, Func<Context, string> errorMessage) =>
            context => predicate(context) ? context : context.CreateProblem(errorMessage(context));

        public static FlowStep CreateProblem(object problem) =>
            context => context.CreateProblem(problem);

        public static FlowStep CreateProblem(Func<Context, object> problem) =>
            context => context.CreateProblem(problem(context));
    }
}
