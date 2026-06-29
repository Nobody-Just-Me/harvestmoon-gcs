#!/usr/bin/env python3
"""
Integration Guide for MoonHarvest Recommendation Engine

This module provides integration helpers and examples for connecting
the recommendation engine with existing MoonHarvest detection pipelines.
"""

import json
from pathlib import Path
from typing import Dict, Any, Optional

try:
    from .recommendation_engine import RecommendationEngine
except ImportError:
    import recommendation_engine
    RecommendationEngine = recommendation_engine.RecommendationEngine


class MoonHarvestRecommendationIntegration:
    """
    Helper class for integrating recommendations with MoonHarvest detection
    """
    
    def __init__(self, output_dir: str = "recommendations"):
        self.engine = RecommendationEngine()
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
    
    def process_detection_results(
        self,
        detection_results: Dict[str, Any],
        field_config: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        Process detection results and generate recommendations
        
        Args:
            detection_results: Output from moonharvest_detect.py or similar
                Expected format: {
                    "field_health": float (0-100),
                    "class_pct": {
                        "healthy_crop": float,
                        "stressed_crop": float,
                        "drought_stress": float,
                        "bare_soil": float
                    },
                    # Optional:
                    "frames_sampled": int,
                    "duration_s": float,
                    ...
                }
            field_config: Optional field configuration
                {
                    "field_area_ha": float,
                    "days_after_transplant": int,
                    "available_resources": {...},
                    "field_name": str
                }
        
        Returns:
            Comprehensive recommendation report
        """
        if field_config is None:
            field_config = {}
        
        # Extract class percentages
        class_pct = detection_results.get("class_pct", {})
        
        # Map to standard names if needed
        class_mapping = {
            "lush_green": "healthy_crop",
            "inconsistent_growth": "stressed_crop",
            "drought_severe_stress": "drought_stress",
            "bare_soil_gap": "bare_soil"
        }
        
        normalized_pct = {}
        for key, value in class_pct.items():
            standard_key = class_mapping.get(key, key)
            normalized_pct[standard_key] = value
        
        # Get FHI
        fhi = detection_results.get("field_health", detection_results.get("avg_field_health", 50.0))
        
        # Generate recommendations
        report = self.engine.analyze_field(
            class_percentages=normalized_pct,
            fhi=fhi,
            field_area_ha=field_config.get("field_area_ha", 1.0),
            days_after_transplant=field_config.get("days_after_transplant"),
            available_resources=field_config.get("available_resources")
        )
        
        # Add detection metadata
        report["detection_metadata"] = {
            "frames_sampled": detection_results.get("frames_sampled"),
            "duration_s": detection_results.get("duration_s"),
            "detection_method": detection_results.get("detection_method", "HSV+YOLO Fusion"),
            "field_name": field_config.get("field_name", "Unknown")
        }
        
        return report
    
    def save_recommendations(
        self,
        report: Dict[str, Any],
        base_name: str = "recommendations"
    ) -> Dict[str, str]:
        """
        Save recommendations in multiple formats
        
        Returns:
            Dict with paths to saved files
        """
        json_path = self.output_dir / f"{base_name}.json"
        text_path = self.output_dir / f"{base_name}.txt"
        summary_path = self.output_dir / f"{base_name}_summary.txt"
        
        # Save JSON (machine-readable)
        self.engine.export_to_json(report, str(json_path))
        
        # Save full text report (human-readable)
        self.engine.export_to_text(report, str(text_path))
        
        # Save quick summary
        with open(summary_path, 'w', encoding='utf-8') as f:
            f.write(self.engine.generate_quick_summary(report))
            f.write("\n\n")
            f.write("See full report: " + str(text_path) + "\n")
        
        return {
            "json": str(json_path),
            "text": str(text_path),
            "summary": str(summary_path)
        }
    
    def integrate_with_video_output(
        self,
        summary_json_path: str,
        field_config: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        Load detection summary JSON and generate recommendations
        
        Args:
            summary_json_path: Path to JSON output from video processing
            field_config: Optional field configuration
        
        Returns:
            Recommendation report
        """
        with open(summary_json_path, 'r', encoding='utf-8') as f:
            detection_results = json.load(f)
        
        report = self.process_detection_results(detection_results, field_config)
        
        # Save alongside video output
        output_base = Path(summary_json_path).stem
        saved_paths = self.save_recommendations(report, output_base + "_recommendations")
        
        print(f"Recommendations generated:")
        print(f"  JSON: {saved_paths['json']}")
        print(f"  Text: {saved_paths['text']}")
        print(f"  Summary: {saved_paths['summary']}")
        
        return report


# ============================================================================
# Integration Examples for Existing Scripts
# ============================================================================

def integrate_moonharvest_detect_py():
    """
    Example integration with moonharvest_detect.py
    
    Add this code at the end of the video processing function:
    """
    code = '''
# At the end of cmd_video() or cmd_hsv() in moonharvest_detect.py

from recommendations.integration_guide import MoonHarvestRecommendationIntegration

# After generating summary JSON
integration = MoonHarvestRecommendationIntegration(output_dir="output/recommendations")

field_config = {
    "field_area_ha": 2.5,  # Set actual field area
    "days_after_transplant": 40,  # Set actual DAT if known
    "field_name": "Field-A"
}

report = integration.integrate_with_video_output(
    summary_json_path="output/summary.json",
    field_config=field_config
)

# Print quick summary to console
print("\\n" + "="*80)
print("AGRONOMIC RECOMMENDATIONS")
print("="*80)
print(integration.engine.generate_quick_summary(report))
print("\\nFull report saved to: output/recommendations/")
'''
    print(code)


def integrate_moonharvest_v2_4class_py():
    """
    Example integration with moonharvest_v2_4class.py
    
    Add this code after summary generation:
    """
    code = '''
# At the end of main() in moonharvest_v2_4class.py, after saving summary JSON

from recommendations import RecommendationEngine

engine = RecommendationEngine()

# Use the summary data
report = engine.analyze_field(
    class_percentages=summary["avg_class_pct"],
    fhi=summary["avg_field_health"],
    field_area_ha=2.0  # Configure as needed
)

# Save recommendations
rec_base = Path(args.output) / "recommendations"
rec_base.mkdir(exist_ok=True)
engine.export_to_json(report, str(rec_base / "recommendations.json"))
engine.export_to_text(report, str(rec_base / "recommendations.txt"))

# Print summary
print("\\n" + "="*80)
print("AGRONOMIC RECOMMENDATIONS")
print("="*80)
print(engine.generate_quick_summary(report))
print(f"\\nFull recommendations: {rec_base / 'recommendations.txt'}")
'''
    print(code)


def integrate_run_detection_video_py():
    """
    Example integration with run_detection_video.py
    """
    code = '''
# At the end of main() in run_detection_video.py

from recommendations import RecommendationEngine

if timeline:  # If we have detection results
    engine = RecommendationEngine()
    
    # Calculate average percentages from timeline
    n = len(timeline)
    avg_pct = {}
    for class_name in ["healthy_crop", "stressed_crop", "drought_stress", "bare_soil"]:
        # Adapt based on actual class names in timeline
        avg_pct[class_name] = sum(r.get(class_name, 0) for r in timeline) / n
    
    # Calculate average FHI
    avg_fhi = sum(r.get("fhi", 50) for r in timeline) / n
    
    report = engine.analyze_field(
        class_percentages=avg_pct,
        fhi=avg_fhi,
        field_area_ha=1.5
    )
    
    # Save recommendations
    rec_path = Path(output_video).parent / "recommendations.txt"
    engine.export_to_text(report, str(rec_path))
    print(f"\\nRecommendations saved: {rec_path}")
'''
    print(code)


def integrate_with_gcs_realtime():
    """
    Example integration with GCS real-time monitoring
    """
    code = '''
# For real-time GCS integration, add periodic recommendation updates

from recommendations import RecommendationEngine
import threading
import time

class RealtimeRecommendationUpdater:
    def __init__(self, update_interval_seconds=60):
        self.engine = RecommendationEngine()
        self.update_interval = update_interval_seconds
        self.latest_report = None
        self.running = False
        self.thread = None
    
    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._update_loop, daemon=True)
        self.thread.start()
    
    def stop(self):
        self.running = False
        if self.thread:
            self.thread.join()
    
    def _update_loop(self):
        while self.running:
            try:
                # Get latest detection statistics from GCS
                class_pct = self.get_latest_class_distribution()
                fhi = self.calculate_current_fhi(class_pct)
                
                # Generate recommendations
                self.latest_report = self.engine.analyze_field(
                    class_percentages=class_pct,
                    fhi=fhi,
                    field_area_ha=2.0
                )
                
                # Update UI or send notification
                self.update_recommendation_display()
                
            except Exception as e:
                print(f"Recommendation update error: {e}")
            
            time.sleep(self.update_interval)
    
    def get_latest_class_distribution(self):
        # Implement: Get from GCS telemetry or accumulated stats
        pass
    
    def calculate_current_fhi(self, class_pct):
        # Implement: Calculate FHI from class percentages
        pass
    
    def update_recommendation_display(self):
        # Implement: Update GCS UI with recommendations
        if self.latest_report:
            summary = self.engine.generate_quick_summary(self.latest_report)
            print(f"[Recommendations] {summary}")

# Usage in GCS main loop
rec_updater = RealtimeRecommendationUpdater(update_interval_seconds=120)
rec_updater.start()
'''
    print(code)


# ============================================================================
# CLI Tool for Standalone Usage
# ============================================================================

def cli_generate_recommendations():
    """
    Command-line interface for generating recommendations from detection JSON
    
    Usage:
        python integration_guide.py recommendations.json --field-area 2.5 --dat 45
    """
    import argparse
    
    parser = argparse.ArgumentParser(
        description="Generate agronomic recommendations from MoonHarvest detection results"
    )
    parser.add_argument(
        "detection_json",
        type=str,
        help="Path to detection results JSON file"
    )
    parser.add_argument(
        "--field-area",
        type=float,
        default=1.0,
        help="Field area in hectares (default: 1.0)"
    )
    parser.add_argument(
        "--dat",
        type=int,
        default=None,
        help="Days after transplant (optional)"
    )
    parser.add_argument(
        "--field-name",
        type=str,
        default="Unknown",
        help="Field identifier (optional)"
    )
    parser.add_argument(
        "--output-dir",
        type=str,
        default="recommendations",
        help="Output directory for recommendations (default: recommendations/)"
    )
    parser.add_argument(
        "--no-irrigation",
        action="store_true",
        help="Irrigation not available"
    )
    parser.add_argument(
        "--no-fertilizer",
        action="store_true",
        help="Fertilizer not available"
    )
    parser.add_argument(
        "--no-pesticides",
        action="store_true",
        help="Pesticides not available"
    )
    
    args = parser.parse_args()
    
    # Load detection results
    with open(args.detection_json, 'r', encoding='utf-8') as f:
        detection_results = json.load(f)
    
    # Configure field
    field_config = {
        "field_area_ha": args.field_area,
        "field_name": args.field_name
    }
    
    if args.dat is not None:
        field_config["days_after_transplant"] = args.dat
    
    # Configure resources
    if any([args.no_irrigation, args.no_fertilizer, args.no_pesticides]):
        field_config["available_resources"] = {
            "irrigation": not args.no_irrigation,
            "fertilizer": not args.no_fertilizer,
            "pesticides": not args.no_pesticides,
            "labor": True
        }
    
    # Generate recommendations
    integration = MoonHarvestRecommendationIntegration(output_dir=args.output_dir)
    report = integration.process_detection_results(detection_results, field_config)
    
    # Save outputs
    base_name = Path(args.detection_json).stem + "_recommendations"
    saved_paths = integration.save_recommendations(report, base_name)
    
    # Print summary
    print("\n" + "="*80)
    print("MOONHARVEST AGRONOMIC RECOMMENDATIONS")
    print("="*80)
    print(f"\nField: {field_config['field_name']}")
    print(f"Area: {field_config['field_area_ha']:.2f} ha")
    if args.dat:
        print(f"Days After Transplant: {args.dat}")
    print("\n" + "-"*80)
    print(integration.engine.generate_quick_summary(report))
    print("-"*80)
    
    # Show immediate actions if urgent
    if report['summary']['urgency_level'] in ['critical', 'high']:
        print("\n⚠️  URGENT ACTIONS REQUIRED:")
        timeline = report['action_timeline']
        for action in timeline['immediate'] + timeline['today']:
            print(f"  • {action['action']} (Priority {action['priority']})")
            print(f"    Deadline: {action['deadline']}")
    
    print(f"\nRecommendations saved:")
    print(f"  • JSON: {saved_paths['json']}")
    print(f"  • Text: {saved_paths['text']}")
    print(f"  • Summary: {saved_paths['summary']}")
    print("\n" + "="*80 + "\n")


if __name__ == "__main__":
    import sys
    
    if len(sys.argv) > 1:
        # CLI mode
        cli_generate_recommendations()
    else:
        # Show integration examples
        print("\n" + "="*80)
        print("MOONHARVEST RECOMMENDATION ENGINE - INTEGRATION GUIDE")
        print("="*80 + "\n")
        
        print("1. Integration with moonharvest_detect.py:")
        print("-" * 80)
        integrate_moonharvest_detect_py()
        
        print("\n2. Integration with moonharvest_v2_4class.py:")
        print("-" * 80)
        integrate_moonharvest_v2_4class_py()
        
        print("\n3. Integration with run_detection_video.py:")
        print("-" * 80)
        integrate_run_detection_video_py()
        
        print("\n4. Real-time GCS Integration:")
        print("-" * 80)
        integrate_with_gcs_realtime()
        
        print("\n" + "="*80)
        print("\nCLI Usage:")
        print("  python integration_guide.py <detection.json> --field-area 2.5 --dat 45")
        print("\nFor more examples, see: example_usage.py")
        print("="*80 + "\n")
