/*
* Oxygen.Flow.Playwright.Sync library
*/

using Microsoft.Playwright;
using System.Text;

namespace Ozone
{
    /// <summary>
    /// Flow context with monadic Bind for synchronous Playwright usage.
    /// Immutable value type.
    /// </summary>
    public readonly record struct Context
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
        public IFrame Frame { get; }

        /// <summary>
        /// Current element.
        /// </summary>
        public ILocator Element { get; }

        /// <summary>
        /// Collection of elements.
        /// </summary>
        public IReadOnlyList<ILocator> Collection { get; }

        /// <summary>
        /// Arbitrary user data.
        /// </summary>
        public object UserCredentials { get; }

        /// <summary>
        /// Problem / error value.
        /// </summary>
        public object Problem { get; }

        internal Context(
            IPlaywright playwright,
            IBrowser browser,
            IPage page,
            IFrame frame,
            ILocator element,
            IReadOnlyList<ILocator> collection,
            object problem = null,
            object credentials = null)
        {
            Playwright = playwright;
            Browser = browser;
            Page = page;
            Frame = frame;
            Element = element;
            Collection = collection;
            Problem = problem;
            UserCredentials = credentials;
        }

        /// <summary>
        /// Indicates that there is a problem in the context.
        /// </summary>
        public bool HasProblem => Problem != null;

        /// <summary>
        /// Indicates that there is an element in the context.
        /// </summary>
        public bool HasElement => Element != null;

        /// <summary>
        /// Current page title (synchronous).
        /// </summary>
        public string Title {
            get {
                var page = Page;
                if (page == null)
                {
                    return null;
                }
                else
                {
                    return Sync.Run(() => page.TitleAsync());
                }
            }
        }

        /// <summary>
        /// Root "scope" to search in (frame or page).
        /// </summary>
        internal IPage EffectivePage => Page;
        internal IFrame EffectiveFrame => Frame ?? Page.MainFrame;

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
            new(Playwright, Browser, Page, Frame, null, null, null, UserCredentials);

        /// <summary>
        /// Set context Element.
        /// </summary>
        internal Context NextElement(ILocator element) =>
            new(Playwright, Browser, Page, Frame, element, null, null, UserCredentials);

        /// <summary>
        /// Set context Collection.
        /// </summary>
        internal Context NextCollection(IReadOnlyList<ILocator> collection) =>
            new(Playwright, Browser, Page, Frame, null, collection, null, UserCredentials);

        /// <summary>
        /// Set frame.
        /// </summary>
        internal Context WithFrame(IFrame frame) =>
            new(Playwright, Browser, Page, frame, Element, Collection, Problem, UserCredentials);

        /// <summary>
        /// Clear frame.
        /// </summary>
        internal Context WithoutFrame() =>
            new(Playwright, Browser, Page, null, Element, Collection, Problem, UserCredentials);

        /// <summary>
        /// Set problem.
        /// </summary>
        public Context CreateProblem(object problem)
        {
            problem ??= "Null passed as problem!";
            Flow.Log($"Problem created: {problem}");
            return new Context(Playwright, Browser, Page, Frame, Element, Collection, problem, UserCredentials);
        }

        /// <summary>
        /// Monadic bind.
        /// </summary>
        public Context Bind(FlowStep step)
        {
            if (HasProblem)
            {
                return this;
            }

            if (step == null)
            {
                return CreateProblem($"{nameof(Bind)}: NULL argument: {nameof(step)}");
            }

            string signature = $"{ExtractMethodName(step.Method.Name)} ({FormatTarget(step.Target)})";
            Flow.Log(signature);

            try
            {
                return step.Invoke(this);
            }
            catch (Exception x)
            {
                return CreateProblem(x);
            }
        }

        /// <summary>
        /// Synchronous action bind.
        /// </summary>
        public Context Use(Action<Context> action)
        {
            if (HasProblem)
            {
                return this;
            }

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

        static string FormatTarget(object target)
        {
            StringBuilder args = new();

            if (target != null)
            {
                Type type = target.GetType();

                foreach (var field in type.GetFields())
                {
                    object argval = field.GetValue(target);

                    if (argval == null)
                    {
                        args.AppendWithComma($"{field.Name}=null");
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        args.AppendWithComma($"{field.Name}=\"{argval}\"");
                    }
                    else if (field.FieldType == typeof(char))
                    {
                        args.AppendWithComma($"{field.Name}='{argval}'");
                    }
                    else if (field.FieldType.IsValueType)
                    {
                        args.AppendWithComma($"{field.Name}={argval}");
                    }
                    else if (field.FieldType.IsArray)
                    {
                        args.AppendWithComma($"{field.Name}=[");
                        foreach (object value in (Array)argval)
                        {
                            args.Append($"\"{value}\", ");
                        }
                        args.Append(']');
                    }
                    else if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    {
                        args.AppendWithComma($"{field.Name}=[{field.FieldType.Name}]");
                    }
                    else
                    {
                        args.AppendWithComma($"{field.Name}={{");
                        foreach (var prop in field.FieldType.GetProperties().Select(p => p.Name))
                        {
                            try
                            {
                                object val = field.FieldType
                                    .InvokeMember(prop, System.Reflection.BindingFlags.GetProperty, null, argval, null, null);
                                args.Append($"{prop}:\"{val}\", ");
                            }
                            catch (Exception x)
                            {
                                args.Append($"{prop}:\"<Exception of type {x.GetType().Name}>\", ");
                            }
                        }
                        args.Append('}');
                    }
                }
            }
            return args.ToString();
        }

        static string ExtractMethodName(string reflectedName)
        {
            string name = reflectedName;
            if (!string.IsNullOrEmpty(name) && name[0] == '<')
            {
                name = name[1..name.IndexOf('>')];
            }
            return name;
        }

        public override string ToString()
        {
            if (HasProblem)
            {
                return Problem.ToString();
            }

            if (Page != null)
            {
                return Page.Url;
            }

            return "Uninitialized Context";
        }

        public static implicit operator string(Context c) => c.ToString();

        /// <summary>
        /// Overloaded | operator for Context.Bind(FlowStep)
        /// </summary>
        public static Context operator |(Context a, FlowStep b) => a.Bind(b);
    }
}
