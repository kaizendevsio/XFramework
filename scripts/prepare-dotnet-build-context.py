"""Stage tracked project/dependency sources to avoid unrelated publish cache misses.

Unsupported MSBuild references/imports fall back to the existing repository context.
This is intentionally not an MSBuild evaluator. Conditional static references are
included conservatively; Docker's existing .dockerignore remains authoritative.
"""
import argparse
import json
import pathlib
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET


def project_directories(root, project):
    pending = [root / project]
    visited = set()
    linked_files = set()
    while pending:
        path = pending.pop().resolve()
        if path in visited:
            continue
        if not path.is_relative_to(root / "src"):
            raise ValueError("project outside src")
        visited.add(path)
        documents = [path]
        for parent in (path.parent, *path.parent.parents):
            if not parent.is_relative_to(root):
                break
            for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
                if (parent / name).is_file():
                    documents.append(parent / name)
        for document in documents:
            for item in ET.parse(document).iter():
                tag = item.tag.rsplit("}", 1)[-1]
                if tag == "Import":
                    if document == root / "Directory.Build.props" and item.get("Project") == "Version.props":
                        continue
                    raise ValueError("custom import requires full context")
                reference = item.get("Include", "").replace("\\", "/")
                if tag == "ProjectReference":
                    if not reference or any(char in reference for char in "$@*?;"):
                        raise ValueError("dynamic project reference")
                    pending.append(document.parent / reference)
                elif "../" in reference:
                    linked = (document.parent / reference).resolve()
                    if any(char in reference for char in "$@*?;") or not linked.is_relative_to(root) or not linked.is_file():
                        raise ValueError("dynamic external input requires full context")
                    linked_files.add(linked)
                elif any(char in reference for char in "$@") or reference.startswith("/"):
                    raise ValueError("dynamic input requires full context")
                # Custom build commands may consume paths not expressed as items.
                elif tag in ("Exec", "Target"):
                    raise ValueError("custom build command requires full context")
    return {path.parent for path in visited} | linked_files


def stage(root, project, destination, tracked):
    directories = project_directories(root, project)
    for relative in tracked:
        source = root / relative
        # Root configuration and all Directory.* files preserve ancestor discovery.
        include = len(relative.parts) == 1 or relative.name.startswith("Directory.")
        # Docker's shared native stage is outside the MSBuild project graph.
        include = include or relative.is_relative_to(pathlib.Path("src/Libraries/XFramework.Opaque.Native"))
        include = include or any(source == directory or source.is_relative_to(directory) for directory in directories)
        if not include:
            continue
        if source.is_symlink():
            raise ValueError("symlink input requires full context")
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    return len(directories)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--services", nargs="+", required=True)
    args = parser.parse_args()
    root = pathlib.Path.cwd().resolve()
    tracked = subprocess.run(["git", "ls-files", "-z"], check=True, capture_output=True).stdout
    tracked = [pathlib.Path(path) for path in tracked.decode("utf-8").split("\0") if path]
    compose = json.load(sys.stdin)
    overrides = {"services": {}}
    for service in args.services:
        build = compose["services"][service]["build"]
        project = build["args"]["PROJECT_PATH"]
        destination = args.output.parent.resolve() / service
        try:
            count = stage(root, project, destination, tracked)
            context = str(destination)
            print(f"{service}: scoped context ({count} projects)", file=sys.stderr)
        except (ValueError, OSError, ET.ParseError) as error:
            context = str(root)
            print(f"{service}: full context fallback ({type(error).__name__})", file=sys.stderr)
        overrides["services"][service] = {"build": {"context": context}}
    args.output.write_text(json.dumps(overrides) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
