"""Đọc và làm sạch TextMap của game thành các dòng chữ hiển thị trên màn hình (bản PC)."""
import json
import re
import unicodedata

_TAG = re.compile(r"<[^<>]{1,80}>")
_GENDER_FEMALE = re.compile(r"\{F#[^{}]*\}")
_GENDER_MALE = re.compile(r"\{M#([^{}]*)\}")
_LAYOUT_PC = re.compile(r"\{LAYOUT_PC#([^{}]*)\}")
_PLACEHOLDER = re.compile(r"\{[^{}]{1,80}\}")
_NEWLINE = re.compile(r"\\n|\n|\r")
_SPACES = re.compile(r"\s+")


def clean(text: str) -> list[str]:
    """Trả về các dòng hiển thị: giữ dạng nam của {M#..}{F#..} và bản PC của {LAYOUT_*#..},
    bỏ thẻ màu và placeholder (tên người chơi...), tách theo xuống dòng, chuẩn hóa NFC."""
    if text.startswith("#"):
        text = text[1:]
        text = _GENDER_FEMALE.sub("", text)
        text = _GENDER_MALE.sub(r"\1", text)
    text = _LAYOUT_PC.sub(r"\1", text)
    text = _TAG.sub("", text)
    text = _PLACEHOLDER.sub("", text)
    lines = []
    for part in _NEWLINE.split(text):
        part = _SPACES.sub(" ", unicodedata.normalize("NFC", part)).strip()
        if part:
            lines.append(part)
    return lines


def load_lines(path: str) -> list[str]:
    """Đọc TextMap*.json ({hash: chuỗi}) và trả về mọi dòng đã làm sạch (có thể trùng)."""
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    lines = []
    for value in data.values():
        if isinstance(value, str):
            lines.extend(clean(value))
    return lines
