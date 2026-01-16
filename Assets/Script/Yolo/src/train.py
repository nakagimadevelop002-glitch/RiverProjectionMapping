from ultralytics import YOLO
import os

def main():
    # 1. モデルの読み込み（事前学習済みの YOLO11n）
    model = YOLO("yolo11n.pt")

    # 2. データセット設定ファイルを指定
    data_yaml = "additional_dataset.yaml"  # 同一名のdatasets がある

    # 3. 学習を実行
    print(f"Training started using dataset: {data_yaml}")
    train_results = model.train(
        data=data_yaml,  # データセット設定ファイルのパス
        epochs=100,       # 学習エポック数（必要に応じて変更）
        imgsz=640,       # 入力画像サイズ
        device=0,        # GPU:0を使用（CPUで動かす場合は 'cpu'）
        workers=0,       # 単プロセス指定
    )

    # 4. 結果を出力
    print("Training completed!")
    print(train_results)

    # 5. モデル評価を実行
    print("Evaluating model on validation set...")
    model.val()

    # 5. 学習済みモデルの保存
    save_dir = "trained_models"
    os.makedirs(save_dir, exist_ok=True)

    # best.pt（最良モデル）を読み込み、任意の場所に保存
    best_model_path = model.ckpt_path if hasattr(model, 'ckpt_path') else model.trainer.best
    model = YOLO(best_model_path)
    save_path = os.path.join(save_dir, "model.pt")
    model.save(save_path)


if __name__ == "__main__":
    main()