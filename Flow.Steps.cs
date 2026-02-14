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
        public static Func<Context, Task<Context>> Script(string script, params object[] args) =>
            async context =>
            {
                if (context.Frame != null)
                {
                    await context.Frame.EvaluateAsync(script, args);
                }
                else
                {
                    await context.Page.EvaluateAsync(script, args);
                }
                return context;
            };


        /// <summary>
        /// Locates and switches to iframe by selector.
        /// </summary>
        public static Func<Context, Task<Context>> SwitchToFrame(string iframeSelector) =>
            async context =>
            {
                var frameLocator = context.Page.FrameLocator(iframeSelector);
                // FrameLocator does not return ILocator, so we need to get the frame itself.
                // Use FrameLocator.First.Locator(":root") to get the root locator of the frame.
                var locator = frameLocator.Locator(":root");
                return context.NextElement(locator);
            };

        /// <summary>
        /// Executes the step only if the condition returns true.
        /// </summary>
        public static Func<Context, Task<Context>> If(Func<Context, Task<bool>> condition, Func<Context, Task<Context>> step) =>
            async context => await condition(context) ? await step(context) : context;

        /// <summary>
        /// Executes the step while the condition returns true.
        /// </summary>
        public static Func<Context, Task<Context>> While(Func<Context, Task<bool>> condition, Func<Context, Task<Context>> step) =>
            async context =>
            {
                var current = context;

                while (await condition(current))
                {
                    current = await step(current);
                }

                return current;
            };

        public static Func<Context, Task<Context>> While(Func<Context, Task<bool>> condition, AsyncStep step) =>
            async context =>
            {
                var current = context;

                while (current != null && await condition(current))
                {
                    current = (await step.Bind(current)).Context;
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

        public async static Task<bool> Retry(Func<Task<bool>> success, int maxAttempts = 10)
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
                    if (await success())
                    {
                        return true;
                    }
                }
                catch (Exception x)
                {
                    LogError(x.Message);
                    Log($"RETRY [{i}]");
                    delay += 200;
                    await Task.Delay(delay);
                }
            }

            return false;
        }

        /// <summary>
        /// Provides the step result context for action.
        /// </summary>
        public static Func<Context, Task<Context>> Use(Func<Context, Task<Context>> step, Action<Context> action) =>
            async context =>
            {
                if (action == null)
                {
                    return context.CreateProblem($"{nameof(Use)}: NULL action argument.");
                }

                var result = await step(context);

                return result;
            };

        /// <summary>
        /// Provides the step result element for action.
        /// </summary>
        public static Func<Context, Task<Context>> Use(Func<Context, Task<Context>> step, Action<ILocator> action) =>
            async context =>
            {
                if (action == null)
                {
                    return context.CreateProblem($"{nameof(Use)}: NULL action argument.");
                }

                var result = await step(context);

                if (result.Element != null)
                {
                    action(result.Element);
                }

                return result;
            };

        /// <summary>
        /// Provides current context element for the action.
        /// </summary>
        public static Func<Context, Task<Context>> UseElement(Action<ILocator> action) =>
            async context =>
            {
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
        public async static Task<Context> Click(Context context)
        {
            if (context.Element == null)
            {
                return context.CreateProblem(LogError($"{nameof(Click)}: NULL element!"));
            }

            try
            {
                await context.Element.ClickAsync(new() { Timeout = 60000 });
                return context;
            }
            catch (Exception x)
            {
                return context.CreateProblem(x);
            }
        }

        /// <summary>
        /// Mouse click on page element returned by CSS selector.
        /// </summary>

        public static Func<Context, Task<Context>> Click(string selector) =>
            async context => await Click(await Find(selector)(context));


        //public static AsyncStep Click(string selector) =>
        //        new AsyncStep(Find(selector)) | Click;

        /// <summary>
        /// Double-click on context element.
        /// </summary>
        public async static Task<Context> DblClick(Context context)
        {
            if (context.Element != null)
            {
                await context.Element.ClickAsync();
                await context.Element.DblClickAsync();
            }
            return context;
        }

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
                var c1 = await Find(selector)(context);
                return await SetText(text)(c1);
            };


        /// <summary>
        /// Sets current context element text.
        /// </summary>
        public static Func<Context, Task<Context>> SetText(string text) =>
            async context =>
            {
                if (context.Element == null)
                {
                    return context.CreateProblem($"{nameof(SetText)}: missing context Element");
                }

                try
                {
                    var tag = await context.Element.EvaluateAsync("el=>el.tagName");

                    switch (tag.ToString())
                    {
                        case "SELECT":
                            await context.Element.ClickAsync();
                            await context.Element.SelectOptionAsync(text);
                            break;

                        default:
                            await context.Element.FillAsync(text);
                            break;
                    }
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
        public async static Task<Context> PressEnter(Context context)
        {
            if (context.Element == null)
            {
                return context.CreateProblem("PressEnter: Missing context element");
            }

            try
            {
                await context.Element.PressAsync("Enter");
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
        public static Func<Context, Task<Context>> FollowLink(string selector, string targetTitle) =>
            async context =>
            {
                var c = await Click(selector)(context);

                await context.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                string actual = await context.Page.TitleAsync();

                if (string.Equals(actual, targetTitle, StringComparison.InvariantCulture))
                {
                    return context;
                }

                context.CreateProblem($"Expected title '{targetTitle}', actual '{actual}'");

                return context;
            };


        static Func<Context, Task<Context>> CollectionFilter(Func<IReadOnlyList<ILocator>, Task<ILocator?>> filter) =>
            async context =>
            {
                if (filter == null)
                {
                    return context.CreateProblem("CollectionFilter: NULL filter passed");
                }

                if (context.Collection == null)
                {
                    return context.CreateProblem("CollectionFilter: missing collection");
                }

                int count = context.Collection.Count;
                if (count == 0)
                {
                    return context.CreateProblem("CollectionFilter: empty collection");
                }

                ILocator? child = await filter(context.Collection);

                if (child == null)
                {
                    return context.CreateProblem($"CollectionFilter: no matches in {count} item(s)");
                }

                return context.NextElement(child);
            };


        public static Func<Context, Task<Context>> FirstContainingText(string text) =>
           async context =>
           {
               if (context.Collection == null)
               {
                   return context.CreateProblem("Missing collection in context!");
               }

               foreach (var current in context.Collection)
               {
                   var itemText = await current.Text();

                   if (itemText != null && itemText.Contains(text))
                   {
                       return context.NextElement(current);
                   }
               }

               return context.CreateProblem($"Text '{text}' not found in the context collection."); ;
           };

        public static Func<Context, Task<Context>> FirstContainingContextItem(string contextKey) =>
          async context =>
          {
              if (context.Collection == null)
              {
                  return context.CreateProblem("Missing collection in context!");
              }

              var text = context.Items[contextKey];

              foreach (var current in context.Collection)
              {
                  var itemText = await current.Text();

                  if (itemText != null && itemText.Contains(text))
                  {
                      return context.NextElement(current);
                  }
              }

              return context.CreateProblem($"Text '{text}' not found in the context collection."); ;
          };


        /// <summary>
        /// Returns element from context Collection
        /// </summary>
        public static Func<Context, Task<Context>> LastContainingText(string text) =>
            async context =>
            {
                if (context.Collection == null)
                {
                    return context.CreateProblem("Missing collection in context!");
                }

                ILocator? last = null;

                foreach (var current in context.Collection)
                {
                    var itemText = await current.Text();

                    if (itemText != null && itemText.Contains(text))
                    {
                        last = current;
                    }
                }

                if (last != null)
                {
                    return context.NextElement(last);
                }

                return context.CreateProblem($"Text '{text}' not found in the context collection."); ;
            };

        public static Func<Context, Task<Context>> LastContainingContextItem(string contextKey) =>
           async context =>
           {
               if (context.Collection == null)
               {
                   return context.CreateProblem("Missing collection in context!");
               }

               ILocator? last = null;

               var text = context.Items[contextKey];

               foreach (var current in context.Collection)
               {
                   var itemText = await current.Text();

                   if (itemText != null && itemText.Contains(text))
                   {
                       last = current;
                   }
               }

               if (last != null)
               {
                   return context.NextElement(last);
               }

               return context.CreateProblem($"Text '{text}' not found in the context collection."); ;
           };

        /// <summary>
        /// Select display text in the combobox context element
        /// </summary>
        public static AsyncStep SelectComboText(string selector, string value) =>
            new AsyncStep(Find(selector)) | SelectComboText(value);

        /// <summary>
        /// Select display text in the combobox context element
        /// </summary>
        public static Func<Context, Task<Context>> SelectComboText(string value) =>
        async (Context context) =>
        {
            ILocator? combo = context.Element;

            if (combo == null)
            {
                return context.CreateProblem($"{nameof(SelectComboText)}: Missing context element");
            }

            var items = combo.SelectOptionAsync([value]).Result;

            if (items == null || items.Count == 0)
            {
                return context.CreateProblem($"Can't find combo text '{value}'");
            }

            return context;
        };


        public static Func<Context, Task<Context>> AssertAttributeValue(string attributeName, string expected) =>
            async context =>
            {
                ArgumentNullException.ThrowIfNull(attributeName);

                if (context.Element == null)
                {
                    return context.CreateProblem($"AssertAttributeValue {attributeName}='{expected}': Missing context element.");
                }

                string? actual = await context.Element.GetAttributeAsync(attributeName);

                return actual == expected ?
                    context : context.CreateProblem($"Expected {attributeName}='{expected}', actual {attributeName}='{actual}'");
            };

        public static Func<Context, Task<Context>> Assertion(Predicate<Context> predicate, string errorMessage) =>
            async context => predicate(context) ? context : context.CreateProblem(errorMessage);

        public static Func<Context, Task<Context>> Assertion(Predicate<Context> predicate, Func<Context, string> errorMessage) =>
            async context => predicate(context) ? context : context.CreateProblem(errorMessage(context));

        public static Func<Context, Task<Context>> AssertAsync(Func<Context, Task<bool>> predicate, string errorMessage) =>
            async context => await predicate(context) ? context : context.CreateProblem(errorMessage);

        public static Func<Context, Task<Context>> AssertAsync(Func<Context, Task<bool>> predicate, Func<Context, Task<string>> errorMessage) =>
            async context => await predicate(context) ? context : context.CreateProblem(await errorMessage(context));


        public static Func<Context, Task<Context>> CreateProblem(object problem) =>
            async context => context.CreateProblem(problem);

        public static Func<Context, Task<Context>> CreateProblem(Task<object> problem) =>
            async context => context.CreateProblem(await problem);

        public static Func<Context, Task<Context>> CreateProblem(Func<Context, object> problem) =>
            async context => context.CreateProblem(problem(context));
    }
}