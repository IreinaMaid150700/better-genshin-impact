# Fine-tune OCR tiếng Việt (PP-OCRv5 latin rec) cho BetterGI

Chỉ dùng trong fork. Train trên Kaggle (GPU T4), đánh giá ở máy bằng `BetterGenshinImpact.OcrDataTool`.

| File | Vai trò |
|---|---|
| `vi_dict.txt` | Dict 930 ký tự = 836 dòng dict latin chính thức (giữ nguyên thứ tự) + 90 chữ Việt + `♪ █ ■ ♫` |
| `build_vi_dict.py` | Sinh lại `vi_dict.txt` từ dict gốc + TextMapVI.json (in báo cáo độ phủ) |
| `expand_head.py` | Mở rộng head CTC (838→932) và NRTR (842→936) của trọng số pretrained, chép nguyên ký tự cũ, chữ mới khởi tạo từ chữ gốc (`ậ←â`) |
| `textmap.py` | Làm sạch TextMap (bỏ thẻ màu, placeholder, chọn bản PC/nam) |
| `gen_synth.py` | Sinh ảnh dòng chữ tổng hợp ≤ 25 ký tự (cân bằng chữ hiếm, số kiểu UI, chữ đang hiện dần) |
| `vi_PP-OCRv5_mobile_rec.yml` | Config fine-tune (đường dẫn `/kaggle/working`) |
| `kaggle_train.ipynb` | Notebook chạy toàn bộ: cài đặt → tải dữ liệu → mở rộng head → sinh dữ liệu → train → export → ONNX → kiểm tra |

## Quy trình

1. Push branch `feat/vi-ocr-dataset-tool` lên fork (notebook clone thư mục này từ branch đó).
   Không muốn push: nén thư mục `training/` thành Kaggle Dataset tên `vi-ocr-kit`, gắn vào notebook.
2. Kaggle → New Notebook → File → Import notebook → `kaggle_train.ipynb`.
   Settings: Accelerator **GPU T4**, Internet **On** (cần xác minh số điện thoại).
3. Lượt 1: giữ `SMOKE_TEST = True` (cell đầu), bấm **Run All** để chạy thử ~15 phút, bắt lỗi môi trường.
   Lượt 2: đổi `SMOKE_TEST = False` → **Save Version → Save & Run All (Commit)**.
   Xong thì tải `vi_rec_result.zip` ở tab Output.
4. Giải nén, ví dụ vào `E:\ocr\models\`, rồi so sánh trên bộ eval thật:
   ```powershell
   E:\ocr\tool\BetterGenshinImpact.OcrDataTool.exe eval --labels E:\ocr\eval_v1\labels.tsv `
     --model E:\ocr\models\vi_PP-OCRv5_mobile_rec_infer `
     --model E:\ocr\models\latin_PP-OCRv5_mobile_rec_infer --model V5Latin
   ```
5. Đạt mục tiêu (CER < 2%, khớp > 90%) thì tích hợp vào BetterGI (bước 5 của kế hoạch).

`E:\ocr\eval_v1` chỉ dùng để đo, không đưa vào dữ liệu train.
