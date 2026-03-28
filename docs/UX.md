# Footing UX & Workflow Design Guide

This document defines the user experience principles, interaction patterns, and workflow
design for Footing. It builds on the brand personality of a "patient teacher" and the core
values: reduce anxiety, earn simplicity, and meet people where they are.

---

## Brand Personality in UX

Footing is a **patient teacher** — never judgmental, always encouraging. Every interaction
should feel like a conversation with a knowledgeable friend who wants you to succeed.

| Principle | What it means in practice |
|---|---|
| **Reduce anxiety** | No financial jargon. No red warnings on first visit. Frame negatives as opportunities. |
| **Earn simplicity** | Start with the minimum viable UI. Reveal complexity only as the user engages deeper. |
| **Meet people where they are** | Don't assume financial literacy. Don't assume shame. Don't assume expertise. |

---

## User Flows

### First Visit (Onboarding)

```
Landing (/)
  |
  +-- "Get Started" --> Footing Me (/footing-me)
  |                       User sees empty accordion sections
  |                       Privacy notice ("Nothing you put here leaves your browser")
  |                       First action: add an income entry
  |
  +-- "There Is Hope" --> Control Your Money (/control-your-money)
                           Empathetic narrative about financial anxiety
                           Ends with CTA back to Footing Me
```

**Design intent:** Two entry paths serve two emotional states. "Get Started" is for users
ready to act. "There Is Hope" is for users who need reassurance first. Both paths converge
on the same tool.

**Key rules for onboarding:**
- No sign-up, no account creation, no email capture — ever.
- The privacy notice on Footing Me is the first thing the user reads in the tool area.
  It must remain above the fold.
- The landing page hero text sets the emotional tone. Keep it short, warm, and direct.

### Core Workflow (Footing Me)

```
Footing Me (/footing-me)
  |
  +-- Income section (collapsed by default)
  |     Add income entries: amount + frequency + description
  |
  +-- Recurring Bills section
  |     Add predictable recurring expenses
  |
  +-- Household Budgets section
  |     Add variable shared expenses
  |
  +-- Personal Budgets section
  |     Add individual spending allowances
  |
  +-- Event Budgets section
  |     Add seasonal/occasional expenses
  |
  +-- Net Total (always visible as collapsed header)
  |     Shows weekly net: income minus all expenses
  |     Expands to show contextual guidance
  |
  +-- Export to Excel button
```

**Progression model:** The accordion order is intentional — it mirrors the mental model of
"what comes in" before "what goes out," with expenses broken into increasingly granular
categories. Users naturally work top-to-bottom.

**Key rules:**
- Each section shows its weekly subtotal in the collapsed header. Users see running
  feedback without expanding anything.
- The Net Total card uses the primary brand color to stand out from expense sections.
- Negative net totals show encouraging ("roll up your sleeves") language, not alarming
  language.

### Data Lifecycle

```
User enters data --> Saved to localStorage automatically
                     (every render after first)
  |
  +-- "Clear" link --> Confirmation dialog --> Wipes localStorage
  |
  +-- "Download Excel" --> Generates .xlsx in-browser --> File download
```

**Key rules:**
- Auto-save is silent. No toast, no "saved" indicator. Saving is a given, not an event.
- The clear action requires confirmation. This is the only destructive action in the app.
- Excel export works offline. No server round-trip.

---

## Page-to-Page Navigation

### Sidebar Navigation

The sidebar is the primary navigation mechanism. It contains three links:

| Link | Route | Purpose |
|---|---|---|
| Home | `/` | Landing page with hero + entry cards |
| Footing Me | `/footing-me` | The core financial tool |
| Control Your Money | `/control-your-money` | Empathetic onboarding narrative |

**Navigation rules:**
- The active page is highlighted in the sidebar.
- Sidebar collapses to a hamburger toggle on small screens.
- Navigation never discards entered data (it persists in localStorage).

### Top Bar

The top bar contains external links (Buy Me A Coffee, Become a Patron, Learn More) and
the theme toggle. These are secondary actions — support links, not core navigation.

**Rules:**
- External links open in new tabs.
- The theme toggle is always accessible, never hidden behind a menu.

---

## Empty States

Footing has one primary empty state: when the user first visits Footing Me with no
data in localStorage.

### Empty Footing Me

When all accordion sections are empty:
- Each section header shows `$0.00 / Week`.
- The Net Total shows `$0.00 / Week`.
- The privacy notice provides context ("Nothing you put in here will be sent to our servers").
- No placeholder illustrations or "get started" prompts inside sections — the form itself
  is the prompt.

**Philosophy:** The empty state IS the tool. The forms are visible immediately. No gates,
no tutorials, no "add your first item" hero images. The user sees the input fields and
understands what to do.

### After Clearing Data

Same as the initial empty state. The confirmation dialog is the only ceremony around
data deletion. Once cleared, the UI resets silently.

---

## Interaction Patterns

### Form Entry (Money Flow Cards)

Each money flow section uses the same form pattern:

```
[Prompt text with popover hint]
$[amount] [frequency dropdown] [connector word] [description]
[Add button]
```

**Pattern rules:**
- The prompt reads as a natural sentence: "I receive a net amount of $[X] [every week]
  from [my job]."
- Popover hints explain financial terms on hover/tap. They use `text-info` styling to
  signal interactivity.
- The frequency dropdown defaults to "how often?" — not a pre-selected value.
- Validation uses DataAnnotations. Invalid fields get a 1px red outline.
- The Add button is a primary button. One primary action per form.

### Entry Deletion

- Each table row has an "X" icon for deletion.
- The icon starts at 50% opacity and darkens on hover.
- On hover, it turns the negative amount color (red).
- No confirmation dialog for individual deletions — the action is small and recoverable
  by re-entry.

### Accordion Behavior

- Sections are mutually exclusive — expanding one collapses others (Bootstrap parent
  accordion).
- Section headers always show the weekly subtotal, even when collapsed.
- Click anywhere on the header row to expand/collapse.

### Theme Toggle

- Click toggles between light and dark mode.
- Preference is stored via `data-theme` attribute on `<html>`.
- Respects `prefers-color-scheme` when no explicit choice has been made.
- Icon changes: sun in dark mode (switch to light), moon in light mode (switch to dark).

### Excel Export

- Single button at the bottom of the accordion.
- Generates the file entirely client-side using Simplexcel.
- Downloads as `Footing.xlsx`.
- No loading spinner needed — generation is near-instant for typical data sizes.

---

## Error Handling UX

### Blazor Error Boundary

Runtime errors display a yellow warning bar with "An error has occurred." This is the
framework default, styled with brand tokens (`--ft-error-bg`, `--ft-error-text`).

**Rules:**
- Keep the error message generic for end users.
- The "Reload" link and dismiss button are always available.

### Server Reconnection

When the Blazor SignalR connection drops, the reconnect modal appears:

1. **Rejoining** — animated dots, "Rejoining the server..." message.
2. **Retry** — "Rejoin failed... trying again in X seconds."
3. **Failed** — "Failed to rejoin. Please retry or reload the page." with a Retry button.
4. **Paused** — "The session has been paused by the server." with a Resume button.

**Rules:**
- The modal is a `<dialog>` element for proper accessibility.
- It appears as a centered overlay with backdrop.
- The user always has an action available (Retry or Resume).

### Not Found (404)

Minimal page with "Not Found" heading and a brief message. No illustration, no search
box, no suggestions.

### Validation Errors

- Individual field validation uses colored outlines (green for valid, red for invalid).
- A `ValidationSummary` appears below the form when submission fails.
- Validation messages use `--ft-invalid` color.

---

## Responsive Design

### Breakpoints

| Breakpoint | Behavior |
|---|---|
| < 641px | Single-column card grid on landing. Sidebar collapses to toggle. |
| >= 641px | Two-column card grid on landing. Sidebar expands. |

### Mobile Considerations

- The sidebar toggle is a checkbox-based pattern (no JavaScript for open/close).
- Forms use `size` attributes on inputs for natural sizing.
- Popovers use `data-bs-placement="auto"` to adapt to available space.
- Tables in money flow sections may scroll horizontally on narrow screens.

---

## Tone of Voice in UI Copy

### Headings

- Use active, empowering language: "Take Control of Your Money," "Manage My Money."
- Avoid passive or clinical language: not "Financial Dashboard" or "Budget Tracker."

### Body Copy

- Write in first and second person: "I receive," "your money," "you can do this."
- Keep sentences short. One idea per paragraph on the motivational page.
- Use italics for emotional emphasis: "*You can do this.*"
- Financial terms get popover explanations, not inline definitions.

### Contextual Guidance (Net Total)

- **Positive net:** "You're looking good so far" — cautiously optimistic, not celebratory.
  Acknowledges that things change.
- **Negative net:** "Time to roll up your sleeves" — action-oriented, not alarming. No
  "you're in trouble" or "danger" language.

### Privacy Notice

- Always present on Footing Me.
- Plain language: "Nothing you put in here will be sent to our servers."
- Includes a "clear" action link inline — accessible but not prominent.

---

## Accessibility

### Current Implementation

- Semantic HTML: `<nav>`, `<main>`, `<article>`, `<dialog>`.
- ARIA labels on the theme toggle button.
- `aria-hidden="true"` on decorative icons.
- Focus ring uses `--ft-focus-ring` token for visibility.
- Keyboard-navigable accordion via Bootstrap's collapse component.
- Color contrast ratios maintained across light and dark themes.

### Guidelines for New Features

- All interactive elements must be keyboard-accessible.
- Use `aria-label` or `aria-labelledby` on controls without visible text labels.
- Never rely on color alone to convey meaning (e.g., positive/negative amounts also
  use +/- signs).
- Test both light and dark modes for contrast compliance.
- The reconnect modal uses `<dialog>` — new modals should follow the same pattern.

---

## Design Token Usage

All colors are defined as CSS custom properties with the `--ft-` prefix. Components
must use tokens, never hardcoded color values.

### Token Categories

| Category | Prefix | Example |
|---|---|---|
| Brand | `--ft-primary`, `--ft-accent` | Buttons, links, highlighted cards |
| Monetary | `--ft-amount-positive`, `--ft-amount-negative` | Income vs. expense coloring |
| Text | `--ft-text`, `--ft-text-muted` | Body copy, secondary labels |
| Background | `--ft-bg`, `--ft-bg-surface`, `--ft-bg-topbar` | Page, cards, top bar |
| Sidebar | `--ft-sidebar-*` | Navigation panel |
| Focus | `--ft-focus-ring` | Keyboard focus indicators |
| Validation | `--ft-valid`, `--ft-invalid` | Form field states |
| Error UI | `--ft-error-*` | Error boundaries, error pages |
| Modal | `--ft-modal-*` | Reconnect dialog |

### Dark Mode

- Dark mode is activated via `data-theme="dark"` on the root element.
- Falls back to `prefers-color-scheme: dark` when no explicit theme is set.
- Every token has a dark-mode variant. New tokens must define both light and dark values.

---

## Workflow Principles

1. **No login, no friction.** The tool works immediately. Data stays in the browser.
2. **Progressive disclosure.** Start with income, reveal expense categories, end with
   the net total. Don't show everything at once.
3. **Immediate feedback.** Every entry instantly updates the weekly subtotal in the
   section header and the net total.
4. **No dead ends.** Every page has a clear next action. The motivational page leads
   to the tool. The tool leads to export.
5. **Forgiveness over prevention.** Let users delete entries freely. Only confirm
   destructive bulk actions (clear all data).
6. **Privacy is a feature.** The privacy notice isn't legalese — it's a trust signal.
   Surface it early and prominently.
7. **Encourage, don't judge.** A negative balance isn't a failure message. It's an
   invitation to take action.
