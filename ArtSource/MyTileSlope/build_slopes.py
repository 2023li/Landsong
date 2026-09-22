"""Entry point: regenerate slopes from the exported existing land mesh."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name("build_protruding_slopes.py")), run_name="__main__")
