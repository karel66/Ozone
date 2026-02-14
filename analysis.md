## 1) Concise architecture summary

**What it is**  
`Ozone` is a small C#/.NET library that builds Playwright UI automation as composable async “steps” of shape `Func<Context, Task<Context>>`. A `Context` record carries Playwright objects (Playwright/Browser/Page/Frame) plus a “current” `Element`/`Collection` and a shared `Items` dictionary for passing data between steps.

**Main modules**
- **Context / runtime state**
  - `Context.cs`: `record Context : IAsyncDisposable` holding `IPlaywright`, `IBrowser`, `IPage`, optional `IFrame`, current `ILocator? Element`, `IReadOnlyList<ILocator>? Collection`, and `ConcurrentDictionary<string,string> Items`. Provides helper methods to create derived contexts (`NextElement`, `NextCollection`, `EmptyContext`) and error creation (`CreateProblem` throws `OzoneException`).
- **Step chaining**
  - `AsyncStep.cs`: wraps a single step and links to the next (linked-list). `Bind(Context)` executes the chain. Overloads `|` to chain steps. Includes reflective method/closure tracing.
- **Flow DSL**
  - `Flow.cs`: creates Playwright/browser/page and navigates to a start URL; provides `Close(Context)`. Defines timeouts.
  - `Flow.Absolute.cs`: “global” finders on page/frame (`Find`, `FindAll`, XPath variants, `Exists`, `IfExists`).
  - `Flow.Relative.cs`: finders relative to the current `Context.Element`.
  - `Flow.Steps.cs`: actions/control flow helpers (`Click`, `SetText`, `PressEnter`, `While`, `If`, `Retry`, `Script`, `SwitchToFrame`, assertions, collection filters).
  - `Flow.Logging.cs`: console logging.
- **Utilities**
  - `ExtensionMethods.cs`: locator text/value/attribute helpers.

**Entrypoints**
- Primary startup: `Flow.CreateContext(BrowserBrand, Uri startPageUrl, bool headless=true)` (`Flow.cs`)
- Execution patterns:
  - Call step directly: `context = await Flow.Find("#id")(context);`
  - Chain via `AsyncStep`: `await (context | (new AsyncStep(Flow.Find("#id")) | Flow.Click | ...));`
- Cleanup: `await Flow.Close(context)` or `await context.DisposeAsync()` (`Context : IAsyncDisposable`)

---

## 2) Top 10 risks / issues (with file references)

1) **TLS certificate validation disabled**
   - `browser.NewPageAsync(new() { IgnoreHTTPSErrors = true })` makes MITM/cert issues invisible and is unsafe as a library default.
   - File: `Flow.cs`

2) **Potential secret exfiltration via reflective logging**
   - `AsyncStep.MethodTrace()` logs `FormatTarget(_step.Target)` which reflects closure fields and prints string values unredacted (passwords/tokens captured in lambdas can end up in CI logs).
   - Files: `AsyncStep.cs`, `Flow.Logging.cs`

3) **Frame switching is likely incorrect / misleading**
   - `SwitchToFrame` uses `FrameLocator(...).Locator(":root")` and stores it as `Element`, but does **not** set `Context.Frame`. Absolute finders (`RootLocatorForSelector/XPath`) rely on `Context.Frame` to scope searches, so subsequent “global” finds won’t be frame-scoped as the API implies.
   - Files: `Flow.Steps.cs`, `Context.cs`, `Flow.Absolute.cs`

4) **`Use(step, Action<Context>)` ignores the provided action**
   - The overload checks for null, runs the step, and returns result without invoking `action(result)`—callers will assume side-effects occur but they won’t.
   - File: `Flow.Steps.cs`

5) **Retry behavior: delays only on exceptions, not on ordinary failure**
   - `Retry(Func<Task<bool>> ...)` backs off only when an exception is thrown. If the function returns `false` repeatedly, it hot-loops with no delay.
   - File: `Flow.Steps.cs`

6) **Inconsistent waiting semantics across finders**
   - `Find/FindAll/FindOnXPath` wait for `locator.First.WaitForAsync(FindTimeout)`, but `FindByText` does not wait, and `FindAllOnXPath` counts without a wait. This creates flaky timing differences depending on which API is used.
   - File: `Flow.Absolute.cs`

7) **`Items[...]` indexer can throw `KeyNotFoundException`**
   - `FirstContainingContextItem` / `LastContainingContextItem` use `context.Items[contextKey]` without `TryGetValue`, producing non-actionable runtime crashes.
   - File: `Flow.Steps.cs`

8) **Project targets `net10.0` (preview / adoption risk)**
   - `TargetFramework` is `net10.0` which is not broadly available/stable in most CI and consumer environments yet; increases friction unnecessarily for a utility library.
   - File: `Ozone.csproj`

9) **Package reference is MSTest-flavored Playwright**
   - Library references `Microsoft.Playwright.MSTest`; as a general-purpose DSL library, this couples to MSTest unnecessarily and may bring unwanted dependencies.
   - File: `Ozone.csproj`

10) **Overly broad exception handling + console logging in core path**
   - Many steps catch `Exception` and immediately throw via `CreateProblem` after logging to console; hard to integrate with structured logging or test frameworks without noisy output.
   - Files: `Flow.Steps.cs`, `Context.cs`, `Flow.Logging.cs`

---

## 3) Prioritized plan of next actions (smallest safe steps first)

1) **Stop leaking captured values in step tracing (security first)**
   - Change `AsyncStep.MethodTrace()` / `FormatTarget` to avoid printing closure field values by default (log only method name, or redact strings, or add an explicit opt-in flag).
   - Files: `AsyncStep.cs`, `Flow.Logging.cs`

2) **Make TLS behavior safe-by-default**
   - Add parameter `ignoreHttpsErrors = false` to `Flow.CreateContext(...)` and pass it to `NewPageAsync`. Default should be `false`.
   - File: `Flow.cs`

3) **Fix the `Use(step, Action<Context>)` bug**
   - Invoke `action(result)` (ideally wrapped with try/catch -> `CreateProblem` for consistency).
   - File: `Flow.Steps.cs`

4) **Fix `Retry` to avoid hot loops**
   - Add delay/backoff when `success()` returns `false` (not only on exception). Consider supporting cancellation token and configurable base delay.
   - File: `Flow.Steps.cs`

5) **Harden `Items` access**
   - Replace `context.Items[contextKey]` with `TryGetValue` and throw `CreateProblem($"Missing item '{contextKey}'")` when absent.
   - File: `Flow.Steps.cs`

6) **Normalize finder waiting semantics**
   - Decide a consistent contract: all `Find*` methods wait (with shared default timeout) or provide explicit “TryFind/NoWait” variants.
   - File: `Flow.Absolute.cs`

7) **Repair iframe/frame model**
   - Decide whether `Context.Frame` is the authoritative scope:
     - If yes: implement `SwitchToFrame` to actually set `Frame` in the context (new `NextFrame(IFrame frame)`), and ensure absolute searches use `Frame`.
     - If no: remove `Frame` from `Context` and scope everything through locators consistently.
   - Files: `Context.cs`, `Flow.Steps.cs`, `Flow.Absolute.cs`

8) **Retarget framework to stable LTS**
   - Move to `net8.0` (optionally multi-target `net8.0;net9.0`) unless you have a concrete `net10` requirement.
   - File: `Ozone.csproj`

9) **Decouple from MSTest package**
   - Use `Microsoft.Playwright` unless MSTest-specific features are required; keep MSTest integration in a separate test project/package.
   - File: `Ozone.csproj`

10) **Add minimal tests/examples**
   - Add a small test project validating: chaining, `Use` actually runs, retry delays, finders wait consistently, frame switching works.
   - New: `Ozone.Tests/*` (not currently present)

---

## 4) Secrets / unsafe data callout (explicit)

- **No hard-coded credentials or API keys are visible** in the provided snapshot.
- **High risk of accidental secret leakage to logs:** `AsyncStep.FormatTarget()` reflects and prints captured closure fields (including raw strings) which can include passwords/tokens/PII passed into step lambdas. This will be written to console via `Flow.Log`.
  - Files: `AsyncStep.cs`, `Flow.Logging.cs`
- **Unsafe transport default:** `IgnoreHTTPSErrors = true` disables TLS certificate validation by default.
  - File: `Flow.cs`