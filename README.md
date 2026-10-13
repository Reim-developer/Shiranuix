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
- Light and dark themes (follows the OS by default, toggleable).

## Tech stack

| Concern         | Choice                                          |
| --------------- | ----------------------------------------------- |
| Language        | C# 14 on .NET 10 (LTS)                          |
| UI toolkit      | Avalonia 12                                     |
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
│   ├── Program.cs              # Entry point
│   ├── App.axaml(.cs)          # Application root: theme, view locator
│   ├── Styles.axaml            # App-wide styles (built on Fluent brushes)
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

TBD.
