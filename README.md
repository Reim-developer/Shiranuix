# Shiranuix

A manga reader desktop application built with **C# / .NET** and **Avalonia UI**.

> **Status:** early scaffold. The home and reader screens are implemented
> against a local folder of images; a persistent library (SQLite) and more
> reading modes are planned.

## Features

- Open a folder of images and read it as a chapter.
- Recent-folders list on the home screen.
- Reader with previous/next, a seek slider, and independent choices for:
  - **Reading mode** — single page (webtoon / double-page reserved),
  - **Reading direction** — left-to-right / right-to-left,
  - **Fit mode** — fit screen / fit width / fit height / original size.
- Light and dark themes (follows the OS by default, toggleable), both tuned to
  a warm "peach-rose on cream" palette — see [Look and feel](#look-and-feel).

## Look and feel

The visual language is deliberately warm and a little cute, without tipping
into kitsch: rose-peach accent, cream (light) or plum (dark) surfaces, generous
corner radii, and soft tinted shadows instead of hairline dividers. Nunito's
rounded terminals carry the friendliness; ExtraBold is reserved for headings.

| Token                | Light            | Dark             | Used for                        |
| -------------------- | ---------------- | ---------------- | ------------------------------- |
| Accent               | `#FFC6445E`      | `#FFCB4360`      | Primary buttons, chips, hearts  |
| Region (background)  | `#FFFFF9F5`      | `#FF251D23`      | Window background               |

Both accents were picked so white text clears a 4.5:1 contrast ratio. The dark
palette is built from plum/near-black rather than blue-grey, so it stays warm
next to the rose accent.

The theming is layered, deliberately:

- `App.axaml` sets `FluentTheme.Palettes` — the supported way to re-tint the
  whole Fluent control library for both variants at once.
- `Themes/Palette.axaml` holds the app's own `Sx.*` design tokens, plus
  overrides of the few Fluent brushes that palettes don't reach (text fields,
  tooltips, dropdowns, scrollbars).
- `Styles.axaml` styles the app-specific pieces. Selectors use **classes**
  (`.card`, `.accent`, `.chip`) rather than control themes, because a style
  trigger outranks a control theme setter and so wins even where Fluent
  styles the same property.

## Tech stack

| Concern         | Choice                                          |
| --------------- | ----------------------------------------------- |
| Language        | C# 14 on .NET 10 (LTS)                          |
| UI toolkit      | Avalonia 12 (Fluent theme, re-palette'd)         |
| Typeface        | Nunito (bundled, SIL OFL)                       |
| MVVM            | CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) |
| Build           | `dotnet` CLI (SDK-style projects, `.slnx`)      |
| Unit tests      | xUnit                                           |

## Requirements

- The **.NET 10 SDK**. No other installs needed — dependencies restore from
  NuGet on first build.

## Getting started

```bash
# Run the application
dotnet run --project Shiranuix

# Run all tests
dotnet test

# Build a self-contained release (single-folder output)
dotnet publish Shiranuix -c Release -r win-x64 --self-contained true
```

## Project layout

```
Shiranuix/
├── Shiranuix.slnx              # Solution (app + tests)
├── Shiranuix/                  # The application
│   ├── Program.cs              # Entry point (registers the font collection)
│   ├── App.axaml(.cs)          # Application root: palettes, view locator
│   ├── Styles.axaml            # App-wide styles (cards, pills, chips, ...)
│   ├── Fonts/                  # EmbeddedFontCollection for the bundled fonts
│   ├── Themes/                 # Palette.axaml: design tokens + brush overrides
│   ├── Assets/                 # Fonts (Nunito, SIL OFL)
│   ├── Models/                 # Domain: Chapter, MangaPage, reading enums
│   ├── Services/               # Folder scanning, image loading, library store
│   ├── Utilities/              # Natural sort, image formats, fit arithmetic
│   ├── ViewModels/             # Home / Reader / Main (navigation shell)
│   └── Views/                  # HomeView, ReaderView, MainWindow
└── Shiranuix.Tests/            # xUnit tests mirroring the source layout
```

## Architecture

The app follows **MVVM**: view models hold all state and behaviour and never
reference UI controls, so they are unit-testable headlessly. Views (AXAML +
thin code-behind) only wire controls to view models and handle the few things
that must live in the view (the native folder picker, applying the theme,
sizing the page image).

- `Models/` — immutable domain types and the three independent reading axes.
- `Services/` — `IFolderScanner`, `IImageLoader`, `ILibraryRepository`
  (interfaces, so implementations can be swapped — e.g. a future SQLite-backed
  repository) with in-memory/async defaults.
- `Utilities/` — pure helpers (natural sort, fit-mode arithmetic) tested in
  isolation.
- `ViewModels/` — `MainViewModel` navigates between `HomeViewModel` and
  `ReaderViewModel`.

## Testing

`dotnet test` runs the unit suite: utilities, services, and both screen view
models (image loading is faked, so no real decoding or window is needed).

## License

TBD for Shiranuix itself.

The bundled **Nunito** typeface is separate: it is licensed under the
[SIL Open Font License 1.1](https://scripts.sil.org/OFL), copyright 2014 The
Nunito Project Authors. The full licence ships with the app as
`Assets/Fonts/OFL.txt`. OFL permits bundling with software, but not selling
the fonts on their own.
