"""Sinh ảnh dòng chữ tổng hợp cho fine-tune nhận dạng PaddleOCR từ TextMap tiếng Việt của game.

Dùng:
  python gen_synth.py --textmap TextMapVI.json --dict vi_dict.txt --fonts fonts/ --out synth/ \
      --train 300000 --val 5000 [--backgrounds bg/] [--workers 4] [--seed 0]
Kết quả: synth/images/*.jpg, synth/train.txt, synth/val.txt (mỗi dòng "images/xxx.jpg<TAB>nhãn").
"""
import argparse
import glob
import multiprocessing as mp
import os
import random
import re

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

from build_vi_dict import vietnamese_letters
from textmap import load_lines

MAX_LEN = 25  # 与训练配置 max_text_length 一致
WORD = re.compile(r"\S+")
RARE_CAP = 3000

# 每个工作进程的全局状态（由 _init 填充）
G = {}


def _load_fonts(font_dir):
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        TTFont = None
    fonts = []
    for path in sorted(glob.glob(os.path.join(font_dir, "*.[ot]tf"))):
        cmap = None
        if TTFont is not None:
            cmap = set(TTFont(path, lazy=True).getBestCmap().keys())
        fonts.append((path, cmap))
    if not fonts:
        raise SystemExit(f"Không có font trong {font_dir}")
    return fonts


def _init(textmap, dict_path, font_dir, bg_dir):
    with open(dict_path, encoding="utf-8") as f:
        charset = {line.rstrip("\r\n") for line in f if line.rstrip("\r\n")}
    charset.add(" ")
    lines = sorted({line for line in load_lines(textmap) if all(c in charset for c in line)})
    rare_chars = [c for c in vietnamese_letters() if c in charset and not c.isascii()]
    rare_index = {c: [] for c in rare_chars}
    for i, line in enumerate(lines):
        for pos, c in enumerate(line):
            bucket = rare_index.get(c)
            if bucket is not None and len(bucket) < RARE_CAP:
                bucket.append((i, pos))
    G.update(
        lines=lines,
        charset=sorted(c for c in charset if c != " "),
        rare=[c for c in rare_chars if rare_index[c]],
        rare_index=rare_index,
        fonts=_load_fonts(font_dir),
        backgrounds=sorted(glob.glob(os.path.join(bg_dir, "*.*"))) if bg_dir else [],
        font_cache={},
    )


# ---------------------------------------------------------------- văn bản


def _window(line, rng, anchor=None):
    """Lấy một đoạn từ liền nhau dài tối đa MAX_LEN (chứa vị trí anchor nếu có)."""
    spans = [m.span() for m in WORD.finditer(line)]
    if not spans:
        return ""
    if anchor is None:
        k = rng.randrange(len(spans))
    else:
        k = next((i for i, (s, e) in enumerate(spans) if s <= anchor < e), 0)
    target = rng.randint(1, MAX_LEN)
    lo = hi = k
    while True:
        options = []
        if lo > 0 and spans[hi][1] - spans[lo - 1][0] <= target:
            options.append(-1)
        if hi < len(spans) - 1 and spans[hi + 1][1] - spans[lo][0] <= target:
            options.append(1)
        if not options:
            break
        if rng.choice(options) < 0:
            lo -= 1
        else:
            hi += 1
    text = line[spans[lo][0]:spans[hi][1]]
    if len(text) > MAX_LEN:
        center = anchor - spans[lo][0] if anchor is not None else rng.randrange(len(text))
        start = max(0, min(center - MAX_LEN // 2, len(text) - MAX_LEN))
        text = text[start:start + MAX_LEN].strip()
    return text


def _numeric(rng):
    n = lambda a, b: str(rng.randint(a, b))  # noqa: E731
    return rng.choice([
        lambda: f"Lv.{n(1, 100)}",
        lambda: f"{n(1, 99999)} / {n(1, 99999)}",
        lambda: f"{n(10, 300)} ms",
        lambda: f"Nơi Cao {n(1, 500)}m",
        lambda: f"Nơi Thấp {n(1, 500)}m",
        lambda: f"{n(0, 100)}%",
        lambda: f"+{n(1, 99)}.{n(0, 9)}%",
        lambda: f"x{n(1, 999)}",
        lambda: f"{int(n(0, 59)):02d}:{int(n(0, 59)):02d}",
        lambda: f"{n(1, 9999)}m",
        lambda: f"Cấp {n(1, 90)}/{n(1, 90)}",
        lambda: n(1, 9999999),
    ])()


def sample_text(rng):
    lines, mode = G["lines"], rng.random()
    if mode < 0.05:
        return _numeric(rng)
    if mode < 0.08:
        size = rng.randint(3, 12)
        chars = [rng.choice(G["charset"]) for _ in range(size)]
        for i in range(1, size - 1):
            if rng.random() < 0.2:
                chars[i] = " "
        return re.sub(r"\s+", " ", "".join(chars)).strip()
    if mode < 0.33 and G["rare"]:
        char = rng.choice(G["rare"])
        line_index, pos = rng.choice(G["rare_index"][char])
        text = _window(lines[line_index], rng, anchor=pos)
    else:
        text = _window(rng.choice(lines), rng)
    if rng.random() < 0.10:
        text = text.upper() if rng.random() < 0.5 else text.title()
    if len(text) > 3 and rng.random() < 0.08:
        # 对话逐字出现时截到的半截文字
        text = text[:rng.randint(1, len(text) - 1)].rstrip()
    return text


# ---------------------------------------------------------------- hình ảnh

DARK_BACKGROUNDS = [(30, 36, 51), (43, 50, 69), (22, 22, 26), (58, 44, 52), (44, 62, 84), (70, 60, 50)]
LIGHT_BACKGROUNDS = [(236, 229, 216), (244, 241, 233), (222, 215, 200), (230, 233, 238)]


def _jitter(color, rng, amount=12):
    return tuple(int(np.clip(c + rng.randint(-amount, amount), 0, 255)) for c in color)


def pick_style(rng):
    """Trả về (màu chữ, nền tối?)."""
    r = rng.random()
    if r < 0.45:
        gray = rng.randint(215, 255)
        return (gray, gray, gray), True
    if r < 0.60:
        return _jitter(rng.choice([(255, 204, 50), (240, 180, 60), (255, 220, 120)]), rng), True
    if r < 0.80:
        return _jitter(rng.choice([(59, 66, 85), (73, 83, 102), (40, 40, 40)]), rng), False
    if r < 0.88:
        return _jitter(rng.choice([(150, 220, 255), (120, 200, 240)]), rng), True
    if r < 0.94:
        return _jitter((163, 224, 72), rng), True
    return _jitter(rng.choice([(255, 140, 140), (230, 120, 200), (255, 255, 255)]), rng), True


def make_background(width, height, dark, rng):
    if G["backgrounds"] and rng.random() < 0.5:
        with Image.open(rng.choice(G["backgrounds"])) as source:
            source = source.convert("RGB")
            scale = max(width / source.width, height / source.height) * rng.uniform(1.0, 3.0)
            source = source.resize((max(width, int(source.width * scale)), max(height, int(source.height * scale))))
            x, y = rng.randint(0, source.width - width), rng.randint(0, source.height - height)
            background = np.asarray(source.crop((x, y, x + width, y + height)), dtype=np.float32)
        overlay = np.array(rng.choice(DARK_BACKGROUNDS if dark else LIGHT_BACKGROUNDS), dtype=np.float32)
        alpha = rng.uniform(0.35, 0.85) if dark else rng.uniform(0.7, 0.92)
        background = background * (1 - alpha) + overlay * alpha
    else:
        base = np.array(_jitter(rng.choice(DARK_BACKGROUNDS if dark else LIGHT_BACKGROUNDS), rng, 15), dtype=np.float32)
        background = np.broadcast_to(base, (height, width, 3)).copy()
        if rng.random() < 0.5:
            ramp = np.linspace(1.0, rng.uniform(0.6, 1.2), width, dtype=np.float32)[None, :, None]
            background *= ramp
    noise = rng.uniform(0, 6)
    if noise > 1:
        background += np.random.default_rng(rng.randrange(2**32)).normal(0, noise, background.shape)
    return Image.fromarray(np.clip(background, 0, 255).astype(np.uint8))


def _font(path, size, rng):
    key = (path, size)
    font = G["font_cache"].get(key)
    if font is None:
        font = ImageFont.truetype(path, size)
        G["font_cache"][key] = font
    try:
        axes = font.get_variation_axes()
        font.set_variation_by_axes([
            rng.uniform(max(a["minimum"], 400), min(a["maximum"], 700)) if a.get("name") in (b"Weight", "Weight")
            else a["default"] for a in axes])
    except (OSError, AttributeError, KeyError):
        pass
    return font


def _glyph_bitmap(font, char):
    canvas = Image.new("L", (48, 48), 0)
    ImageDraw.Draw(canvas).text((8, 4), char, font=font, fill=255)
    return canvas.tobytes()


def _covers(path, cmap, text):
    """Font có đủ glyph cho text không. Không có fontTools thì so bitmap với glyph .notdef."""
    if cmap is not None:
        return all(ord(c) in cmap for c in text if c != " ")
    cache = G.setdefault("glyph_cache", {}).setdefault(path, {})
    if "\0notdef" not in cache:
        cache["\0font"] = ImageFont.truetype(path, 24)
        cache["\0notdef"] = _glyph_bitmap(cache["\0font"], "\U0010FFFD")
    for c in set(text) - {" "}:
        ok = cache.get(c)
        if ok is None:
            ok = cache[c] = _glyph_bitmap(cache["\0font"], c) != cache["\0notdef"]
        if not ok:
            return False
    return True


def render(text, rng):
    candidates = [p for p, cmap in G["fonts"] if _covers(p, cmap, text)]
    if not candidates:
        return None
    size = rng.randint(20, 44)
    font = _font(rng.choice(candidates), size, rng)
    color, dark = pick_style(rng)
    stroke = rng.choice([0, 0, 0, 1, 2]) if dark else 0
    left, top, right, bottom = font.getbbox(text, stroke_width=stroke)
    if right - left < 2 or bottom - top < 2:
        return None
    pad_x = int(size * rng.uniform(0.1, 0.6))
    pad_y = int(size * rng.uniform(0.12, 0.45))
    width, height = right - left + 2 * pad_x, bottom - top + 2 * pad_y
    image = make_background(width, height, dark, rng)
    origin = (pad_x - left, pad_y - top)
    if dark and rng.random() < 0.3:
        shadow = Image.new("L", (width, height), 0)
        offset = rng.randint(1, 2)
        ImageDraw.Draw(shadow).text((origin[0] + offset, origin[1] + offset), text, font=font, fill=200)
        shadow = shadow.filter(ImageFilter.GaussianBlur(rng.uniform(0.5, 1.5)))
        image.paste((0, 0, 0), mask=shadow)
    ImageDraw.Draw(image).text(origin, text, font=font, fill=color, stroke_width=stroke,
                               stroke_fill=_jitter((20, 20, 25), rng, 10))
    # 模拟不同分辨率下的检测裁剪：按目标高度缩放
    target_h = rng.randint(16, 56)
    target_w = max(1, round(width * target_h / height))
    image = image.resize((target_w, target_h), rng.choice([Image.BILINEAR, Image.BICUBIC, Image.LANCZOS, Image.BOX]))
    if rng.random() < 0.25:
        image = image.filter(ImageFilter.GaussianBlur(rng.uniform(0.3, 0.9)))
    return image


def render_one(job):
    split, k, seed, out_dir = job
    rng = random.Random(f"{seed}:{split}:{k}")
    for _ in range(20):
        text = sample_text(rng)
        if not text or len(text) > MAX_LEN:
            continue
        image = render(text, rng)
        if image is None:
            continue
        name = f"images/{split}_{k:07d}.jpg"
        image.save(os.path.join(out_dir, name), quality=rng.randint(70, 95))
        return split, k, name, text
    return split, k, None, None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--textmap", required=True)
    parser.add_argument("--dict", required=True)
    parser.add_argument("--fonts", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--backgrounds", default=None)
    parser.add_argument("--train", type=int, default=300000)
    parser.add_argument("--val", type=int, default=5000)
    parser.add_argument("--workers", type=int, default=os.cpu_count() or 1)
    parser.add_argument("--seed", type=int, default=0)
    args = parser.parse_args()

    os.makedirs(os.path.join(args.out, "images"), exist_ok=True)
    jobs = [("val", k, args.seed, args.out) for k in range(args.val)] + \
           [("train", k, args.seed, args.out) for k in range(args.train)]
    results = {"train": [], "val": []}
    init_args = (args.textmap, args.dict, args.fonts, args.backgrounds)
    with mp.Pool(args.workers, initializer=_init, initargs=init_args) as pool:
        for i, (split, k, name, text) in enumerate(pool.imap_unordered(render_one, jobs, chunksize=256), 1):
            if name:
                results[split].append((k, name, text))
            if i % 20000 == 0:
                print(f"{i}/{len(jobs)}", flush=True)
    for split, items in results.items():
        items.sort()
        with open(os.path.join(args.out, f"{split}.txt"), "w", encoding="utf-8", newline="\n") as f:
            for _, name, text in items:
                f.write(f"{name}\t{text}\n")
        print(f"{split}: {len(items)}")


if __name__ == "__main__":
    main()
