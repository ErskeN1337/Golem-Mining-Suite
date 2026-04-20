# Lessons

Short, pithy rules that prevent recurring mistakes. Prepend new bullets; keep each one punchy.

- **Empty catches hide bugs.** Every `catch { }` (and comment-only / silent-fallback variants) must log via the injected `ILogger<T>`. Use `LogError` if functionality is impaired, `LogWarning` for recoverable/defaultable failures, `LogDebug` only for deliberate silent probes (and justify with a comment). Never log-and-ignore without a logger — use DI, not Serilog's static `Log.Logger` in instance classes.
- **Prefer interface surfaces over `is ConcreteType` casts.** If a ViewModel needs an event/property from a service, add it to the interface instead of casting. Casts in VMs are a smell that the interface is under-specified.
- **Keep behavior identical when adding logging.** Don't change fallback values, control flow, or exception types while retrofitting log statements. Log-only diffs should be reviewable at a glance.
