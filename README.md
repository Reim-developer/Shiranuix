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

## License

TBD for Shiranuix itself.
