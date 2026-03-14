---
description: Review code changes across 6 legs with structured grading (A-F)
allowed-tools: Agent, Bash(git diff:*), Bash(git rev-parse:*), Bash(gh pr diff:*), Bash(git log:*), Bash(dotnet test:*), Bash(npm test:*), Bash(npx vitest:*), Read, Glob, Grep, Skill
argument-hint: [--staged | --branch | --pr <url>]
---

Review code changes across 6 structured review legs, each producing its own grade.

Arguments: $ARGUMENTS

## Diff Source Resolution

Determine the diff to review based on arguments:

| Argument | Diff command | Use case |
|----------|-------------|----------|
| (none) | `git diff` + `git diff --staged` | Review uncommitted + staged changes |
| `--staged` | `git diff --staged` | Review only staged changes |
| `--branch` | `git diff origin/<base>...HEAD` | Review branch diff vs base branch |
| `--pr <url>` | `gh pr diff <url>` | Review a GitHub PR |

### Step 1: Get the diff

Based on the arguments, run the appropriate diff command:

```bash
# Default (no args): uncommitted + staged
DIFF=$(git diff; git diff --staged)

# --staged: only staged
DIFF=$(git diff --staged)

# --branch: branch diff (detect base branch)
BASE=$(git rev-parse --abbrev-ref HEAD@{upstream} 2>/dev/null | sed 's|origin/||' || git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|refs/remotes/origin/||' || echo "main")
DIFF=$(git diff origin/$BASE...HEAD)

# --pr <url>: PR diff
DIFF=$(gh pr diff <url>)
```

If the diff is empty, report "No changes to review" and stop.

### Step 2: Identify changed files

From the diff, extract the list of changed files and classify them:

- **UI files**: .razor, .cshtml, .astro, .html, .css, .js, .ts, .tsx, .jsx, .vue, .svelte
- **Test files**: files in `*Tests*`, `*tests*`, `*spec*` directories, or files named `*Test.cs`, `*Tests.cs`, `*.test.ts`, `*.spec.ts`, etc.
- **Source files**: all non-test code files
- **Config files**: .csproj, package.json, appsettings.json, Dockerfile, Helm charts, Bicep/ARM, etc.
- **Consent/privacy files**: anything touching consent, privacy, PII, DSAR, policy, terms, cookie, iubenda, GTM

### Step 2.5: Load Review Profile

Before running legs, load the project's skill configuration:

1. **Look for `.claude/review-profile.json`** in the repo root (or nearest parent directory).
2. If found, parse it. It maps leg names to skill lists:
   ```json
   {
     "architecture": ["skill-1", "skill-2"],
     "ui": ["skill-3"],
     "accessibility": ["skill-4"],
     "testing": ["skill-5", "skill-6"],
     "consent": ["skill-7"]
   }
   ```
3. If **not found**, fall back to auto-detection (see Fallback Detection below).

Each leg uses `profile.<leg-name>` to determine which skills to invoke. If a leg key is missing from the profile, that leg uses auto-detection for its skills.

#### Fallback Detection (when no review-profile.json exists)

Auto-detect the tech stack from project files and map to skills:

| Detection Signal | Skills to invoke |
|---|---|
| `*.csproj` or `*.sln` found | `dotnet-best-practices`, `modern-csharp-coding-standards`, `csharp-async`, `dependency-injection-patterns`, `type-design-performance`, `package-management`, `dotnet-project-structure`, `microsoft-extensions-configuration` |
| Any `.csproj` references `Akka` packages | Add: `akka-net-best-practices`, `akka-hosting-actor-patterns`, `akka-net-testing-patterns`, `akka-net-management` |
| `*.AppHost.csproj` or Aspire SDK reference found | Add: `aspire`, `aspire-configuration`, `aspire-service-defaults`, `akka-net-aspire-configuration` (if Akka also detected) |
| `*.razor` files changed | Add to ui: `playwright-blazor-testing`; Add to testing: `aspire-integration-testing` |
| `package.json` found | `web-coder` |
| `Dockerfile` changed | `containerize-aspnetcore` or `multi-stage-dockerfile` |
| `*.bicep` changed | `azure-deployment-preflight` |
| Protobuf/gRPC references | `serialization`, `api-design` |

Auto-detected skills are merged into the appropriate leg. The profile always takes precedence over auto-detection for any leg it defines.

### Step 3: Run 6 Review Legs in Parallel

Launch **all 6 legs as parallel subagents**. Each leg reviews the diff independently and returns its own grade. Each agent receives the full diff, the list of changed files, and the skill list for its leg (from the profile or auto-detection).

**IMPORTANT**: Each subagent MUST invoke the skills listed for its leg (via the Skill tool) as part of its review. Skills provide domain-specific checklists and patterns that the review must check against. Log which skills were invoked for the final report.

---

#### Leg 1: UI/UX Branding & Design

**Skills**: Use `profile.ui` if defined, otherwise auto-detect.

**Skip condition**: No UI files changed. Grade: A (N/A).

**Review scope**: All changed UI files.

**Process**:
1. Look for `context/` directories or files named `*brand*`, `*guidelines*`, `*design*`, `*ux*` in the repo for branding context. Also check for a skip marker (e.g., a comment or file containing "SKIP_BRAND_REVIEW" or instructions to skip branding checks).
2. If skip marker found, report "Branding review skipped per project instructions" and grade A.
3. Otherwise, invoke each skill from the profile's `ui` list to inform the review.
4. Check changed UI code against branding guidelines found in context files.
5. Flag: inconsistent colors/fonts, missing design tokens, layout that breaks the design system, components that don't match the established visual language.

**Severity mapping**:
- CRITICAL: UI renders broken or unusable
- MAJOR: Violates branding guidelines, inconsistent visual language, poor responsive behavior
- MINOR: Spacing/alignment nits, minor style inconsistencies

---

#### Leg 2: Accessibility (a11y)

**Skills**: Use `profile.accessibility` if defined, otherwise auto-detect (default: `accessibility-auditor`).

**Skip condition**: No UI files changed. Grade: A (N/A).

**Review scope**: All changed UI files.

**Process**:
1. Invoke each skill from the profile's `accessibility` list.
2. Review changed UI for:
   - Missing or incorrect ARIA attributes
   - Missing alt text on images
   - Insufficient color contrast
   - Non-keyboard-accessible interactive elements
   - Missing form labels
   - Incorrect heading hierarchy
   - Missing skip navigation links
   - Focus management issues in dynamic content
   - RTL layout compatibility (check if `dir` attributes or logical CSS properties are used)

**Severity mapping**:
- CRITICAL: Interactive elements not keyboard accessible, images without alt text, form inputs without labels
- MAJOR: Insufficient color contrast, incorrect ARIA roles, heading hierarchy violations
- MINOR: Missing optional ARIA attributes, suboptimal focus order

---

#### Leg 3: Consent, Privacy & Legal Compliance

**Skills**: Use `profile.consent` if defined, otherwise default: `consent-privacy-engineering`.

**Skip condition**: No source files changed AND no consent/privacy files changed. Grade: A (N/A).

**Review scope**: All changed source files, with special attention to consent/privacy-related code.

**Process**:
1. Invoke each skill from the profile's `consent` list.
2. Check for legal document versioning:
   - Look for policy documents (privacy policy, ToS) in the repo.
   - If a policy has a "TO-DO" or "DRAFT" marker, note it but do not fail the review for missing policy content — only fail if the **code** doesn't respect the policy framework.
3. Review changed code for:
   - **PII leaks**: PII stored in analytics tables, logs, or event streams where it shouldn't be
   - **Consent checks missing**: Data collection or processing without checking consent state
   - **Tag manager issues**: Tags firing without consent gates, missing default-deny
   - **DSAR gaps**: New data stores that aren't covered by access/export/erasure flows
   - **Missing jurisdiction awareness**: User-facing behavior that doesn't account for locale/country
   - **Policy acceptance**: New features that should require updated ToS/privacy policy acceptance but don't trigger re-consent
   - **GPC/Do Not Sell**: Marketing or ad-related code that doesn't check GPC signal

**Severity mapping**:
- CRITICAL: PII in analytics/logs, data collection without consent check, DSAR-invisible data store
- MAJOR: Missing consent gate on tag, no jurisdiction routing, new processing without legal basis
- MINOR: Missing consent audit trail entry, consent purpose not localized

---

#### Leg 4: Test Coverage & Correctness

**Skills**: Use `profile.testing` if defined, otherwise auto-detect.

**Skip condition**: None — always runs.

**Review scope**: All changed source and test files.

**Process**:
1. Invoke each skill from the profile's `testing` list.
2. Review:
   - **Changed code without changed tests**: If source code changed, did corresponding tests change or get added?
   - **New code coverage**: New public methods/classes/components should have unit tests. New API endpoints need integration tests. New UI components need functional tests. New user flows need E2E tests.
   - **Test quality**: Tests should assert behavior, not implementation. No empty test bodies. No commented-out assertions. No `Assert.True(true)`.
   - **Test correctness**: Do tests actually test what they claim? Are assertions meaningful?
   - **All tests pass**: If feasible, run the test suite (`dotnet test` or `npm test`) and report failures.
   - **Slopwatch**: Check for reward-hacking patterns — disabled tests, `[Skip]` attributes added, `#pragma warning disable` added, empty catch blocks, swallowed exceptions.

**Severity mapping**:
- CRITICAL: Tests disabled or deleted to make the build pass, assertions removed, slopwatch violations
- MAJOR: New code with no tests, changed behavior with unchanged tests, test failures
- MINOR: Missing edge case coverage, test naming inconsistencies

---

#### Leg 5: Architectural Patterns & Best Practices

**Skills**: Use `profile.architecture` if defined, otherwise auto-detect.

**Skip condition**: No source or config files changed. Grade: A (N/A).

**Review scope**: All changed source and config files.

**Process**:
1. Invoke each skill from the profile's `architecture` list.
2. Review changed code against the patterns established by those skills:
   - Are architectural patterns followed consistently?
   - Are new dependencies justified?
   - Is the code in the right layer/project?
   - Are abstractions appropriate (not over- or under-engineered)?
   - Are concurrency patterns correct?
   - Is error handling consistent with the codebase?

**Severity mapping**:
- CRITICAL: Architectural violation that could cause data loss, security hole, or system instability
- MAJOR: Wrong abstraction layer, missing DI registration, anti-pattern usage, breaking API compatibility
- MINOR: Style inconsistency, suboptimal but functional pattern choice

---

#### Leg 6: Review Profile Hygiene

**Skills**: None (this leg checks the profile itself).

**Skip condition**: None — always runs.

**Review scope**: The `.claude/review-profile.json` file AND the diff AND the installed skills at `~/.claude/skills/`.

**Process**:
1. **List installed skills**: Read the directory listing of `~/.claude/skills/` to get the set of all installed skill names.
2. **Load the review profile**: Read `.claude/review-profile.json` from the repo.
3. **Check for uninstalled skills**: For every skill name referenced in the profile, verify it exists in `~/.claude/skills/<skill-name>/SKILL.md`. If a skill is referenced but not installed, flag it as MAJOR with a suggested fix telling the human to install it (e.g., `npx skills add <package>` or `mkdir -p ~/.claude/skills/<name>/`).
4. **Check if the MR introduces technologies not covered by the profile**:
   - Scan the diff for new technology signals:
     - New NuGet package references in `.csproj` files (especially frameworks like Akka, MassTransit, EF Core, SignalR, etc.)
     - New npm dependencies in `package.json` (React, Vue, Svelte, Tailwind, etc.)
     - New `Dockerfile`, `docker-compose`, `*.bicep`, `*.tf`, Helm charts
     - New file types not previously present (`.razor`, `.astro`, `.vue`, `.proto`, etc.)
   - For each new technology detected, check whether a relevant skill exists in `~/.claude/skills/` that is **not** already in the profile.
   - If a matching skill exists but isn't in the profile, flag as MAJOR: "New technology `X` detected in MR but skill `Y` is not in the review profile."
   - If a new technology is detected and no matching skill exists at all, flag as MINOR: "New technology `X` detected — no matching skill is installed. Consider searching for one with `npx skills find <query>`."
5. **Check for stale profile entries**:
   - If the MR **removes** a technology (e.g., removes all Akka package references, deletes all `.razor` files), and the profile still lists skills for that technology, flag as MINOR: "Technology `X` appears to have been removed but related skills remain in the profile."

**Severity mapping**:
- CRITICAL: (none — profile issues don't block merges, but they degrade future reviews)
- MAJOR: Skill referenced in profile but not installed (review quality is reduced); new technology in MR not covered by profile (review blind spot)
- MINOR: Stale profile entries for removed technologies; new technology with no available skill

**Output format for this leg**: In addition to the standard issue format, include an **ACTION ITEMS** section addressed to the human:

```
ACTION ITEMS (for human):
  Install missing skill: <skill-name>
    Run: npx skills find <query>

  Add to review-profile.json:
    "architecture": [..., "<new-skill>"]

  Remove stale entry:
    "<old-skill>" — technology no longer used in this project
```

---

### Step 4: Aggregate Results

After all 6 legs complete, produce the final report:

```
============================================================
CODE REVIEW REPORT
============================================================

Leg 1: UI/UX Branding & Design ................ Grade: <A-F>
Leg 2: Accessibility (a11y) ................... Grade: <A-F>
Leg 3: Consent, Privacy & Legal ............... Grade: <A-F>
Leg 4: Test Coverage & Correctness ............ Grade: <A-F>
Leg 5: Architectural Patterns ................. Grade: <A-F>
Leg 6: Review Profile Hygiene ................ Grade: <A-F>

Overall Grade: <worst grade across all legs>
Overall Verdict: <PASS if all legs pass, FAIL if any leg fails>

============================================================
DETAILS BY LEG
============================================================

--- Leg 1: UI/UX Branding & Design (Grade: X) ---

CRITICAL (<count>)
  <file>:<line> -- <description>
    Suggested fix: <actionable fix>

MAJOR (<count>)
  <file>:<line> -- <description>
    Suggested fix: <actionable fix>

MINOR (<count>)
  <file>:<line> -- <description>
    Suggested fix: <actionable fix>

--- Leg 2: Accessibility (Grade: X) ---
...

--- Leg 3: Consent & Privacy (Grade: X) ---
...

--- Leg 4: Test Coverage (Grade: X) ---
...

--- Leg 5: Architecture (Grade: X) ---
...

--- Leg 6: Review Profile Hygiene (Grade: X) ---
...

ACTION ITEMS (for human):
  <list of skills to install, profile entries to add/remove>

============================================================
SUMMARY
============================================================
Total: <N> CRITICAL, <N> MAJOR, <N> MINOR across all legs
Skills invoked: <list of skills used>
```

### Grading Rules (per leg)

| Grade | Criteria | Verdict |
|-------|----------|---------|
| **A** | No issues, or leg skipped (N/A) | PASS |
| **B** | MINOR issues only | PASS |
| **C** | MAJOR issues present (no CRITICAL) | FAIL |
| **D** | CRITICAL issues present | FAIL |
| **F** | Unreviewable | SKIP |

**Overall grade** = worst individual leg grade.
**Overall verdict** = PASS only if ALL legs pass (A or B).

Omit empty severity sections within each leg. If a leg was skipped (no relevant files changed), show it as Grade: A (N/A) with a one-line explanation.
