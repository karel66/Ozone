## 1) Concise architecture summary

**What it is**  
`Ozone` is a small .NET library that wraps **Microsoft Playwright** with a lightweight DSL for composing UI automation as async “steps”. A step is typically `Func<Context, Task<Context>>`, where `Context` carries Playwright runtime objects plus the “current” element/collection and a shared `Items` dictionary.

**Main modules**
- **Runtime state**
  - `Context.cs`: `record Context : IAsyncDisposable` containing `IPlaywright`, `IBrowser`, `IPage`, optional `IFrame`, current `ILocator? Element`, `IReadOnlyList<ILocator>? Collection`, and `ConcurrentDictionary<string,string> Items`. Provides “next” context helpers (`NextElement`, `NextCollection`, `NextFrame`) and `CreateProblem()` which throws `OzoneException`.
- **Step chaining / execution**
  - `AsyncStep.cs`: a linked-list style chain of steps with `Bind(Context)` execution and operator `|` for chaining. Logs a “method trace” before executing each step.
- **Flow DSL**
  - `Flow.cs`: creates Playwright + browser + page and navigates to a start URL (`CreateContext`), plus `Close`.
  - `Flow.Absolute.cs`: global finders on page/frame: `Find`, `FindAll`, XPath variants, `Exists`, `IfExists`, etc.
  - `Flow.Relative.cs`: relative finders scoped to `Context.Element`.
  - `Flow.Steps.cs`: user actions / control flow: `Click`, `SetText`, `PressEnter`, `If`, `While`, `Retry`, assertions, collection filters, JS `Script`.
  - `Flow.Logging.cs`: simple console logger.
- **Misc**
  - `ExtensionMethods.cs`: helper extensions for `ILocator.Text()`, `TextContains()`, etc.
  - `BrowserBrand.cs`, `OzoneException.cs`.

**Entrypoints**
- Primary entry: `Flow.CreateContext(BrowserBrand, Uri, bool headless=true)` (`Flow.cs`)
- Execution pattern:
  - Directly run steps: `ctx = await Flow.Find("#id")(ctx);`
  - Chain with `AsyncStep` and `|`: `await (ctx | (new AsyncStep(Flow.Find("#id")) | Flow.Click | ...));`
- Cleanup: `await Flow.Close(ctx)` or `await ctx.DisposeAsync()` (`Context.cs`)

---

## 2) Top 10 risks/issues (with file references)

1) **TLS verification disabled (security / correctness)**
   - `IgnoreHTTPSErrors = true` on every page created, making MITM possible and hiding cert problems.
   - File: `Flow.cs`

2) **High likelihood of leaking secrets/PII to logs (security)**
   - `AsyncStep.MethodTrace()` logs `FormatTarget(_step.Target)` which reflects closure fields and prints *string values* (passwords/tokens frequently live in captured variables).
   - Files: `AsyncStep.cs` (`FormatTarget`, `MethodTrace`), output via `Flow.Logging.cs`

3) **Bug: `Use(step, Action<Context>)` never invokes the action (correctness)**
   - The overload returns `result` without calling `action(result)`.
   - File: `Flow.Steps.cs` (`Use(Func<Context,Task<Context>>, Action<Context>)`)

4) **Frame scoping is inconsistent / misleading (correctness, flakiness)**
   - `Context` supports `Frame` and has `NextFrame`, but no step actually sets `Frame`. Any “switch to frame” behavior is incomplete or absent, so “global” finders may run on the page instead of intended frame.
   - Files: `Context.cs` (`Frame`, `RootLocatorForSelector/XPath`, `NextFrame`), DSL in `Flow.Absolute.cs`, `Flow.Steps.cs` (no real frame switch)

5) **Inconsistent waiting semantics across finders (flakiness)**
   - `Find/FindAll/FindOnXPath` wait with `WaitForAsync`, but `FindByText` does not wait at all; `FindAllOnXPath` counts without a wait.
   - File: `Flow.Absolute.cs`

6) **`Retry` API is constrained and can create noisy/hot retry patterns (performance / diagnosability)**
   - Hard-caps `maxAttempts` to 10; linear delay grows but no cancellation token; logs errors without surfacing last exception; retries even on non-transient issues.
   - File: `Flow.Steps.cs` (`Retry`)

7) **Unsafe dictionary indexing causes non-domain exceptions (correctness / UX)**
   - `LastContainingContextItem` uses `context.Items[contextKey]` and can throw `KeyNotFoundException` instead of `OzoneException` with helpful context.
   - File: `Flow.Steps.cs` (`LastContainingContextItem`)

8) **Resource creation lacks safety on partial failures (correctness / leaks)**
   - `CreateContext` does not use try/finally: if `NewPageAsync` or `GotoAsync` fails, browser/playwright may leak.
   - File: `Flow.cs`

9) **Library targets `net10.0` (maintainability / ecosystem compatibility)**
   - Many environments/CI and consumers won’t have `net10.0` available; reduces adoption and testability.
   - File: `Ozone.csproj`

10) **Package reference couples library to MSTest-specific Playwright package (dependency hygiene)**
   - `Microsoft.Playwright.MSTest` is typically for test projects; the core library should depend on `Microsoft.Playwright`.
   - File: `Ozone.csproj`

---

## 3) Prioritized plan of next actions (smallest safe steps first)

1) **Stop leaking secrets in step tracing (must-do security)**
   - Change `AsyncStep.MethodTrace()` / `FormatTarget` to avoid printing captured field *values*.
   - Minimal safe change: log only method name, or only field names/types; optionally add explicit redaction for strings.
   - Files: `AsyncStep.cs`, `Flow.Logging.cs`

2) **Make HTTPS validation safe-by-default**
   - Add parameter `ignoreHttpsErrors = false` to `Flow.CreateContext(...)` and pass through to `NewPageAsync`.
   - Keep current behavior only when explicitly enabled.
   - File: `Flow.cs`

3) **Fix `Use(step, Action<Context>)` bug**
   - Call `action(result)` before returning; wrap action exceptions via `CreateProblem`.
   - File: `Flow.Steps.cs`

4) **Normalize `Items` access errors**
   - Replace direct indexing with `TryGetValue` and throw `OzoneException` via `CreateProblem` when missing.
   - File: `Flow.Steps.cs` (`LastContainingContextItem`)

5) **Unify finder behavior (reduce flakiness)**
   - Decide: “all Find* wait” vs “explicit WaitFind / TryFind”. Then implement consistently.
   - Immediate patch: add waits to `FindByText` and `FindAllOnXPath` to match others.
   - File: `Flow.Absolute.cs`

6) **Make context creation/disposal robust**
   - Wrap `CreateContext` in try/catch/finally; if navigation fails, close page/browser and dispose Playwright.
   - File: `Flow.cs` (and optionally enhance `Context.DisposeAsync`)

7) **Add cancellation support to long-running control flow**
   - Add `CancellationToken` overloads for `Retry`, `While`, and long waits where appropriate.
   - File: `Flow.Steps.cs`

8) **Clarify/implement frame model**
   - Either implement a real `SwitchToFrame` step that sets `Context.Frame` using `NextFrame`, or remove `Frame` from `Context` and rely purely on locators/frame locators.
   - Files: `Context.cs`, `Flow.Absolute.cs`, `Flow.Steps.cs`

9) **Retarget framework**
   - Retarget to `net8.0` (or multi-target `net8.0;net9.0`) unless there is a specific need for `net10.0`.
   - File: `Ozone.csproj`

10) **Decouple MSTest package from library**
   - Replace `Microsoft.Playwright.MSTest` with `Microsoft.Playwright` in `Ozone.csproj`.
   - If MSTest helpers are needed, create a separate test project/package.
   - File: `Ozone.csproj`

---

## 4) Secrets / unsafe data callout (explicit)

- No hard-coded API keys/passwords are visible in the provided snapshot.
- **However, logging is currently unsafe**:
  - `AsyncStep.FormatTarget()` reflects closure fields and prints raw string values, which can easily include credentials, tokens, session IDs, or PII used in automation.
  - Files: `AsyncStep.cs` (primary), output via `Flow.Logging.cs`
- **Transport safety is also weakened by default**:
  - `IgnoreHTTPSErrors = true` disables TLS certificate validation for all pages.
  - File: `Flow.cs`