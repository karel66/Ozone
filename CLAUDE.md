# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Ozone is a small .NET class library (`net10.0`, nullable enabled) that wraps **Microsoft Playwright** in a functional DSL for composing UI automation flows. `Ozone.sln` at the repo root contains two projects in sibling folders: the library `Ozone/Ozone.csproj` and the MSTest project `Ozone.Tests/Ozone.Tests.csproj`. Source file paths below are relative to `Ozone/`.

## Commands

```sh
dotnet build Ozone.sln                                   # build library + tests
dotnet test Ozone.sln                                    # all tests (browser tests need Chromium, see below)
dotnet test Ozone.sln --filter "TestCategory!=Browser"   # fast tests only, no browser
dotnet test Ozone.sln --filter "FullyQualifiedName~FinderTests.Find_WithIndex"  # single test
```

Tests are split into `Ozone.Tests/Unit` (no browser: they build a `Context` with null Playwright objects via `TestContexts.Detached()`, enabled by `InternalsVisibleTo`) and `Ozone.Tests/Browser` (`[TestCategory("Browser")]`, load inline HTML via `TestContexts.WithHtml(...)`, which opens an isolated browser context on one headless Chromium shared by the whole run, with a 1 s find timeout). Tests run in parallel at method level; tests that toggle global state such as `Flow.TraceSensitiveValues` must be marked `[DoNotParallelize]`. The full suite takes a few seconds. Tests of launching or disposing the browser itself must use `Flow.CreateContext`, not `WithHtml`.

To run browser tests or flows locally, the Playwright browsers must be installed once after building:
`powershell Ozone.Tests/bin/Debug/net10.0/playwright.ps1 install chromium` (Windows PowerShell 5.1 works; `pwsh` is not required). The browser revision is tied to the `Microsoft.Playwright` package version, so rerun this after upgrading the package.

CI (`.github/workflows/build.yml`, ubuntu) mirrors this: Release build, unit tests, `playwright.ps1 install --with-deps chromium`, browser tests, then `dotnet pack` (the package includes the root `README.md`).

## Architecture

**The step model.** Every operation is a `Func<Context, Task<Context>>`. Static factory methods on `Flow` (e.g. `Find(selector)`, `SetText(text)`) return such functions; parameterless steps (`Click`, `PressEnter`, `DblClick`) are themselves `Task<Context> M(Context)` methods usable as method groups. Steps return a *new* `Context` rather than mutating it.

**`Context`** (`Context.cs`) is the immutable state threaded through steps: Playwright/Browser/Page, optional `Frame`, the current `Element` (`ILocator`) **or** `Collection` (`IReadOnlyList<ILocator>`), plus `Items`, a `ConcurrentDictionary<string,string>` that is shared by reference across all derived contexts for passing data between steps. `NextElement`/`NextCollection`/`NextFrame`/`NextPage` (internal) produce the next context (`EnterFrame`/`ExitFrame` move into and out of iframes; `FollowLink` continues in the frame or window the link targets, which may be a popup); setting an Element clears the Collection and vice versa. Steps fall into four roles by what they consume and produce:
- *Absolute finders* (`Flow.Absolute.cs`): search the page or frame via `RootLocatorForSelector`/`RootLocatorForXPath` and set `Element` or `Collection`.
- *Relative finders* (`Flow.Relative.cs`): require `Element` and search inside it.
- *Collection filters* (`FirstContainingText`, `LastContainingContextItem`, … in `Flow.Steps.cs`): require `Collection` and reduce it to an `Element`.
- *Actions / assertions / control flow* (`Flow.Steps.cs`): `Click`, `SetText`, `If`, `While`, `Retry`, `Assert*`, `Script`, `FollowLink`, `EnterFrame`. `While` fails after `maxIterations` (default 1000); `Retry` comes as a bool helper and as a step that reruns a step from the same context.

**Error handling.** Failure is signalled by `context.CreateProblem(...)`, which logs and **always throws `OzoneException`** even though it is typed to return `Context` (the return type exists so it can be used in expression positions like `return context.CreateProblem(...)`). It is marked `[DoesNotReturn]`. Passing an exception wraps it as the `InnerException` (an `OzoneException` is rethrown as-is), so `catch (Exception x) { return context.CreateProblem(x); }` is the standard pattern. Steps must never let another exception type escape: run their body through `Guard(context, async () => …)` (or `OnElement` for steps that act on the context element), which converts any exception into a problem. Precondition failures must surface as a faulted task, not a synchronous throw, so step lambdas are `async` (or return `Guard`'s task).

**`AsyncStep`** (`AsyncStep.cs`) is an optional chaining wrapper: `ctx | (new AsyncStep(Flow.Find("#q")) | Flow.SetText("x") | Flow.PressEnter)`. `operator |` on two steps calls `Link`, which **mutates** the left-hand chain (it's a mutable linked list; a multi-step right-hand chain is linked as one nested step, not flattened), and `Bind` runs the chain, logging each step via `MethodTrace()`. `AsyncStep.BreakUnless(name, predicate)` makes a gate that stops the rest of the chain it is linked into when the predicate is false (`Bind` wraps the predicate in `Guard`, returns the gate and sets its `Stopped`; `While(cond, AsyncStep)` treats that as `break`); `Flow.BreakIfNotExists` is built on it. `MethodTrace` uses reflection over the closure's captured fields to print step arguments; primitive values are printed, but every string (selectors included), char and array is redacted as `***` unless `Flow.TraceSensitiveValues = true` (opt-in for local debugging). Log and problem messages that include a caller-supplied selector, XPath or search text must format it with `Flow.Sensitive(...)`, which applies the same switch. `CreateProblem(Exception)` and `LogError` redact foreign exception messages the same way (first line only, quoted text as `***`), because Playwright messages contain the selector.

**Lifecycle.** `Flow.CreateContext(BrowserBrand, Uri, headless)` (`Flow.cs`) launches Playwright and the browser, opens a page and navigates (cleaning up if any of that fails); the caller disposes the returned `Context` (`await using`), which closes page and browser and disposes Playwright. All contexts derived from one `CreateContext` share these resources. Contexts built with `ownsBrowser: false` (internal; used by the shared test browser) close only their page's browser context on dispose. `Chrome`/`Edge` launch the branded browser via Playwright channels and must be installed locally; TLS errors are only ignored when `ignoreHttpsErrors: true` is passed. `Flow` is a `partial class` split across `Flow*.cs` files by concern; logging (`Flow.Log`/`LogError`) writes to the console.

## Conventions

- New steps should follow the existing factory shape: `public static Func<Context, Task<Context>> Name(args) => async context => { … }`, validate preconditions (`Element`/`Collection` present) with `CreateProblem` first, and use `nameof(Name)` as the message prefix. Collection filters go through `CollectionFilter`. Read element text with `context.TextAsync()` (`Text` and `Value()` are obsolete).
- Finders build a locator (absolute ones via `Context.RootLocatorFor*` so they respect `Frame`; relative ones via `ChildLocator`) and hand it to `SelectOne`/`SelectAll` in `Flow.Finders.cs`. Those helpers own the shared behavior: wait for the first match to be attached (`context.FindTimeout`, default 10 s, changed with `context.WithFindTimeout(...)` and carried to derived contexts; a step may take its own timeout instead), treat a timeout as "not found" and raise `OzoneException`, treat a wait of zero as "check once" (Playwright reads a 0 timeout as "wait forever"; public wait arguments are checked with `CheckWait`, which rejects negative, NaN and infinite values), and treat a negative index as counting from the end (-1 = last). Don't reimplement waiting or indexing in individual finders.
