# Spike: static landing page at `/`, Blazor app at `/app/`

Status: investigation complete, minimal PoC on this branch. **Not merge-ready** — see
"Known test breakage" below. This document is the deliverable for bead `fo-aaq`.

## Recommendation

**Proceed.** The split is mechanically sound, verified end-to-end on this branch, and
delivers the intended win: `/` now serves a self-contained 3.7 KB HTML document instead of
the WASM shell that pulls in a 19 MB `_framework` payload before any content exists. No new
outbound requests are introduced anywhere in the split. The main real cost is the test
suite rework enumerated below, which is mechanical rather than risky.

## 1. Publish layout — `StaticWebAssetBasePath`

Adding one line to `Footing.Client.csproj`:

```xml
<StaticWebAssetBasePath>app</StaticWebAssetBasePath>
```

relocates the **entire** Blazor publish output under `wwwroot/app/` — confirmed by an
actual `dotnet publish -c Release` on this branch, not assumed. Before:

```
publish/wwwroot/{index.html, 404.html, app.css, _framework/, js/, fonts/,
                 favicon.svg, favicon.png, apple-touch-icon.svg, manifest.json,
                 robots.txt, sitemap.xml, appsettings*.json, Footing.Client.styles.css}
```

After:

```
publish/wwwroot/app/{index.html, 404.html, app.css, _framework/, js/, fonts/,
                      favicon.svg, favicon.png, apple-touch-icon.svg, manifest.json,
                      robots.txt, sitemap.xml, appsettings*.json, Footing.Client.styles.css}
```

Important consequence not mentioned in the bead: `StaticWebAssetBasePath` moves
**everything**, including files that are conceptually site-wide rather than
app-specific — `robots.txt`, `sitemap.xml`, `manifest.json`, and the favicons. Those
need to live at the true site root, not under `/app/`. On this branch they were moved
out of `Footing.Client/wwwroot` into the new `src/Footing.Site/` directory (see §4) so
there is exactly one copy of each, at the root where they belong.

`Footing.Client/wwwroot/404.html` (the current spa-github-pages shim) was deleted
entirely — once it publishes to `app/404.html` it is unreachable dead weight, because
GitHub Pages never looks there (§3).

## 2. `<base href>` and route breakage

Blazor route templates (`@page "/find-my-footing"`) are always resolved **relative to
`<base href>`**, so `Pages/FindMyFooting.razor` needed **no changes at all** — with
`<base href="/app/" />` its route resolves to `/app/find-my-footing` automatically.
Verified by publishing and inspecting `app/index.html`.

What did need to change:

- `Footing.Client/wwwroot/index.html`: `<base href="/" />` → `<base href="/app/" />`.
  Canonical updated to `https://footing.app/app/find-my-footing`, since that's the one
  real page left in the app (see next point).
- `Pages/Index.razor` **deleted**. It was pure static markup (no `@code`, `@inject`,
  `@using` — confirmed by inspection) and is now served by the static root page
  instead. This leaves the app with no route at its own base (`/app/`); hitting `/app/`
  directly now renders the app's existing `<NotFound>` view (`Routes.razor`) rather than
  a blank page. That's an acceptable interim state — nothing in the app links to `/app/`
  bare — but is worth a follow-up (e.g. redirect `/app/` → `/app/find-my-footing`, or
  just accept NotFound since no real link ever points there).
- `Layout/NavMenu.razor`: the wordmark link was `href=""`, i.e. relative to
  `<base href>` — after the base moved to `/app/`, `href=""` would point at `/app/`
  (the now-empty app root) instead of the real marketing page. Changed to `href="/"`,
  an absolute link that leaves the app and returns to the static landing page. The
  `href="find-my-footing"` link on the same component needed no change — relative to
  the new base it still resolves to `/app/find-my-footing`.

## 3. The 404 shim — verified, not assumed

Claim checked against GitHub's own docs and multiple community reports (GitHub Docs
["Creating a custom 404 page"](https://docs.github.com/en/pages/getting-started-with-github-pages/creating-a-custom-404-page-for-your-github-pages-site),
[community discussion #160746](https://github.com/orgs/community/discussions/160746)):
GitHub Pages serves **exactly one** `404.html`, and it must be at the site root. A
`404.html` published under `/app/` is never served for a missing path under `/app/` —
confirming the bead's premise and the reason the old `wwwroot/404.html` had to move.

The new root `src/Footing.Site/404.html` merges two responsibilities:

1. **Real 404 content** for anything that isn't a live app route (title, "page not
   found" body, `noindex`, link home). This is what GitHub Pages serves (with an actual
   HTTP 404 status, since Pages sets that regardless of the file's content) for e.g.
   `/nonsense`.
2. A **scoped** spa-github-pages redirect, active only when the missing path starts
   with `/app`. Where the original shim used `pathSegmentsToKeep = 0` (rewrite
   everything at the root, because the whole site used to be the app), the new one uses
   `pathSegmentsToKeep = 1`, keeping the `/app` segment and only SPA-encoding what comes
   after it.

Logic (see the file for the real version) and its behavior, checked in Node against a
mock `window.location` rather than by inspection alone:

| Requested path         | Result                                              |
|-------------------------|------------------------------------------------------|
| `/app/find-my-footing`  | redirect → `/app/?/find-my-footing` → app restores to `/app/find-my-footing` |
| `/app` or `/app/`       | redirect → `/app/?/` → app restores to `/app/`      |
| `/nonsense`             | real 404 content rendered, no redirect              |
| `/find-my-footing` (old, pre-split URL) | real 404 content rendered — **known regression**, see below |

The app-side restore script in `Footing.Client/wwwroot/index.html` (`l.pathname.slice(0,
-1) + decoded + l.hash`) needed **no change** — it's already generic over the base path
as long as the app is loaded from a URL ending in `/`, which `/app/` satisfies.

**Known regression, not fixed here:** any external link or bookmark to the old
`/find-my-footing` URL (pre-split) now 404s instead of loading the app. A permanent
redirect rule could be added to the new 404.html for that one specific legacy path if
this is a concern (mayor's note confirms `sitemap.xml` already dropped that entry in
PR #60, so search engines aren't pointing at it, but external bookmarks/links could
still exist).

## 4. Deploy wiring

New directory **`src/Footing.Site/`** holds everything that is source-controlled site
shell rather than build output: `index.html`, `404.html`, `robots.txt`, `sitemap.xml`,
`manifest.json`, `favicon.svg`, `favicon.png`, `apple-touch-icon.svg`. It sits alongside
`Footing.Client`, `Footing.Tests.*` as an obvious peer in `src/`, and nothing in it is
generated — satisfies Riley's "must be a real file checked into source control"
requirement directly (unlike the rejected prerender approach).

`.github/workflows/deploy-pages.yml`'s existing "Prepare GitHub Pages output" step is
the right place, as the bead expected. One line added, before the `.nojekyll` /
`CNAME` writes so it can't clobber them:

```yaml
cp -r src/Footing.Site/. "$publish_dir/"
```

This runs after `dotnet publish` has already written `$publish_dir/app/*`, so it lays
the static shell into the same directory without touching `app/`. Verified locally by
running the publish + copy sequence exactly as the workflow will and inspecting the
resulting tree (not by reading the YAML and assuming) — final `publish/wwwroot/`:

```
404.html   CNAME   app/   apple-touch-icon.svg   favicon.png   favicon.svg
index.html   manifest.json   .nojekyll   robots.txt   sitemap.xml
```

`GET /` against that tree returns **200, 3702 bytes**. `GET /app/app.css` returns
**200, 34309 bytes** (the original file, not a copy — see §5). `GET /app.css` (old
location) now correctly 404s.

The workflow was **not dispatched** — everything above was verified by running the
underlying `dotnet publish` and shell copy locally and inspecting/serving the output
with `python3 -m http.server`, per the hard constraint against triggering
`deploy-pages.yml`.

## 5. CSS reuse — no duplication

`Footing.Site/index.html` links `app/app.css` directly:

```html
<link rel="stylesheet" href="app/app.css" />
```

Because `Footing.Site/` is copied to the publish root *after* `Footing.Client`
publishes its output to `app/`, this resolves to the one real `app.css` the Blazor
build already produced (34 KB, unchanged) — no second copy, no subset, no drift risk
between two stylesheets. The static page also reuses `app/js/footing.js` (for the dark
mode toggle button) and `app/fonts/rubik-latin.woff2` (preloaded, same as the app) the
same way. This does create a soft build-order dependency (`Footing.Site` assets assume
`Footing.Client` has already published to `app/`), which is already how the deploy
workflow is sequenced, but is worth calling out for anyone touching that step later.

## Known test impact (enumerated, not fixed in this bead)

Confirmed by actually running the suites on this branch (`dotnet test`, Release
config):

- **Unit** (63 tests) and **Integration** (11 tests): pass unchanged — neither touches
  `Index.razor` or routing.
- **Functional** (23 bUnit tests): pass unchanged.
- **E2E**: **fails**, and the breakage is wider than the bead's description — it isn't
  just `HomePageTests.cs`. This blocks CI either way: `ci.yml`'s `build-and-test` check
  runs E2E with `PLAYWRIGHT_REQUIRED=1`, so a follow-up PR implementing this split must
  update these tests in the same PR or CI will not go green.

  **New finding beyond the bead's enumeration:** the entire
  `Footing.Tests.E2E/FindMyFootingPageTests.cs` suite (9 tests) also fails, actually
  observed by running it, not inferred. Its private helper
  `NavigateToFindMyFooting()` hardcodes `page.GotoAsync($"{_fixture.BaseUrl}/find-my-footing")`
  — the pre-split URL. `PlaywrightFixture` serves the publish tree with
  `MapFallbackToFile("index.html")`, so on this branch that now falls back to the
  *static landing page* (the new root `index.html`), not the Blazor app, and
  `WaitForSelectorAsync("#moneyFlows", ...)` times out after 60s per test because that
  element is on a page that never loads. All 9 tests fail with the same
  `TimeoutException`. Fixing this is a one-line change (`/app/find-my-footing` instead
  of `/find-my-footing` in that one helper), but it means the real test-update surface
  for a follow-up PR is `HomePageTests.cs` **and** `FindMyFootingPageTests.cs`, not just
  the former.
  - `ErrorUi_IsSingleAndHidden` — loops over `["", "find-my-footing"]` against
    `BaseUrl`; needs `["", "app/find-my-footing"]`, and the `""` case now hits the
    static page which has no `#blazor-error-ui` at all — that path needs dropping from
    the loop, not just renaming.
  - `HomePage_HasFindMyFootingLink` / `HomePage_NavigatesToFindMyFooting` — look for
    `a[href='find-my-footing']`; the static page's link is `a[href='app/find-my-footing']`.
  - `HomePage_LoadsSuccessfully` — no change needed, still true for the static root.
  - `PageLoad_IssuesNoThirdPartyRequests` — still passes conceptually (manually
    verified zero third-party requests from the static page in this spike — see §3/§4
    server output), and stays pointed at `_fixture.BaseUrl` (the site root), so no
    rework needed; it becomes more load-bearing than before, not less.
  - `PageLoad_RendersSelfHostedRubik` — currently expects the font request from the
    root page; after the split the static root doesn't render Blazor content but does
    still `<link rel="preload">` and reference the same Rubik font via `app/app.css`,
    so this should keep passing, but wasn't run against a real browser in this spike
    (Playwright run was still in flight when this doc was written) — **verify in the
    follow-up PR, don't assume**.
  - Additionally, `PlaywrightFixture` serves the publish output with ASP.NET Core's
    `MapFallbackToFile("index.html")` (a real SPA fallback), which is **not equivalent**
    to GitHub Pages' `404.html` JS-redirect shim. The E2E harness cannot exercise the
    §3 redirect logic at all — that logic was validated separately in this spike with a
    standalone Node script mocking `window.location` (see §3), and should stay that way
    or gain a dedicated JS unit test; don't assume E2E green implies the GH Pages shim
    works.

## Follow-up beads (if this proceeds)

1. Implement the split for real: apply this branch's changes (or equivalent) — csproj
   `StaticWebAssetBasePath`, `src/Footing.Site/`, `NavMenu.razor` link fix,
   `deploy-pages.yml` copy step — as a reviewed PR.
2. Fix `HomePageTests.cs` per the enumeration above, in the same PR as (1) so CI stays
   green.
3. Decide whether `/app/` bare (no sub-route) should redirect somewhere or is fine
   showing `<NotFound>`.
4. Decide whether to add a legacy-URL redirect for `/find-my-footing` → `/app/find-my-footing`
   in the new 404.html, given it's a known regression (§3).
5. Once `/app/find-my-footing` is live in production and returns 200, restore its entry
   in `sitemap.xml` — explicitly flagged by the mayor as downstream of this spike, not
   in scope here.
