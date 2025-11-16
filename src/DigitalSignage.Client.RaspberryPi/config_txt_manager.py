#!/usr/bin/env python3
"""
Detect available display modes and keep /boot/config.txt in sync so Raspberry Pi
can drive connected HDMI panels reliably.

- Prefers tvservice (Pi specific) to read CEA/DMT modes and active mode
- Falls back to xrandr when tvservice is unavailable
- Writes an auto-generated block into config.txt with the detected resolutions
  and recommended hdmi_group/hdmi_mode (or hdmi_cvt when only xrandr is present)
"""

from __future__ import annotations

import argparse
import logging
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Iterable, List, Optional, Sequence

logging.basicConfig(level=logging.INFO, format="%(message)s")
logger = logging.getLogger(__name__)

START_MARKER = "# --- digital-signage display block (auto-generated) ---"
END_MARKER = "# --- end digital-signage display block ---"


@dataclass(frozen=True)
class DisplayMode:
    """Represents a detected display mode."""

    group: str
    width: int
    height: int
    refresh: float
    mode_id: Optional[int] = None  # tvservice mode id if available

    def label(self) -> str:
        mode_label = str(self.mode_id) if self.mode_id is not None else "-"
        return f"{self.group} {mode_label}: {self.width}x{self.height} @ {self.refresh:.2f}Hz"


def run_cmd(cmd: Sequence[str], timeout: int = 8) -> str:
    """Run command and return stdout or empty string on failure."""
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
            check=False,
        )
        if result.returncode == 0:
            return result.stdout
        logger.debug("Command %s failed: %s", cmd, result.stderr.strip())
    except Exception as exc:  # noqa: BLE001
        logger.debug("Command %s failed: %s", cmd, exc)
    return ""


def parse_tvservice_modes(output: str, group: str) -> List[DisplayMode]:
    """Parse tvservice -m output for a group (CEA/DMT)."""
    modes: List[DisplayMode] = []
    pattern = re.compile(
        r"mode\s+(?P<id>\d+):\s+(?P<w>\d+)x(?P<h>\d+)\s+@\s*(?P<r>[\d.]+)Hz",
        re.IGNORECASE,
    )
    for line in output.splitlines():
        match = pattern.search(line)
        if match:
            modes.append(
                DisplayMode(
                    group=group,
                    width=int(match.group("w")),
                    height=int(match.group("h")),
                    refresh=float(match.group("r")),
                    mode_id=int(match.group("id")),
                )
            )
    return modes


def parse_tvservice_status(output: str) -> Optional[DisplayMode]:
    """Parse tvservice -s output to determine the active mode."""
    # Example: state 0x12000a [HDMI CEA (16) RGB full 16:9], 1920x1080 @ 60.00Hz
    pattern = re.compile(
        r"HDMI\s+(?P<group>CEA|DMT)\s+\((?P<mode>\d+)\).*?(?P<w>\d{3,4})x(?P<h>\d{3,4})\s*@\s*(?P<r>[\d.]+)Hz",
        re.IGNORECASE,
    )
    match = pattern.search(output)
    if not match:
        return None
    try:
        return DisplayMode(
            group=match.group("group").upper(),
            width=int(match.group("w")),
            height=int(match.group("h")),
            refresh=float(match.group("r")),
            mode_id=int(match.group("mode")),
        )
    except ValueError:
        return None


def parse_xrandr_modes(output: str) -> List[DisplayMode]:
    """Parse xrandr --query output for all modes on connected outputs."""
    modes: List[DisplayMode] = []
    pattern = re.compile(r"^\s+(?P<w>\d{3,5})x(?P<h>\d{3,5})\s+(?P<r>[\d.]+)")
    for line in output.splitlines():
        match = pattern.match(line)
        if match:
            try:
                modes.append(
                    DisplayMode(
                        group="XRANDR",
                        width=int(match.group("w")),
                        height=int(match.group("h")),
                        refresh=float(match.group("r")),
                        mode_id=None,
                    )
                )
            except ValueError:
                continue
    # Deduplicate identical entries
    unique = {(m.group, m.width, m.height, round(m.refresh, 2)): m for m in modes}
    return list(unique.values())


def detect_modes() -> List[DisplayMode]:
    """Detect display modes via tvservice (preferred) or xrandr."""
    modes: List[DisplayMode] = []

    tv_out = run_cmd(["tvservice", "-m", "CEA"])
    if tv_out:
        modes.extend(parse_tvservice_modes(tv_out, "CEA"))

    tv_out = run_cmd(["tvservice", "-m", "DMT"])
    if tv_out:
        modes.extend(parse_tvservice_modes(tv_out, "DMT"))

    if modes:
        return modes

    xrandr_out = run_cmd(["xrandr", "--query"])
    if xrandr_out:
        return parse_xrandr_modes(xrandr_out)

    return []


def detect_active_mode() -> Optional[DisplayMode]:
    """Read current mode from tvservice -s if available."""
    status = run_cmd(["tvservice", "-s"])
    if not status:
        return None
    return parse_tvservice_status(status)


def pick_preferred_mode(modes: Iterable[DisplayMode]) -> Optional[DisplayMode]:
    """Pick the best available mode (largest area, highest refresh)."""
    modes_list = list(modes)
    if not modes_list:
        return None

    def sort_key(mode: DisplayMode):
        area = mode.width * mode.height
        group_score = 1 if mode.group == "CEA" else 0  # prefer CEA when in doubt
        return (area, mode.refresh, group_score)

    return max(modes_list, key=sort_key)


def build_block(modes: List[DisplayMode], active: Optional[DisplayMode], preferred: Optional[DisplayMode]) -> str:
    """Create the config.txt block content."""
    lines: List[str] = [
        f"# Generated: {datetime.now().isoformat(timespec='seconds')}",
        "# Detected HDMI modes:"
        if modes
        else "# No HDMI modes detected; keeping safe defaults",
    ]

    for mode in sorted(modes, key=lambda m: (m.width * m.height, m.refresh)):
        tags = []
        if active and mode.group == active.group and mode.mode_id == active.mode_id:
            tags.append("active")
        if preferred and mode == preferred:
            tags.append("preferred")
        tag_str = f" [{' ,'.join(tags)}]" if tags else ""
        lines.append(f"# - {mode.label()}{tag_str}")

    # Base settings to ensure HDMI output
    lines.extend(
        [
            "hdmi_force_hotplug=1",
            "hdmi_drive=2",
            "disable_overscan=1",
            "config_hdmi_boost=7",
        ]
    )

    if preferred:
        if preferred.group in {"CEA", "DMT"} and preferred.mode_id is not None:
            hdmi_group = 1 if preferred.group == "CEA" else 2
            lines.append(f"hdmi_group={hdmi_group}")
            lines.append(f"hdmi_mode={preferred.mode_id}")
        else:
            # xrandr fallback: generate a custom CVT mode
            lines.extend(
                [
                    "hdmi_group=2",
                    "hdmi_mode=87",
                    f"hdmi_cvt={preferred.width} {preferred.height} {int(round(preferred.refresh))} 3 0 0 0",
                ]
            )

    return "\n".join(lines)


def find_config_path(custom_path: Optional[str]) -> Path:
    """Locate the config.txt path to use."""
    if custom_path:
        return Path(custom_path)

    for candidate in (Path("/boot/config.txt"), Path("/boot/firmware/config.txt")):
        if candidate.exists():
            return candidate
    return Path("/boot/config.txt")


def update_config_file(path: Path, block: str) -> Path:
    """Insert or replace the managed block inside config.txt."""
    existing = path.read_text() if path.exists() else ""
    new_block = f"{START_MARKER}\n{block}\n{END_MARKER}"

    pattern = re.compile(
        re.escape(START_MARKER) + r".*?" + re.escape(END_MARKER),
        flags=re.DOTALL,
    )

    if pattern.search(existing):
        updated = pattern.sub(new_block, existing)
        logger.info("Updating existing digital-signage section in %s", path)
    else:
        if existing and not existing.endswith("\n"):
            existing += "\n"
        updated = existing + new_block + "\n"
        logger.info("Appending digital-signage section to %s", path)

    backup_path = path.with_suffix(path.suffix + ".bak-digitalsignage")
    if path.exists() and not backup_path.exists():
        backup_path.write_text(existing)
        logger.info("Backup created at %s", backup_path)

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(updated)
    return path


def ensure_root():
    """Ensure the script is executed with sufficient privileges."""
    if hasattr(os, "geteuid"):
        if os.geteuid() != 0:
            logger.error("Please run with sudo/root so /boot/config.txt can be updated.")
            sys.exit(1)


def main():
    parser = argparse.ArgumentParser(
        description="Auto-populate Raspberry Pi /boot/config.txt with detected display modes."
    )
    parser.add_argument(
        "--config-path",
        help="Path to config.txt (defaults to auto-detect /boot/config.txt or /boot/firmware/config.txt).",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the block without writing to the config file.",
    )
    args = parser.parse_args()

    ensure_root()

    modes = detect_modes()
    active = detect_active_mode()
    preferred = pick_preferred_mode(modes)

    block = build_block(modes, active, preferred)
    config_path = find_config_path(args.config_path)

    if args.dry_run:
        # Show what would be written (including markers for clarity)
        print(f"{START_MARKER}\n{block}\n{END_MARKER}")
        return

    try:
        path = update_config_file(config_path, block)
        logger.info("config.txt updated at %s", path)
    except PermissionError:
        logger.error("Permission denied while writing %s. Run with sudo.", config_path)
        sys.exit(1)
    except OSError as exc:  # noqa: BLE001
        logger.error("Failed to update %s: %s", config_path, exc)
        sys.exit(1)


if __name__ == "__main__":
    main()
