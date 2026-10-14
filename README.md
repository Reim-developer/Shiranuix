# Shiranuix

A local-first plain-text editor for writing prose — stories, drafts, notes.
Built with Avalonia 12 and .NET 10, offline, opens ordinary `.txt` files.

Not a code editor: no syntax highlighting, no file tree, no terminal. The
document is the entire point of the window.

> **Not a Word replacement.** This is simply the version the author uses every
> day — it may have bugs. Issues and PRs are welcome.

## What it does

- **Open / Save / Save As** — `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`
- **New** — `Ctrl+N`, prompting first only if you have unsaved work
- **Font / size** — any typeface installed on the machine, for the whole document
- **Word wrap** — off until asked for, remembered between sessions
- **Recent files** — on the start screen, newest first, capped at 10
- **Status bar** — line, column, encoding (UTF-8), newline convention (LF/CRLF)
- **Theme** — cycles System → Light → Dark
- **Dirty tracking** — a bullet in the title while the buffer differs from disk
- **From the shell** — `Shiranuix story.txt`, or double-click a `.txt`
- **Between sessions** — font, size, wrap, theme and history in a local SQLite
  store at `%APPDATA%\Shiranuix\shiranuix.db`

## Build and run

The .NET 10 SDK is the only requirement.

```bash
dotnet build Shiranuix.slnx
dotnet run --project Shiranuix -- path\to\file.txt
dotnet test                 # unit tests, no window needed
dotnet format               # the house style
```

## How it is put together

- **Dependency injection** — every registration lives in `AddShiranuix()`;
  Microsoft.Extensions.DependencyInjection is the container, Splat is the
  resolver the application exposes. Nothing constructs its own dependencies.
- **View models** — `EditorViewModel` is the coordinator (commands, status line,
  title); the rules live in `DocumentSession` (text, path, dirty state, open /
  save) and `TypographyState` (font, size, wrap, theme). None of them holds a UI
  type, so the whole graph runs headlessly in tests.
- **Dialogs** — a `DialogRequest<TResult>` is data, shown by one XAML
  `DialogWindow`; `WindowConfirmService` builds no UI at all.
- **Persistence** — EF Core over SQLite with a real migration applied at
  startup, behind `IUserSettings`, `IRecentFilesStore` and `IFontHistoryStore`.

## Known limits

- **No per-selection formatting.** The buffer is a plain string, so font and
  size apply to the whole document.
- **Very large files.** The document is one string and one text layout;
  chapters and short stories are instant, one huge file will stutter.
- **No spell check.** Avalonia 12 removed `SpellCheck`.
- **Windows-only pathing so far.** Nothing in the code is Windows-specific —
  it just has not been exercised elsewhere.

## License

TBD for Shiranuix itself. The bundled **Nunito** typeface is separate, under
the [SIL Open Font License 1.1](https://scripts.sil.org/OFL); the full licence
ships with the app as `Assets/Fonts/OFL.txt`.
