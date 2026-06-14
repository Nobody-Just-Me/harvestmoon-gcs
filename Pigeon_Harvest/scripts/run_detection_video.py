#!/usr/bin/env python3
"""
UAV Video Detection with Bounding Boxes and Health Classification
Optimized for drone footage with grid-based detection
"""

import argparse
import cv2
import numpy as np
import time
from pathlib import Path
from ultralytics import YOLO


def create_grid_detections(frame, grid_rows=4, grid_cols=6, classifier=None):
    """
    Divide frame into grid and classify each cell
    Returns list of detections with bounding boxes
    """
    height, width = frame.shape[:2]
    cell_height = height // grid_rows
    cell_width = width // grid_cols
    
    detections = []
    
    for row in range(grid_rows):
        for col in range(grid_cols):
            # Calculate cell boundaries
            x1 = col * cell_width
            y1 = row * cell_height
            x2 = x1 + cell_width
            y2 = y1 + cell_height
            
            # Extract cell
            cell = frame[y1:y2, x1:x2]
            
            # Classify cell
            if classifier:
                results = classifier(cell, verbose=False)
                if results and len(results) > 0:
                    # Get top prediction
                    probs = results[0].probs
                    class_id = int(probs.top1)
                    confidence = float(probs.top1conf)
                    class_name = results[0].names[class_id]
                    
                    detections.append({
                        'bbox': (x1, y1, x2, y2),
                        'class': class_name,
                        'confidence': confidence,
                        'class_id': class_id
                    })
    
    return detections


def draw_detections(frame, detections, min_conf=0.3):
    """
    Draw bounding boxes and labels on frame
    """
    annotated = frame.copy()
    
    # Color mapping for classes
    colors = {
        'healthy_crop': (0, 255, 0),              # Green
        'stressed_crop': (0, 165, 255),           # Orange
        'disease_stress_vegetation': (0, 0, 255), # Red
        'drought_stress': (0, 255, 255),          # Yellow
        'bare_soil': (128, 128, 128)              # Gray
    }
    
    for det in detections:
        if det['confidence'] < min_conf:
            continue
        
        x1, y1, x2, y2 = det['bbox']
        class_name = det['class']
        confidence = det['confidence']
        
        # Get color
        color = colors.get(class_name, (255, 255, 255))
        
        # Draw rectangle
        cv2.rectangle(annotated, (x1, y1), (x2, y2), color, 2)
        
        # Create label
        label = f"{class_name} {confidence:.2f}"
        
        # Draw label background
        (label_w, label_h), baseline = cv2.getTextSize(
            label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1
        )
        cv2.rectangle(
            annotated,
            (x1, y1 - label_h - 10),
            (x1 + label_w + 10, y1),
            color,
            -1
        )
        
        # Draw label text
        cv2.putText(
            annotated,
            label,
            (x1 + 5, y1 - 5),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (255, 255, 255),
            1,
            cv2.LINE_AA
        )
    
    return annotated


def add_statistics_overlay(frame, detections):
    """
    Add statistics overlay showing class distribution
    """
    # Count classes
    class_counts = {}
    for det in detections:
        class_name = det['class']
        class_counts[class_name] = class_counts.get(class_name, 0) + 1
    
    # Create overlay
    overlay = frame.copy()
    height, width = frame.shape[:2]
    
    # Draw semi-transparent background
    cv2.rectangle(overlay, (10, 10), (300, 200), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.6, frame, 0.4, 0, frame)
    
    # Draw title
    cv2.putText(
        frame,
        "Health Distribution",
        (20, 35),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.7,
        (255, 255, 255),
        2,
        cv2.LINE_AA
    )
    
    # Draw class counts
    y_offset = 60
    colors = {
        'healthy_crop': (0, 255, 0),
        'stressed_crop': (0, 165, 255),
        'disease_stress_vegetation': (0, 0, 255),
        'drought_stress': (0, 255, 255),
        'bare_soil': (128, 128, 128)
    }
    
    for class_name, count in sorted(class_counts.items()):
        color = colors.get(class_name, (255, 255, 255))
        text = f"{class_name}: {count}"
        
        cv2.putText(
            frame,
            text,
            (20, y_offset),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            color,
            1,
            cv2.LINE_AA
        )
        y_offset += 25
    
    return frame


def process_video(
    video_path,
    model_path,
    output_path=None,
    show=True,
    grid_rows=4,
    grid_cols=6,
    min_conf=0.3,
    skip_frames=0
):
    """
    Process video with grid-based detection and classification
    """
    print("=" * 60)
    print("UAV CROP HEALTH DETECTION WITH BOUNDING BOXES")
    print("=" * 60)
    print(f"Video:      {video_path}")
    print(f"Model:      {model_path}")
    print(f"Grid:       {grid_rows}x{grid_cols}")
    print(f"Min Conf:   {min_conf}")
    print(f"Output:     {output_path}")
    print("=" * 60)
    
    # Load model
    print("\nLoading model...")
    classifier = YOLO(model_path)
    print("✓ Model loaded")
    
    # Open video
    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        print(f"Error: Cannot open video {video_path}")
        return
    
    # Get video properties
    fps = cap.get(cv2.CAP_PROP_FPS)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    
    print(f"\nVideo properties:")
    print(f"  Resolution: {width}x{height}")
    print(f"  FPS:        {fps:.2f}")
    print(f"  Frames:     {total_frames}")
    
    # Setup output video
    writer = None
    if output_path:
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        fourcc = cv2.VideoWriter_fourcc(*'mp4v')
        writer = cv2.VideoWriter(
            str(output_path),
            fourcc,
            fps,
            (width, height)
        )
    
    # Process video
    frame_idx = 0
    processed_frames = 0
    start_time = time.time()
    
    print("\nProcessing video... (Press 'q' to stop)")
    
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        
        frame_idx += 1
        
        # Skip frames if requested
        if skip_frames > 0 and (frame_idx % (skip_frames + 1)) != 0:
            continue
        
        # Detect and classify
        detections = create_grid_detections(
            frame,
            grid_rows=grid_rows,
            grid_cols=grid_cols,
            classifier=classifier
        )
        
        # Draw detections
        annotated = draw_detections(frame, detections, min_conf=min_conf)
        
        # Add statistics overlay
        annotated = add_statistics_overlay(annotated, detections)
        
        # Add frame info
        cv2.putText(
            annotated,
            f"Frame: {frame_idx}/{total_frames}",
            (width - 250, height - 20),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            (255, 255, 255),
            2,
            cv2.LINE_AA
        )
        
        # Save frame
        if writer:
            writer.write(annotated)
        
        # Show frame
        if show:
            cv2.imshow('UAV Crop Health Detection', annotated)
            key = cv2.waitKey(1) & 0xFF
            if key == ord('q') or key == 27:  # q or ESC
                print("\nStopped by user")
                break
        
        processed_frames += 1
        
        # Print progress
        if processed_frames % 30 == 0:
            elapsed = time.time() - start_time
            fps_actual = processed_frames / elapsed
            print(f"Processed {processed_frames} frames ({fps_actual:.1f} FPS)")
    
    # Cleanup
    cap.release()
    if writer:
        writer.release()
    if show:
        cv2.destroyAllWindows()
    
    # Final statistics
    elapsed = time.time() - start_time
    print("\n" + "=" * 60)
    print("PROCESSING COMPLETE")
    print("=" * 60)
    print(f"Frames processed: {processed_frames}/{total_frames}")
    print(f"Time elapsed:     {elapsed:.1f}s")
    print(f"Average FPS:      {processed_frames/elapsed:.1f}")
    if output_path:
        print(f"Output saved:     {output_path}")
    print("=" * 60)


def main():
    parser = argparse.ArgumentParser(
        description="UAV video detection with bounding boxes"
    )
    parser.add_argument(
        "video",
        type=str,
        help="Input video path"
    )
    parser.add_argument(
        "--model",
        type=str,
        default="runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/weights/best.pt",
        help="Model path"
    )
    parser.add_argument(
        "--output",
        type=str,
        default=None,
        help="Output video path"
    )
    parser.add_argument(
        "--show",
        action="store_true",
        help="Show preview window"
    )
    parser.add_argument(
        "--grid-rows",
        type=int,
        default=4,
        help="Number of grid rows (default: 4)"
    )
    parser.add_argument(
        "--grid-cols",
        type=int,
        default=6,
        help="Number of grid columns (default: 6)"
    )
    parser.add_argument(
        "--min-conf",
        type=float,
        default=0.3,
        help="Minimum confidence threshold (default: 0.3)"
    )
    parser.add_argument(
        "--skip-frames",
        type=int,
        default=0,
        help="Skip N frames between processing (default: 0)"
    )
    
    args = parser.parse_args()
    
    # Auto-generate output path if not specified
    if args.output is None and not args.show:
        args.output = "runs/detection_output/" + Path(args.video).stem + "_detected.mp4"
    
    process_video(
        video_path=args.video,
        model_path=args.model,
        output_path=args.output,
        show=args.show,
        grid_rows=args.grid_rows,
        grid_cols=args.grid_cols,
        min_conf=args.min_conf,
        skip_frames=args.skip_frames
    )


if __name__ == "__main__":
    main()
