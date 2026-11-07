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

Tests are split into `Ozone.Tests/Unit` (no browser: they build a `Context` with null Playwright objects via `TestContexts.Detached()`, enabled by `InternalsVisibleTo`) and `Ozone.Tests/Browser` (`[TestCategory("Browser")]`, launch headless Chromium and load inline HTML via `TestContexts.WithHtml(...)`). Tests run in parallel at method level; tests that toggle global state such as `Flow.TraceSensitiveValues` must be marked `[DoNotParallelize]`. Tests for a missing element wait out the 10 s find timeout, so the browser suite takes about 30 s.

To run browser tests or flows locally, the Playwright browsers must be installed once after building:
`powershell Ozone.Tests/bin/Debug/net10.0/playwright.ps1 install chromium` (Windows PowerShell 5.1 works; `pwsh` is not required). The browser revision is tied to the `Microsoft.Playwright` package version, so rerun this after upgrading the package.

## Architecture

**The step model.** Every operation is a `Func<Context, Task<Context>>`. Static factory methods on `Flow` (e.g. `Find(selector)`, `SetText(text)`) return such functions; parameterless steps (`Click`, `PressEnter`, `DblClick`) are themselves `Task<Context> M(Context)` methods usable as method groups. Steps return a *new* `Context` rather than mutating it.

**`Context`** (`Context.cs`) is the immutable state threaded through steps: Playwright/Browser/Page, optional `Frame`, the current `Element` (`ILocator`) **or** `Collection` (`IReadOnlyList<ILocator>`), plus `Items`, a `ConcurrentDictionary<string,string>` that is shared by reference across all derived contexts for passing data between steps. `NextElement`/`NextCollection`/`NextFrame` (internal) produce the next context; setting an Element clears the Collection and vice versa. Steps fall into four roles by what they consume and produce:
- *Absolute finders* (`Flow.Absolute.cs`): search the page or frame via `RootLocatorForSelector`/`RootLocatorForXPath` and set `Element` or `Collection`.
- *Relative finders* (`Flow.Relative.cs`): require `Element` and search inside it.
- *Collection filters* (`FirstContainingText`, `LastContainingContextItem`, … in `Flow.Steps.cs`): require `Collection` and reduce it to an `Element`.
- *Actions / assertions / control flow* (`Flow.Steps.cs`): `Click`, `SetText`, `If`, `While`, `Retry`, `Assert*`, `Script`.

**Error handling.** Failure is signalled by `context.CreateProblem(...)`, which logs and **always throws `OzoneException`** even though it is typed to return `Context` (the return type exists so it can be used in expression positions like `return context.CreateProblem(...)`). It is marked `[DoesNotReturn]`. Passing an exception wraps it as the `InnerException` (an `OzoneException` is rethrown as-is), so `catch (Exception x) { return context.CreateProblem(x); }` is the standard pattern. Steps are expected to convert Playwright exceptions (especially `TimeoutException`) into `CreateProblem` calls.

**`AsyncStep`** (`AsyncStep.cs`) is an optional chaining wrapper: `ctx | (new AsyncStep(Flow.Find("#q")) | Flow.SetText("x") | Flow.PressEnter)`. `operator |` on two steps calls `Link`, which **mutates** the left-hand chain (it's a mutable linked list), and `Bind` runs the chain, logging each step via `MethodTrace()`. `MethodTrace` uses reflection over the closure's captured fields to print step arguments; string values are printed only for allow-listed field names (`LoggableStringFields`: `selector`, `xpath`, …) and everything else is redacted unless `Flow.TraceSensitiveValues = true` (opt-in for local debugging). If you add a step whose string parameter is a locator, either name it `selector`/`xpath` or add the name to the allow-list.

**Lifecycle.** `Flow.CreateContext(BrowserBrand, Uri, headless)` (`Flow.cs`) launches Playwright and the browser, opens a page and navigates (cleaning up if any of that fails); the caller disposes the returned `Context` (`await using`), which closes page and browser and disposes Playwright. All contexts derived from one `CreateContext` share these resources. `Chrome`/`Edge` launch the branded browser via Playwright channels and must be installed locally; TLS errors are only ignored when `ignoreHttpsErrors: true` is passed. `Flow` is a `partial class` split across `Flow*.cs` files by concern; logging (`Flow.Log`/`LogError`) writes to the console.

## Conventions

- New steps should follow the existing factory shape: `public static Func<Context, Task<Context>> Name(args) => async context => { … }`, validate preconditions (`Element`/`Collection` present) with `CreateProblem` first, and use `nameof(Name)` as the message prefix.
- Finders build a locator (absolute ones via `Context.RootLocatorFor*` so they respect `Frame`; relative ones via `ChildLocator`) and hand it to `SelectOne`/`SelectAll` in `Flow.Finders.cs`. Those helpers own the shared behavior: wait for the first match to be attached (`FindTimeout`, 10 s, unless the step takes a timeout), treat a timeout as "not found" and raise `OzoneException`, and treat a negative index as counting from the end (-1 = last). Don't reimplement waiting or indexing in individual finders.
