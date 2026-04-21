# Lessons

Short, pithy rules that prevent recurring mistakes. Prepend new bullets; keep each one punchy.

- **Orchestrator owns git, subagents own files.** When dispatching 2+ implementers in parallel, every prompt must include: *"DO NOT run `git add` or `git commit`. Write files only; the orchestrator stages and commits."* Then the orchestrator verifies all agents finished, stages each agent's paths individually, and commits in wave order. Seen in Waves 3A/3C: concurrent `git add`/`commit` calls scrambled commit subjects vs. diffs (each commit got another agent's files). Exception: a single solo implementer can commit its own work. Never when ≥ 2 implementers run in parallel.

- **Recover from a dead subagent with `git status` first, always.** Agents die — API 401s, OOM, network blips. Before redispatching: `git status --short`. Three cases: (a) clean tree → redispatch the same prompt, they start fresh; (b) partial work worth keeping → `git add -A && git commit -m "WIP: <wave> partial (agent killed)"` first, then redispatch with a *"extend the partial work at HEAD"* prompt; (c) partial work is garbage → `git restore .` + `git clean -fd`, then redispatch clean. Never redispatch into a dirty tree blindly — the replacement mixes its output with the previous agent's debris.

- **Defensive subagent prompts start with a git check.** Every implementer prompt's first bullet: *"Run `git status`. If it's not clean AND the dirty files overlap your task scope, STOP and report NEEDS_CONTEXT."* Catches redispatches into unexpected state before real work begins.

- **Empty catches hide bugs.** Every `catch { }` (and comment-only / silent-fallback variants) must log via the injected `ILogger<T>`. Use `LogError` if functionality is impaired, `LogWarning` for recoverable/defaultable failures, `LogDebug` only for deliberate silent probes (and justify with a comment). Never log-and-ignore without a logger — use DI, not Serilog's static `Log.Logger` in instance classes.
- **Prefer interface surfaces over `is ConcreteType` casts.** If a ViewModel needs an event/property from a service, add it to the interface instead of casting. Casts in VMs are a smell that the interface is under-specified.
- **Keep behavior identical when adding logging.** Don't change fallback values, control flow, or exception types while retrofitting log statements. Log-only diffs should be reviewable at a glance.
