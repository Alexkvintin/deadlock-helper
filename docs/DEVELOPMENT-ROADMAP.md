# Development checkpoint and roadmap

19 November 2026. Local development branch `codex/review-data-integrity`, based on the published v1.1.0-beta.1 source (`a139fe6`). The original development worktree and the release worktree's existing scripting changes are preserved. No release or remote update is authorized by this checkpoint.

## Completed before this increment

- Scout roster, public profiles, current-hero history and explained tags; inferred party labels; Match Review with recorded progression, item history, contribution cards and comparison views.
- A portable prerelease was published on 13 November. The local checkpoint `G:/OwerLock/docs/SCOUT-2.2-FINAL.md` records prior validation, remaining limitations and the published version.

## Completed in this increment

- Validated the metadata match, account, hero, duration, timestamps and item IDs before converting values. A duplicate account or ambiguous killer slot is not assigned a player's statistics. A missing killer cannot match a missing roster slot. Final totals and the final chart sample come from the same valid record; an invalid future timestamp no longer hides an otherwise valid completed result.
- Partial recorded timelines and item events remain available without claiming final totals. Near-end samples are represented within the chart's actual time axis. Zero and missing values remain distinct. Chart tooltips now show the exact unrounded recorded number.
- Sale times outside the recorded item/match interval and zero sale sentinels are excluded. Raw upgrade IDs are retained for further research. Item event categories no longer imply unique purchases or a verified final inventory.
- Historical selected-player disk details remain available offline after their online refresh interval. A failed or incomplete refresh no longer replaces an available final result with missing data.
- Removed the unused synchronous item catalogue read in the player detail window. Item asset work is requested only for the Items tab, and updated item ID sets are handled on a later metadata refresh.
- A bounded, in-process cache shares one public metadata response across selected players of the same match. At most 16 matches are retained; complete entries expire after 15 minutes, partial entries after one minute. Requests are cancellable; the profile detail service shares its API cooldown across instances. Only selected player details are persisted. Privacy and README explanations updated.
- HTML export resolves portraits from the runtime asset cache as well as bundled assets. Missing, invalid or oversized files fall back to a placeholder rather than preventing the report from being generated. Team comparisons require six players in each team rather than just 12 total records.

## Validation

- Managed application compiled successfully in this worktree. No native reader rebuild or live game process interaction was needed.
- New `ReviewDataIntegrityTests.cs`: 25 checks, including real zero vs missing values, invalid times, duplicate accounts, partial chart rendering, exact tooltips, sale time bounds, ambiguous killer slots, aged offline cache, shared match response, identity checks, cache eviction, cancellation, offline export placeholders, synthetic runtime portrait, corrupt portrait and incomplete team roster.
- `FinalizationTests.cs`: 46 checks. `ScoutProfileTests.cs`: 128 in the isolated worktree; the prior run against the original workspace's recorded metadata added the 129th check. Scout 33, Scout 2.1 109 and layout suite passed earlier in this increment.
- The older `ReviewTests.cs` HTML portrait assertion originally failed on both the published assembly and the initial change because export ignored runtime portraits. After the fix, the legacy suite passed all 781 checks with the existing runtime cache. The new fixture independently tests a synthetic portrait and the missing/corrupt fallback, without redistributing game artwork. Actual gameplay FPS, mouse latency and physical multi-monitor/DPI remain unmeasured here.
- Final offline managed regression also passed Core (34), Live (`--headless`), Pixel, Connection, LocalSource fixture, HUD/UI and Scout Profile (129, using the saved public metadata fixture). The previously passed Scout/Scout 2.1/layout suites were not repeated after changes limited to HTML export. Unchanged native reader and JavaScript bridge builds/tests were not rerun.

## Next priorities

1. P0 Scout switching: completed in the follow-up below. Keep legacy export fixtures independent of the real runtime cache in subsequent test maintenance.
2. P1: final inventory research checkpoint completed in Live Match Intelligence. Authoritative upgrade/removal semantics or a verified final inventory source remain required. The visible history is improved without inventing equipped items. Build interaction and role analysis only from verified observed data.
3. P1: physical FHD/1440p multi-monitor/DPI and controlled gameplay frame-time measurement when a suitable session is available. No new polling or capture mechanism is needed for the report improvements.
4. Deferred: patch-aware LOW/HIGH calibration requires independent verified reference data and acceptance criteria in `docs/PERFORMANCE-CALIBRATION.md`. Current party membership also requires an authoritative source. Keep unsupported labels disabled.

## Scout profile switching follow-up

Based on `a455125`. Completed the P0 switching task without redesigning Scout or changing Match Review controls.

- The selection worker has one active selection and at most one pending (latest) selection. Superseded pending work is discarded; identical active selections are retained. Cancellation and HTTP abort happen on workers, outside the UI thread. Every publication is gated by the selection generation and validated account, match and hero.
- Concurrent consumers of the same match share one transport and each retain their own cancellation token. A new profile that needs the current match attaches before the previous consumer is cancelled. The shared transport is cancelled only when its last consumer leaves. Metadata transports run one at a time; actual profile HTTP requests are limited to two across instances. The existing 16-match bound, 15-minute completed/one-minute partial metadata TTLs and provider cooldown are retained.
- Historical disk records and the in-memory selected-player cache remain available while refreshing. A failed or less complete refresh does not replace a verified result; per-record retry suppression avoids repeated immediate retries. Atomic file replacement prevents competing selected-player writes from truncating a valid cache. A changed hero identity does not borrow an older match's statistics.
- The profile updates its selected identity and clears obsolete hit targets immediately; the header indicates loading for the active account only. Worker notifications are coalesced into UI invalidations and hidden/closed profiles cancel unnecessary work. Returning to teams restores the team scroll position.

Validation: controlled local fixtures in `ScoutSwitchTests.cs` cover rapid and sequential switching among all 12 participants, one shared download, repeat-open, cancellation with another consumer still waiting, cancellation of all consumers and queued work, unrelated match switches, late responses, API failure and incomplete refresh, cache retention, and changed hero identities. Final targeted checks: Scout switching 101, Review data integrity 25, Scout profile 128. Physical gameplay responsiveness and real API latency were not measured in this task. No full legacy regression was repeated.

## Resume

### Live Match Intelligence 1.2 development

See [LIVE-MATCH-INTELLIGENCE.md](LIVE-MATCH-INTELLIGENCE.md) for the integrated implementation and verification. The 1.2 dev increment adds roster identity reconciliation, Ranked/spectator perspective gating, team/lane/situational recommendations, manual lane and ownership selection, Scout live synchronization, expanded tactical notes and chronological item event analysis. Remaining real checks are confirmed automatic lane evidence, physical Ranked swaps, real gameplay frame-time/DPI, authoritative inventory and calibrated style cohorts. Do not describe these as completed.

Worktree: `G:/OwerLock/artifacts/review-data-integrity`. Latest local executable: `dist/live-intelligence/DeadlockHelper.exe` (`1.2.0-dev`, kept separate from the previous running build). Run `build.ps1` when the regular output is not locked, then compile the relevant test executables in `tests` against the resulting managed assembly using the .NET Framework x64 compiler; the source workspace retains broader legacy regression fixtures. No new ZIP or GitHub release has been created.
