"""Mở rộng head của trọng số pretrained latin_PP-OCRv5_mobile_rec cho dict mới (dict cũ là tiền tố của dict mới).

Bố cục lớp (PaddleOCR, use_space_char: true):
  CTC : [blank] + dict + [space]                          -> len(dict) + 2
  NRTR: [blank, <unk>, <s>, </s>] + dict + [space] + [1]  -> len(dict) + 6 (NRTRHead dùng out_channels + 1)
Hàng của ký tự cũ được chép nguyên; ký tự mới khởi tạo từ chữ gốc gần nhất có trong dict cũ
(ậ <- â, ờ <- ơ, ạ <- a), ký tự không có chữ gốc (♪ █ ...) khởi tạo ngẫu nhiên nhỏ.
Chỉ cần numpy; file .pdparams là pickle {tên: ndarray} mà paddle.load đọc được.

Dùng:
  python expand_head.py --pretrained latin_PP-OCRv5_mobile_rec_pretrained.pdparams \
      --old-dict ppocrv5_latin_dict.txt --new-dict vi_dict.txt --out vi_PP-OCRv5_mobile_rec_init.pdparams
"""
import argparse
import collections
import pickle
import unicodedata

import numpy as np

HEADS = {
    # 参数名: (类别所在的轴, 字典前的类别数, 字典后的类别数)
    "head.ctc_head.fc.weight": (1, 1, 1),
    "head.ctc_head.fc.bias": (0, 1, 1),
    "head.gtc_head.embedding.embedding.weight": (0, 4, 2),
    "head.gtc_head.tgt_word_prj.weight": (1, 4, 2),
}

# 越南语声调符号；帽子、短音、角等字母本身的符号保留
TONE_MARKS = {"̀", "́", "̃", "̉", "̣"}


class SafeUnpickler(pickle.Unpickler):
    """Chỉ cho phép dựng lại mảng numpy và dict (file tải từ internet)."""

    def find_class(self, module, name):
        if (module, name) == ("collections", "OrderedDict"):
            return collections.OrderedDict
        if (module, name) in (("numpy", "ndarray"), ("numpy", "dtype")):
            return getattr(np, name)
        if module in ("numpy.core.multiarray", "numpy._core.multiarray") and name in ("_reconstruct", "scalar"):
            return getattr(np._core.multiarray, name)
        raise pickle.UnpicklingError(f"blocked {module}.{name}")


def read_dict(path):
    with open(path, encoding="utf-8") as f:
        return [line.rstrip("\r\n") for line in f if line.rstrip("\r\n") != ""]


def source_char(char, old_index):
    """Chữ gốc gần nhất có trong dict cũ: thử bỏ dấu thanh (giữ â ă ê ô ơ ư), rồi tới chữ cái gốc."""
    decomposed = unicodedata.normalize("NFD", char)
    without_tone = unicodedata.normalize("NFC", "".join(c for c in decomposed if c not in TONE_MARKS))
    for candidate in (without_tone, decomposed[0]):
        if candidate != char and candidate in old_index:
            return candidate
    return None


def expand(array, axis, prefix, suffix, old_len, sources, rng):
    assert array.shape[axis] == prefix + old_len + suffix, f"shape {array.shape} không khớp dict {old_len}"
    moved = np.moveaxis(array, axis, 0)
    head, body, tail = moved[:prefix], moved[prefix:prefix + old_len], moved[prefix + old_len:]
    scale = float(body.std()) * 0.1
    rows = [body[s].copy() if s is not None else rng.normal(0.0, scale, size=body.shape[1:]).astype(array.dtype)
            for s in sources]
    added = np.stack(rows) if rows else body[:0]
    result = np.concatenate([head, body, added, tail], axis=0)
    return np.ascontiguousarray(np.moveaxis(result, 0, axis))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--pretrained", required=True)
    parser.add_argument("--old-dict", required=True)
    parser.add_argument("--new-dict", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--seed", type=int, default=0)
    args = parser.parse_args()

    old_dict, new_dict = read_dict(args.old_dict), read_dict(args.new_dict)
    assert new_dict[:len(old_dict)] == old_dict, "dict cũ phải là tiền tố của dict mới (giữ nguyên thứ tự)"
    added = new_dict[len(old_dict):]
    old_index = {}
    for i, char in enumerate(old_dict):
        old_index.setdefault(char, i)
    sources = []
    for char in added:
        source = source_char(char, old_index)
        sources.append(old_index[source] if source else None)
    print(f"old={len(old_dict)} new={len(new_dict)} added={len(added)}")
    print(" ".join(f"{c}<-{old_dict[s] if s is not None else '?'}" for c, s in zip(added, sources)))

    with open(args.pretrained, "rb") as f:
        state = SafeUnpickler(f).load()

    rng = np.random.default_rng(args.seed)
    for name, (axis, prefix, suffix) in HEADS.items():
        old = state[name]
        new = expand(old, axis, prefix, suffix, len(old_dict), sources, rng)
        # 旧类别（前缀 + 旧字典 + 后缀）的参数必须原样保留
        old_moved, new_moved = np.moveaxis(old, axis, 0), np.moveaxis(new, axis, 0)
        assert np.array_equal(new_moved[:prefix + len(old_dict)], old_moved[:prefix + len(old_dict)])
        assert np.array_equal(new_moved[-suffix:], old_moved[-suffix:])
        state[name] = new
        print(f"{name}: {old.shape} -> {new.shape}")

    with open(args.out, "wb") as f:
        pickle.dump(state, f, protocol=4)
    print(f"-> {args.out}")


if __name__ == "__main__":
    main()
