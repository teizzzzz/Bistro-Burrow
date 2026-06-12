#!/usr/bin/env python3
"""把完整 CJK 字体裁剪为"项目实际用到的字符"子集，控制 WebGL 包体。

字符来源：Assets/ 下全部 .cs 与 .json 中出现的字符 + ASCII 可打印字符 + 常用标点。
新增 UI 文案后需重跑：python3 scripts/subset_font.py <完整字体路径>
"""
import string
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets/Resources/Fonts/NotoSansSC-Sub.otf"
# 兜底补充：动态拼接可能用到的标点/符号
EXTRA = "★×·…—、。，！？：；（）「」『』【】《》％+-/=<>~ 　"


def collect_chars() -> str:
    chars = set(string.printable)
    chars.update(EXTRA)
    for pattern in ("*.cs", "*.json"):
        for f in (ROOT / "Assets").rglob(pattern):
            chars.update(f.read_text(encoding="utf-8"))
    # 去掉控制字符
    return "".join(sorted(c for c in chars if c.isprintable() or c == " "))


def main() -> int:
    if len(sys.argv) < 2:
        print("用法: subset_font.py <完整字体.otf/.ttf>", file=sys.stderr)
        return 1
    src = Path(sys.argv[1])
    if not src.exists():
        print(f"字体不存在: {src}", file=sys.stderr)
        return 1

    chars = collect_chars()
    chars_file = ROOT / "scripts" / ".charset.txt"
    chars_file.write_text(chars, encoding="utf-8")
    OUT.parent.mkdir(parents=True, exist_ok=True)

    cmd = [
        sys.executable, "-m", "fontTools.subset", str(src),
        f"--text-file={chars_file}",
        f"--output-file={OUT}",
        "--no-hinting",
        "--desubroutinize",
        "--name-IDs=*",
        "--layout-features=",  # 纯展示用，去掉 OpenType 排版特性进一步减体积
    ]
    subprocess.run(cmd, check=True)
    print(f"字符数 {len(chars)} → {OUT}（{OUT.stat().st_size / 1024:.0f} KB）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
