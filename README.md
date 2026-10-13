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
| **Font / size**             | Any typeface installed on the machine, at any point size, for the whole document |
| **Word / character / line count** | Live, in the status bar                                         |
| **Recent files**            | On the start screen, newest first, capped at 10                       |
| **Theme**                   | Cycles System → Light → Dark, from the Office/Fluent neutral ramp    |
| **Dirty tracking**          | Title carries a bullet while the buffer differs from disk             |
| **Open from the shell**     | `Shiranuix story.txt`, or double-click a `.txt`                       |

Unsaved-work prompts only ever appear when there is actually something to
lose. A clean document never interrupts you.

## Look and feel

The design is built around one idea: **the document is a sheet of paper on a
desk**, not a region of the window. The chrome is Microsoft's own Office
palette — a neutral ramp with the Office brand blue — because a writing tool
should read as a tool, and that is the reference everyone already knows.

| Token              | Light       | Dark        | Role                          |
| ------------------ | ----------- | ----------- | ----------------------------- |
| `Sx.CanvasBrush`   | `#EDEBE9`   | `#1F1E1D`   | Window background, the desk   |
| `Sx.SurfaceBrush`  | `#FFFFFF`   | `#292827`   | The document sheet            |
| `Sx.BorderSoftBrush` | `#E1DFDD` | `#3B3A39`   | Hairline around the sheet     |
| `Sx.TextPrimaryBrush` | `#201F1E` | `#F3F2F1`   | Body text                     |
| `Sx.TextSecondaryBrush` | `#605E5C` | `#C8C6C4` | Counts and hints            |
| `Sx.AccentBrush`   | `#0F6CBD`   | `#4894FE`   | Office blue, used sparingly   |

`SurfaceBrush` is deliberately *not* the window colour — lighter in light
mode, lighter still in dark mode than the desk — so the page reads as a
separate surface. A soft neutral shadow under the sheet supplies the lift.

Two chrome bands surround it, the same white-over-grey split Word uses: a
white title band, and a grey format band carrying the typography controls. In
dark mode both invert to the near-black ramp, and the sheet — being *lighter*
than the desk there — reads as lit paper rather than as a hole.

The sheet is centred with `MaxWidth="816"`: about 66 characters in Georgia,
the readable upper bound for serif prose. Two details are worth noting:

- **The column uses `HorizontalAlignment="Stretch"`, not `Center`.**
  `Layoutable.ArrangeCore` sizes a `Center` element to its `DesiredSize`, and
  an empty `TextBox` desires only ~80px, so the page collapsed to a sliver.
  `Stretch` takes the full width, `MinMax` clamps it to `MaxWidth`, and the
  same pass centres the result — full-width and centred at once.
- **The editor is not the Fluent themed `TextBox`.** Fluent's is a *form
  control*: bordered, padded, sized to its content, focus ring on click. All
  of that is replaced in `Styles.axaml`, including retunning the scrollbar —
  Fluent ships the thumb tinted with the accent, which on a white page reads
  as a stray line rather than as a control. The neutral used here is tinted
  from the Office ink instead.

The corners are square, at `4px` for controls and `0` for the page. Softer
radii read as a consumer app; a page is a page.

## Typography

Two typefaces, with a clear split of duties — plus a picker, because a
writing tool that cannot change its font is a notepad.

- **The document** starts in **Georgia**, a system serif that carries
  Vietnamese diacritics correctly and reads well at length. Rounded UI fonts
  suit interface chrome; they are tiring across a chapter. The format bar
  offers every typeface installed on the machine and a ladder of point sizes.

  The choice is a **view model property, not a style token**, and that is
  deliberate: the same value drives both the page and the two ComboBoxes, so
  the picker cannot drift from what is on screen. It is held as a `string`
  rather than an `Avalonia.Media.FontFamily`, because a typeface is a UI type
  and the view model stays free of them — `StringToFontFamilyConverter` does
  the conversion in the view.

  > Avalonia has no rich-text control, so the choice applies to the whole
  > document. That is why the picker reads as *the font*, not as *the font of
  > the selection*.

  One conversion is not optional: a compiled binding carrying a `string` does
  **not** publish into a `FontFamily` property. Without the converter the
  property keeps whatever the control theme gave it, and the document
  silently renders in the interface font no matter what the picker says.

- **Nunito** (bundled, SIL OFL) for every piece of interface — buttons,
  titles, status bar. Its rounded terminals carry the friendliness, and
  ExtraBold is reserved for headings.

Nothing needs to be installed for either. A document opened on a machine
that does not have its typeface still gets it offered in the picker, so the
choice is never silently lost.

## Theming

The theming is layered, deliberately:

- `App.axaml` sets `FluentTheme.Palettes` — the supported way to re-tint the
  whole Fluent control library for both variants at once.
- `Themes/Palette.axaml` holds the app's own `Sx.*` design tokens, plus
  overrides of the few Fluent brushes palettes don't reach (text fields,
  tooltips, dropdowns, scrollbars).
- `Styles.axaml` styles the app-specific pieces. Selectors use **classes**
  (`.editor`, `.measure`, `.tool`, `.format-bar`, `.field`) rather than
  control themes, because a style trigger outranks a control theme setter and
  so wins even where Fluent styles the same property.

The window maps `ThemeMode` onto `Application.RequestedThemeVariant` in its
code-behind; the view model owns the cycle but knows nothing about Avalonia.

## Dependency injection

Nothing in the application constructs its own dependencies. Every registration
lives in one list — `AddShiranuix()` — and the container builds the rest from
constructor arguments, so no class reaches into another to `new` it. Swapping
an implementation is one line in that list, not a hunt through the code.

Two libraries divide the work:

- **Microsoft.Extensions.DependencyInjection** is the container: it holds the
  registrations and works out each constructor's arguments.
- **Splat** is the resolver the application exposes. Handing the built
  container to `AppLocator` via `UseMicrosoftDependencyResolver()` means
  anything that asks Splat for a service — the window today, ReactiveUI if it
  ever arrives — gets the same instances the composition root does.

The one asymmetry is the window. The two dialog services are shown *over* the
window, and the window is built *from* the view model those services are
injected into, so the container cannot be handed the window while it is still
building it. The dialog services are given a `Func<Window>` instead: a way of
asking for the window, called when a dialog opens rather than at construction.
That single break in the circle is what lets everything else be constructor
injected.

The container is built in `App`'s constructor rather than in
`OnFrameworkInitializationCompleted`, the usual home for this, because the XAML
previewer creates the application but never completes framework
initialization — and the window it constructs still asks for a view model to
preview against.

## Layout

```
Shiranuix/
├── Shiranuix.slnx              # Solution (app + tests)
├── Shiranuix/                  # The application
│   ├── Program.cs              # Entry point (registers the font collection)
│   ├── App.axaml(.cs)          # Palettes, composition root, startup file argument
│   ├── ServiceCollectionExtensions.cs  # AddShiranuix(): every service registration
│   ├── Styles.axaml            # editor / measure / chrome styling
│   ├── Themes/Palette.axaml    # Sx.* tokens, Light and Dark
│   ├── Fonts/                  # EmbeddedFontCollection for the bundled fonts
│   ├── Assets/                 # Fonts (Nunito, SIL OFL)
│   ├── Converters/
│   │   ├── DoubleToSizeConverter.cs       # 17.0 → "17" in the size picker
│   │   └── StringToFontFamilyConverter.cs # a name → a typeface for the page
│   ├── Models/ThemeMode.cs     # System / Light / Dark
│   ├── Services/
│   │   ├── IFileService / FileService.cs  # disk I/O, behind an interface
│   │   ├── IFileDialogService / WindowFileDialogService.cs  # pickers
│   │   ├── IConfirmDialogService / WindowConfirmService.cs  # discard prompt
│   │   └── IFontCatalog / FontCatalog.cs  # installed typefaces
│   ├── Utilities/TextStatistics.cs  # pure word/char/line counting
│   ├── ViewModels/
│   │   └── EditorViewModel.cs  # document, path, dirty state, font, commands
│   └── Views/MainWindow.axaml  # title band, format band, sheet, status bar
└── Shiranuix.Tests/            # xUnit tests mirroring the source layout
```

`EditorViewModel` contains no UI types. The three things that would have
forced one — picking a file, confirming a discard, enumerating typefaces —
are interfaces handed in at construction by the container, which is why the
whole view model runs headlessly in tests. The font catalog especially:
reading Avalonia's font manager needs a running application, and a headless
test has none.

The document is a single `string`: simple, correct, and fast up to a few
megabytes of prose. Swapping in a gap buffer or a rope later stays contained
behind `Text`, because nothing else knows how the text is stored.

## Tests

```bash
dotnet test
```

44 tests, no window and no file system required:

- `Utilities/TextStatisticsTests` — the counting rules, including the
  Vietnamese cases: whitespace is the only word delimiter, so `người đó` is
  two words while `chẳng` is one.
- `ServiceCollectionExtensionsTests` — the composition root, resolved
  headlessly: every interface binds to its implementation, the view model's
  whole constructor graph builds without a window to aim at, the services
  share one instance, and handing the container to Splat serves the same
  instances it does.
- `ViewModels/EditorViewModelTests` — dirty tracking, save/reopen flows, the
  refusal branch of every prompt, and the typography state. Two of those
  exist because they are easy to get wrong and invisible in a screenshot:
  `Failed_Save_Does_Not_Repoint_The_Document` (a save that throws must not
  leave the view model claiming the text lives on disk) and
  `Changing_The_Font_Is_Not_An_Edit_To_The_Document` (a `.txt` file holds
  text and nothing else, so choosing a typeface is not something to write
  back).

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

- **No per-selection formatting.** The buffer is a plain string with nowhere
  to record per-run styling, so font and size apply to the whole document.
  Real rich text means a gap buffer plus a run store behind `Text` — contained
  work, but a different app.
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
