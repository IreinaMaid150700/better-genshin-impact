using System.Text;
using Newtonsoft.Json;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// 生成离线校对页：逐行显示裁剪图与可编辑的标注，导出为 PaddleOCR 识别训练格式（相对路径\t标注）
/// </summary>
internal static class ReviewPage
{
    public sealed record Entry(string Path, string Text);

    public static void Write(string path, string modelName, IReadOnlyList<Entry> entries)
    {
        // EscapeHtml 避免识别文本中的 </script> 等破坏页面
        var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml };
        var data = JsonConvert.SerializeObject(entries.Select(entry => new { p = entry.Path, t = entry.Text }), settings);
        var html = Template
            .Replace("__MODEL__", JsonConvert.SerializeObject(modelName, settings))
            .Replace("__DATA__", data);
        File.WriteAllText(path, html, new UTF8Encoding(false));
    }

    private const string Template = """
        <!doctype html>
        <html lang="vi">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Duyệt nhãn OCR</title>
        <style>
        :root { color-scheme: dark; }
        body { margin: 0; background: #1b1b1b; color: #ddd; font-family: "Segoe UI", Arial, sans-serif; }
        header { position: sticky; top: 0; z-index: 1; display: flex; flex-wrap: wrap; gap: 12px; align-items: center;
                 padding: 8px 16px; background: #2b2b2b; border-bottom: 1px solid #444; }
        header small { color: #aaa; }
        .row { display: grid; grid-template-columns: 56px minmax(160px, 1fr) minmax(220px, 1fr) 64px; gap: 12px;
               align-items: center; padding: 6px 16px; border-bottom: 1px solid #333; }
        .row img { max-width: 100%; max-height: 72px; background: #000; }
        .row input[type=text] { width: 100%; box-sizing: border-box; padding: 4px 8px; font-size: 20px;
                                color: #fff; background: #111; border: 1px solid #555; }
        .row.empty input[type=text] { border-color: #d08a00; }
        .row.excluded { opacity: .35; }
        .num { color: #888; text-align: right; }
        button { padding: 6px 14px; font-size: 14px; }
        </style>
        </head>
        <body>
        <header>
          <button id="download">Tải labels.tsv</button>
          <span id="stat"></span>
          <small>Nhãn sơ bộ từ model <b id="model"></b>. Gõ đúng chữ trong ảnh (kể cả dấu). Enter: sang dòng sau.
            Bỏ chọn "Dùng" với ảnh không phải chữ / bị cắt hỏng. Bản sửa tự lưu trong trình duyệt.</small>
        </header>
        <main id="list"></main>
        <script>
        const MODEL = __MODEL__;
        const DATA = __DATA__;
        const KEY = 'ocr-review:' + location.pathname;
        let saved = {};
        try { saved = JSON.parse(localStorage.getItem(KEY) || '{}'); } catch (e) { saved = {}; }
        document.getElementById('model').textContent = MODEL;

        const list = document.getElementById('list');
        const rows = DATA.map((item, index) => {
          const state = saved[item.p] || { t: item.t, use: true };
          const row = document.createElement('div');
          row.className = 'row';
          const num = document.createElement('div');
          num.className = 'num';
          num.textContent = index + 1;
          const img = document.createElement('img');
          img.src = item.p;
          img.loading = 'lazy';
          img.alt = item.p;
          const input = document.createElement('input');
          input.type = 'text';
          input.value = state.t;
          input.spellcheck = false;
          const label = document.createElement('label');
          const use = document.createElement('input');
          use.type = 'checkbox';
          use.checked = state.use;
          label.append(use, ' Dùng');
          row.append(num, img, input, label);
          list.append(row);
          const entry = { item, input, use, row };
          input.addEventListener('input', () => update(entry));
          use.addEventListener('change', () => update(entry));
          input.addEventListener('keydown', event => {
            if (event.key === 'Enter') {
              event.preventDefault();
              const next = rows[index + 1];
              if (next) { next.input.focus(); next.input.select(); }
            }
          });
          return entry;
        });

        function clean(text) {
          return text.normalize('NFC').replace(/\s+/g, ' ').trim();
        }

        function update(entry, persist = true) {
          entry.row.classList.toggle('empty', clean(entry.input.value) === '');
          entry.row.classList.toggle('excluded', !entry.use.checked);
          if (persist) {
            saved[entry.item.p] = { t: entry.input.value, use: entry.use.checked };
            try { localStorage.setItem(KEY, JSON.stringify(saved)); } catch (e) { /* 仅失去自动保存 */ }
          }
          stat();
        }

        function stat() {
          const used = rows.filter(r => r.use.checked);
          const empty = used.filter(r => clean(r.input.value) === '').length;
          document.getElementById('stat').textContent =
            `${used.length}/${rows.length} dòng được dùng, ${empty} dòng chưa có nhãn`;
        }

        document.getElementById('download').addEventListener('click', () => {
          const lines = rows
            .filter(r => r.use.checked && clean(r.input.value) !== '')
            .map(r => `${r.item.p}\t${clean(r.input.value)}`);
          const blob = new Blob([lines.join('\n') + '\n'], { type: 'text/tab-separated-values;charset=utf-8' });
          const link = document.createElement('a');
          link.href = URL.createObjectURL(blob);
          link.download = 'labels.tsv';
          link.click();
          URL.revokeObjectURL(link.href);
        });

        rows.forEach(r => update(r, false));
        </script>
        </body>
        </html>
        """;
}
