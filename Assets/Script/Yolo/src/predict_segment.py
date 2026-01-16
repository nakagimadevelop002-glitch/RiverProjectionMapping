from ultralytics import YOLO
import os
import numpy as np
import cv2

def predict_image(image_path,
                  model_path,
                  conf=0.25,
                  save_dir="predictions",
                  line_thickness=2,
                  crop_to_bbox=False,   # Trueにすると輪郭画像をbboxで切り出して保存
                  ):
    """
    指定した画像ファイルを読み込み、YOLOモデルで予測を実行する関数
    """
    sub_name = "val"

    # 1. モデルのロード
    model = YOLO(model_path)

    # 2. 画像の存在確認
    if not os.path.exists(image_path):
        raise FileNotFoundError(f"画像が見つかりません: {image_path}")

    img_bgr = cv2.imread(image_path, cv2.IMREAD_COLOR)
    if img_bgr is None:
        raise ValueError(f"画像を読み込めませんでした: {image_path}")
    h, w = img_bgr.shape[:2]

    # 3. 保存先フォルダを作成
    os.makedirs(save_dir, exist_ok=True)

    # 4. 予測を実行
    print(f"Running segmentation prediction on: {image_path}")
    results = model.predict(
        source=image_path,
        save=True,                 # マスク付き画像を保存
        save_txt=True,             # polygon(txt)保存
        project=save_dir,          # 保存先ディレクトリ
        name=sub_name,             # サブフォルダ名
        conf=conf ,                # 信頼度閾値（必要に応じて変更）
        task="segment"             # ← 明示的に segmentation
    )

    # 4.1 輪郭抽出用ディレクトリの作成
    out_root = results[0].save_dir  #
    out_contours_dir = os.path.join(out_root, "contours")
    os.makedirs(out_contours_dir, exist_ok=True)

    # 5. 結果の出力
    print("\n--- Prediction Results ---")

    for result in results:
        boxes = result.boxes
        masks = result.masks  # segmentationの要

        if boxes is None:
            print("No objects detected.")
            continue

        for i, box in enumerate(boxes):
            cls_id = int(box.cls[0])
            score = float(box.conf[0])
            label = model.names[cls_id]

            # マスク情報
            if masks is not None:
                polygon = masks.xy[i]  # (N, 2)
                num_points = len(polygon)
            else:
                num_points = 0

            print(
                f"Detected: {label} | "
                f"Confidence: {score:.3f} | "
                f"Mask points: {num_points}"
            )

    print(f"\n結果は以下に保存されました:")
    print(os.path.join(save_dir, sub_name))


    # 6. 輪郭の抽出
    print("\n--- Exporting contour-only images ---")
    base = os.path.splitext(os.path.basename(image_path))[0]

    for r_idx, result in enumerate(results):
        boxes = result.boxes
        masks = result.masks

        if boxes is None or masks is None:
            print("No masks detected (model might not be a segmentation model).")
            continue

        # masks.xy は「画像座標(px)のポリゴン頂点列」(N,2) のリスト
        polys = masks.xy

        for i, box in enumerate(boxes):
            cls_id = int(box.cls[0])
            score = float(box.conf[0])
            label = model.names[cls_id]

            poly = polys[i]  # (num_points, 2) float (px)
            if poly is None or len(poly) < 3:
                continue

            # RGBA（透明背景）キャンバスを作る
            canvas = np.zeros((h, w, 4), dtype=np.uint8)

            # polyline描画用に int へ
            pts = np.round(poly).astype(np.int32).reshape((-1, 1, 2))

            # 輪郭線だけ描く（色は白・アルファ255）
            cv2.polylines(
                canvas,
                [pts],
                isClosed=True,
                color=(255, 255, 255, 255),  # RGBA
                thickness=line_thickness
            )

            # bboxで切り出す（任意）
            if crop_to_bbox:
                x1, y1, x2, y2 = box.xyxy[0].cpu().numpy()
                x1, y1, x2, y2 = map(int, [max(0, x1), max(0, y1), min(w, x2), min(h, y2)])
                canvas_to_save = canvas[y1:y2, x1:x2]
            else:
                canvas_to_save = canvas

            out_name = f"{base}_obj{i:03d}_{label}_{score:.3f}.png"
            out_path = os.path.join(out_contours_dir, out_name)
            cv2.imwrite(out_path, canvas_to_save)

            print(f"Saved contour: {out_path}")

    print(f"\n輪郭画像の保存先: {out_contours_dir}")

if __name__ == "__main__":
    # 予測する画像ファイルのパスを指定
    test_image_path = "../sample/sample5.png"
    trained_model_path = "./trained_models/model_seg.pt"

    # 実行
    predict_image(test_image_path, trained_model_path)