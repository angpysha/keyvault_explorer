fastlane documentation
----

# Installation

Make sure you have the latest version of the Xcode command line tools installed:

```sh
xcode-select --install
```

For _fastlane_ installation instructions, see [Installing _fastlane_](https://docs.fastlane.tools/#installing-fastlane)

# Available Actions

## Mac

### mac build_xcarchive

```sh
[bundle exec] fastlane mac build_xcarchive
```

Build a Mac Catalyst .xcarchive via dotnet publish (ArchiveOnBuild)

### mac export_developer_id

```sh
[bundle exec] fastlane mac export_developer_id
```

Export Developer ID–signed .app from an xcarchive

### mac create_dmg

```sh
[bundle exec] fastlane mac create_dmg
```

Build UDZO DMG (unsigned DMG until you run codesign_dmg / full lane)

### mac notarize_dmg

```sh
[bundle exec] fastlane mac notarize_dmg
```

Notarize DMG (notarytool) and staple app + DMG

### mac developer_id_dmg

```sh
[bundle exec] fastlane mac developer_id_dmg
```

Full flow: xcarchive → export (Developer ID) → DMG → codesign DMG → notarize + staple

### mac sign_mac_os_app_and_create_dmg

```sh
[bundle exec] fastlane mac sign_mac_os_app_and_create_dmg
```

Legacy lane name — same as developer_id_dmg

----

This README.md is auto-generated and will be re-generated every time [_fastlane_](https://fastlane.tools) is run.

More information about _fastlane_ can be found on [fastlane.tools](https://fastlane.tools).

The documentation of _fastlane_ can be found on [docs.fastlane.tools](https://docs.fastlane.tools).
