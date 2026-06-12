#!/usr/bin/env python3
"""为 Assets/ 下所有资源生成 Unity .meta 文件（确定性 GUID），
并回填 ManagerScene.unity / EditorBuildSettings.asset 中的 __GUID__path__ 占位符。

GUID 规则：md5("bistro-burrow:" + 仓库相对路径) → 32 位 hex。
同一路径永远得到同一 GUID，重复执行幂等；新增资源后重跑本脚本即可。
"""
import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "Assets"

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

MONO_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

DEFAULT_META = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASMDEF_META = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FONT_META = """fileFormatVersion: 2
guid: {guid}
TrueTypeFontImporter:
  externalObjects: {{}}
  serializedVersion: 4
  fontSize: 16
  forceTextureCase: -2
  characterSpacing: 0
  characterPadding: 1
  includeFontData: 1
  fontName: {font_name}
  fontNames:
  - {font_name}
  fallbackFontReferences: []
  customCharacters:
  fontRenderingMode: 0
  ascentCalculationMode: 1
  useLegacyBoundsCalculation: 0
  shouldRoundAdvanceValue: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(rel_path: str) -> str:
    return hashlib.md5(f"bistro-burrow:{rel_path}".encode()).hexdigest()


def meta_content(path: Path, guid: str) -> str:
    if path.is_dir():
        return FOLDER_META.format(guid=guid)
    suffix = path.suffix.lower()
    if suffix == ".cs":
        return MONO_META.format(guid=guid)
    if suffix == ".json":
        return TEXT_META.format(guid=guid)
    if suffix == ".asmdef":
        return ASMDEF_META.format(guid=guid)
    if suffix in (".ttf", ".otf"):
        return FONT_META.format(guid=guid, font_name=path.stem)
    return DEFAULT_META.format(guid=guid)  # .unity 等


def main() -> int:
    if not ASSETS.is_dir():
        print("Assets/ 不存在", file=sys.stderr)
        return 1

    guids: dict[str, str] = {}
    targets = [p for p in sorted(ASSETS.rglob("*")) if p.suffix != ".meta"]
    targets.insert(0, ASSETS)  # Assets 根目录本身不需要 meta，但收录 GUID 无害——实际跳过
    written = 0

    for p in targets:
        rel = p.relative_to(ROOT).as_posix()
        guid = guid_for(rel)
        guids[rel] = guid
        if p == ASSETS:
            continue  # Unity 不为 Assets 根生成 meta
        meta_path = Path(str(p) + ".meta")
        content = meta_content(p, guid)
        if not meta_path.exists() or meta_path.read_text() != content:
            meta_path.write_text(content)
            written += 1

    # 回填占位符
    placeholder = re.compile(r"__GUID__(.+?)__")
    patched = []
    for f in [ROOT / "Assets/Scenes/ManagerScene.unity", ROOT / "ProjectSettings/EditorBuildSettings.asset"]:
        if not f.exists():
            continue
        text = f.read_text()

        def sub(m: "re.Match[str]") -> str:
            rel_path = m.group(1)
            if rel_path not in guids:
                print(f"!! 占位符引用了未知资源: {rel_path}", file=sys.stderr)
                return m.group(0)
            return guids[rel_path]

        new_text = placeholder.sub(sub, text)
        if new_text != text:
            f.write_text(new_text)
            patched.append(f.name)

    print(f"meta 写入/更新 {written} 个；占位符回填: {patched or '无需更新'}")
    leftover = [str(f) for f in [ROOT / 'Assets/Scenes/ManagerScene.unity'] if '__GUID__' in f.read_text()]
    if leftover:
        print(f"!! 仍有未回填占位符: {leftover}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
