"""Install the pinned ORK Unity package into this checkout."""

import argparse
import gzip
import hashlib
from pathlib import Path, PurePosixPath
import tarfile

PROJECT = Path(__file__).resolve().parent.parent
PACKAGE_SHA256 = "ba2879da647514dac8087531c69c58612aac42270fa4c8fce56d37ea3124eb35"
VENDOR_ROOTS = (
    PurePosixPath("Assets/Gaming Is Love"),
    PurePosixPath("Assets/Gizmos"),
)


def read_package(package):
    with package.open("rb") as stream:
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
        if digest.hexdigest() != PACKAGE_SHA256:
            raise ValueError(f"Package SHA-256 mismatch: {digest.hexdigest()}")

        stream.seek(0)
        entries = {}
        with gzip.GzipFile(fileobj=stream) as payload, tarfile.open(fileobj=payload, mode="r|") as archive:
            for member in archive:
                entry, _, kind = member.name.rpartition("/")
                if not member.isfile() or kind not in {"pathname", "asset.meta", "asset"}:
                    continue
                entries.setdefault(entry, {})[kind] = archive.extractfile(member).read()

    files = {}
    for entry in entries.values():
        if "pathname" not in entry:
            continue
        raw_path = entry["pathname"].decode("utf-8").splitlines()[0]
        path = PurePosixPath(raw_path)
        if (
            path.is_absolute()
            or ".." in path.parts
            or "\\" in raw_path
            or not any(path == root or root in path.parents for root in VENDOR_ROOTS)
        ):
            raise ValueError(f"Unexpected asset path: {raw_path!r}")

        files[PurePosixPath(f"{path}.meta")] = entry["asset.meta"]
        if "asset" in entry:
            files[path] = entry["asset"]
    return files


def install(files):
    missing = {}
    for relative, data in files.items():
        target = PROJECT.joinpath(*relative.parts)
        for path in (target, *target.relative_to(PROJECT).parents):
            candidate = path if path.is_absolute() else PROJECT / path
            if candidate.is_symlink():
                raise ValueError(f"Refusing to write through a symlink: {candidate}")
        if target.exists():
            if not target.is_file() or target.read_bytes() != data:
                raise ValueError(f"Existing asset differs: {relative}")
        else:
            missing[target] = data

    created = []
    try:
        for target, data in missing.items():
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open("xb") as output:
                created.append(target)
                output.write(data)
    except OSError:
        for target in reversed(created):
            target.unlink()
        raise
    return len(created)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    args = parser.parse_args()
    try:
        files = read_package(args.package)
        added = install(files)
    except (OSError, ValueError, KeyError, tarfile.TarError) as error:
        parser.exit(1, f"{error}\n")
    print(f"Verified {len(files)} package files, installed {added}.")


if __name__ == "__main__":
    main()
