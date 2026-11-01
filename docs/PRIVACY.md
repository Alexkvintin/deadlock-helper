# Privacy

Deadlock Helper does not require an application account and does not collect product analytics or automatically upload feedback, screenshots or logs.

The local helper reads match data from the running game using read-only process access. It communicates with the desktop application through a private child-process output pipe. It does not inject code or install game input hooks.

Online features request public match data, hero and item assets, and recommendation data. Match discovery can send the locally detected Steam account ID to the public match API; manual connections send the entered match ID. These requests necessarily expose ordinary connection metadata to their destination services. They do not send Steam passwords or authentication cookies.

Optional screen recognition processes the relevant game regions locally. Captured images are not uploaded. Settings, cache files and diagnostic logs are stored locally. Logs may contain match identifiers and errors.

Feedback buttons open GitHub in your browser. You decide what to type, attach and submit. Repository access controls apply while the project is private. No feedback is sent automatically.
