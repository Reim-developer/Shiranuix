# Shiranuix

A local-first text editor for **writing prose** — stories, drafts, notes.

Not a code editor. No syntax highlighting, no line numbers, no file tree, no
terminal. The document is the entire point of the window.

Built with Avalonia 12 and .NET 10.

> **Note:** Shiranuix began as a manga reader. That was deleted — reader
> views, folder scanning, image loading and the library store are all gone.
> The name stayed; the app is a writer now.

---

## Why

Code editors are optimised for code: they assume fixed-width fonts, a gutter,
and a hierarchy of files. Prose wants the opposite — a comfortable serif, a
measure narrow enough that the eye finds the end of a line, and as little
chrome as possible between you and the sentence.

Draft and Ulysses are paid; Notepad++ is a code tool. Shiranuix is a free,
offline, plain-text editor that starts instantly and writes to ordinary files
you can open anywhere.

## What it does

|                             |                                                                       |
| --------------------------- | --------------------------------------------------------------------- |
| **Open / Save / Save As**   | `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`                                    |
| **New**                     | `Ctrl+N` — prompts first if you have unsaved work                     |
| **Word / character / line count** | Live, in the status bar                                         |
| **Recent files**            | On the start screen, newest first, capped at 10                       |
| **Theme**                   | Cycles System → Light → Dark, warm in all three — cream and plum, never blue-grey |
| **Dirty tracking**          | Title carries a bullet while the buffer differs from disk             |
| **Open from the shell**     | `Shiranuix story.txt`, or double-click a `.txt`                       |

Unsaved-work prompts only ever appear when there is actually something to
lose. A clean document never interrupts you.

## Look and feel

The design is built around one idea: **the document is a sheet of paper on a
desk**, not a region of the window.

| Token              | Light       | Dark        | Role                          |
| ------------------ | ----------- | ----------- | ----------------------------- |
| `Sx.CanvasBrush`   | `#F2EAE8`   | `#1C161C`   | Window background, the desk   |
| `Sx.SurfaceBrush`  | `#FFFFFF`   | `#302630`   | The document sheet            |
| `Sx.BorderSoftBrush` | `#F1E1E0` | `#453742`   | Hairline around the sheet     |
| `Sx.TextPrimaryBrush` | `#3D2E33` | `#F4E9EE`   | Body text                     |
| `Sx.TextSecondaryBrush` | `#7E656A` | `#C2A8B1`   | Counts and hints              |
| `Sx.AccentBrush`   | `#C6445E`   | `#CB4360`   | Rose, used sparingly          |

`SurfaceBrush` is deliberately *not* the window colour — lighter in light
mode, darker in dark mode — so the page reads as a separate surface. A soft
shadow under the sheet supplies the lift; its colour is a warm tint rather
than pure black, so it belongs to the palette.

The sheet is centred with `MaxWidth="780"`: about 66 characters in Georgia,
the readable upper bound for serif prose. Two details are worth noting:

- **The column uses `HorizontalAlignment="Stretch"`, not `Center`.**
  `Layoutable.ArrangeCore` sizes a `Center` element to its `DesiredSize`, and
  an empty `TextBox` desires only ~80px, so the page collapsed to a sliver.
  `Stretch` takes the full width, `MinMax` clamps it to `MaxWidth`, and the
  same pass centres the result — full-width and centred at once.
- **The editor is not the Fluent themed `TextBox`.** Fluent's is a *form
  control*: bordered, padded, sized to its content, focus ring on click. All
  of that is replaced in `Styles.axaml`, including retuning the scrollbar —
  Fluent ships the thumb tinted with the accent, which on a white page reads
  as a stray line rather than as a control.

## Typography

Two typefaces, with a clear split of duties:

- **Georgia** (system) for the document — a serif that carries Vietnamese
  diacritics correctly and reads well at length. Rounded UI fonts suit
  interface chrome; they are tiring across a chapter.

  ```xml
  <FontFamily x:Key="Sx.PageFont">Georgia, 'Times New Roman', serif</FontFamily>
  ```

- **Nunito** (bundled, SIL OFL) for every piece of interface — buttons,
  titles, status bar. Its rounded terminals carry the friendliness, and
  ExtraBold is reserved for headings.

Nothing needs to be installed for either.

## Theming

The theming is layered, deliberately:

- `App.axaml` sets `FluentTheme.Palettes` — the supported way to re-tint the
  whole Fluent control library for both variants at once.
- `Themes/Palette.axaml` holds the app's own `Sx.*` design tokens, plus
  overrides of the few Fluent brushes palettes don't reach (text fields,
  tooltips, dropdowns, scrollbars).
- `Styles.axaml` styles the app-specific pieces. Selectors use **classes**
  (`.editor`, `.measure`, `.tool`, `.accent`) rather than control themes,
  because a style trigger outranks a control theme setter and so wins even
  where Fluent styles the same property.

The window maps `ThemeMode` onto `Application.RequestedThemeVariant` in its
code-behind; the view model owns the cycle but knows nothing about Avalonia.

## Layout

```
Shiranuix/
├── Shiranuix.slnx              # Solution (app + tests)
├── Shiranuix/                  # The application
│   ├── Program.cs              # Entry point (registers the font collection)
│   ├── App.axaml(.cs)          # Palettes, startup file argument
│   ├── Styles.axaml            # editor / measure / chrome styling
│   ├── Themes/Palette.axaml    # Sx.* tokens, Light and Dark
│   ├── Fonts/                  # EmbeddedFontCollection for the bundled fonts
│   ├── Assets/                 # Fonts (Nunito, SIL OFL)
│   ├── Models/ThemeMode.cs     # System / Light / Dark
│   ├── Services/
│   │   ├── IFileService        # disk I/O, behind an interface
│   │   ├── IFileDialogService  # open/save pickers
│   │   └── IConfirmDialogService  # discard prompt
│   ├── Utilities/TextStatistics.cs  # pure word/char/line counting
│   ├── ViewModels/
│   │   └── EditorViewModel.cs  # document, path, dirty state, commands
│   └── Views/MainWindow.axaml  # app bar, sheet, status bar
└── Shiranuix.Tests/            # xUnit tests mirroring the source layout
```

`EditorViewModel` contains no UI types. The two things that would have
forced one — picking a file, confirming a discard — are interfaces handed in
at construction, which is why the whole view model runs headlessly in tests.

The document is a single `string`: simple, correct, and fast up to a few
megabytes of prose. Swapping in a gap buffer or a rope later stays contained
behind `Text`, because nothing else knows how the text is stored.

## Tests

```bash
dotnet test
```

31 tests, no window and no file system required:

- `Utilities/TextStatisticsTests` — the counting rules, including the
  Vietnamese cases: whitespace is the only word delimiter, so `người đó` is
  two words while `chẳng` is one.
- `ViewModels/EditorViewModelTests` — dirty tracking, save/reopen flows, and
  the refusal branch of every prompt. `Failed_Save_Does_Not_Repoint_The_Document`
  exists because a save that throws must not leave the view model claiming the
  text lives on disk.

## Requirements

The **.NET 10 SDK**. No other installs needed — dependencies restore from
NuGet on first build.

## Running

```bash
dotnet build Shiranuix.slnx
dotnet run --project Shiranuix -- path\to\file.txt
```

The app reads a path argument on launch, which is what makes double-clicking
a `.txt` in Explorer work.

```bash
dotnet test              # unit suite
dotnet format            # the house style
```

> If `dotnet` is not on `PATH`, call the SDK by full path — the apphost that
> `dotnet build` emits resolves the runtime itself and will otherwise prompt
> to install the .NET Desktop Runtime.

## Known limits

- **Very large files.** The whole document is one string and one `TextLayout`.
  Chapters and short stories are instant; a very large single file will
  eventually stutter. Splitting by chapter sidesteps it today.
- **No spell check.** Avalonia 12 removed `SpellCheck`; there is no spell
  checker in the framework to enable. Reintroducing one means a third-party
  or a hand-rolled checker.
- **Windows-only pathing so far.** Nothing in the code is Windows-specific —
  it just has not been exercised elsewhere.

## License

TBD for Shiranuix itself.

The bundled **Nunito** typeface is separate: it is licensed under the
[SIL Open Font License 1.1](https://scripts.sil.org/OFL), copyright 2014 The
Nunito Project Authors. The full licence ships with the app as
`Assets/Fonts/OFL.txt`. OFL permits bundling with software, but not selling
the fonts on their own.
