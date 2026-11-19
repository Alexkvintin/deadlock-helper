# Live Match Intelligence 1.2 development checkpoint

19 November 2026. Branch `codex/review-data-integrity`, based on `e73a342`. Local version `1.2.0-dev`; the published release remains v1.1.0-beta.1. No game files, native reader implementation, license or release publication changed.

## 1. Hero and lane context

- `LiveRoster` preserves reported identity by Account ID within the match. Pre-game snapshots with a known match ID are accepted without publishing live counters. The existing Scout pre-game path also handles an unknown match ID. Temporary missing heroes or partial rosters retain known identities; numeric statistics are never copied across snapshots. A new match clears the previous roster. Older pending/processed sequence numbers and conflicting same-tick broadcast hero updates are rejected.
- Original assignment, HUD ordering hint and explicitly confirmed starting lane are separate fields. A missing confirmed lane removes lane-specific recommendations but retains the team context. A user-selected opponent follows their account through a hero change and is scoped to the match and chosen perspective.
- The existing local producer reports a fallback `lane` value, which cannot distinguish original versus current assignment reliably. Public broadcast `lane` explicitly describes the original assignment. No confirmed live lane field was demonstrated. Accordingly auto lane guidance requires an explicitly confirmed field; otherwise the player chooses manually. There is no new polling of positions or hidden game data. Physical lane swaps and Ranked hero swaps still require live verification.
- Ranked perspective can recover a unique local account when the source explicitly reports non-spectator mode but temporarily omits its local flag. Explicit spectator mode is never overridden by account membership.

## 2. Counter Items

- Open **Counter Items** from the tray or Overlay settings. Modes: Auto, Team, Lane and Situational. Auto prioritizes known lane opponents during the first eight minutes and team counters afterwards; the upstream item stage model remains early <12 minutes, mid <25 minutes, late thereafter.
- The window shows item icons when cached, names, current catalogue prices, hero targets and specific source explanations. Team results combine recommendations for the actual enemy roster; Lane filters to confirmed or manually selected opponents. If no lane is known, the overlay retains team suggestions. Situational rows describe conditional cases from the public Counter Helper guide, not events detected in-game. The current source provides authored recommendations, not verified win-rate comparisons.
- Current available souls affect affordability and order; expensive options remain visible as future choices. Inventory is not available from the verified source; **Mark selected item owned** excludes a user-marked item across all modes, and **Clear marked items** resets these marks. They are per match/perspective and are not treated as a complete inventory.
- Recommendations recompute only when the player, enemy roster, stage, selected lane, affordability threshold, user marks/settings, or catalogue revision changes. Per-player recommendations are bound to identity *and hero*, preventing a new portrait from displaying old results. Item images use the existing asset cache and a bounded background queue. Source explanations are English; UI labels are RU/EN.

## 3. Scout sync and tactical insights

- Scout uses the same identity reconciliation for local/pre-game and public/active snapshots. The current hero updates portrait and hero-sensitive tags without recreating historical profiles. Existing generation-safe profile switching, MAIN rules, independent tags, and inferred party semantics remain in place. Party colors remain attached to group identities; unchanged assignments no longer rewrite the marker cache. Roster rendering is invalidated for changed data, options/assets or the periodic stale-data label, rather than every visible tick.
- Tactical notes now include current-hero sample KDA, kills + assists per ten minutes and recent scored results where there is enough data. Existing deaths, economy and actual ally-healing observations remain. Same-hero, same-mode objective damage compares two five-game windows; enemy/ally guidance is conditional on that observed change. The unavailable role/rank/patch cohorts still prevent labels such as aggressive, cautious or weak; no LOW/HIGH or guessed teamfight participation is activated.

## 4. Match Review and item evidence

- The public [metadata schema](https://github.com/SteamDatabase/Protobufs/blob/master/deadlock/citadel_gcmessages_common.proto) defines item ID, event time, upgrade ID, sale time, flags, imbued ability and packed upgrade info. It does not document a final equipped inventory or the exact semantics of upgrade/flag transitions. The existing stored public match contains 381 item records across 12 players, 75 positive sale timestamps, 333 nonzero upgrade IDs and 48 groups of repeated item IDs. No explicit final inventory field was present.
- Preserve raw optional upgrade fields on normal metadata refresh (no forced bulk refetch). Match Review now interleaves valid sale events with item-event chronology, shows unique known shop item categories and recorded sale counts, and discloses unclassified records. Known non-shop/ability records are excluded. No final inventory or total spend is guessed from the records. Authoritative upgrade/removal semantics or a verified final state remain the blocker.

## 5. Validation and local package

The controlled `LiveIntelligenceTests.cs` suite includes hero swaps, confirmed lane swaps, partial input, stale sequence and same-tick conflicts, spectator perspective, manual choice, owned exclusion, stage change, stale counter suppression, tactical comparisons, chronological sales, and Counter Items form rendering at 1280x720, 1920x1080, 2560x1440 with 100/125/150/200% simulated scaling. Existing Scout switching/integrity/profile/review tests are reused. No physical DPI matrix, actual Ranked game, live frame-time or mouse latency measurement was performed in this increment.

Final managed build and 47 live intelligence checks passed. Integrated regression passed Scout switching (101), review integrity (25), profile (129 including the local recorded fixture), finalization (46), legacy Review (791), Scout (33), Scout 2.1 (109), Core (34), layout, LocalSource, headless Live, Connection, HUD/UI and Pixel suites. The final narrowly scoped Ranked perspective correction was then verified by Live Intelligence, LocalSource, HUD/UI and Scout suites. The Pixel rendering benchmark is an offline rendering measurement, not gameplay FPS.

Local build: `dist/live-intelligence/DeadlockHelper.exe`. Keep its accompanying files together. The existing verified v1.1.0-beta.1 runtime companion is unchanged; no new game component is installed. Do not publish this development version without permission.
