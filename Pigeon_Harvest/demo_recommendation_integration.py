#!/usr/bin/env python3
"""
Demo: MoonHarvest Recommendation Engine Integration
Shows practical integration with detection results
"""

import json
import sys
from pathlib import Path

# Add recommendations to path
sys.path.insert(0, str(Path(__file__).parent / "recommendations"))

try:
    from recommendations import RecommendationEngine
except ImportError:
    print("Installing recommendations package...")
    import recommendations.recommendation_engine as rec_engine
    RecommendationEngine = rec_engine.RecommendationEngine


def demo_with_config():
    """
    Demonstrate recommendation generation using FINAL_CONFIG.json settings
    """
    print("="*80)
    print("MOONHARVEST RECOMMENDATION ENGINE - PRACTICAL DEMO")
    print("="*80)
    print()
    
    # Load config
    config_path = Path(__file__).parent.parent / "FINAL_CONFIG.json"
    with open(config_path, 'r') as f:
        config = json.load(f)
    
    print("📋 Configuration loaded:")
    print(f"   Model: {config['yolo']['weights']}")
    print(f"   Mode: {config['demo']['mode']}")
    print(f"   Detection FPS: {config['detection']['fps']}")
    print()
    
    # Initialize recommendation engine
    engine = RecommendationEngine()
    print("✓ Recommendation engine initialized")
    print()
    
    # ========================================================================
    # SCENARIO 1: Good Field Health (Preventive Monitoring)
    # ========================================================================
    print("\n" + "="*80)
    print("SCENARIO 1: Good Field Health (Lush Green Dominant)")
    print("="*80)
    
    scenario1_data = {
        "healthy_crop": 72.5,
        "stressed_crop": 18.2,
        "drought_stress": 4.3,
        "bare_soil": 5.0
    }
    
    # Calculate FHI using severity from config
    severity = config["severity"]
    fhi1 = 100.0 - sum(severity[k] * scenario1_data[k] for k in scenario1_data.keys())
    
    print(f"\n📊 Detection Results:")
    for class_name, pct in scenario1_data.items():
        display = config["classes_display"][class_name]
        print(f"   {display:30s}: {pct:5.1f}%")
    print(f"\n   Field Health Index (FHI): {fhi1:.1f}/100")
    
    report1 = engine.analyze_field(
        class_percentages=scenario1_data,
        fhi=fhi1,
        field_area_ha=2.5,
        days_after_transplant=30
    )
    
    print(f"\n🎯 Analysis:")
    print(f"   Status: {report1['summary']['field_status']}")
    print(f"   Urgency: {report1['summary']['urgency_level'].upper()}")
    print(f"   Recommended Actions: {len(report1['recommended_actions'])}")
    
    print(f"\n💰 Cost Estimate:")
    print(f"   Total: ${report1['cost_estimate']['total_usd']:.2f}")
    print(f"   Per Hectare: ${report1['cost_estimate']['per_hectare']:.2f}/ha")
    
    print(f"\n📝 Quick Recommendation:")
    print(f"   {engine.generate_quick_summary(report1)}")
    
    # Top 2 actions
    print(f"\n✅ Top Priority Actions:")
    for i, action in enumerate(report1['recommended_actions'][:2], 1):
        print(f"   {i}. {action['action']}")
        print(f"      Priority: {action['priority']} | Deadline: {action.get('deadline', 'Ongoing')}")
    
    # ========================================================================
    # SCENARIO 2: Moderate Stress (Action Required)
    # ========================================================================
    print("\n\n" + "="*80)
    print("SCENARIO 2: Moderate Stress (Inconsistent Growth Visible)")
    print("="*80)
    
    scenario2_data = {
        "healthy_crop": 42.3,
        "stressed_crop": 38.5,
        "drought_stress": 12.2,
        "bare_soil": 7.0
    }
    
    fhi2 = 100.0 - sum(severity[k] * scenario2_data[k] for k in scenario2_data.keys())
    
    print(f"\n📊 Detection Results:")
    for class_name, pct in scenario2_data.items():
        display = config["classes_display"][class_name]
        print(f"   {display:30s}: {pct:5.1f}%")
    print(f"\n   Field Health Index (FHI): {fhi2:.1f}/100")
    
    report2 = engine.analyze_field(
        class_percentages=scenario2_data,
        fhi=fhi2,
        field_area_ha=2.5,
        days_after_transplant=45  # Reproductive stage - critical!
    )
    
    print(f"\n🎯 Analysis:")
    print(f"   Status: {report2['summary']['field_status']}")
    print(f"   Urgency: {report2['summary']['urgency_level'].upper()}")
    print(f"   Recommended Actions: {len(report2['recommended_actions'])}")
    
    # Show urgent actions
    timeline = report2['action_timeline']
    urgent_count = len(timeline['immediate']) + len(timeline['today'])
    
    if urgent_count > 0:
        print(f"\n⚠️  URGENT: {urgent_count} actions required within 24 hours!")
        print(f"\n🚨 Immediate Actions:")
        for action in timeline['immediate'] + timeline['today']:
            print(f"   • {action['action']}")
            print(f"     Deadline: {action['deadline']}")
            print(f"     {action['description']}")
    
    print(f"\n💰 Cost Estimate:")
    print(f"   Total: ${report2['cost_estimate']['total_usd']:.2f}")
    print(f"   Per Hectare: ${report2['cost_estimate']['per_hectare']:.2f}/ha")
    
    # Show growth stage warning
    if report2.get('growth_stage_guidance'):
        stage_info = report2['growth_stage_guidance']
        print(f"\n🌾 Growth Stage Alert:")
        print(f"   Stage: {stage_info.get('stage', 'Unknown')}")
        print(f"   Critical Note: Reproductive stage is most sensitive to stress")
        print(f"   Water stress during flowering highly detrimental to yield!")
    
    # ========================================================================
    # SCENARIO 3: Critical Drought (Emergency)
    # ========================================================================
    print("\n\n" + "="*80)
    print("SCENARIO 3: Critical Drought Stress (Emergency Intervention)")
    print("="*80)
    
    scenario3_data = {
        "healthy_crop": 18.2,
        "stressed_crop": 28.5,
        "drought_stress": 42.3,
        "bare_soil": 11.0
    }
    
    fhi3 = 100.0 - sum(severity[k] * scenario3_data[k] for k in scenario3_data.keys())
    
    print(f"\n📊 Detection Results:")
    for class_name, pct in scenario3_data.items():
        display = config["classes_display"][class_name]
        print(f"   {display:30s}: {pct:5.1f}%")
    print(f"\n   Field Health Index (FHI): {fhi3:.1f}/100")
    
    report3 = engine.analyze_field(
        class_percentages=scenario3_data,
        fhi=fhi3,
        field_area_ha=2.5,
        days_after_transplant=55
    )
    
    print(f"\n🎯 Analysis:")
    print(f"   Status: {report3['summary']['field_status']}")
    print(f"   Urgency: {report3['summary']['urgency_level'].upper()}")
    
    print(f"\n🚨🚨🚨 CRITICAL SITUATION 🚨🚨🚨")
    print(f"\n⚠️  Risk Factors:")
    for risk in report3['primary_diagnosis']['risk_factors']:
        print(f"   • {risk}")
    
    print(f"\n🆘 EMERGENCY ACTIONS (Immediate):")
    for i, action in enumerate(report3['recommended_actions'][:3], 1):
        print(f"\n   {i}. {action['action']} (Priority {action['priority']})")
        print(f"      Deadline: {action['deadline']}")
        print(f"      {action['description']}")
        if action.get('details'):
            print(f"      Steps:")
            for detail in action['details'][:3]:
                print(f"        - {detail}")
    
    print(f"\n💰 Cost Estimate:")
    print(f"   Total: ${report3['cost_estimate']['total_usd']:.2f}")
    print(f"   Per Hectare: ${report3['cost_estimate']['per_hectare']:.2f}/ha")
    print(f"   Note: Emergency intervention costs are high but necessary to prevent total loss")
    
    # ========================================================================
    # Export Example
    # ========================================================================
    print("\n\n" + "="*80)
    print("EXPORT DEMONSTRATION")
    print("="*80)
    
    output_dir = Path(__file__).parent / "demo_output"
    output_dir.mkdir(exist_ok=True)
    
    # Export scenario 2 (most practical - moderate stress)
    json_path = output_dir / "recommendations_scenario2.json"
    text_path = output_dir / "recommendations_scenario2.txt"
    
    engine.export_to_json(report2, str(json_path))
    engine.export_to_text(report2, str(text_path))
    
    print(f"\n✓ Full recommendations exported:")
    print(f"   JSON: {json_path}")
    print(f"   Text: {text_path}")
    
    # ========================================================================
    # Integration Code Example
    # ========================================================================
    print("\n\n" + "="*80)
    print("INTEGRATION CODE EXAMPLE")
    print("="*80)
    
    integration_code = '''
# Add to moonharvest_detect.py after generating summary

from recommendations import RecommendationEngine

engine = RecommendationEngine()

# Get class percentages from detection
class_pct = {
    "healthy_crop": fused_stats["healthy_crop"],
    "stressed_crop": fused_stats["stressed_crop"],
    "drought_stress": fused_stats["drought_stress"],
    "bare_soil": fused_stats["bare_soil"]
}

# Generate recommendations
report = engine.analyze_field(
    class_percentages=class_pct,
    fhi=field_health_index,
    field_area_ha=2.5,  # Configure from user input
    days_after_transplant=45  # Configure from user input
)

# Save alongside detection results
engine.export_to_text(report, f"{output_dir}/recommendations.txt")
engine.export_to_json(report, f"{output_dir}/recommendations.json")

# Print quick summary
print("\\n" + "="*80)
print("AGRONOMIC RECOMMENDATIONS")
print("="*80)
print(engine.generate_quick_summary(report))
print(f"\\nFull report: {output_dir}/recommendations.txt")
'''
    
    print(integration_code)
    
    # ========================================================================
    # Summary
    # ========================================================================
    print("\n" + "="*80)
    print("DEMO SUMMARY")
    print("="*80)
    
    print("\n✅ Successfully demonstrated:")
    print("   • Scenario 1: Healthy field (preventive monitoring)")
    print("   • Scenario 2: Moderate stress (urgent action)")
    print("   • Scenario 3: Critical drought (emergency intervention)")
    print("   • Multi-format export (JSON + Text)")
    print("   • Integration code example")
    
    print("\n📚 Evidence Base:")
    print("   • 13 international journal references (2018-2026)")
    print("   • Publishers: IEEE, Elsevier, MDPI, Springer, Nature, PLOS ONE")
    print("   • Full citations: See recommendations/JOURNAL_CITATIONS.md")
    
    print("\n🎯 Key Features:")
    print("   • Evidence-based recommendations")
    print("   • Priority-ranked actions")
    print("   • Cost estimates")
    print("   • Growth stage awareness")
    print("   • IPM guidelines")
    print("   • Multi-format export")
    
    print("\n📖 Documentation:")
    print("   • README.md - Full English documentation")
    print("   • PANDUAN_SINGKAT.md - Indonesian quick guide")
    print("   • JOURNAL_CITATIONS.md - Complete bibliography")
    print("   • example_usage.py - 7 usage examples")
    print("   • integration_guide.py - Integration helpers")
    
    print("\n🚀 Next Steps:")
    print("   1. Review output files in demo_output/")
    print("   2. Run: python3 recommendations/example_usage.py")
    print("   3. Run: python3 recommendations/test_recommendations.py")
    print("   4. Integrate with your detection pipeline")
    print("   5. See recommendations/README.md for full API docs")
    
    print("\n" + "="*80)
    print("Demo completed successfully!")
    print("="*80 + "\n")


if __name__ == "__main__":
    try:
        demo_with_config()
    except Exception as e:
        print(f"\n❌ Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
