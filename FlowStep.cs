/*
* Oxygen.Flow.Playwright library
*/

using System.Runtime.CompilerServices;

namespace Ozone
{
    public class FlowStep
    {
        private readonly Func<Task<Context>> _taskSource;

        private FlowStep(Func<Task<Context>> source)
        {
            _taskSource = source;
        }
        private FlowStep(Context context)
        {
            _taskSource = async () => context;
        }

        public static FlowStep From(Func<Task<Context>> source) => new(source);
        public static FlowStep From(Context context) => new(context);

        public Task<Context> RunAsync() => _taskSource();

        public TaskAwaiter<Context> GetAwaiter() => _taskSource().GetAwaiter(); // enables await chain

        public FlowStep Bind(Func<Context, Task<Context>> next)
        {
            var taskSource = _taskSource;

            return new FlowStep(async () =>
            {
                var context = await taskSource();

                if (context.HasProblem)
                {
                    return context;
                }
                else
                {
                    try
                    {
                        return await next(context);
                    }
                    catch (Exception ex)
                    {
                        return context.CreateProblem(ex);
                    }
                }
            });
        }

        public static FlowStep operator |(FlowStep step, Func<Context, Task<Context>> next) => step.Bind(next);

    }

}
