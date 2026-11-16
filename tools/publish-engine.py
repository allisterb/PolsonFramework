"""Publishes Polson Engine: one self-contained executable per platform, zipped, with SHA-256 checksums.

    py -3.13 tools/publish-engine.py                      # win-x64, linux-x64, osx-arm64 from HEAD
    py -3.13 tools/publish-engine.py --rid win-x64        # one platform
    py -3.13 tools/publish-engine.py --worktree           # uncommitted changes too (not for a release)
    py -3.13 tools/publish-engine.py --offline            # restore from the local NuGet cache only

The publish runs in a copy of the source in a temporary folder, never in the checkout. A restore for one platform (-r)
adds a platform section to the packages.lock.json of every project the engine references, so publishing in place
would leave seven lock files modified. Building from `git archive HEAD` also means a release is built from committed
source and nothing else.

Out: bin/engine/polson-engine-<version>-<rid>.zip and bin/engine/SHA256SUMS. Each archive holds the executable,
LICENSE, THIRD-PARTY-NOTICES.md (rewrite it with tools/engine-notices.py first if a package changed), README.md,
appsettings.example.json, licenses/ and notices/ (the runtime's and Skia's own, from the NuGet cache).

--with-data adds what tools/bootstrap.py fetched into the checkout: Mesh's Library/ (the rig template, pose clips and
stock bodies) and Animation's cmudict. They are not committed and carry their own licences, listed in
src/Polson.Drawing.Mesh/Library/README.md; read those before shipping them.

--offline points restore at an empty folder, so anything not already in the NuGet cache fails instead of downloading.
A platform whose runtime pack has never been restored on this machine then fails with NU1101.
"""
import argparse, hashlib, io, json, os, pathlib, re, shutil, subprocess, sys, tarfile, tempfile, zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
RIDS = ["win-x64", "linux-x64", "osx-arm64"]
SHIPPED = {  # file in the archive: source in the copy
    "LICENSE": "LICENSE",
    "THIRD-PARTY-NOTICES.md": "src/Polson.Engine/THIRD-PARTY-NOTICES.md",
    "README.md": "src/Polson.Engine/README.md",
    "appsettings.example.json": "src/Polson.Engine.CLI/appsettings.example.json",
}


def git(*args: str) -> bytes:
    return subprocess.run(["git", "-C", str(ROOT), *args], check=True, capture_output=True).stdout


def export(dest: pathlib.Path, worktree: bool) -> None:
    if worktree:
        names = git("ls-files", "-z", "--cached", "--others", "--exclude-standard").decode("utf-8").split("\0")
        for name in filter(None, names):
            source = ROOT / name
            if source.is_file():
                (dest / name).parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, dest / name)
    else:
        with tarfile.open(fileobj=io.BytesIO(git("archive", "--format=tar", "HEAD"))) as tar:
            tar.extractall(dest, filter="data")


def add_data(dest: pathlib.Path) -> None:
    for pattern in ["src/Polson.Drawing.Mesh/Library/**/*", "src/Polson.Animation/Speech/cmudict.*"]:
        for source in ROOT.glob(pattern):
            if source.is_file():
                target = dest / source.relative_to(ROOT)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)


def version(copy: pathlib.Path) -> str:
    props = (copy / "Directory.Build.props").read_text(encoding="utf-8")
    return re.search(r"<ProjectAssemblyVersion>([^<]+)<", props).group(1)


def cache() -> pathlib.Path:
    return pathlib.Path(os.environ.get("NUGET_PACKAGES", pathlib.Path.home() / ".nuget" / "packages"))


def newest(folder: pathlib.Path) -> pathlib.Path:
    versions = [p for p in folder.iterdir() if p.is_dir()] if folder.is_dir() else []
    if not versions:
        sys.exit(f"{folder} is missing; the publish should have restored it.")
    return max(versions, key=lambda p: [int(x) if x.isdigit() else 0 for x in re.split(r"[.-]", p.name)])


def add_notices(copy: pathlib.Path, rid: str, staged: pathlib.Path) -> None:
    """The licence texts, and the notices of what the executable carries: the runtime, ASP.NET Core and Skia's natives."""
    shutil.copytree(copy / "src/Polson.Engine/licenses", staged / "licenses")
    notices = staged / "notices"
    notices.mkdir()
    lock = json.loads((copy / "src/Polson.Engine.CLI/packages.lock.json").read_text(encoding="utf-8"))["dependencies"]["net10.0"]
    native = {"win": "win32", "linux": "linux", "osx": "macos"}[rid.split("-")[0]]
    sources = {
        "dotnet-runtime.txt": newest(cache() / f"microsoft.netcore.app.runtime.{rid}") / "THIRD-PARTY-NOTICES.TXT",
        "aspnetcore.txt": newest(cache() / f"microsoft.aspnetcore.app.runtime.{rid}") / "THIRD-PARTY-NOTICES.TXT",
        "skia-harfbuzz.txt": cache() / f"skiasharp.nativeassets.{native}" / lock["SkiaSharp"]["resolved"] / "THIRD-PARTY-NOTICES.txt",
    }
    for name, source in sources.items():
        if not source.is_file():
            sys.exit(f"{source} is missing; a release must carry it.")
        shutil.copy2(source, notices / name)


def publish(copy: pathlib.Path, rid: str, out: pathlib.Path, offline: bool) -> None:
    command = ["dotnet", "publish", str(copy / "src/Polson.Engine.CLI"), "-c", "Release", "-r", rid, "-o", str(out),
               "-nologo", "-clp:ErrorsOnly"]
    if offline:
        empty = copy / ".empty-feed"
        empty.mkdir(exist_ok=True)
        command.append(f"-p:RestoreSources={empty}")
    subprocess.run(command, check=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--rid", action="append", choices=RIDS, help="a platform to publish; repeatable (default: all)")
    parser.add_argument("--worktree", action="store_true", help="publish the working tree, uncommitted changes included")
    parser.add_argument("--with-data", action="store_true", help="include the fetched Mesh library and cmudict")
    parser.add_argument("--offline", action="store_true", help="restore from the local NuGet cache only")
    parser.add_argument("--out", type=pathlib.Path, default=ROOT / "bin" / "engine")
    args = parser.parse_args()

    args.out.mkdir(parents=True, exist_ok=True)
    sums = []
    with tempfile.TemporaryDirectory(prefix="polson-engine-") as temp:
        copy = pathlib.Path(temp) / "src"
        copy.mkdir()
        export(copy, args.worktree)
        if args.with_data:
            add_data(copy)
        ver = version(copy)
        for rid in args.rid or RIDS:
            staged = pathlib.Path(temp) / "out" / rid
            publish(copy, rid, staged, args.offline)
            for name, source in SHIPPED.items():
                shutil.copy2(copy / source, staged / name)
            add_notices(copy, rid, staged)
            for symbols in staged.glob("*.pdb"):
                symbols.unlink()
            # Mesh copies Library/ to output; without --with-data only its README comes, describing files not here.
            library = staged / "Library"
            if library.is_dir() and [p.name for p in library.rglob("*") if p.is_file()] == ["README.md"]:
                shutil.rmtree(library)

            archive = args.out / f"polson-engine-{ver}-{rid}.zip"
            with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as zf:
                for path in sorted(p for p in staged.rglob("*") if p.is_file()):
                    info = zipfile.ZipInfo.from_file(path, path.relative_to(staged).as_posix())
                    if path.name == "polson-engine":
                        info.external_attr = 0o755 << 16  # executable on Linux and macOS
                    with path.open("rb") as f:
                        zf.writestr(info, f.read(), zipfile.ZIP_DEFLATED)
            digest = hashlib.sha256(archive.read_bytes()).hexdigest()
            sums.append(f"{digest}  {archive.name}")
            print(f"{archive.name}: {archive.stat().st_size / 1e6:.1f} MB, sha256 {digest}")

    # Keep the other platforms' lines when only some were published, so the file always covers every archive here.
    listed = args.out / "SHA256SUMS"
    fresh = {line.split()[-1] for line in sums}
    kept = [line for line in (listed.read_text(encoding="utf-8").splitlines() if listed.exists() else [])
            if line.split() and line.split()[-1] not in fresh and (args.out / line.split()[-1]).exists()]
    listed.write_text("\n".join(sorted(kept + sums, key=lambda l: l.split()[-1])) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
