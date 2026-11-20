# Live Validation 1.2.1

20 November 2026. Local development build `1.2.1-dev`, based on `93815c9` in `codex/review-data-integrity`. No release or push, license changes, game-file changes, new native reader or new screen capture.

## Implemented

- Live roster reconciliation preserves account identities; stale sequences and conflicting broadcast ticks are discarded. Explicit non-spectator identity recovery now also works with pregame or temporarily incomplete snapshots. The perspective is resolved before Scout observes the managed state. Team/lane statistics are never synthesized.
- Enable **Live state diagnostic log (optional)** in About to record match changes, confirmed hero changes, original/confirmed lane changes, counter recomputations and rate-limited stale-event rejections with UTC timestamps in the existing size-rotated local `errors.log`. The option defaults to off. These new events contain only match numbers, source labels, public hero/team/lane numbers and counts, not names, account IDs, tokens, raw packets or session identifiers.
- **Counter Items** (tray or Overlay settings): direct opponent portrait buttons allow multiple selections; selected accounts stay selected through a hero swap and temporary source outage. Clear lane selection is one click. Auto, Team, Lane and Situational categories are preserved. Rows have ranked positions, catalogue cost, now/later/unknown affordability, target and source reason. User-marked purchases have names and can be individually undone or cleared; they are not detected inventory. New matches reset the match-specific selections and marks.
- Counter icons load in bounded batches independently of recommendation recomputation; cached icons are reused; visible details refresh at most five times per second. Catalogue refresh retains its cooldown across match transitions.
- Scout caches party analysis separately from hero-sensitive decorations and reuses unaffected player decorations on hero-only changes. Profile tactical notes show up to three historical observations with ally/enemy-specific guidance and a minimum five-game hero sample. Source, rank and party evidence remains available in an expandable section. No rank-based strength claims or confirmed-party claims are invented.
- Match Review has a Compare view for K/D/A, hero damage, objective damage, healing, net worth and the existing Impact Score. Missing totals remain missing. The item history has category filters, highlighted recorded sales, event times, icons/names when available and raw recorded upgrade ID in the tooltip. The build direction describes the predominant observed shop category when supported; counts include subsequently sold items. No final equipped build or optimality claim is inferred. Inventory analysis is cached by detail/catalogue and only visible item rows are painted.

## Verified in a real spectator match

User supplied match `140709031` while watching it. The explicit opt-in `PublicSpectatorSmoke.cs` ran the existing public connector for a bounded period, using its normal cooldown and without game access, OCR, UI automation, profile requests or starting a Ranked game.

| Measurement | Observed |
| --- | ---: |
| Total elapsed until full roster and two distinct ticks | 3,508 ms |
| Published snapshots | 6 |
| Known roster rows / identified heroes | 12 / 12 |
| Rows with hero damage, objective damage and healing | 12 |
| Original lane assignments | 12 |
| Confirmed actual starting lane assignments | 0 |

This is one successful public spectator connection and live statistics observation, not a claim about universal connection latency. No actual local Ranked hero swap, lane swap, pregame-to-end transition or FPS/frame-time measurement was performed. The test did not determine the player's camera target. No automatic or new capture mechanism was added.

## Controlled local validation

`LiveIntelligenceTests.cs`: 71 checks, including a shared 12-player pregame snapshot driving Scout and advice, two real button click handlers, visible selection marks, long nickname, hero swap updating Scout decoration and counter context, reuse of unrelated decorations, rejection of stale response, retained manual choices on outage, per-item undo, scoped new-match reset, final-state report capture, category filtering, compact tactical notes and Compare rendering. The Counter Items viewport was rendered at 1280x720, 1920x1080 and 2560x1440 at simulated 100/125/150/200% scales. Layout checks at smaller and larger overlay scales passed; this is not a physical monitor/DPI test.

Integrated offline regression: Scout switching 101, Review integrity 25, Scout profile 128, Finalization 46, Scout 30, Scout 2.1 109, Review 796, Core 34, Layout, LocalSource, HUD/UI, headless Live and Connection passed. Legacy fixtures are located in the original workspace; Layout and HUD/UI passed when invoked from that workspace rather than the isolated source tree. The final localized UI and Scout cache adjustments were followed by Live Intelligence, Scout switching/profile/Scout/Scout 2.1, Review integrity, HUD/UI, Review and LocalSource checks.

Managed portable EXE built successfully, dependencies were retained with the existing runtime companion (SHA-256 identical to the preceding local build). The offline visual-review executable reached input-idle without exiting; no online runtime smoke was performed against local gameplay. The actual spectator check above used the final assembly.

## Outstanding evidence and reproduction

1. Before a future Ranked match, start the new portable build and enable the optional live diagnostic log. Keep Scout on the other monitor. No need to alter game files or capture settings.
2. During pregame note the initial hero, change it if normal gameplay permits, verify the same player identity, portrait and counter suggestions. If line assignments actually change, compare the reported original/confirmed fields; if only original exists, use the portrait selection rather than guessing.
3. Select two opponents, mark and undo an item, then confirm the choices remain after a temporary outage. Pause should not open a report; confirm normal progression and match completion produce appropriate review. Check that a new match resets the old context and that missing statistics are not replaced by zeros.
4. Test actual FHD/QHD monitors and Windows DPI settings. For performance, compare equal-duration, comparable scenes with the helper off/on using the user's usual FPS/frame-time monitoring. Record mean FPS, percentile/frame time, mouse responsiveness and scene settings; no synthetic measurement substitutes for this.
5. The existing source exposes no authoritative confirmed live lane or final equipped inventory. The recorded item event stream does not establish upgrade/removal semantics; a verified catalogue/source is required before extending build analysis to specific synergy/anti-mechanic labels.

Local package: `dist/live-validation-1.2.1/DeadlockHelper.exe`. Keep the accompanying files together. Previous working packages remain intact.
