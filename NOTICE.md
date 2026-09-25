# Source and attribution

Original ShotCab source and original ShotCab contributions to vendored files are
offered under GNU GPL version 3 or any later version (`GPL-3.0-or-later`).
The ShareX portions retain their upstream GPLv3 terms; the combined program is
distributed under GPLv3. The full GPLv3 text is in [LICENSE](LICENSE).
Copyright remains with the respective contributors.

ShotCab includes selected, modified source files from
[ShareX 17.1.0](https://github.com/ShareX/ShareX), pinned to commit
`7c0537fd8f55ab865a668aae4e0c915bd350b4f7`. ShareX copyright and
license notices remain in `upstream/ShareX`; see
[docs/UPSTREAM.md](docs/UPSTREAM.md). ShotCab is independently maintained and
is not an official ShareX release. Do not remove upstream notices when
redistributing a modified version.

**Modification notice (2026-09-26):** ShotCab changes selected ShareX
capture and image-editing source files to build the ShotCab application and
provide its editor and capture workflow. The vendored code is therefore not
an unmodified upstream release. The original upstream revision and scope are
documented in [docs/UPSTREAM.md](docs/UPSTREAM.md).

The repository contains source for ShotCab's optional offline OCR worker, but
does not contain OCR models or redistributable binaries. Its dependency and
model provenance is recorded in [docs/OCR-NOTICE.md](docs/OCR-NOTICE.md).
Third-party code and assets used by the vendored ShareX libraries retain their
own terms; upstream notices are in `upstream/ShareX/Licenses`. In particular:

- Some imported icon resources come from **Fugue Icons** by
  [Yusuke Kamiyamane](https://p.yusukekamiyamane.com/) and are licensed under
  [Creative Commons Attribution 3.0](https://creativecommons.org/licenses/by/3.0/).
  The license text is preserved in
  `upstream/ShareX/Licenses/Icons_license.txt`. These are upstream assets, not
  original ShotCab artwork.
- The imported **Blob Emoji** sticker pack is credited to Google, Arjen
  Nienhuis, and the Blob Emoji community under Apache License 2.0. The pack is
  in `upstream/ShareX/ShareX.ScreenCaptureLib/Stickers/BlobEmoji`; its source
  notice is preserved in `upstream/ShareX/Licenses/BlobEmoji_license.txt`.
  [Blob Emoji](https://blobs.gg/) identifies its official server images as
  Apache-2.0-licensed. The Apache license does not grant trademark rights;
  individual character references in stickers may require separate review
  before an installer bundles this pack.

Anyone building a binary distribution should review and include the notices
for the actual components and artwork shipped with that build.
