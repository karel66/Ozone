## 1) Concise architecture summary

**What it is:**  
`Ozone` is a small C#/.NET library that wraps **Microsoft Playwright** with a lightweight “flow/step” DSL. Steps are functions of type `Func<Context, Task<Context>>` that transform a shared `Context` (page/frame + current element/collection + shared items).

**Core modules**
- **Runtime context**
  - `Context.cs`: record holding `IPlaywright`, `IBrowser`, `IPage`, optional `IFrame`, current `ILocator` (`Element`) or `Collection`, plus `Items` (`ConcurrentDictionary<string,string>`) for cross-step data.
- **Step chaining**
  - `AsyncStep.cs`: linked-list chain of async steps with `Bind(Context)` execution and `|` operator overloads. Includes tracing (`MethodTrace`) that reflects closure fields.
- **DSL “Flow” step library**
  - `Flow.cs`: creates Playwright/browser/page and navigates to a start URL.
  - `Flow.Absolute.cs`: global finders (`Find`, `FindAll`, XPath variants), `Exists`, `IfExists`.
  - `Flow.Relative.cs`: finders relative to current `Context.Element`.
  - `Flow.Steps.cs`: actions/control-flow (click, set text, retry, while, assertions, script, frame switching).
  - `Flow.Logging.cs`: `Console.WriteLine` logging.

**Entrypoints / usage**
- Main entry: `await Flow.CreateContext(BrowserBrand, Uri startPageUrl, bool headless=true)` → `Context`
- Execute steps:
  - Direct: `context = await Flow.Find("#id")(context);`
  - Chained: `await (context | (new AsyncStep(Flow.Find("#id")) | Flow.Click("...") /* etc */));`

---

## 2) Top 10 risks / issues (with file references)

1) **Playwright/Browser/Page lifecycle is never disposed**
   - `CreateContext` allocates `IPlaywright`, `IBrowser`, `IPage` with no close/dispose API.
   - Risk: leaked browser processes, CI instability, file handle exhaustion.
   - Files: `Flow.cs`, `Context.cs`

2) **TLS security weakened by default (`IgnoreHTTPSErrors = true`)**
   - Accepts invalid/mitm certs silently.
   - Risk: tests mask real security problems; unsafe default for shared library.
   - Files: `Flow.cs`

3) **Deadlock / hang risk: blocking on async using `.Result`**
   - `SelectComboText(string value)` calls `combo.SelectOptionAsync([value]).Result` inside an async step.
   - Risk: deadlocks under synchronization contexts, threadpool starvation/hangs.
   - Files: `Flow.Steps.cs`

4) **Frame switching is incorrect (does not set `Context.Frame`)**
   - `SwitchToFrame` stores `frameLocator.Locator(":root")` into `Element` but leaves `Context.Frame` null.
   - Meanwhile, global finders scope by `Context.Frame` (`RootLocatorForSelector/XPath`), so subsequent searches are not truly in the iframe.
   - Risk: incorrect/flaky iframe flows.
   - Files: `Flow.Steps.cs`, `Context.cs`, `Flow.Absolute.cs`

5) **`Context.EmptyContext()` silently drops `Items`**
   - `EmptyContext()` creates `new(..., items: new())` instead of preserving `Items`.
   - Risk: step-to-step data loss, nondeterministic behavior when flows rely on `Items`.
   - Files: `Context.cs`

6) **Sensitive data leakage via reflective trace logging**
   - `AsyncStep.FormatTarget` reflects captured closure fields/properties and prints their values (including strings).
   - Risk: passwords/tokens passed into lambdas get written to console logs.
   - Files: `AsyncStep.cs`, `Flow.Logging.cs`

7) **Nullability contract violation**
   - `AsyncStep.MethodTrace()` returns `null` but its signature is `string` (nullable enabled).
   - Risk: NREs / misleading compiler assumptions.
   - Files: `AsyncStep.cs`

8) **Finder semantics inconsistent (waits/timeouts vary)**
   - `Find`/`FindAll` wait for `.First.WaitForAsync(FindTimeout)`, but `FindByText` does not wait; `FindAllOnXPath` doesn’t wait either.
   - Risk: flakiness depending on which finder is used.
   - Files: `Flow.Absolute.cs`

9) **`Retry(Func<bool>)` blocks threads and only delays on exceptions**
   - Uses `Thread.Sleep` and doesn’t backoff when predicate returns false (only on exception).
   - Risk: slow suites, thread starvation in parallel runs; inconsistent retry behavior.
   - Files: `Flow.Steps.cs`

10) **Target framework choice likely breaks consumers/build agents**
   - `net10.0-windows` is very new/preview; also forces Windows-only.
   - Risk: build failures on common CI images; unnecessary restriction for Playwright (which is cross-platform).
   - Files: `Ozone.csproj`

---

## 3) Prioritized next-actions plan (smallest safe steps first)

1) **Remove the `.Result` deadlock**
   - Change `SelectComboText(string value)` to `await combo.SelectOptionAsync(...)`.
   - Add a minimal test (or sample) that exercises it to ensure it doesn’t hang.
   - File: `Flow.Steps.cs`

2) **Make logging safe by default**
   - Disable reflective argument dumping by default (or gate behind a flag).
   - Add redaction/truncation for common secret names/values if you keep it.
   - Files: `AsyncStep.cs`, `Flow.Logging.cs`

3) **Add explicit shutdown / disposal API**
   - Minimal: `Flow.Close(Context)` that `await Page.CloseAsync()`, `await Browser.CloseAsync()`, and disposes Playwright (`DisposeAsync`/`Dispose` depending on API).
   - Better: introduce `OzoneSession : IAsyncDisposable` that owns lifecycle and produces a `Context`.
   - Files: `Flow.cs`, `Context.cs` (new type)

4) **Fix iframe support properly**
   - Update `SwitchToFrame` to actually resolve and store an `IFrame` into `Context.Frame`.
   - Update/extend `Context` with `WithFrame(IFrame?)` helper to keep immutability clean.
   - Files: `Flow.Steps.cs`, `Context.cs`

5) **Harden `CreateContext` defaults**
   - Make `ignoreHttpsErrors` parameterized and default to `false`.
   - File: `Flow.cs`

6) **Preserve `Items` across `EmptyContext()`**
   - Change `EmptyContext()` to reuse the existing `Items` dictionary (or rename to `ClearItems()` if the wipe is intentional).
   - File: `Context.cs`

7) **Normalize finder wait semantics**
   - Decide a consistent contract (recommended: all “Find” methods wait up to `FindTimeout`).
   - Update `FindByText`, `FindAllOnXPath` accordingly.
   - File: `Flow.Absolute.cs`

8) **Fix nullability issue**
   - Change `MethodTrace()` to `string?` or return `string.Empty`.
   - File: `AsyncStep.cs`

9) **Improve retry behavior**
   - Replace `Thread.Sleep` with `Task.Delay` (or remove sync retry).
   - Apply backoff on “false” results too, not only exceptions.
   - File: `Flow.Steps.cs`

10) **Revisit target frameworks / packaging**
   - Consider `net8.0` (and possibly multi-target) unless you truly require `net10.0-windows`.
   - Consider referencing `Microsoft.Playwright` (core) instead of `Microsoft.Playwright.MSTest` if this is a general library.
   - File: `Ozone.csproj`

---

## 4) Secrets / unsafe data callout (explicit)

- **No hard-coded API keys/passwords are visible** in the snapshot provided.
- **High risk of accidental secret leakage through logs**: `AsyncStep.FormatTarget()` reflects and logs captured closure values (strings, properties) unredacted to console via `Flow.Log`. If a caller passes credentials/tokens into a step closure, they may be printed.
  - Files: `AsyncStep.cs`, `Flow.Logging.cs`
- **Insecure transport behavior by default**: `IgnoreHTTPSErrors = true` disables TLS cert validation, which is unsafe as a default.
  - File: `Flow.cs`