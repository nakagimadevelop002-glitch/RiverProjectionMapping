#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
baseline_stiv.py

Space-Time Image (STI) を用いた簡易流速推定（構造テンソルの全体平均版）。

元: STIV.ipynb の内容を、main() で実行できるスクリプトとして整理。

依存:
  - opencv-python (cv2)
  - numpy
  - scipy
  - matplotlib (可視化を行う場合)

使い方例:
  python baseline_stiv.py --video ../data/raw_videos/IMG_7158.mp4 --fps 30 --spatial-res 0.01 \\
      --line-start 100 240 --line-end 540 240 --max-frames 300 --show

  # 画像保存（GUIなし環境でもOK）
  python baseline_stiv.py --video input.mp4 --save-sti sti.png --save-overlay overlay.png
"""

from __future__ import annotations

import argparse
import os
import re
import json
from dataclasses import dataclass
from typing import Tuple, Optional, Union

import cv2
import numpy as np
from scipy.ndimage import gaussian_filter


def find_camera_id_by_name(camera_name: str) -> Optional[int]:
    """
    カメラ名からOpenCVのカメラIDを特定する。

    WMIでWindowsが認識しているカメラデバイスを取得し、
    カメラ名が一致するデバイスのインデックスをOpenCVで試行する。

    Returns:
        カメラID（0-9）、見つからない場合はNone
    """
    try:
        import wmi
        c = wmi.WMI()

        # Windowsが認識しているカメラデバイスを取得
        cameras = c.Win32_PnPEntity(PNPClass="Camera")

        camera_index = 0
        for cam in cameras:
            if camera_name in cam.Name:
                # OpenCVで該当IDが開けるか試行
                for test_id in range(10):
                    cap = cv2.VideoCapture(test_id)
                    if cap.isOpened():
                        cap.release()
                        # camera_indexと一致したら返す
                        if test_id == camera_index:
                            return test_id
                camera_index += 1

        return None
    except Exception as e:
        print(f"[ERROR] Failed to find camera by name: {e}")
        return None


@dataclass
class Config:
    video_source: Union[str, int]
    fps: float
    spatial_res: float  # [m/pixel]
    line_start: Tuple[int, int]
    line_end: Tuple[int, int]
    max_frames: int
    sigma_pre: float = 1.0
    sigma_tensor: float = 2.0
    show: bool = False
    save_sti: Optional[str] = None
    save_overlay: Optional[str] = None
    save_npy: Optional[str] = None
    save_json: Optional[str] = None
    output_dir: str = "../outputs/"
    gray: bool = True


def extract_line_pixels(frame: np.ndarray,
                        start: Tuple[int, int],
                        end: Tuple[int, int],
                        length: int,
                        *,
                        force_gray: bool = True) -> np.ndarray:
    """
    フレームから指定されたライン上の画素値を抽出します（線形補間）。
    返り値は 1D 配列（length 要素）。

    - frame: BGR or Gray
    - start/end: (x, y)
    - length: サンプル点数（ピクセル長の近似）
    """
    x0, y0 = start
    x1, y1 = end

    x_coords = np.linspace(x0, x1, length).astype(np.float32).reshape(1, -1)
    y_coords = np.linspace(y0, y1, length).astype(np.float32).reshape(1, -1)

    # cv2.remap は map_x, map_y を指定
    pixels = cv2.remap(frame, x_coords, y_coords, interpolation=cv2.INTER_LINEAR)

    if force_gray and pixels.ndim == 3:
        pixels = cv2.cvtColor(pixels, cv2.COLOR_BGR2GRAY)

    return pixels.flatten()


def generate_sti(cfg: Config) -> np.ndarray:
    """
    動画から STI を生成。
    Shape: (time, space) = (T, X)
    """
    # カメラソースが数値ならそのまま、文字列ならカメラ名からIDを検索
    if isinstance(cfg.video_source, int):
        cap = cv2.VideoCapture(cfg.video_source)
    else:
        # カメラ名からIDを特定
        camera_id = find_camera_id_by_name(cfg.video_source)
        if camera_id is None:
            raise IOError(f"Camera not found: {cfg.video_source}")
        print(f"[INFO] Camera '{cfg.video_source}' found at ID {camera_id}")
        cap = cv2.VideoCapture(camera_id)

    if not cap.isOpened():
        raise IOError(f"Cannot open video source: {cfg.video_source}")

    # カメラ情報を出力
    print("=== Camera Information ===")
    print(f"Camera ID: {cfg.video_source}")
    print(f"Resolution: {int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))}x{int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))}")
    print(f"FPS: {cap.get(cv2.CAP_PROP_FPS)}")
    print(f"Backend: {cap.getBackendName()}")
    print("=========================")

    # 線分長（ピクセル）を length として使う（ノートブック準拠）
    line_length_px = int(np.linalg.norm(np.array(cfg.line_start) - np.array(cfg.line_end)))
    line_length_px = max(line_length_px, 1)

    sti_list = []
    frame_count = 0

    while frame_count < cfg.max_frames:
        ret, frame = cap.read()
        if not ret:
            break

        line_data = extract_line_pixels(
            frame,
            cfg.line_start,
            cfg.line_end,
            line_length_px,
            force_gray=cfg.gray
        )
        sti_list.append(line_data)
        frame_count += 1

    cap.release()

    if not sti_list:
        raise RuntimeError("No frames were read from the video source. Check --video or camera availability.")

    sti_image = np.array(sti_list)
    return sti_image


def calculate_velocity_structure_tensor(sti_img: np.ndarray,
                                        fps: float,
                                        spat_res: float,
                                        *,
                                        sigma_pre: float = 1.0,
                                        sigma_tensor: float = 2.0) -> Tuple[float, float]:
    """
    構造テンソル（全体平均）により代表流速を推定。

    Returns:
      velocity_ms: [m/s]
      v_pix: [px/frame]
    """
    img_smooth = gaussian_filter(sti_img.astype(np.float32), sigma=sigma_pre)

    # Ix: 空間方向(横)の微分, It: 時間方向(縦)の微分
    Ix = cv2.Sobel(img_smooth, cv2.CV_64F, 1, 0, ksize=3)
    It = cv2.Sobel(img_smooth, cv2.CV_64F, 0, 1, ksize=3)

    Ixx = Ix ** 2
    Ixt = Ix * It

    Jxx = gaussian_filter(Ixx, sigma=sigma_tensor)
    Jxt = gaussian_filter(Ixt, sigma=sigma_tensor)

    avg_Jxx = float(np.mean(Jxx))
    avg_Jxt = float(np.mean(Jxt))

    v_pix = 0.0 if avg_Jxx == 0.0 else (-avg_Jxt / avg_Jxx)
    velocity_ms = v_pix * (spat_res * fps)

    return float(velocity_ms), float(v_pix)


def _safe_import_matplotlib():
    # GUIなし環境でも保存だけできるようにする
    import matplotlib
    if os.environ.get("DISPLAY", "") == "" and os.environ.get("MPLBACKEND", "") == "":
        matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    return plt


def plot_sti(sti_image: np.ndarray,
             *,
             title: str = "Generated Space-Time Image (STI)",
             save_path: Optional[str] = None,
             show: bool = False) -> None:
    plt = _safe_import_matplotlib()
    plt.figure(figsize=(10, 6))
    plt.imshow(sti_image, cmap="gray", aspect="auto")
    plt.title(title)
    plt.xlabel("Space (x) [pixels]")
    plt.ylabel("Time (t) [frames]")
    plt.colorbar(label="Intensity")
    if save_path:
        plt.tight_layout()
        plt.savefig(save_path, dpi=200)
    if show:
        plt.show()
    plt.close()


def plot_sti_with_vector(sti_image: np.ndarray,
                         v_px_frame: float,
                         estimated_velocity: float,
                         *,
                         save_path: Optional[str] = None,
                         show: bool = False) -> None:
    plt = _safe_import_matplotlib()
    plt.figure(figsize=(10, 6))
    plt.imshow(sti_image, cmap="gray", aspect="auto")

    h, w = sti_image.shape
    center_t, center_x = h // 2, w // 2

    t_vals = np.array([0, h], dtype=np.float64)
    x_vals = center_x + v_px_frame * (t_vals - center_t)

    plt.plot(
        x_vals, t_vals,
        color="red", linewidth=2, linestyle="--",
        label=f"Est. V = {estimated_velocity:.3f} m/s"
    )
    plt.xlim(0, w)
    plt.ylim(h, 0)  # 画像座標系に合わせてY軸（時間）を反転
    plt.title(f"STI with Estimated Flow Vector\nVelocity: {estimated_velocity:.3f} m/s")
    plt.xlabel("Space [px]")
    plt.ylabel("Time [frame]")
    plt.legend()

    if save_path:
        plt.tight_layout()
        plt.savefig(save_path, dpi=200)
    if show:
        plt.show()
    plt.close()


def build_argparser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description="Baseline STIV: build STI from a video and estimate surface velocity using a global structure-tensor method."
    )
    p.add_argument("--video", default="../data/raw_videos/IMG_7158.mp4",
                   help="Video file path or camera index (e.g., 0). Default matches the notebook.")
    p.add_argument("--fps", type=float, default=30.0, help="Video frame rate [fps].")
    p.add_argument("--spatial-res", type=float, default=0.01, help="Spatial resolution [m/pixel].")
    p.add_argument("--line-start", type=int, nargs=2, default=(100, 240), metavar=("X", "Y"),
                   help="Line start (x y) in pixels.")
    p.add_argument("--line-end", type=int, nargs=2, default=(540, 240), metavar=("X", "Y"),
                   help="Line end (x y) in pixels.")
    p.add_argument("--max-frames", type=int, default=300, help="Number of frames to use to build STI.")
    p.add_argument("--sigma-pre", type=float, default=1.0, help="Gaussian sigma for pre-smoothing STI.")
    p.add_argument("--sigma-tensor", type=float, default=2.0, help="Gaussian sigma for tensor smoothing.")
    p.add_argument("--no-gray", action="store_true", help="Do not force grayscale when sampling the line.")
    p.add_argument("--show", action="store_true", help="Show matplotlib windows (if environment supports it).")
    p.add_argument("--output-dir", default="../outputs/",
                   help="Output directory. If --save-* are not specified, files will be saved here with default names.")
    p.add_argument("--save-sti", default=None, help="Save STI image to this path (png, jpg, ...).")
    p.add_argument("--save-overlay", default=None, help="Save STI overlay (with estimated vector) to this path.")
    p.add_argument("--save-npy", default=None, help="Save STI array to this path (.npy).")
    p.add_argument("--save-json", default=None, help="Save numeric results to this path (.json).")
    return p


def _parse_video_source(video_arg: str) -> Union[str, int]:
    # "0" のように数字だけならカメラ index として扱う
    if re.fullmatch(r"\d+", video_arg.strip()):
        return int(video_arg.strip())
    return video_arg


def main() -> None:
    parser = build_argparser()
    args = parser.parse_args()

    # 出力先の既定挙動:
    #  - --save-* が未指定なら --output-dir 配下に既定名で保存する
    #  - 画像/結果を「保存しない」にしたい場合は明示的に --save-sti "" 等ではなく、
    #    ここでは常に保存する仕様とする（ベースライン用途）
    output_dir = args.output_dir
    os.makedirs(output_dir, exist_ok=True)

    save_sti = args.save_sti or os.path.join(output_dir, "sti.png")
    save_overlay = args.save_overlay or os.path.join(output_dir, "velocity_vector.png")
    save_npy = args.save_npy or os.path.join(output_dir, "sti.npy")
    save_json = args.save_json or os.path.join(output_dir, "result.json")

    cfg = Config(
        video_source=_parse_video_source(args.video),
        fps=args.fps,
        spatial_res=args.spatial_res,
        line_start=tuple(args.line_start),
        line_end=tuple(args.line_end),
        max_frames=args.max_frames,
        sigma_pre=args.sigma_pre,
        sigma_tensor=args.sigma_tensor,
        show=args.show,
        save_sti=save_sti,
        save_overlay=save_overlay,
        save_npy=save_npy,
        save_json=save_json,
        output_dir=output_dir,
        gray=not args.no_gray,
    )

    sti_image = generate_sti(cfg)

    # STI 配列保存
    if cfg.save_npy:
        os.makedirs(os.path.dirname(cfg.save_npy) or ".", exist_ok=True)
        np.save(cfg.save_npy, sti_image)

    estimated_velocity, v_px_frame = calculate_velocity_structure_tensor(
        sti_image, cfg.fps, cfg.spatial_res,
        sigma_pre=cfg.sigma_pre,
        sigma_tensor=cfg.sigma_tensor
    )

    print("--- Analysis Result ---")
    print(f"STI shape: {sti_image.shape} (time, space)")
    print(f"Estimated Velocity (Pixel domain): {v_px_frame:.6f} [px/frame]")
    print(f"Estimated Surface Velocity: {estimated_velocity:.6f} [m/s]")
    direction = "Downstream" if estimated_velocity > 0 else "Upstream (or Reverse)"
    print(f"Flow Direction: {direction}")

    # 数値結果保存
    if cfg.save_json:
        os.makedirs(os.path.dirname(cfg.save_json) or ".", exist_ok=True)
        payload = {
            "sti_shape": [int(sti_image.shape[0]), int(sti_image.shape[1])],
            "fps": float(cfg.fps),
            "spatial_res_m_per_px": float(cfg.spatial_res),
            "line_start": [int(cfg.line_start[0]), int(cfg.line_start[1])],
            "line_end": [int(cfg.line_end[0]), int(cfg.line_end[1])],
            "max_frames": int(cfg.max_frames),
            "sigma_pre": float(cfg.sigma_pre),
            "sigma_tensor": float(cfg.sigma_tensor),
            "v_px_per_frame": float(v_px_frame),
            "velocity_m_per_s": float(estimated_velocity),
            "flow_direction": direction,
        }
        with open(cfg.save_json, "w", encoding="utf-8") as f:
            json.dump(payload, f, ensure_ascii=False, indent=2)

    # 可視化（既定で保存）
    if cfg.save_sti or cfg.show:
        if cfg.save_sti:
            os.makedirs(os.path.dirname(cfg.save_sti) or ".", exist_ok=True)
        plot_sti(sti_image, save_path=cfg.save_sti, show=cfg.show)

    if cfg.save_overlay or cfg.show:
        if cfg.save_overlay:
            os.makedirs(os.path.dirname(cfg.save_overlay) or ".", exist_ok=True)
        plot_sti_with_vector(
            sti_image,
            v_px_frame,
            estimated_velocity,
            save_path=cfg.save_overlay,
            show=cfg.show,
        )


if __name__ == "__main__":
    main()
