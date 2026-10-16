# reading-time

A whole Shiranuix plugin, in five small files. It counts the document and says
what it would take to read it aloud — a number the status line has no room
for and a writer is often curious about.

It exists to be read as much as to be used.

## Installing it

Copy the folder `reading-time` — the assembly, its manifest, and anything else
you want it to carry — into:

```
%APPDATA%\Shiranuix\plugins\reading-time\
```

Start the editor. The plugin loads at startup, and anything it refuses to do
is a line in `%APPDATA%\Shiranuix\logs\shiranuix-<today>.log`.

Two names have to agree, and the editor checks both before it runs any of this
code: the folder is called `reading-time`, the manifest's `id` is
`reading-time`, and the assembly is `reading-time.dll`.

## What is in here

| File | What it is |
| --- | --- |
| `manifest.json` | who the plugin is, and which contract it speaks |
| `ReadingTime.cs` | the arithmetic, as plain functions |
| `ReadingTimePlugin.cs` | the entry point the editor builds and calls |
| `EstimateCommand.cs` | the one command it offers |
| `CommandBinding.cs` | the `ICommand` the editor's menus bind to |

## Writing your own

Build against `Shiranuix.Plugins` and nothing else. The contract has no runtime
dependency — no Avalonia, no view-model toolkit — because every plugin is
handed the editor's copy of it rather than shipping its own, and a contract
that dragged a library along would be asking for two versions of itself in one
process.

Three things to know:

- **Ask, do not take.** `context.Capability<IEditorHost>()` returns the
  document, or `null` on a host that has none. A plugin that never asks cannot
  touch anything.
- **Refuse, do not throw.** `PluginOutcome.Failure("...")` is how a call says
  it will not happen; exceptions are for the genuinely unexpected, and the host
  catches those around every call.
- **Change through the editor.** Everything a plugin does to a document goes
  through `IEditorHost`, which is what puts the change in the writer's own undo
  history — one press of Ctrl+Z takes back exactly what the plugin did.