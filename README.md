# Ozone

Ozone is a small .NET library that wraps [Microsoft Playwright](https://playwright.dev/dotnet/) in a functional style for writing UI automation flows. A flow is built from small async steps, and each step takes a `Context` (browser, page, current element) and returns the next one.

```csharp
using Ozone;

await using var context = await Flow.CreateContext(BrowserBrand.Chromium, new Uri("https://example.com/search"));

var results = await (context
    | (new AsyncStep(Flow.SetText("#q", "ozone"))
    | Flow.PressEnter
    | Flow.FindAll(".result")
    | Flow.FirstContainingText("Playwright")
    | Flow.Click));
```

## Requirements

- .NET 10
- Playwright browsers. Install them once after building:

  ```sh
  pwsh bin/Debug/net10.0/playwright.ps1 install chromium
  ```

  Windows PowerShell 5.1 (`powershell`) works as well. The browser build is tied to the `Microsoft.Playwright` version, so rerun this after upgrading the package.

## Concepts

**Steps.** Every operation is a `Func<Context, Task<Context>>`. `Flow` provides factory methods that return steps, such as `Find(selector)` and `SetText(text)`. Steps without arguments, such as `Click` and `PressEnter`, are methods you can pass directly. Steps can be called on their own:

```csharp
var input = await Flow.Find("#q")(context);
await Flow.SetText("ozone")(input);
await Flow.PressEnter(input);
```

or chained with `AsyncStep` and the `|` operator as shown above.

**Context.** Holds the Playwright objects, the current `Element` or `Collection`, and `Items`, a dictionary that all contexts from one `CreateContext` share, for passing values between steps. Disposing the context closes the page and the browser.

**Main step groups:**

| Group | Steps |
|---|---|
| Find on page or frame | `Find`, `FindLast`, `FindAll`, `FindOnXPath`, `FindAllOnXPath`, `FindByText`, `Exists`, `IfExists` |
| Find inside the current element | `RelativeFind`, `RelativeFindAll`, `RelativeFindOnXPath`, `RelativeFindAllOnXPath` |
| Pick from a collection | `FirstContainingText`, `LastContainingText`, `FirstContainingContextItem`, `LastContainingContextItem` |
| Actions | `Click`, `DblClick`, `SetText`, `PressEnter`, `FollowLink`, `Script` |
| Frames | `EnterFrame`, `ExitFrame` |
| Control flow and checks | `If`, `While` (stops with an error after 1000 iterations by default), `Retry`, `Assertion`, `AssertAsync`, `AssertAttributeValue` |

Finders wait up to 10 seconds for the first match. Use `context.WithFindTimeout(TimeSpan.FromSeconds(30))` to change this for a context and every context derived from it. A negative index counts from the end (`-1` is the last match).

**Errors.** A failing step throws `OzoneException`. When the failure came from Playwright, the original exception is kept as `InnerException`.

**Logging.** Each step in an `AsyncStep` chain is logged to the console with its arguments. Locator arguments are shown; other text, such as values typed by `SetText`, is shown as `***`. Set `Flow.TraceSensitiveValues = true` to log all values while debugging locally.

## Building and testing

```sh
dotnet build Ozone.sln
dotnet test Ozone.sln                                   # all tests, needs Chromium (see Requirements)
dotnet test Ozone.sln --filter "TestCategory!=Browser"  # unit tests only, no browser
dotnet pack Ozone/Ozone.csproj -c Release               # NuGet package
```

Browser tests load inline HTML into headless Chromium. Install it from the test output folder: `pwsh Ozone.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`.
