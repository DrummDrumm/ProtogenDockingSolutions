# Updating the plugin and releasing to PluginHub

Players get the plugin from [PluginHub](https://github.com/StarCpt/PluginHub). PluginHub holds one file for it,
`Plugins/ProtogenDockingSolutions.xml`, and that file's `<Commit>` says which commit of this repository Pulsar
downloads and compiles on players' PCs. Releasing an update means pushing a new commit here and getting a
PluginHub pull request merged that changes `<Commit>` to it. A maintainer reviews the code between the old and
the new commit by hand, so the update reaches players only after that.

| | |
|---|---|
| Plugin repository | https://github.com/DrummDrumm/ProtogenDockingSolutions (`main`) |
| Your hub fork | https://github.com/DrummDrumm/PluginHub |
| Hub | https://github.com/StarCpt/PluginHub |
| First submission | https://github.com/StarCpt/PluginHub/pull/361 |

## Quick way: the release script

Close the game, then in this folder:

```powershell
.\Release-PluginHub.ps1 -Version 1.2.0 -Message "What changed"
```

- `-Message` commits all your pending changes with that message. Leave it out if you've already committed.
- `-Version` sets the version in `ClientPlugin/BuildVersion.cs` and commits it. Leave it out to release the
  current version (for example to resubmit a fix during review).
- `-DryRun` does every check and writes the hub file, but commits, pushes and opens nothing. Run it first if
  you're unsure.
- `-ReviewInBrowser` opens the filled-in pull request page for you to check and submit, instead of `gh`
  opening the pull request right away.
- `-SkipTests` skips the Tests project. `-NoBrowser` prints the browser page's link instead of opening it.

What it does, in order (it stops at the first failure, before anything is pushed):

1. Checks you're on `main`, and commits your changes and the version bump.
2. Builds the plugin, runs the tests, and compiles it **the way Pulsar does on players' PCs**: only
   `ClientPlugin` and `Shared`, without `LOCAL_BUILD`, without the `.csproj` extras. This catches code that
   builds for you but not for players.
3. Pushes `main` to GitHub and checks that GitHub has the same commit.
4. Writes the hub file with the new commit into your fork (cloned under `artifacts/pluginhub/`), checks it with
   PluginHub's own validator and pushes a branch:
   - while the first submission isn't merged, it updates that pull request's branch, so the open pull request
     now points at the new commit;
   - after that, each version gets a branch `protogen-docking-solutions-<version>`.
5. Opens the pull request, titled for example `Protogen Systems Docking Solutions 1.2.0`, with a link to the
   changes since the listed commit. It uses the GitHub CLI (`gh`), signed in as you, so it posts publicly under
   your name without another click, and prints the pull request's link. If `gh` isn't installed or signed in,
   or you pass `-ReviewInBrowser`, it opens GitHub's filled-in pull request page instead; check it and click
   **Create pull request**.

**One-time setup for `gh`:** install it with `winget install --id GitHub.cli`, open a new terminal and run
`gh auth login --web --git-protocol https`. `gh auth status` should show `DrummDrumm`. A dry run says
whether the script will use `gh`.

The script can't test the plugin in the game. Do that before you run it (see below).

## By hand

1. **Change the code** and test it in game: `.\Deploy-LocalDev.ps1` (game closed), enable the local plugin in
   Pulsar, and go through [IN-GAME-TESTS.md](IN-GAME-TESTS.md).
2. **Raise the version** in `ClientPlugin/BuildVersion.cs` (`1.1.0` → `1.2.0`: the last number for fixes, the
   middle one for features).
3. **Check Pulsar can compile it**: everything the plugin needs must be in `ClientPlugin/` or `Shared/` (the
   only folders listed in `SourceDirectories`). Don't rely on `LOCAL_BUILD`, `DEBUG`, the `.csproj`, or
   `Api/`. If you publicize another game assembly, add it both to the `<Publicize>` entries in
   `ClientPlugin.csproj` and to `ClientPlugin/GameAssembliesToPublicize.cs`.
4. **Commit and push** to `main`: `git commit -am "…"`, `git push`. Copy the full hash: `git rev-parse HEAD`.
5. **Update your fork**: on https://github.com/DrummDrumm/PluginHub click **Sync fork**, then make a
   branch, edit `Plugins/ProtogenDockingSolutions.xml` and change only `<Commit>` to the new hash.
6. **Open a pull request** to `StarCpt/PluginHub` titled `Protogen Systems Docking Solutions 1.2.0`.
7. Wait for the review and answer comments. If the reviewer asks for changes, fix them here, push, and
   change `<Commit>` on the same branch again: the open pull request updates itself.

## Testing the exact release commit (recommended before step 6)

Pulsar → **Sources** → add your fork's clone (`artifacts/pluginhub/PluginHub` once the script has run) as a
local hub, disable the main PluginHub source, enable the plugin and restart. Pulsar then downloads and
compiles the commit exactly as players will get it. Compile errors appear in Pulsar's `info.log`.

## Rules worth remembering

- Never change `<Id>` (`47A4C4B6-BE43-4B1D-A3C6-85069FA67F89`): profiles refer to it.
- The `<Commit>` in this repository's `ProtogenDockingSolutions.xml` stays a placeholder. Only the hub's copy
  carries the real hash. Other changes to the manifest (description, tooltip) go into the hub copy too: edit
  them here, and the script copies them over on the next release.
- The description must stay under 1000 characters. The script checks it.
- No DLLs or other binaries in the repository, no network calls, no self-updating: updates go only through
  the hub.
- Pulsar ships Harmony, so don't add it to the manifest.
