#!/usr/bin/env python3
"""Build the compact Traditional Chinese ECDICT index shipped with PowerTranslator.

ECDICT (https://github.com/skywind3000/ECDICT, MIT License) is an English -> Simplified
Chinese dictionary. This script downloads a pinned revision of ecdict.csv, converts the
Chinese translations to Traditional Chinese (Taiwan phrasing) with OpenCC ``s2twp`` and
writes a sorted, binary-searchable file that the plugin reads with random access, so the
dictionary never has to be loaded into memory.

File layout (little endian)::

    magic   8 bytes  b"PTECD001"
    count   uint32   number of records
    offsets uint32 * (count + 1), relative to the start of the record blob
    records UTF-8, fields separated by 0x1F:
            key, word, phonetic, translation, exchange, tag, frq

``key`` is the word with ASCII letters lower-cased; records are sorted by the UTF-8 bytes
of the key (then by word) so lookups can binary-search with an ordinal byte comparison.
Translation lines are separated by "\\n".

Usage::

    pip install opencc
    python tools/ecdict/build_ecdict.py --out data/ecdict.bin
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import os
import struct
import sys
import tempfile
import urllib.request

ECDICT_COMMIT = "bc015ed2e24a7abef49fc6dbbb7fe32c1dadaf8b"
ECDICT_URL = f"https://raw.githubusercontent.com/skywind3000/ECDICT/{ECDICT_COMMIT}/ecdict.csv"
MAGIC = b"PTECD001"
SEP = "\x1f"


def ascii_lower(s: str) -> str:
    return "".join(chr(ord(c) + 32) if "A" <= c <= "Z" else c for c in s)


def clean(s: str) -> str:
    # Fields must not contain the separator; real newlines are stored as "\n" escapes in ECDICT.
    return s.replace(SEP, " ").replace("\r", "").replace("\n", "\\n").strip()


def download(url: str, dest: str) -> None:
    print(f"downloading {url}", file=sys.stderr)
    with urllib.request.urlopen(url, timeout=120) as resp, open(dest, "wb") as f:
        while chunk := resp.read(1 << 20):
            f.write(chunk)


def build(csv_path: str, out_path: str) -> None:
    try:
        import opencc  # type: ignore
    except ImportError:
        sys.exit("OpenCC is required: pip install opencc")
    converter = opencc.OpenCC("s2twp")

    csv.field_size_limit(1 << 24)
    records: list[tuple[bytes, bytes, bytes]] = []
    with open(csv_path, encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            word = clean(row.get("word") or "")
            translation = clean(row.get("translation") or "")
            if not word or not translation:
                continue
            translation = converter.convert(translation)
            key = ascii_lower(word)
            frq = (row.get("frq") or "0").strip()
            bnc = (row.get("bnc") or "0").strip()
            # Prefer the contemporary corpus rank, fall back to BNC.
            rank = frq if frq not in ("", "0") else bnc
            fields = [
                key,
                word,
                clean(row.get("phonetic") or ""),
                translation,
                clean(row.get("exchange") or ""),
                clean(row.get("tag") or ""),
                rank if rank.isdigit() else "0",
            ]
            records.append((key.encode("utf-8"), word.encode("utf-8"), SEP.join(fields).encode("utf-8")))

    records.sort(key=lambda r: (r[0], r[1]))

    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    tmp = out_path + ".tmp"
    with open(tmp, "wb") as f:
        f.write(MAGIC)
        f.write(struct.pack("<I", len(records)))
        offset = 0
        offsets = bytearray()
        for _, _, data in records:
            offsets += struct.pack("<I", offset)
            offset += len(data)
        offsets += struct.pack("<I", offset)
        f.write(offsets)
        for _, _, data in records:
            f.write(data)
    os.replace(tmp, out_path)
    print(f"wrote {len(records)} entries to {out_path} ({os.path.getsize(out_path) / 1e6:.1f} MB)", file=sys.stderr)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--csv", help="use a local ecdict.csv instead of downloading it")
    parser.add_argument("--out", default=os.path.join("data", "ecdict.bin"), help="output file")
    parser.add_argument("--force", action="store_true", help="rebuild even if the output exists")
    args = parser.parse_args()

    if os.path.exists(args.out) and not args.force:
        print(f"{args.out} already exists, skipping (use --force to rebuild)", file=sys.stderr)
        return

    if args.csv:
        build(args.csv, args.out)
        return
    with tempfile.TemporaryDirectory() as tmpdir:
        csv_path = os.path.join(tmpdir, "ecdict.csv")
        download(ECDICT_URL, csv_path)
        digest = hashlib.sha256(open(csv_path, "rb").read()).hexdigest()
        print(f"ecdict.csv sha256 {digest}", file=sys.stderr)
        build(csv_path, args.out)


if __name__ == "__main__":
    main()
