#!/usr/bin/env python3
"""Copy Windows TFMs from Windows-packed nupkgs into the macOS nupkgs.

dotnet pack on macOS cannot emit net*-windows*. The Windows runner packs that
TFM, but publish previously pushed only the macOS artifact — so NuGet.org
showed net10.0-windows as a compatibility hint, not a real framework.
"""

from __future__ import annotations

import argparse
import io
import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET


def fail(message: str) -> None:
    print(f"::error::{message}")
    raise SystemExit(1)


def package_stem(path: Path) -> str:
    name = path.name
    if name.endswith(".snupkg"):
        return name[: -len(".snupkg")]
    if name.endswith(".nupkg"):
        return name[: -len(".nupkg")]
    return path.stem


def is_windows_payload(name: str) -> bool:
    lower = name.replace("\\", "/").lower()
    if "windows" not in lower:
        return False
    return lower.startswith("lib/") or lower.startswith("ref/") or lower.startswith("runtimes/")


def find_nuspec(archive: zipfile.ZipFile) -> str | None:
    for name in archive.namelist():
        if name.lower().endswith(".nuspec") and "/" not in name.replace("\\", "/").strip("/"):
            return name
    return None


def merge_nuspec(mac_xml: bytes, win_xml: bytes) -> bytes:
    mac_root = ET.fromstring(mac_xml)
    win_root = ET.fromstring(win_xml)
    ns = ""
    if mac_root.tag.startswith("{"):
        ns = mac_root.tag.split("}", 1)[0][1:]
        ET.register_namespace("", ns)

    def qname(local: str) -> str:
        return f"{{{ns}}}{local}" if ns else local

    mac_deps = mac_root.find(f".//{qname('dependencies')}")
    win_deps = win_root.find(f".//{qname('dependencies')}")
    if win_deps is None:
        return mac_xml
    if mac_deps is None:
        metadata = mac_root.find(qname("metadata"))
        if metadata is None:
            return mac_xml
        mac_deps = ET.SubElement(metadata, qname("dependencies"))

    existing = {
        (group.get("targetFramework") or "").lower()
        for group in mac_deps.findall(qname("group"))
    }
    added = 0
    for group in win_deps.findall(qname("group")):
        tfm = (group.get("targetFramework") or "").lower()
        if "windows" not in tfm or tfm in existing:
            continue
        mac_deps.append(group)
        existing.add(tfm)
        added += 1
        print(f"  nuspec + group {group.get('targetFramework')}")
    if added == 0:
        return mac_xml
    buffer = io.BytesIO()
    tree = ET.ElementTree(mac_root)
    tree.write(buffer, encoding="utf-8", xml_declaration=True)
    return buffer.getvalue()


def merge_archives(mac: Path, win: Path, dest: Path) -> int:
    added = 0
    with zipfile.ZipFile(mac, "r") as mz, zipfile.ZipFile(win, "r") as wz, zipfile.ZipFile(
        dest, "w", compression=zipfile.ZIP_DEFLATED
    ) as out:
        mac_names = set(mz.namelist())
        extras = [name for name in wz.namelist() if is_windows_payload(name) and name not in mac_names]
        nuspec_name = find_nuspec(mz)
        win_nuspec = find_nuspec(wz) if nuspec_name else None

        for name in mz.namelist():
            data = mz.read(name)
            if nuspec_name and name == nuspec_name and win_nuspec:
                data = merge_nuspec(data, wz.read(win_nuspec))
            out.writestr(name, data)

        for name in extras:
            out.writestr(name, wz.read(name))
            added += 1
            print(f"  + {name}")

    return added


def index_packages(folder: Path, suffix: str) -> dict[str, Path]:
    found: dict[str, Path] = {}
    if not folder.is_dir():
        return found
    for path in folder.rglob(f"*{suffix}"):
        if suffix == ".nupkg" and path.name.endswith(".snupkg"):
            continue
        found[package_stem(path)] = path
    return found


def merge_folder(mac_dir: Path, win_dir: Path) -> None:
    for suffix in (".nupkg", ".snupkg"):
        mac_pkgs = index_packages(mac_dir, suffix)
        win_pkgs = index_packages(win_dir, suffix)
        if not mac_pkgs:
            continue
        if not win_pkgs:
            print(f"No Windows {suffix} artifacts; leaving macOS packages unchanged")
            continue
        for stem, mac in mac_pkgs.items():
            win = win_pkgs.get(stem)
            if win is None:
                print(f"No Windows match for {mac.name}; leaving unchanged")
                continue
            print(f"Merging Windows TFMs into {mac.name}")
            tmp = mac.with_suffix(mac.suffix + ".merged")
            added = merge_archives(mac, win, tmp)
            tmp.replace(mac)
            print(f"  added {added} Windows file(s)")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mac", required=True, type=Path)
    parser.add_argument("--windows", required=True, type=Path)
    args = parser.parse_args()
    if not args.mac.is_dir():
        fail(f"macOS package folder not found: {args.mac}")
    merge_folder(args.mac, args.windows)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
