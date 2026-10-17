[![CI](https://github.com/Reim-developer/Shiranuix/actions/workflows/ci.yml/badge.svg)](https://github.com/Reim-developer/Shiranuix/actions/workflows/ci.yml)

# Shiranuix

A small plain-text editor for Windows, kept for one writer — the author. It
opens ordinary `.txt` files offline, runs on Avalonia and .NET 10, and
carries its own file tree and command palette.

> **Not a Word replacement.** This is simply the version the author uses
> every day — it may have bugs. If it fits how you write, install it and
> use it as it is.

## Build and run

The .NET 10 SDK is the only requirement.

```bash
dotnet build Shiranuix.slnx
dotnet run --project Shiranuix -- path\to\file.txt   # or cd Shiranuix && dotnet run
dotnet test                 # unit tests, no window needed
dotnet format               # the house style
```

## What a launch costs

A writer who opens a text editor is waiting, so two numbers matter: how long
before a window exists, and how much memory the thing keeps while nothing is
happening. Both are measured, not guessed.

```bash
dotnet publish Shiranuix/Shiranuix.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish
uv run tools/measure.py --exe publish/Shiranuix.exe --runs 4
```

`tools/measure.py` (Python, [uv](https://docs.astral.sh/uv/), type checked in
strict mode by `pyrightconfig.json`) launches the editor and reports time to
first window, working set, private bytes and CPU. Three profiles, because
they are three different claims:

| Profile | What it is |
| --- | --- |
| `fresh` | an empty data folder: the store is created, the schema brought up |
| `minimal` | the writer's own settings, nothing else — the plain editor |
| `existing` | the writer's own settings, files, chords and fonts, as a daily user leaves it |

Windows decides where `%APPDATA%` is through an API that ignores the
environment, so the tool moves the editor's folder with `SHIRANUIX_HOME`
instead — which is also how the editor runs off a memory stick.

Release build, median of four launches on an idle machine:

| | Before | After |
| --- | --- | --- |
| To a window | 2213 ms | **750 ms** |
| Working set | 230 MB | **203 MB** |
| Private bytes | 137 MB | **133 MB** |
| Exe size | 108 MB | 162 MB |

The startup win is `PublishReadyToRun` (the work the JIT would do at first
launch is done at publish time) and `InvariantGlobalization` (one language and
a handful of numbers do not need ICU loaded, which is tens of megabytes). The
memory win is mostly that same ICU decision; the rest is the store reading
with tracking off, and the typeface catalogue no longer being read twice.

**One caveat worth knowing**: the first launch after publishing takes seconds,
not milliseconds. Windows Defender reads every newly written assembly before
the first run of a fresh build. Measure twice; the second run is the one a
writer will feel.

**One door left open**: reading the machine's installed typefaces costs about
26 MB, and the toolbar's font list is bound eagerly so that cost is paid at
startup. Filling it when the list is actually opened measures 177 MB instead
of 203 MB, but it depends on an event this project's tests cannot fire, so it
stays closed until somebody can click it with a real mouse.

## How a change becomes a release

```
dev ──PR──▶ main ──merge──▶ tag vX.Y.Z ──▶ release artifact
```

- **`dev`** is where changes land. CI runs on every push to it and on
  every pull request.
- **`main`** is release-only: it takes nothing but a pull request from
  `dev`, and the pull request merges only after CI (`build` + `guards`)
  is green.
- On the pull request, the **prepare-version** job moves
  `AppVersion.cs` and `Shiranuix.csproj` together — the same edit a human
  would make — and commits the bump onto the pull request's own branch,
  so the checks run over what actually lands.
- The merge therefore carries the number, and the **tag** job cuts
  `vX.Y.Z` from it. The tag **publishes** a self-contained, single-file
  Windows exe (`Shiranuix-X.Y.Z-win-x64.zip`) as a GitHub Release, after
  checking the code under the tag says the same number.

By default a merge moves the **patch** part. Put the intent in the pull
request's title or body:

| Marker | Result |
| --- | --- |
| `[version X.Y.Z]` | cut exactly this version |
| `[minor]` or a `feat:` merge | feature, no break |
| `[major]`, `feat!` or `BREAKING` | breaking change |
| *(nothing)* | patch |

`[release]` is the prepare job's own mark — never put it in a title by hand.

## The file tree

Down the left of the page runs the editor's own tree of files: browse, open,
make, rename, delete, mark several at once, sort, copy a path, open a folder
in Explorer, watch the disk. It is not something loaded from anywhere - it is
code the application holds, and it starts when the editor starts.

Its shape is the one every editor's file tree has: a heading with the folder's
own name and five buttons - open another folder, new file, new folder, refresh,
close every folder - then rows twenty-two points high holding a twisty, a
symbol and a name. A folder opens on the first click and a file opens on the
first click, guide lines say how deep a row sits, and a new name is typed in
the tree itself: the file or folder is born only when the name is given, and
turning away leaves nothing behind. A name too long for the column is cut off
rather than pushing the column wider. The side column opens at 240 px and can
be dragged between 180 and 360 - a double press on its edge puts it back.
Every symbol it draws is its own geometry rather than a character from a
font, so it is right in both themes and on a machine with no icon fonts
installed.

A folder is read a level at a time, and every file it changes goes through the
editor's own doors, so opening one from the tree asks about unsaved work
exactly as if the writer had opened it themselves. Standing the tree somewhere
else clears the page the way the New command does - asked first, and a writer
who keeps their work keeps the whole arrangement as it is.

## License

Shiranuix is free software under the **GNU General Public License, version 3
or later** (`GPL-3.0-or-later`). The full text is in [LICENSE](LICENSE).

Copyright (C) 2026 Shiranuix contributors.
