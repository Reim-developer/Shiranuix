# Shiranuix

A manga reader desktop application built with **JavaFX**.

> **Status:** early scaffold. The app currently boots to an empty main
> window; reader and library features are planned.

## Features (planned)

- Browse and organise a local/remote manga library.
- Page-flip reader with zoom and reading-direction options.
- Download & cache chapters for offline reading.

## Tech stack

| Concern    | Choice                                             |
| ---------- | -------------------------------------------------- |
| Language   | Java 25 (LTS)                                      |
| UI toolkit | JavaFX 24                                          |
| Build      | Gradle 9.8 (Kotlin DSL, wrapper included)          |
| Unit tests | JUnit Jupiter 5                                    |
| UI tests   | TestFX (+ Hamcrest, optional Monocle headless)     |

## Requirements

- **JDK 25** on your `PATH` (or let Gradle provision it via toolchains).
- No separate Gradle install needed — use the wrapper (`./gradlew`).

## Getting started

```bash
# Run the application (opens the empty main window)
./gradlew run            # macOS/Linux
gradlew.bat run          # Windows

# Run all tests (opens a real window briefly)
./gradlew test

# Run UI tests headless (no window, for CI)
./gradlew test -Pheadless

# Build a full distribution (jar + start scripts) into build/distributions
./gradlew build

# Generate Javadoc into build/docs/javadoc
./gradlew javadoc
```

## Project layout

```
Shiranuix/
├── build.gradle.kts              # Build definition & dependencies
├── settings.gradle.kts           # Root project name
├── gradle.properties             # Build flags (parallel, caching)
├── gradle/
│   ├── libs.versions.toml        # Version catalog — single source of dependency versions
│   └── wrapper/                  # Gradle wrapper (pins the Gradle version)
└── src/
    ├── main/java/com/reim/shiranuix/
    │   ├── Main.java             # Launcher entry point
    │   └── App.java              # JavaFX Application (empty window shell)
    └── test/java/com/reim/shiranuix/
        └── AppTest.java          # TestFX UI tests for the main window
```

## Testing notes

- `AppTest` is a TestFX suite that starts the real application and asserts
  the window is shown, titled correctly, and sized to the defaults.
- UI tests need a display by default. Pass `-Pheadless` to run them on the
  Monocle headless Glass platform (useful on CI machines without a screen).

## License

TBD.
