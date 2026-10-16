[![CI](https://github.com/Reim-developer/Shiranuix/actions/workflows/ci.yml/badge.svg)](https://github.com/Reim-developer/Shiranuix/actions/workflows/ci.yml)

# Shiranuix

A local-first plain-text editor for writing prose — stories, drafts, notes —
built with Avalonia 12 and .NET 10, offline, opening ordinary `.txt` files.

> **Not a Word replacement.** This is simply the version the author uses every
> day — it may have bugs. If it fits how you write, install it and use it as
> it is.

Issues and pull requests are always welcome.

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
| `minimal` | the writer's own settings, **no plugins** — the plain editor |
| `existing` | the writer's own settings and plugins, as a daily user leaves it |

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

## Plugins

An editor that cannot grow with its writer is a tool they stop using. A plugin
is an assembly in `%APPDATA%\Shiranuix\plugins\`, one folder each, beside a
manifest that says who it is and which contract version it speaks:

```
plugins/
  reading-time/
    reading-time.dll   ← named after the plugin, like its folder
    manifest.json
```

The folder, the manifest's `id` and the assembly file all carry the same name,
and the editor checks all three before it runs a line of somebody else's code.

What a plugin is asked to implement is written down, and the build fails when
that promise changes without anyone deciding it should: see
[`Shiranuix.Plugins/PublicAPI.Unshipped.txt`](Shiranuix.Plugins/PublicAPI.Unshipped.txt)
for the whole surface, and [`samples/reading-time`](samples/reading-time) for a
whole plugin in five small files.

A plugin is handed the document — and only the document — by asking for it:

```csharp
public Task InitializeAsync(IPluginContext context, CancellationToken token) {
    _editor = context.Capability<IEditorHost>();   // null on a host with none
    return Task.CompletedTask;
}
```

Everything it does to the text goes through that door, which is what puts the
change in the writer's own undo history: one Ctrl+Z takes back exactly what the
plugin did. One bad plugin is one bad plugin — a manifest with a typo, an
assembly that will not load, a contract version that does not match — and each
is a line in the log while the editor carries on.

A plugin can do two things the contract does not name, and neither needs the
editor to change:

```csharp
Services.Register<IWordList>(new WordList());   // offer, first one wins
Events.Publish(new ChapterStarted("One"));      // announce to whoever listens
```

And a plugin that draws something implements `IPluginPanelProvider` — a second,
smaller contract, `Shiranuix.Plugins.UI`, which is where Avalonia lives. The
panels sit in one column down the left of the page, one at a time, opened and
closed with **Ctrl+B**. That split is the reason a plugin that wants nothing on
screen needs no UI framework at all.

### The three packages

| Package | What it is | Version |
| --- | --- | --- |
| `Shiranuix.Plugins` | the contract, with no runtime dependency at all; `net10.0` and `netstandard2.0` | 0.3.0 |
| `Shiranuix.Plugins.UI` | the interface half: a panel, its provider and the editor's column; `net10.0`, and Avalonia | 0.1.0 |
| `Shiranuix.Plugins.Testing` | the other half, for a plugin's own tests: a document door, a question door, a harness, a temporary folder tree | 0.1.0 |

Namespaces say what a type is for: `.Commands`, `.Editor`, `.Dialogs`,
`.Diagnostics`, `.Text`, `.Services`, `.Messaging`, and `.UI` in the second
package. The root keeps only what every plugin needs whatever it does.

### Two plugins to read

- [`samples/reading-time`](samples/reading-time) — counts the document, says
  how long it would take to read, offers a panel, and announces a kind of news
  nothing in the editor has heard of. The whole thing in seven small files.
- [`samples/file-tree`](samples/file-tree) — a file tree down the left of the
  page: browse, open, make, rename, delete, mark several rows and delete them
  together, sort by name, kind or date, filter, copy a path, open a folder in
  Explorer, watch the disk. Every symbol it draws is its own geometry rather
  than a character from a font, so it is right in both themes and on a machine
  with no icon fonts installed. A folder is read a level at a time, and every
  file it changes goes through the editor's own doors, so opening one from the
  tree asks about unsaved work exactly as if the writer had opened it
  themselves.

## License

Shiranuix is free software under the **GNU General Public License, version 3
or later** (`GPL-3.0-or-later`). The full text is in [LICENSE](LICENSE).

The three plugin packages carry the same licence as the editor they plug into:
a plugin written against a GPL editor is itself part of that work, so the
contract, its interface half and its testing helpers are all GPL too.

Copyright (C) 2026 Shiranuix contributors.
