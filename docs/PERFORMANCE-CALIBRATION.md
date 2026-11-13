# Performance calibration protocol

12 November 2026. LOW/HIGH remain disabled. This is a bounded experiment plan, not a calibrated model.

## Evidence and missing fields

Sources inspected: [API specification](https://api.deadlock-api.com/openapi.json), [metadata protobuf](https://github.com/SteamDatabase/Protobufs/blob/master/deadlock/citadel_gcmessages_common.proto), and one already downloaded completed match. Metadata has hero, mode, duration, rank data, player damage, boss damage, ally healing and economy. Its `game_mode_version` is NOT a verified balance-patch identifier. Never substitute it for one. Our inspected cache therefore supplies zero records with verified patch provenance. No independent labeled cohort exists locally; empirical error and bootstrap stability are unavailable, not zero.

The same protobuf's player metadata has no current party roster. Lobby/party messages elsewhere in the protocol do not imply public access. No additional party endpoint was demonstrated. Historical friendship-filtered matches remain inference only.

## Bounded collection

Start with one hero / mode / rank-band / verified patch interval. Request no forced Steam history refresh. Pilot: 80 prior peer observations across at least 40 distinct matches / 20 other accounts, then 30 held-out players with 20 complete later observations each. At most 30 history requests plus 680 distinct metadata requests before deduplication; stop at this budget, on missing patch provenance, or on server cooldown. Reuse one response for multiple players when appropriate. This is pipeline validation, not enough for population-wide labels.

The saved API specification documents metadata limits by backend: IP cached 100/s, S3 100/10s, Steam 3/hour. Backend availability is not guaranteed. At a conservative single request per second the cached/S3 ceiling costs about 12 minutes plus history/network time; if Steam retrieval is required, 680 records could require at least 227 hours at that limit. Do not attempt to evade this. Stored history and bot-refreshed history have different limits; never set force_refetch. Honor Retry-After for 429/503/504 and postpone collection rather than hammering failed requests. No financial API purchase is authorized or required.

## Reproducible acceptance experiment

Use the existing PerformanceEngine, without modifying weights or enabling it in production. Record explicit training and validation account lists, timestamp cutoff, verified patch source, dataset SHA-256 and missingness. Require no account overlap and no future peers. Baseline contract: at least 40 prior matches / 20 accounts for every target match, same hero/mode/patch and rank band. Target contract: 20 complete matches. Compare raw weights 35/25/15/15/10 with the existing support variant only when role labels are independently supported.

Run `tools/CalibrationExperiment.cs` against the audited JSON dataset. It evaluates the existing engine, reports coverage and label MAE only when independent reference scores exist. These reference scores must come from blinded structured review, not wins alone. Test deterministic repeated execution. For subsequent robustness acceptance, bootstrap complete player windows by account (fixed seed, 1,000 replicates), not individual correlated matches; require the 95% score interval to stay within 10 points for enabled labels and fewer than 5% LOW↔HIGH flips across resamples. Audit threshold groups independently by hero/rank/mode; report coverage before dropping incomplete cases. At least 200 independent held-out windows per supported cell is the next validation target. These are proposed acceptance criteria, not measured results.

No scores become production-ready merely by passing synthetic tests. If verified patch or independent labels are unavailable, keep LOW/HIGH unavailable and continue shipping the factual profile and match-review features.
