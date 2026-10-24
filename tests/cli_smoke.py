"""Documented smoke entry for the published native CLI, without AVEVA execution."""
import importlib.util
from pathlib import Path
import sys


def check(runtime):
    path = Path(__file__).resolve().parent / "Phayton" / "frontends.py"
    specification = importlib.util.spec_from_file_location("native_frontends", path)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module.check(Path(runtime) / "PmlTwin.Phayton.Cli.exe",
                        Path(runtime).parent / "native-cli-processes.json")


if __name__ == "__main__":
    check(Path(sys.argv[1]).resolve())
