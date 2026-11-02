# Publication review — 2 November 2026

The public repository history (two original commits, 146 unique blobs) and both downloaded release assets were reviewed. GitHub-reported SHA-256 digests matched the downloaded ZIP and checksum attachment.

- Gitleaks 8.30.1: no findings in the original Git history or extracted release.
- Additional checks covered ASCII and UTF-16 strings in binary files, private keys, common provider-token formats, JWTs, secret assignments, credential-bearing URLs, private user paths and sensitive filenames.
- Three Steam-shaped numeric matches were the same generic ID conversion constant, not user account credentials. The sole detected email was the author's public GitHub noreply address.
- The Token field is empty in source and generated locally using a cryptographic random generator. No populated user token or settings file was published.
- Public API addresses are present; no private user API credentials were found. Public match lookup can send a detected account ID at runtime, as described in PRIVACY.md.
- No Actions runs or issues were present at review time. Screenshots added to this repository use fictional demo players and are rendered offline.

This is a bounded publication/secret review, not proof that all code and dependencies are free from vulnerabilities or that credentials could never be leaked in future changes.
