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
dotnet run --project Shiranuix -- path\to\file.txt
dotnet test                 # unit tests, no window needed
dotnet format               # the house style
```

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

## License

TBD for Shiranuix itself.
