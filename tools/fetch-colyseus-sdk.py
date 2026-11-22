#!/usr/bin/env python3
"""Download and verify the pinned Colyseus Godot native binaries."""

import argparse
import hashlib
import shutil
import sys
import tempfile
import urllib.request
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SDK = ROOT / "addons" / "colyseus"
URL = "https://github.com/colyseus/native-sdk/releases/download/godot-v0.18.10/colyseus-godot-0.18.10.zip"
ARCHIVE_SHA256 = "d2841b3d3cd959edce60c6073ba4c321d79fb09f15afefe69f57d51204293fac"
NAMES = (
    "colyseus_godot.windows.x86_64.debug.dll",
    "colyseus_godot.windows.x86_64.release.dll",
    "libcolyseus_godot.linux.x86_64.release.so",
    "libcolyseus_godot.macos.debug.dylib",
    "libcolyseus_godot.macos.release.dylib",
)


def digest(path):
    sha256 = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            sha256.update(chunk)
    return sha256.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, help="use an already downloaded zip")
    args = parser.parse_args()

    sums = {}
    for line in (SDK / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
        checksum, name = line.split()
        if name in sums:
            raise ValueError(f"duplicate checksum entry: {name}")
        sums[name] = checksum
    if set(sums) != {f"bin/{name}" for name in NAMES}:
        raise ValueError("SHA256SUMS does not list the five expected binaries")

    if all((SDK / name).is_file() and digest(SDK / name) == checksum
           for name, checksum in sums.items()):
        print("Colyseus SDK binaries already verified; no download needed.")
        return

    print("Colyseus SDK binaries are missing or mismatched; fetching verified binaries.")
    with tempfile.TemporaryDirectory(dir=SDK) as temporary:
        staging = Path(temporary)
        archive = args.archive
        if archive is None:
            archive = staging / "sdk.zip"
            print(f"Downloading {URL}")
            with urllib.request.urlopen(URL) as response, archive.open("wb") as file:
                shutil.copyfileobj(response, file)
        if digest(archive) != ARCHIVE_SHA256:
            raise ValueError("archive SHA-256 mismatch")

        with zipfile.ZipFile(archive) as zip_file:
            for name in NAMES:
                member = f"addons/colyseus/bin/{name}"
                if zip_file.namelist().count(member) != 1:
                    raise ValueError(f"archive must contain exactly one {member}")
                with zip_file.open(member) as source, (staging / name).open("wb") as target:
                    shutil.copyfileobj(source, target)
                if digest(staging / name) != sums[f"bin/{name}"]:
                    raise ValueError(f"binary SHA-256 mismatch: {name}")

        (SDK / "bin").mkdir(exist_ok=True)
        for name in NAMES:
            (staging / name).replace(SDK / "bin" / name)
        for name, checksum in sums.items():
            if digest(SDK / name) != checksum:
                raise ValueError(f"installed binary SHA-256 mismatch: {name}")
    print("Colyseus SDK binaries verified.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        print(f"Colyseus SDK fetch failed: {error}", file=sys.stderr)
        sys.exit(1)
