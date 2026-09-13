"""Conservative shared classifier for PR CI and the normal deployment workflow."""
import argparse
import subprocess

YAP_ROOTS = (
    "src/Presentation/XFramework.Yap/",
    "src/Presentation/XFramework.Yap.Client/",
    "src/Presentation/XFramework.Yap.Contracts/",
    "src/Tests/Yap.Tests/",
    "src/Tests/Yap.Client.Tests/",
)


def classify(paths):
    paths = list(paths)
    # Unknown files and build metadata always require the full validation path.
    return "yap" if paths and all(
        path.startswith(YAP_ROOTS)
        and not path.endswith((".csproj", ".props", ".targets", ".config"))
        and path.rsplit("/", 1)[-1] not in ("global.json", "packages.lock.json")
        and not path.rsplit("/", 1)[-1].startswith("appsettings")
        for path in paths
    ) else "full"


def git_scope(base, head):
    try:
        subprocess.run(["git", "merge-base", "--is-ancestor", base, head], check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        # No rename detection: both a moved file's old and new paths must qualify.
        result = subprocess.run(["git", "diff", "--no-renames", "--name-only", "-z", base, head],
                                check=True, capture_output=True)
        return classify(result.stdout.decode("utf-8").rstrip("\0").split("\0"))
    except (subprocess.CalledProcessError, UnicodeError):
        return "full"


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("base")
    parser.add_argument("head")
    args = parser.parse_args()
    print(git_scope(args.base, args.head))
