"""MCP 服务入口：`python tools/gametest/server.py`（stdio 传输）。注册方法见同目录 README.md。"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from gametest.server import main  # noqa: E402

if __name__ == "__main__":
    main()
