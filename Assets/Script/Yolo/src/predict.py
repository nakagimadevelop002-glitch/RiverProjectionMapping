from ultralytics import YOLO
import os

def predict_image(image_path, model_path, save_dir="predictions"):
    """
    指定した画像ファイルを読み込み、YOLOモデルで予測を実行する関数
    """
    sub_name = "val"

    # 1. モデルのロード
    model = YOLO(model_path)

    # 2. 画像の存在確認
    if not os.path.exists(image_path):
        raise FileNotFoundError(f"画像が見つかりません: {image_path}")

    # 3. 保存先フォルダを作成
    os.makedirs(save_dir, exist_ok=True)

    # 4. 予測を実行
    print(f"Running prediction on: {image_path}")
    results = model.predict(
        source=image_path,
        save=True,                # 予測画像を保存
        save_txt=True,            # 結果をtxtで保存（ラベル・座標）
        project=save_dir,         # 保存先ディレクトリ
        name=sub_name,               # サブフォルダ名
        conf=0.25                 # 信頼度閾値（必要に応じて変更）
    )

    # 5. 結果の出力
    print("\n--- Prediction Results ---")
    for result in results:
        boxes = result.boxes
        for box in boxes:
            cls_id = int(box.cls[0])
            conf = float(box.conf[0])
            label = model.names[cls_id]
            print(f"Detected: {label} ({conf:.2f})")

    print(f"\n結果画像とラベルは以下に保存されました: {os.path.join(save_dir, sub_name)}")
    return results

if __name__ == "__main__":
    # 予測する画像ファイルのパスを指定
    test_image_path = "../sample/sample4.png"
    trained_model_path = "./trained_models/model.pt"

    # 実行
    predict_image(test_image_path, trained_model_path)