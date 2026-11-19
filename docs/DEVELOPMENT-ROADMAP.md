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

1. P0: verify cache stability during fast profile switching and concurrent public-metadata requests, using controlled local fixtures and the existing request limits. Extend failure and cancellation coverage only where new behavior needs it. Keep legacy export fixtures independent of the real runtime cache in subsequent test maintenance.
2. P1: investigate final inventory using the already saved public metadata and authoritative item upgrade/removal definitions. The observed `upgrade_id`, `upgrade_info`, `flags` and `sold_time_s` do not by themselves establish final equipped inventory. Do not infer inventory if event semantics remain ambiguous. Build interaction and role analysis only from verified observed data.
3. P1: physical FHD/1440p multi-monitor/DPI and controlled gameplay frame-time measurement when a suitable session is available. No new polling or capture mechanism is needed for the report improvements.
4. Deferred: patch-aware LOW/HIGH calibration requires independent verified reference data and acceptance criteria in `docs/PERFORMANCE-CALIBRATION.md`. Current party membership also requires an authoritative source. Keep unsupported labels disabled.

## Resume

Worktree: `G:/OwerLock/artifacts/review-data-integrity`. Local executable: `dist/DeadlockHelper.exe`. Run `build.ps1` and compile the relevant test executables in `tests` against `dist/DeadlockHelper.exe` using the .NET Framework x64 compiler; the source workspace retains broader legacy regression fixtures. No new ZIP or GitHub release has been created.
