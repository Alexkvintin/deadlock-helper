# Privacy

Deadlock Helper does not require an application account and does not collect product analytics or automatically upload feedback, screenshots or logs.

The local helper reads match data from the running game using read-only process access. It communicates with the desktop application through a private child-process output pipe. It does not inject code or install game input hooks.

Online features request public match data, hero and item assets, and recommendation data. Match discovery can send the locally detected Steam account ID to the public match API; manual connections send the entered match ID. These requests necessarily expose ordinary connection metadata to their destination services. They do not send Steam passwords or authentication cookies.

Optional screen recognition processes the relevant game regions locally. Captured images are not uploaded. Settings, cache files and diagnostic logs are stored locally. Logs may contain match identifiers and errors.

Feedback buttons open GitHub in your browser. You decide what to type, attach and submit. Repository access controls apply while the project is private. No feedback is sent automatically.

## Player Scout

When Scout network analysis is enabled, valid participant Account IDs from the existing match source are sent to `api.deadlock-api.com` to retrieve public ranks, history and hero aggregates. Rank icons are retrieved from its asset endpoints. No Steam credentials or screenshots are sent. Scout and its network analysis can be disabled independently in Scout Settings.

Public responses are cached under `%LOCALAPPDATA%\DeadlockOverlay\scout-cache`, normally reused for 30 minutes. This directory contains player identifiers and public statistics and is not included in release packages. Cache files remain on disk until removed; close the application before deleting this cache folder if desired. Statistical tags are local heuristic descriptions, not verified personal attributes or proof of misconduct. Shared recent matches are not proof of party membership.

Scout also requests public historical teammate aggregates with the API's `same_party` filter. The provider defines this using same-team participation and friendship at match start, not an authoritative current party identifier. Scout combines this with recent same-team history to mark a group as likely; these correlated signals cannot confirm a current party. No separate Steam friends-list login or private API key is requested. Party display and inferred-group display can be disabled in Scout Settings. Rank timestamps and group evidence are retained alongside the public profile cache.

Opening a Scout player profile can request that player's public Steam profile, hero artwork and metadata for up to 20 displayed completed matches. One cancellable worker processes only the selected profile; the team-list view does not start these metadata requests. The Steam endpoint may return other public fields, including friends; the application uses the avatar URL and discards those other fields without persisting them. Avatar images are downloaded only from supported Steam image hosts. No Steam authentication or forced live profile refresh is used.

Selected-player match totals and images are cached locally in `scout-cache\profiles`. Valid match details are reused for seven days, unavailable results for six minutes, avatar images for seven days and hero artwork for 30 days. These are refresh intervals, not automatic deletion periods. Cached files remain until removed and are excluded from release packages. Disabling Scout/network analysis or leaving the profile cancels outstanding profile work. Clicking the Steam profile link opens the selected player's public Steam page in your browser.

The Scout 2.2 development build also caches the selected player's recorded timeline (up to 600 samples) and item events (up to 256), without retaining the other players' raw metadata. Opening Progression or Items in a match-review player window can request that one match through the same cache service, subject to the saved Scout network preference. Closing the player window cancels its worker. New local summary files retain the player's existing Steam identifier so these public records can be matched; older reports lacking that identifier cannot be enriched reliably. Older aggregate caches remain usable offline and are upgraded on an online refresh.

Online roster display may download missing hero portraits from the asset URLs in sources.json and team emblems from the credited public asset repository into art-library. Requests contain hero/team asset names, not Steam credentials or screenshots. These runtime images and player caches are excluded from release archives. Party letter/color assignments are cached locally for the current match.
