## 1) Concise architecture summary

**What it is**  
`Ozone` is a small .NET/C# library that provides a lightweight DSL on top of **Microsoft Playwright** for building UI automation as composable async “steps”. A step is typically `Func<Context, Task<Context>>`, where `Context` carries Playwright objects (Playwright/Browser/Page/optional Frame) plus a “current” `Element` / `Collection` and an `Items` dictionary for passing data between steps.

**Main modules**
- **Runtime state**
  - `Context.cs`: immutable-ish `record Context : IAsyncDisposable` holding `IPlaywright`, `IBrowser`, `IPage`, optional `IFrame`, current `ILocator? Element`, `IReadOnlyList<ILocator>? Collection`, and `ConcurrentDictionary<string,string> Items`. Provides helpers `NextElement`, `NextCollection`, `EmptyContext`, and `CreateProblem()` (throws `OzoneException`).
- **Step chaining**
  - `AsyncStep.cs`: linked-list step container that can `Bind(Context)` and chain via operator `|`. Includes tracing via `MethodTrace()` and reflection-based capture printing (`FormatTarget`).
- **Flow DSL (static helpers)**
  - `Flow.cs`: creates Playwright + browser + page and navigates to a start URL (`CreateContext`), plus `Close`.
  - `Flow.Absolute.cs`: page/frame-level finders and existence checks (`Find`, `FindAll`, XPath variants, `Exists`, `IfExists`).
  - `Flow.Relative.cs`: element-relative finders (`RelativeFind*`).
  - `Flow.Steps.cs`: actions and control flow (`Click`, `SetText`, `PressEnter`, `While`, `If`, `Retry`, `Script`, `SwitchToFrame`, assertions, collection filters, etc.).
  - `Flow.Logging.cs`: console logging helpers.

**Entrypoints**
- Primary “startup”: `Flow.CreateContext(BrowserBrand, Uri startPageUrl, bool headless = true)` in `Flow.cs`.
- Typical execution: call steps directly or chain using `AsyncStep` + `|`:
  - Direct: `ctx = await Flow.Find("#id")(ctx);`
  - Chained: `await (ctx | (new AsyncStep(Flow.Find("#id")) | Flow.Click | ...));`
- Cleanup: `await Flow.Close(ctx)` or `await ctx.DisposeAsync()`.

---

## 2) Top 10 risks/issues (security, correctness, performance, maintainability)

1) **TLS/certificate validation disabled by default (security)**
   - `browser.NewPageAsync(new() { IgnoreHTTPSErrors = true })` silently accepts bad certs → MITM and test false-positives.
   - File: `Flow.cs`

2) **High risk of leaking secrets/PII into logs (security)**
   - `AsyncStep.MethodTrace()` uses `FormatTarget(_step.Target)` which reflects closure fields and prints raw string values (passwords/tokens commonly captured by lambdas).
   - Files: `AsyncStep.cs`, logging to console via `Flow.Logging.cs`

3) **`Use(step, Action<Context>)` is a functional bug (correctness)**
   - The overload never calls `action(result)`; it just returns `result`. Callers will think their side-effect ran.
   - File: `Flow.Steps.cs`

4) **`SwitchToFrame` does not actually set `Context.Frame` (correctness/flakiness)**
   - It sets `Element` to a `FrameLocator(...).Locator(":root")` but leaves `Context.Frame` null. Absolute finders (`RootLocatorForSelector/XPath`) scope by `Context.Frame`, so subsequent “global” finds are not truly frame-scoped as implied.
   - Files: `Flow.Steps.cs`, `Context.cs`, `Flow.Absolute.cs`

5) **Inconsistent waiting semantics across finders (flakiness)**
   - `Find/FindAll/FindOnXPath` wait for `.First.WaitForAsync`, but `FindByText` does not wait; `FindAllOnXPath` doesn’t wait before counting.
   - File: `Flow.Absolute.cs`

6) **`Retry` can hot-loop when `success()` returns `false` (performance/test load)**
   - Delay is applied every iteration, but only after the attempt; more importantly it doesn’t distinguish “fast false” vs exceptions, and has fixed max 10. Also no cancellation token.
   - File: `Flow.Steps.cs`

7) **`Items[...]` indexer can throw non-domain exceptions (correctness/diagnostics)**
   - `FirstContainingContextItem` / `LastContainingContextItem` do `context.Items[contextKey]` → `KeyNotFoundException` instead of a domain error with context.
   - File: `Flow.Steps.cs`

8) **Disposal likely incomplete / potentially leaky (resource mgmt)**
   - `Context.DisposeAsync()` closes the **browser**, disposes Playwright, but never explicitly closes the **page** (usually OK) and doesn’t handle multiple pages/contexts if expanded later. Also no try/finally around partial creation in `CreateContext`.
   - Files: `Context.cs`, `Flow.cs`

9) **Project targets `net10.0` (maintainability/adoption risk)**
   - `net10.0` is not generally available/stable for many consumers/CI environments; makes the library harder to use.
   - File: `Ozone.csproj`

10) **Library depends on `Microsoft.Playwright.MSTest` (dependency hygiene)**
   - This couples a general DSL library to MSTest-flavored package; can pull test-related dependencies and constrain consumers.
   - File: `Ozone.csproj`

---

## 3) Prioritized plan of next actions (smallest safe steps first)

1) **Stop logging closure-captured values by default (security)**
   - Change `AsyncStep.MethodTrace()/FormatTarget` to *not* print field values. Options:
     - log only method name; or
     - redact all strings (e.g., `"***"`), or only show type names.
   - Files: `AsyncStep.cs`, `Flow.Logging.cs`

2) **Make HTTPS validation safe-by-default**
   - Add an `ignoreHttpsErrors = false` parameter to `Flow.CreateContext(...)` and pass it into `NewPageAsync`.
   - File: `Flow.cs`

3) **Fix the broken `Use(step, Action<Context>)` overload**
   - Invoke `action(result)` and consider try/catch to wrap with `CreateProblem`.
   - File: `Flow.Steps.cs`

4) **Harden `Items` access**
   - Use `TryGetValue` and throw a clear domain error via `CreateProblem($"Missing item '{contextKey}'")`.
   - File: `Flow.Steps.cs`

5) **Normalize finder waiting behavior**
   - Decide and document a consistent contract:
     - all `Find*` wait up to a timeout, or
     - provide `Find*` (wait) and `TryFind*` (no-throw / short wait) variants.
   - File: `Flow.Absolute.cs`

6) **Fix frame-scoping model**
   - Either:
     - implement `SwitchToFrame` to locate the actual `IFrame` and set `Context.Frame` (add `NextFrame(IFrame)`), or
     - remove `Frame` from `Context` and scope strictly via locators.
   - Files: `Context.cs`, `Flow.Steps.cs`, `Flow.Absolute.cs`

7) **Improve Retry API**
   - Add cancellation token; configurable backoff; ensure it waits between attempts regardless of false/exception; optionally return diagnostics (attempt count, last exception).
   - File: `Flow.Steps.cs`

8) **Make resource creation/disposal more robust**
   - Wrap `CreateContext` in try/catch/finally to dispose partially created objects on failure (e.g., navigation failure).
   - Consider using a browser context (`IBrowser.NewContextAsync`) for isolation if this library grows.
   - Files: `Flow.cs`, `Context.cs`

9) **Retarget framework**
   - Move to `net8.0` (or multi-target `net8.0;net9.0`) unless there is a hard requirement for `net10.0`.
   - File: `Ozone.csproj`

10) **Decouple from MSTest package**
   - Replace `Microsoft.Playwright.MSTest` with `Microsoft.Playwright` in the library; keep MSTest integration in a separate test project/package.
   - File: `Ozone.csproj`

---

## 4) Secrets / unsafe data callout (explicit)

- **No hard-coded API keys/passwords are visible** in the provided snapshot.
- **However, there is a strong risk of accidental secret leakage to logs**:
  - `AsyncStep.FormatTarget()` reflects closure fields and prints string values unredacted, which can include credentials/tokens/PII passed into lambdas (common in UI automation).
  - Files: `AsyncStep.cs` (FormatTarget/MethodTrace), output via `Flow.Logging.cs`
- **Unsafe transport default**:
  - `IgnoreHTTPSErrors = true` disables TLS verification by default.
  - File: `Flow.cs`