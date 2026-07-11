from __future__ import annotations

import base64
import gzip
from pathlib import Path

parts_root = Path(__file__).with_name("unify_parts")
parts = sorted(parts_root.glob("part*.txt"))
if not parts:
    raise RuntimeError("API migration payload parts are missing.")

payload = "".join(part.read_text(encoding="utf-8").strip() for part in parts)
source = gzip.decompress(base64.b64decode(payload))
exec(compile(source, __file__, "exec"))
