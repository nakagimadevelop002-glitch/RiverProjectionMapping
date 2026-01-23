# =====================================
# Unity連携用 川の速度計測スクリプト
# 変換元: D:\imamura\work\SurfaceVelocimetry\notebooks\AdaptiveSTIV.ipynb
# 方式: YOLO + STIV法（Space-Time Image Velocimetry）
# =====================================

import sys
import cv2
import numpy as np
from scipy.ndimage import gaussian_filter
from ultralytics import YOLO

# =====================================
# 関数定義
# =====================================

def calculate_focal_length_factor(known_width_m, known_dist_m, pixel_width_px):
    """
    事前キャリブレーションにより焦点距離係数 F を算出します。
    F = (P * D) / W
    """
    focal_factor = (pixel_width_px * known_dist_m) / known_width_m
    return focal_factor


def estimate_resolution_from_yolo(frame, model, target_class_id, real_width_m, focal_f):
    """
    フレーム内の対象物をYOLOで検出し、そのBBox幅から
    「距離」と「空間分解能(m/px)」を推定します。
    """
    # YOLO推論 (verbose=Falseでログ抑制)
    results = model(frame, verbose=False)

    detected_width_px = None

    # 検出ループ
    for r in results:
        for box in r.boxes:
            cls = int(box.cls[0])
            # 指定したクラス（例: car=2, person=0）か確認
            if cls == target_class_id:
                x1, y1, x2, y2 = box.xyxy[0].cpu().numpy()
                w_px = x2 - x1

                # 最も信頼度が高い、または大きく映っているものを採用するロジック
                # ここでは最初に見つかったものを採用
                detected_width_px = w_px
                break
        if detected_width_px is not None:
            break

    if detected_width_px is None:
        return None, None

    # --- 【実測】距離と分解能の計算 ---
    # 距離 D = (W_real * F) / P
    distance_m = (real_width_m * focal_f) / detected_width_px

    # 空間分解能 Res = W_real / P  (または D / F)
    # これが「今の水面における 1pixelあたりのメートル数」になります
    spatial_res_m_px = real_width_m / detected_width_px

    return distance_m, spatial_res_m_px


# =====================================
# 設定パラメータ
# =====================================

# カメラキャリブレーション設定
P_known = 200.0   # B-Box幅 [pixel]
D_known = 10.0    # カメラからの距離 [m]
W_real = 1.8      # 対象物の実寸幅 [m] (例: 車の全幅)

# STIV解析用パラメータ
fps = 30.0
target_class = 2      # 2: Car, 0: Person など
target_width = 1.8    # 対象の実寸 [m]

# 計測ライン（画像中央を横切るラインと仮定）
line_y = 360  # Y座標
line_x_start, line_x_end = 100, 1100

input_source = 1   # カメラID（0, 1, 2...）

# STIV解析用フレーム数
max_frames = 150

# 初期スケール (検出されるまでのデフォルト値)
current_spatial_res = 0.01 # 仮置き: 1cm/px

# 構造テンソル法パラメータ
sigma_tensor = 2.0


# =====================================
# メイン処理
# =====================================

def main():
    """
    Unity連携用メイン処理
    継続的にカメラから速度を計測し、標準出力にprint()で出力
    """
    global current_spatial_res

    # 焦点距離係数 F の算出
    F_factor = calculate_focal_length_factor(W_real, D_known, P_known)

    # YOLOモデル読み込み
    model = YOLO("yolov8n.pt")

    # カメラ初期化
    cap = cv2.VideoCapture(input_source)
    if not cap.isOpened():
        print("ERROR: Cannot open camera", file=sys.stderr)
        return

    print("READY", flush=True)  # Unity側に準備完了を通知

    # 無限ループで継続的に計測
    while True:
        sti_list = []
        frame_count = 0

        # max_frames分のフレームを収集してSTIV解析
        while frame_count < max_frames:
            ret, frame = cap.read()
            if not ret:
                print("ERROR: Cannot read frame", file=sys.stderr)
                break

            # 1. YOLOでスケール更新を試みる
            # (計算コスト削減のため、5フレームに1回だけYOLOを走らせる)
            if frame_count % 5 == 0:
                dist, res = estimate_resolution_from_yolo(
                    frame, model, target_class, target_width, F_factor
                )

                if res is not None:
                    current_spatial_res = res

            # 2. 指定ラインの画素抽出 (STIV)
            # グレースケール化
            gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

            # ライン上の画素を取得
            line_pixels = gray[line_y, line_x_start:line_x_end]
            sti_list.append(line_pixels)

            frame_count += 1

        if frame_count == 0:
            break

        sti_image = np.array(sti_list)

        # --- 結果解析 (構造テンソル法) ---
        img_smooth = gaussian_filter(sti_image.astype(np.float32), sigma=1.0)
        Ix = cv2.Sobel(img_smooth, cv2.CV_64F, 1, 0, ksize=3)
        It = cv2.Sobel(img_smooth, cv2.CV_64F, 0, 1, ksize=3)
        Ixx, Itt, Ixt = Ix**2, It**2, Ix*It

        Jxx = gaussian_filter(Ixx, sigma=sigma_tensor)
        Jxt = gaussian_filter(Ixt, sigma=sigma_tensor)

        # 平均流速計算
        valid_mask = Jxx > 0.1  # ゼロ除算防止
        v_pix = np.zeros_like(Jxx)
        v_pix[valid_mask] = - Jxt[valid_mask] / Jxx[valid_mask]

        # ピクセル速度の平均 [px/frame]
        avg_v_pix = np.mean(v_pix)

        # 物理速度への変換 [m/s]
        velocity_ms = avg_v_pix * (current_spatial_res * fps)

        # Unity側に速度を出力（標準出力に1行で出力）
        print(f"{velocity_ms:.4f}", flush=True)

    cap.release()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("STOPPED", file=sys.stderr)
    except Exception as e:
        print(f"ERROR: {e}", file=sys.stderr)
