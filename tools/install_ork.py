"""Install the pinned ORK Unity package into this checkout."""

import argparse
import gzip
import hashlib
import shutil
from tempfile import SpooledTemporaryFile
from pathlib import Path, PurePosixPath
import tarfile

PROJECT = Path(__file__).resolve().parent.parent
PACKAGE_SHA256 = "ba2879da647514dac8087531c69c58612aac42270fa4c8fce56d37ea3124eb35"
VENDOR_ROOTS = (
    PurePosixPath("Assets/Gaming Is Love"),
    PurePosixPath("Assets/Gizmos"),
)


def read_package(package):
    """Check the pinned archive and stream large asset payloads into bounded spools.

    .meta and pathname entries remain small bytes. The caller owns the
    returned asset spools and must close them after install.
    """
    open_spools = []
    try:
        with package.open("rb") as stream:
            digest = hashlib.sha256()
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
            if digest.hexdigest() != PACKAGE_SHA256:
                raise ValueError(f"Package SHA-256 mismatch: {digest.hexdigest()}")
            stream.seek(0)
            entries = {}
            with gzip.GzipFile(fileobj=stream) as payload, tarfile.open(
                    fileobj=payload, mode="r|") as archive:
                for member in archive:
                    entry, _, kind = member.name.rpartition("/")
                    if not member.isfile() or kind not in {"pathname", "asset.meta", "asset"}:
                        continue
                    extracted = archive.extractfile(member)
                    if extracted is None:
                        raise ValueError(f"Cannot read archive member: {member.name}")
                    with extracted:
                        if kind == "asset":
                            spool = SpooledTemporaryFile(max_size=1024 * 1024)
                            try:
                                shutil.copyfileobj(extracted, spool, length=1024 * 1024)
                                spool.seek(0)
                            except Exception:
                                spool.close()
                                raise
                            open_spools.append(spool)
                            entries.setdefault(entry, {})[kind] = spool
                        else:
                            entries.setdefault(entry, {})[kind] = extracted.read()

        files = {}
        for entry in entries.values():
            if "pathname" not in entry:
                continue
            decoded_path = entry["pathname"].decode("utf-8")
            raw_lines = decoded_path.splitlines()
            if len(raw_lines) != 1 or not raw_lines[0].strip() or raw_lines[0] != raw_lines[0].strip():
                raise ValueError(f"Blank or padded asset path in Unity package: {decoded_path!r}")
            raw_path = raw_lines[0]
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
            else:
                # Unity package directories ship only a pathname and .meta.
                files[path] = None

        used = {id(data) for data in files.values() if hasattr(data, "read")}
        for spool in open_spools:
            if id(spool) not in used:
                spool.close()
        return files
    except Exception:
        for spool in open_spools:
            spool.close()
        raise


def matches_existing_asset(target, data):
    if isinstance(data, bytes):
        return target.read_bytes() == data
    data.seek(0)
    with target.open("rb") as existing:
        while True:
            chunk = data.read(1024 * 1024)
            if not chunk:
                return not existing.read(1)
            if existing.read(len(chunk)) != chunk:
                return False


def install(files):
    missing = {}
    for relative, data in files.items():
        target = PROJECT.joinpath(*relative.parts)
        for path in (target, *target.relative_to(PROJECT).parents):
            candidate = path if path.is_absolute() else PROJECT / path
            if candidate.is_symlink():
                raise ValueError(f"Refusing to write through a symlink: {candidate}")
        if target.exists():
            if data is None:
                if not target.is_dir():
                    raise ValueError(f"Existing package folder differs: {relative}")
            elif not target.is_file() or not matches_existing_asset(target, data):
                raise ValueError(f"Existing asset differs: {relative}")
        else:
            missing[target] = data

    created = []
    try:
        # Materialize directory records before their contained files so a
        # failed install can remove files before walking back empty folders.
        ordered = sorted(missing.items(),
                         key=lambda item: (item[1] is not None, len(item[0].parts)))
        for target, data in ordered:
            target.parent.mkdir(parents=True, exist_ok=True)
            if data is None:
                if not target.exists():
                    target.mkdir()
                    created.append(target)
            else:
                with target.open("xb") as output:
                    created.append(target)
                    if isinstance(data, bytes):
                        output.write(data)
                    else:
                        data.seek(0)
                        shutil.copyfileobj(data, output, length=1024 * 1024)
    except OSError:
        for target in reversed(created):
            if target.is_dir():
                target.rmdir()
            else:
                target.unlink()
        raise
    return len(created)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    args = parser.parse_args()
    try:
        files = read_package(args.package)
        try:
            added = install(files)
        finally:
            for data in files.values():
                if hasattr(data, "close"):
                    data.close()
    except (OSError, ValueError, KeyError, tarfile.TarError) as error:
        parser.exit(1, f"{error}\n")
    print(f"Verified {len(files)} package files, installed {added}.")


if __name__ == "__main__":
    main()
