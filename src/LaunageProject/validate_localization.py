"""Validate localization source, generated assets, placeholders and literal lookups.

Run with Python 3 from any directory. Pass --generate to regenerate language assets
from the language project before validation. API response data is never inspected.
"""
import argparse
import json
import re
from pathlib import Path

BASE = Path(__file__).resolve().parent
SOURCE = BASE / "Haiyu.LanguageProject2026-07-27.json"
APP = BASE.parent / "WutheringWavesTool"
LANGUAGES = {"zh-Hans": "zh-Hans.json", "zh-Hant": "zh-Hant.json",
             "en-us": "en-US.json", "ja-Jp": "ja-JP.json"}


def validate(generate=False):
    rows = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    keys = {row["keys"] for row in rows}
    errors = []
    if len(keys) != len(rows):
        errors.append("Duplicate source keys")
    for row in rows:
        expected = sorted(re.findall(r"\{\d+[^{}]*\}", row["zh-Hans"]))
        for language in LANGUAGES:
            value = row.get(language)
            if not isinstance(value, str) or not value.strip():
                errors.append(f"{row['keys']}: empty {language}")
            elif sorted(re.findall(r"\{\d+[^{}]*\}", value)) != expected:
                errors.append(f"{row['keys']}: placeholder mismatch in {language}")
    if errors:
        raise AssertionError("\n".join(errors))
    for language, filename in LANGUAGES.items():
        path = APP / "Assets" / "Languages" / filename
        expected = [{"key": row["keys"], "value": row[language]} for row in rows]
        if generate:
            path.write_text(json.dumps(expected, ensure_ascii=True, separators=(",", ":")), encoding="utf-8")
        if json.loads(path.read_text(encoding="utf-8-sig")) != expected:
            errors.append(f"Generated resource differs from source: {filename}")
    source_texts = {row["zh-Hans"] for row in rows}
    for path in APP.rglob("*"):
        if path.suffix not in (".cs", ".xaml") or any(part in ("bin", "obj", "AppPackages", "BundleArtifacts") for part in path.parts):
            continue
        text = path.read_text(encoding="utf-8-sig")
        for match in re.finditer(r'''GetString\(\s*["']([^"'\r\n]+)["']''', text):
            if match[1] not in keys:
                errors.append(f"{path.relative_to(APP)}: unknown key {match[1]}")
        for match in re.finditer(r'''(?:GetStringByText|FormatByText)\(\s*"((?:\\.|[^"\\])*)"''', text):
            value = json.loads('"' + match[1] + '"')
            if value not in source_texts:
                errors.append(f"{path.relative_to(APP)}: missing source text {value!r}")
        if path.suffix == ".xaml":
            for match in re.finditer(r"GetStringByText\('([^']*)'\)", text):
                if match[1] not in source_texts:
                    errors.append(f"{path.relative_to(APP)}: missing XAML source text {match[1]!r}")
    if errors:
        raise AssertionError("\n".join(errors))
    print(f"Validated {len(rows)} keys across four languages, generated assets and code lookups.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--generate", action="store_true")
    validate(parser.parse_args().generate)
