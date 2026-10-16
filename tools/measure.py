# /// script
# requires-python = ">=3.11"
# dependencies = ["psutil>=7.0.0"]
# ///
"""What Shiranuix costs to start, and what it costs to sit still.

Two numbers matter to a text editor, and they are not the same number. The
first is how long a writer waits between pressing the icon and being able to
type, which is measured here as the time from launching the process to a
visible window existing. The second is how much memory the thing keeps while
nothing is happening, which is the number that decides whether an editor
belongs on a laptop that has twelve hours of battery.

Three profiles are measured, because they are three different claims:

``fresh``
    An empty %APPDATA%: first run. The store is created, the schema is
    brought up, nothing is remembered. This is the worst day, and it is the
    day most people never see twice.

``existing``
    The real %APPDATA%, plugins and all. What a writer who uses the editor
    every day actually gets.

``minimal``
    Real settings, but no plugins installed - the plain text editor with
    nothing plugged into it, which is what this editor is supposed to be when
    nobody has added anything.

Usage::

    uv run tools/measure.py --exe Shiranuix/bin/Release/net10.0/Shiranuix.exe
    uv run tools/measure.py --profile minimal --runs 5 --json before.json
"""

from __future__ import annotations

import argparse
import ctypes
import json
import os
import shutil
import statistics
import subprocess
import sys
import tempfile
import time
from dataclasses import asdict
from dataclasses import dataclass
from dataclasses import field
from pathlib import Path
from typing import Final

import psutil

WINDOW_POLL: Final[float] = 0.01
"""How often the process is asked whether it has shown a window yet."""


@dataclass
class Measurement:
    """What one launch of the editor cost."""

    profile: str
    window_ms: float
    """Milliseconds from launching to a window being visible."""

    rss_start_mb: float
    rss_peak_mb: float
    rss_settled_mb: float
    """Working set: the pages actually in memory, peak and after settling."""

    private_peak_mb: float
    private_settled_mb: float
    """Private bytes: what the process would have to write to start again."""

    cpu_ms: float
    samples: list[float] = field(default_factory=list)
    """The working set over time, so a spike cannot hide inside a summary."""

    killed: bool = False
    """True when the editor was still running at the end and had to be stopped."""

    detected: bool = False
    """True when a window was actually seen; false means window_ms is a timeout."""


@dataclass
class AppData:
    """Where one run keeps its things: the writer's own, or a stand-in."""

    path: Path | None

    def environment(self) -> dict[str, str]:
        """The environment the child process should start with.

        The editor is told with ``SHIRANUIX_HOME`` rather than ``%APPDATA%``:
        Windows decides where %APPDATA% is through an API that ignores what an
        environment variable says, so setting it would leave every profile
        reading the writer's real settings and every number identical.
        """
        env = dict(os.environ)
        if self.path is not None:
            env["SHIRANUIX_HOME"] = str(self.path)
        return env


def make_app_data(profile: str, real: Path, scratch: Path) -> AppData:
    """Builds the data folder a profile means.

    <param name="profile">One of fresh, existing, minimal.</param>
    <param name="real">The writer's own editor folder.</param>
    <param name="scratch">A temporary folder to build a stand-in in.</param>
    <returns>The environment to launch under.</returns>
    """
    if profile == "existing":
        return AppData(None)

    if profile == "fresh":
        return AppData(scratch)

    if profile == "minimal":
        if real.is_dir():
            # Everything the editor remembers, minus what other people added
            # to it: a warmed store with no plugins is the plain editor.
            shutil.copytree(
                real,
                scratch,
                ignore=shutil.ignore_patterns("plugins", "logs"),
                dirs_exist_ok=True,
            )
        return AppData(scratch)

    raise ValueError(f"Unknown profile: {profile}")


def _visible_window_exists(pid: int) -> bool:
    """Whether the process has a window on screen yet.

    Windows is asked directly, because there is nothing else that knows: the
    process being alive says it got this far, and only the window manager knows
    the window exists. Everywhere else the answer is "cannot tell", which the
    caller treats as "yes", because a process that fails to show a window
    fails the run anyway.

    <param name="pid">The process to look for.</param>
    <returns>True when a visible window belongs to it.</returns>
    """
    if sys.platform != "win32":
        return True

    user32 = getattr(ctypes, "windll").user32

    enum_proc = ctypes.WINFUNCTYPE(
        ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p
    )
    user32.EnumWindows.argtypes = [enum_proc, ctypes.c_void_p]
    user32.GetWindowThreadProcessId.argtypes = [
        ctypes.c_void_p,
        ctypes.POINTER(ctypes.c_ulong),
    ]
    user32.GetWindowThreadProcessId.restype = ctypes.c_ulong

    found: list[bool] = [False]

    def visit(window: ctypes.c_void_p, _parameter: ctypes.c_void_p) -> bool:
        if not user32.IsWindowVisible(window):
            return True

        owner = ctypes.c_ulong()
        user32.GetWindowThreadProcessId(window, ctypes.byref(owner))
        if owner.value == pid:
            found[0] = True
            return False

        return True

    user32.EnumWindows(enum_proc(visit), 0)
    return found[0]


def _megabytes(value: int) -> float:
    return value / (1024 * 1024)


def _rss(process: psutil.Process) -> int:
    """The process's working set in bytes, or zero once it has gone.

    <param name="process">The process to ask.</param>
    <returns>Bytes resident in memory.</returns>
    """
    try:
        return process.memory_info().rss
    except psutil.NoSuchProcess:
        return 0


def _private_bytes(process: psutil.Process) -> int | None:
    try:
        return process.memory_full_info().private
    except (psutil.AccessDenied, AttributeError, NotImplementedError):
        return None


def _cpu_ms(process: psutil.Process) -> float:
    """CPU the launch has cost so far, in milliseconds.

    A process that has already gone is not a failure: the editor closed, and
    a closed editor has spent whatever it spent.

    <param name="process">The process to ask.</param>
    <returns>Milliseconds of user and system time.</returns>
    """
    try:
        times = process.cpu_times()
    except psutil.NoSuchProcess:
        return 0.0

    return (times.user + times.system) * 1000


def measure(
    exe: Path,
    profile: str,
    app_data: AppData,
    seconds: float,
    document: Path | None,
) -> Measurement:
    """Launches the editor once and watches what it costs.

    <param name="exe">The executable to launch.</param>
    <param name="profile">The profile's name, for the record.</param>
    <param name="app_data">The %APPDATA% to launch under.</param>
    <param name="seconds">How long to keep watching after the window.</param>
    <param name="document">A file to open on the command line, if any.</param>
    <returns>What the launch cost.</returns>
    """
    command: list[str] = [str(exe)]
    if document is not None:
        command.append(str(document))

    started = time.perf_counter()
    child = subprocess.Popen(  # noqa: S603 - the path is the writer's own
        command,
        env=app_data.environment(),
        cwd=str(exe.parent),
    )
    process = psutil.Process(child.pid)

    window_seconds = seconds + 5.0
    detected = False
    while time.perf_counter() - started < window_seconds:
        if child.poll() is not None:
            print(
                f"  {profile}: the editor closed after "
                f"{(time.perf_counter() - started) * 1000:.0f}ms "
                f"(exit {child.returncode})",
                file=sys.stderr,
            )
            break
        if _visible_window_exists(child.pid):
            detected = True
            break
        time.sleep(WINDOW_POLL)

    window_ms = (time.perf_counter() - started) * 1000
    if not detected and child.poll() is None:
        print(
            f"  {profile}: no window after {window_ms:.0f}ms - "
            "that number is a timeout, not a time",
            file=sys.stderr,
        )
        child.terminate()
        child.wait(timeout=10)
    rss_start = _megabytes(_rss(process))

    samples: list[float] = []
    privates: list[int | None] = []
    until = time.perf_counter() + seconds
    while time.perf_counter() < until and child.poll() is None:
        try:
            samples.append(_megabytes(process.memory_info().rss))
        except psutil.NoSuchProcess:
            break
        privates.append(_private_bytes(process))
        time.sleep(0.2)

    killed = child.poll() is None
    cpu_ms = _cpu_ms(process)
    if killed:
        child.kill()
        child.wait(timeout=10)

    known = [value for value in privates if value is not None]
    private_peak = _megabytes(max(known)) if known else 0.0
    private_settled = _megabytes(known[-1]) if known else 0.0

    return Measurement(
        profile=profile,
        window_ms=window_ms,
        rss_start_mb=rss_start,
        rss_peak_mb=max(samples) if samples else rss_start,
        rss_settled_mb=statistics.median(samples) if samples else rss_start,
        private_peak_mb=private_peak,
        private_settled_mb=private_settled,
        cpu_ms=cpu_ms,
        samples=samples,
        killed=killed,
        detected=detected,
    )


def report(runs: list[Measurement]) -> None:
    """Prints what the runs cost, in the order a reader cares about.

    <param name="runs">Every measurement taken.</param>"""
    if not runs:
        return

    def middle(name: str) -> float:
        return statistics.median(getattr(run, name) for run in runs)

    print()
    print(f"{'profile':<10} {'window':>9} {'rss peak':>10} {'rss calm':>10} "
          f"{'private':>10} {'cpu':>8}")
    print("-" * 60)
    for run in runs:
        print(f"{run.profile:<10} {run.window_ms:>7.0f}ms "
              f"{run.rss_peak_mb:>8.0f}MB {run.rss_settled_mb:>8.0f}MB "
              f"{run.private_peak_mb:>8.0f}MB {run.cpu_ms:>6.0f}ms")

    profiles = sorted({run.profile for run in runs})
    if len(profiles) > 1:
        print()
        print("median of each profile:")
        for profile in profiles:
            group = [run for run in runs if run.profile == profile]
            print(f"  {profile:<10} {middle('window_ms'):>7.0f}ms window, "
                  f"{middle('rss_peak_mb'):>6.0f}MB peak, "
                  f"{middle('rss_settled_mb'):>6.0f}MB calm, "
                  f"{middle('private_peak_mb'):>6.0f}MB private")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--exe",
        type=Path,
        default=Path("Shiranuix/bin/Release/net10.0/Shiranuix.exe"),
        help="the executable to launch",
    )
    parser.add_argument(
        "--profiles",
        default="fresh,minimal,existing",
        help="comma separated: fresh, minimal, existing",
    )
    parser.add_argument("--runs", type=int, default=3, help="launches per profile")
    parser.add_argument(
        "--seconds", type=float, default=8.0, help="seconds to keep watching"
    )
    parser.add_argument("--file", type=Path, default=None, help="a document to open")
    parser.add_argument("--json", type=Path, default=None, help="write results here")
    args = parser.parse_args()

    exe = args.exe.resolve()
    if not exe.is_file():
        print(f"No editor at {exe}. Build it first:", file=sys.stderr)
        print("  dotnet publish Shiranuix/Shiranuix.csproj -c Release", file=sys.stderr)
        return 2

    real_editor_folder = Path(
        os.environ.get("SHIRANUIX_HOME", str(Path.home() / ".config" / "Shiranuix"))
    )
    if os.environ.get("SHIRANUIX_HOME") is None and sys.platform == "win32":
        roaming = Path(os.environ.get("APPDATA", Path.home() / "AppData/Roaming"))
        real_editor_folder = roaming / "Shiranuix"
    profiles = [name.strip() for name in args.profiles.split(",") if name.strip()]

    runs: list[Measurement] = []
    with tempfile.TemporaryDirectory(prefix="shiranuix-measure-") as folder:
        for profile in profiles:
            for _ in range(max(1, args.runs)):
                scratch = Path(folder) / f"{profile}-{time.time_ns()}"
                scratch.mkdir(parents=True)
                app_data = make_app_data(profile, real_editor_folder, scratch)
                runs.append(measure(exe, profile, app_data, args.seconds, args.file))

    report(runs)

    if args.json is not None:
        args.json.write_text(
            json.dumps([asdict(run) for run in runs], indent=2),
            encoding="utf-8",
        )
        print(f"\nwrote {args.json}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())