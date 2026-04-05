# Footing Design Principles

This document defines the enduring UX principles, brand personality, and design
philosophy for Footing. It describes *why* we make design decisions, not *what*
specific UI elements look like — implementation details belong in the code.

---

## Brand Personality: The Patient Teacher

Footing is a **patient teacher** — never judgmental, always encouraging. Every
interaction should feel like a conversation with a knowledgeable friend who
genuinely wants you to succeed with your finances.

The patient teacher:
- **Explains without condescending.** Financial concepts are introduced through
  context, not jargon. When a term needs definition, it appears as a hint the
  user can choose to engage with — not a lecture they must sit through.
- **Celebrates small wins honestly.** A positive balance is acknowledged with
  cautious optimism ("you're looking good so far"), not confetti. We know
  circumstances change.
- **Reframes setbacks as opportunities.** A negative balance is an invitation
  to take action ("time to roll up your sleeves"), never an alarm or a judgment.
- **Waits for the user to be ready.** We offer two emotional entry points:
  one for users ready to act, one for users who need reassurance first. Both
  paths lead to the same capability.

---

## Core UX Principles

### 1. Reduce Anxiety

Financial tools often amplify the stress they claim to relieve. Footing does
the opposite.

- No financial jargon in primary UI paths. Technical terms are available on
  demand through contextual hints, never required to proceed.
- Negative outcomes use action-oriented language, not alarm language. There is
  no "danger," "warning," or "you're in trouble" in the vocabulary.
- First visits are calm. No aggressive onboarding, no urgency, no countdown
  timers, no "complete your profile" pressure.
- The empty state *is* the tool. Users see what they can do immediately, not a
  gate they must pass through.

### 2. Earn Simplicity

Simplicity is not the absence of features — it's the careful sequencing of
complexity.

- Start with the minimum viable interaction. Reveal deeper capabilities only as
  the user engages and demonstrates readiness.
- Order information to mirror the user's mental model: what comes in before
  what goes out, broad categories before granular ones.
- Every piece of visible information should earn its place. If a user doesn't
  need it at their current step, defer it.
- Running feedback (subtotals, net calculations) stays visible without requiring
  the user to seek it out.

### 3. Meet People Where They Are

We make no assumptions about our users' financial literacy, emotional state,
or technical expertise.

- Don't assume financial literacy. A user who doesn't know what "net income"
  means deserves the same quality tool as a user who does.
- Don't assume shame. Many people feel embarrassed about their financial
  situation. The tone must never reinforce that.
- Don't assume expertise with digital tools. The interface should be
  self-evident. If it needs a tutorial, it's too complex.
- Don't assume a specific device or context. The experience must work on
  whatever screen the user has available.

---

## Interaction Principles

### Progressive Disclosure

Reveal complexity gradually. The user's first interaction should be the simplest
possible version of the tool. Deeper features emerge as engagement deepens.

- Collapsed-by-default patterns let users control their depth of engagement.
- The natural top-to-bottom flow guides users through a logical progression
  without requiring instructions.
- Contextual guidance appears at the moment it's relevant, not in advance.

### Immediate Feedback

Every user action should produce a visible result without delay.

- Data entry instantly updates running calculations. There is no "submit" step
  between entering data and seeing its effect.
- Auto-save is silent and assumed. Saving is a given, not an event worth
  announcing.
- State changes (theme toggle, section expand/collapse) are instantaneous and
  require no confirmation.

### Forgiveness Over Prevention

Let users act freely. Protect them from catastrophe, not from small mistakes.

- Individual item deletion requires no confirmation — the action is small and
  easily reversed by re-entry.
- Bulk destructive actions (clearing all data) require explicit confirmation.
  This is the only ceremony around data deletion.
- There are no "are you sure?" dialogs for routine actions. Trust the user.
- Undo-ability is preferred over gatekeeping.

### No Dead Ends

Every state in the application should offer a clear path forward.

- Every page has an obvious next action or clear orientation.
- Motivational content leads to the tool. The tool leads to export. There is
  always a next step.
- Error states always provide an action the user can take (retry, reload,
  resume).
- Empty states are functional, not decorative. The empty form *is* the prompt.

---

## Tone of Voice

### Writing Principles

- **First and second person.** "I receive," "your money," "you can do this."
  The app speaks *with* the user, not *at* them.
- **Active, empowering language.** "Take Control of Your Money," not "Financial
  Dashboard" or "Budget Tracker."
- **Short sentences.** One idea per sentence. One concept per paragraph in
  narrative content.
- **Emotional emphasis through restraint.** Italics for emotional moments
  (*You can do this.*), not exclamation points or emoji.

### What We Don't Say

- No clinical or corporate language ("dashboard," "metrics," "KPIs").
- No shame-adjacent language ("overspending," "debt problem," "financial
  trouble").
- No false urgency ("act now," "don't miss out," "limited time").
- No hollow positivity ("everything is awesome!"). Encouragement must be honest.

### Contextual Guidance

Guidance text adapts to the user's situation:
- **Positive outcomes:** Cautiously optimistic. Acknowledge the good while
  recognizing that circumstances change.
- **Negative outcomes:** Action-oriented. Frame as an invitation to adjust, not
  as a failure to fix.
- **Neutral/empty states:** Calm and welcoming. The tool is ready when you are.

---

## Privacy as a Feature

Privacy is not a legal obligation we grudgingly fulfill — it is a core product
feature and a trust signal.

- **No data leaves the browser.** All user financial data is stored locally.
  There are no server-side databases, no analytics on user finances, no data
  sharing of any kind.
- **No accounts, no sign-up, no email capture — ever.** The tool works
  immediately, with zero friction. Identity is not required because we don't
  store anything to associate it with.
- **The privacy notice is a first-class UI element.** It appears early and
  prominently — not buried in a footer or hidden behind a link. It is written
  in plain language, not legalese.
- **Export is local.** File generation happens entirely in the browser. No
  server round-trip, no temporary cloud storage.
- **Clearing data is real.** When the user clears their data, it is gone. There
  is no soft delete, no recycle bin, no "we kept a backup just in case."

---

## Accessibility Standards

Accessibility is not a checklist item — it is a design constraint that applies
to every decision.

### Principles

- **Semantic HTML first.** Use the right element for the job (`<nav>`, `<main>`,
  `<dialog>`, `<button>`) before reaching for ARIA attributes.
- **Keyboard access is non-negotiable.** Every interactive element must be
  reachable and operable via keyboard alone.
- **Color is never the sole signifier.** Information conveyed through color
  (positive/negative amounts, validation states) must also be conveyed through
  text, icons, or symbols (+/- signs, labels).
- **Both themes must pass contrast.** New color tokens must meet contrast
  requirements in both light and dark modes.
- **Focus visibility matters.** Focus indicators must be clearly visible. They
  are a feature for keyboard users, not a cosmetic problem to suppress.

### Guidelines for New Features

- Test keyboard navigation before considering the feature complete.
- Use `aria-label` or `aria-labelledby` on controls without visible text labels.
- New modal or overlay patterns should use `<dialog>` for proper focus trapping
  and screen reader announcement.
- Verify contrast ratios in both light and dark themes.

---

## Design Token Philosophy

Visual consistency comes from a shared token system, not from copying hex values
between files.

### Principles

- **Tokens over hardcoded values.** All colors, and eventually spacing and
  typography, are defined as CSS custom properties with the `--ft-` prefix.
  Components must reference tokens, never literal values.
- **Semantic naming.** Tokens describe their *purpose* (`--ft-amount-positive`),
  not their *appearance* (`--ft-green`). This allows the palette to evolve
  without renaming.
- **Theme completeness.** Every token must have both a light and a dark mode
  value. Adding a token to one theme without the other is a bug.
- **Brand coherence.** The token palette derives from the Riverbed brand
  identity. New tokens should feel like they belong to the same family — warm,
  natural, grounded.
- **Restraint.** Not every subtle variation needs its own token. Prefer reusing
  existing tokens with opacity or derived values over proliferating new ones.
