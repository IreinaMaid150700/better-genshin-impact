"""Tạo vi_dict.txt = dict latin PP-OCRv5 chính thức + chữ Việt còn thiếu + ký tự hay gặp trong TextMap.

Giữ nguyên thứ tự (kể cả các dòng trùng) của dict gốc để chép được trọng số head cũ sang head mới
(xem expand_head.py). Dấu cách không nằm trong dict: config dùng use_space_char: true.

Dùng:
  python build_vi_dict.py --base ppocrv5_latin_dict.txt --textmap TextMapVI.json --out vi_dict.txt
"""
import argparse
import collections
import unicodedata

from textmap import load_lines

VI_VOWELS = "aăâeêioôơuưy"
VI_TONES = ["", "̀", "́", "̃", "̉", "̣"]


def vietnamese_letters() -> list[str]:
    letters = []
    for vowel in VI_VOWELS:
        for tone in VI_TONES:
            letter = unicodedata.normalize("NFC", vowel + tone)
            letters += [letter, letter.upper()]
    letters += ["đ", "Đ"]
    return list(dict.fromkeys(letters))


def is_glyph(char: str) -> bool:
    category = unicodedata.category(char)
    return not char.isspace() and category[0] not in ("C", "Z") and category != "Mn"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", required=True, help="ppocrv5_latin_dict.txt (836 dòng)")
    parser.add_argument("--textmap", required=True, help="TextMapVI.json")
    parser.add_argument("--out", required=True)
    parser.add_argument("--report", default=None)
    parser.add_argument("--min-count", type=int, default=30, help="số lần xuất hiện tối thiểu để thêm ký tự ngoài bảng chữ Việt")
    args = parser.parse_args()

    with open(args.base, encoding="utf-8") as f:
        base = [line.rstrip("\r\n") for line in f if line.rstrip("\r\n") != ""]
    known = set(base)

    added_vietnamese = [c for c in vietnamese_letters() if c not in known]
    known.update(added_vietnamese)

    counts = collections.Counter()
    for line in load_lines(args.textmap):
        counts.update(line)

    added_extra = sorted(
        (c for c, n in counts.items() if n >= args.min_count and c not in known and is_glyph(c)),
        key=lambda c: (-counts[c], c))
    known.update(added_extra)

    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        for char in base + added_vietnamese + added_extra:
            f.write(char + "\n")

    total = sum(n for c, n in counts.items() if not c.isspace())
    uncovered = {c: n for c, n in counts.items() if c not in known and not c.isspace()}
    report = [
        f"base={len(base)} (unique {len(set(base))})",
        f"added_vietnamese={len(added_vietnamese)}: {''.join(added_vietnamese)}",
        f"added_extra={len(added_extra)}: " + " ".join(f"{c}(U+{ord(c):04X})x{counts[c]}" for c in added_extra),
        f"dict_total={len(base) + len(added_vietnamese) + len(added_extra)} -> CTC classes={len(base) + len(added_vietnamese) + len(added_extra) + 2}",
        f"textmap_chars={total} uncovered={sum(uncovered.values())} ({sum(uncovered.values()) / max(total, 1):.4%})",
        "top uncovered: " + " ".join(f"{c}(U+{ord(c):04X})x{n}" for c, n in sorted(uncovered.items(), key=lambda p: -p[1])[:40]),
    ]
    print("\n".join(report))
    if args.report:
        with open(args.report, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(report) + "\n")


if __name__ == "__main__":
    main()
