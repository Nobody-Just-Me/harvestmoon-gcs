#!/usr/bin/env python3
"""
Example Usage of MoonHarvest Recommendation Engine

This script demonstrates various use cases for the recommendation system
"""

try:
    from .recommendation_engine import RecommendationEngine
except ImportError:
    import recommendation_engine
    RecommendationEngine = recommendation_engine.RecommendationEngine

import json


def example_1_healthy_field():
    """
    Example 1: Healthy field requiring maintenance only
    """
    print("\n" + "="*80)
    print("EXAMPLE 1: Healthy Field")
    print("="*80)
    
    engine = RecommendationEngine()
    
    class_percentages = {
        "healthy_crop": 78.5,
        "stressed_crop": 12.3,
        "drought_stress": 4.2,
        "bare_soil": 5.0
    }
    
    report = engine.analyze_field(
        class_percentages=class_percentages,
        fhi=82.1,
        field_area_ha=3.0,
        days_after_transplant=25
    )
    
    print(engine.generate_quick_summary(report))
    print(f"\nUrgency: {report['summary']['urgency_level']}")
    print(f"Actions required: {len(report['recommended_actions'])}")
    print(f"Estimated cost: ${report['cost_estimate']['total_usd']:.2f}")


def example_2_stressed_field():
    """
    Example 2: Field with significant stress requiring urgent action
    """
    print("\n" + "="*80)
    print("EXAMPLE 2: Stressed Field - Urgent Action Required")
    print("="*80)
    
    engine = RecommendationEngine()
    
    class_percentages = {
        "healthy_crop": 32.5,
        "stressed_crop": 48.3,
        "drought_stress": 12.1,
        "bare_soil": 7.1
    }
    
    report = engine.analyze_field(
        class_percentages=class_percentages,
        fhi=52.3,
        field_area_ha=2.5,
        days_after_transplant=40
    )
    
    print(engine.generate_quick_summary(report))
    
    # Show immediate actions
    immediate = report['action_timeline']['immediate']
    today = report['action_timeline']['today']
    
    print(f"\n⚠️  URGENT ACTIONS (within 24 hours): {len(immediate) + len(today)}")
    for action in immediate + today:
        print(f"  • {action['action']} - Priority {action['priority']}")
        print(f"    Deadline: {action['deadline']}")
    
    # Save full report
    engine.export_to_text(report, "/tmp/stressed_field_recommendations.txt")
    print("\nFull report saved to: /tmp/stressed_field_recommendations.txt")


def example_3_critical_drought():
    """
    Example 3: Critical drought stress - emergency intervention
    """
    print("\n" + "="*80)
    print("EXAMPLE 3: Critical Drought Stress")
    print("="*80)
    
    engine = RecommendationEngine()
    
    class_percentages = {
        "healthy_crop": 15.2,
        "stressed_crop": 28.5,
        "drought_stress": 45.8,
        "bare_soil": 10.5
    }
    
    report = engine.analyze_field(
        class_percentages=class_percentages,
        fhi=28.4,
        field_area_ha=1.8,
        days_after_transplant=55  # Reproductive stage - critical!
    )
    
    print(engine.generate_quick_summary(report))
    print(f"\n🚨 CRITICAL SITUATION - Field Status: {report['summary']['field_status']}")
    
    # Show risk factors
    print("\nRisk Factors:")
    for risk in report['primary_diagnosis']['risk_factors']:
        print(f"  ⚠️  {risk}")
    
    # Show top 3 priority actions
    print("\nTop 3 Priority Actions:")
    for i, action in enumerate(report['recommended_actions'][:3], 1):
        print(f"\n{i}. {action['action']}")
        print(f"   Deadline: {action['deadline']}")
        print(f"   {action['description']}")


def example_4_with_resource_constraints():
    """
    Example 4: Field analysis with resource constraints
    """
    print("\n" + "="*80)
    print("EXAMPLE 4: Analysis with Resource Constraints")
    print("="*80)
    
    engine = RecommendationEngine()
    
    class_percentages = {
        "healthy_crop": 42.1,
        "stressed_crop": 38.5,
        "drought_stress": 10.2,
        "bare_soil": 9.2
    }
    
    # Farmer has limited resources
    available_resources = {
        "irrigation": True,
        "fertilizer": True,
        "pesticides": False,  # No pesticides available
        "labor": True
    }
    
    report = engine.analyze_field(
        class_percentages=class_percentages,
        fhi=58.7,
        field_area_ha=2.0,
        days_after_transplant=35,
        available_resources=available_resources
    )
    
    print(engine.generate_quick_summary(report))
    print("\nResource Availability:")
    for resource, available in available_resources.items():
        status = "✓ Available" if available else "✗ Not Available"
        print(f"  {resource.title()}: {status}")
    
    # Count feasible vs infeasible actions
    feasible = [a for a in report['recommended_actions'] if a.get('feasible', True)]
    infeasible = [a for a in report['recommended_actions'] if not a.get('feasible', True)]
    
    print(f"\nFeasible actions: {len(feasible)}")
    print(f"Infeasible actions: {len(infeasible)}")
    
    if infeasible:
        print("\nActions requiring unavailable resources:")
        for action in infeasible:
            print(f"  • {action['action']} - {action.get('reason', 'Resource unavailable')}")


def example_5_mixed_conditions():
    """
    Example 5: Mixed field conditions with multiple stress types
    """
    print("\n" + "="*80)
    print("EXAMPLE 5: Mixed Field Conditions")
    print("="*80)
    
    engine = RecommendationEngine()
    
    class_percentages = {
        "healthy_crop": 35.8,
        "stressed_crop": 28.3,
        "drought_stress": 22.5,
        "bare_soil": 13.4
    }
    
    report = engine.analyze_field(
        class_percentages=class_percentages,
        fhi=48.2,
        field_area_ha=4.5,
        days_after_transplant=45
    )
    
    print(engine.generate_quick_summary(report))
    print("\nField Condition: Heterogeneous - Multiple stress types present")
    print("\nCrop Distribution:")
    for class_name, pct in report['crop_distribution'].items():
        display = class_name.replace("_", " ").title()
        bar = "█" * int(pct / 2)  # Visual bar
        print(f"  {display:25s}: {pct:5.1f}% {bar}")
    
    # Show priority actions
    print("\nPriority Actions:")
    for action in report['recommended_actions'][:4]:
        print(f"  {action['priority']}. {action['action']}")


def example_6_export_formats():
    """
    Example 6: Demonstrating different export formats
    """
    print("\n" + "="*80)
    print("EXAMPLE 6: Export Formats")
    print("="*80)
    
    engine = RecommendationEngine()
    
    class_percentages = {
        "healthy_crop": 55.2,
        "stressed_crop": 28.5,
        "drought_stress": 8.3,
        "bare_soil": 8.0
    }
    
    report = engine.analyze_field(
        class_percentages=class_percentages,
        fhi=65.8,
        field_area_ha=2.0
    )
    
    # Export to JSON
    json_path = "/tmp/recommendations.json"
    engine.export_to_json(report, json_path)
    print(f"✓ JSON report saved to: {json_path}")
    
    # Export to text
    text_path = "/tmp/recommendations.txt"
    engine.export_to_text(report, text_path)
    print(f"✓ Text report saved to: {text_path}")
    
    # Show JSON structure
    print("\nJSON Report Keys:")
    for key in report.keys():
        print(f"  • {key}")
    
    # Show cost breakdown
    print("\nCost Breakdown:")
    for action_name, cost in report['cost_estimate']['breakdown'].items():
        print(f"  {action_name:40s}: ${cost:8.2f}")
    print(f"  {'TOTAL':40s}: ${report['cost_estimate']['total_usd']:8.2f}")


def example_7_growth_stage_specific():
    """
    Example 7: Growth stage-specific recommendations
    """
    print("\n" + "="*80)
    print("EXAMPLE 7: Growth Stage-Specific Recommendations")
    print("="*80)
    
    engine = RecommendationEngine()
    
    class_percentages = {
        "healthy_crop": 62.5,
        "stressed_crop": 22.8,
        "drought_stress": 8.2,
        "bare_soil": 6.5
    }
    
    # Test different growth stages
    stages = [
        (20, "Vegetative - Early"),
        (55, "Reproductive - Flowering"),
        (85, "Grain Filling - Late")
    ]
    
    for dat, stage_name in stages:
        print(f"\n{stage_name} (DAT {dat}):")
        print("-" * 60)
        
        report = engine.analyze_field(
            class_percentages=class_percentages,
            fhi=67.3,
            field_area_ha=2.0,
            days_after_transplant=dat
        )
        
        if report.get('growth_stage_guidance'):
            guidance = report['growth_stage_guidance']
            print(f"Stage: {guidance.get('stage', 'N/A')}")
            
            print("Key Focus:")
            for focus in guidance.get('key_focus', [])[:2]:
                print(f"  • {focus}")
            
            # Check for stage-specific warnings
            for action in report['recommended_actions'][:3]:
                if action.get('warning'):
                    print(f"⚠️  {action['warning']}")
                if action.get('note'):
                    print(f"📌 {action['note']}")


def main():
    """
    Run all examples
    """
    print("\n" + "="*80)
    print("MOONHARVEST RECOMMENDATION ENGINE - EXAMPLE USAGE")
    print("="*80)
    
    examples = [
        ("Healthy Field", example_1_healthy_field),
        ("Stressed Field", example_2_stressed_field),
        ("Critical Drought", example_3_critical_drought),
        ("Resource Constraints", example_4_with_resource_constraints),
        ("Mixed Conditions", example_5_mixed_conditions),
        ("Export Formats", example_6_export_formats),
        ("Growth Stage Specific", example_7_growth_stage_specific)
    ]
    
    print("\nAvailable Examples:")
    for i, (name, _) in enumerate(examples, 1):
        print(f"  {i}. {name}")
    
    print("\nRunning all examples...\n")
    
    for name, example_func in examples:
        try:
            example_func()
        except Exception as e:
            print(f"\n❌ Error in {name}: {e}")
    
    print("\n" + "="*80)
    print("All examples completed!")
    print("="*80 + "\n")


if __name__ == "__main__":
    main()
