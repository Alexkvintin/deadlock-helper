# Credits

Application: **Deadlock Helper by kvintin**. Original material is offered under the root Deadlock Helper Personal Use License 1.1, subject to its scope and preserved earlier grants. Third-party components retain their own licenses.

Deadlock and game artwork belong to Valve. Runtime team icons and portraits are retrieved from [Deadlock Open Assets](https://github.com/0xThiagoAmaral/deadlock-open-assets), the [Deadlock API](https://deadlock-api.com/) asset catalog, and [Lovely's Deadlock Graphical Library](https://forums.playdeadlock.com/resources/deadlock-graphical-library.30/). Exact image sources and checksums are recorded in `assets/resource-register.json`. The application's license does not transfer ownership of these assets.

The local reader uses [deadlock-rs](https://github.com/deadlock-plus/deadlock-rs), pinned to revision `8b408b9103a022e7bac620bfa4916778b8fdfaa1`. License notices are included in `native-reader/licenses` in source and `local-reader-licenses` in the portable release.

OCR components: Tesseract, its .NET wrapper, Leptonica and tessdata_fast. Their notices are included in `vendor/THIRD-PARTY.md` and the accompanying license files, also present in the portable release.

Counter-item data: [Deadlock Item Builder](https://deadlockitembuilder.com/counter-item-helper). Public recommendation data is refreshed at runtime. No affiliation or endorsement is implied.

## Scope of the project license

The personal-use, distribution and monetization restrictions apply only to original Covered Material, not to separately licensed components. Existing copyright notices and license texts remain unchanged. MIT, Apache-2.0, BSD-2-Clause, Unicode and LLVM exception notices continue to govern the corresponding material; applicable notice, attribution and redistribution obligations must still be fulfilled.

**Unresolved artwork permissions:** `assets/resource-register.json` includes entries marked `redistribute: false` with no verified software-bundle redistribution license. A public download source or credit is not evidence of such permission. License 1.1 and permission from kvintin do not authorize use or redistribution of third-party artwork or runtime data beyond rights granted by its owner. See [the licensing review](LICENSE-REVIEW.md); no new third-party rights are asserted here.

Scout player profiles use static hero-card artwork supplied by the public Deadlock API asset catalog, with existing local high-resolution art as a fallback. Newly retrieved artwork stays in the runtime cache and is not added to the distributable. Steam avatars remain the property of their respective owners. Public availability is not treated as a redistribution license; no new permission to redistribute Valve/community art or avatars is asserted by this feature. Profile glyphs and layout are drawn by the application.

The v1.1.0 Beta 1 package excludes original game-image files without an explicit verified redistribution grant in the asset register. Legacy originals are removed from the release source tree; historical commits are preserved. Hero portraits and team emblems are retrieved into the local art cache when an online match requires them. Original application branding remains bundled.
