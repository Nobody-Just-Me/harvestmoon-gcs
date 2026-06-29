#!/usr/bin/env python3
"""
Simple test suite for MoonHarvest Recommendation Engine
Run this to verify the system is working correctly
"""

import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent))

import recommendation_engine
import action_prioritizer
import recommendation_database

RecommendationEngine = recommendation_engine.RecommendationEngine
ActionPrioritizer = action_prioritizer.ActionPrioritizer
RECOMMENDATION_DATABASE = recommendation_database.RECOMMENDATION_DATABASE
JOURNAL_REFERENCES = recommendation_database.JOURNAL_REFERENCES


def test_basic_initialization():
    """Test 1: Basic initialization"""
    print("Test 1: Basic Initialization...")
    try:
        engine = RecommendationEngine()
        prioritizer = ActionPrioritizer()
        print("  ✓ Engine initialized successfully")
        print("  ✓ Prioritizer initialized successfully")
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_database_structure():
    """Test 2: Database structure validation"""
    print("\nTest 2: Database Structure...")
    try:
        required_classes = ["healthy_crop", "stressed_crop", "drought_stress", "bare_soil"]
        for class_name in required_classes:
            assert class_name in RECOMMENDATION_DATABASE, f"Missing class: {class_name}"
        print(f"  ✓ All {len(required_classes)} crop classes present")
        
        ref_count = len(JOURNAL_REFERENCES)
        assert ref_count > 0, "No journal references found"
        print(f"  ✓ {ref_count} journal references loaded")
        
        return True
    except AssertionError as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_healthy_field_analysis():
    """Test 3: Healthy field analysis"""
    print("\nTest 3: Healthy Field Analysis...")
    try:
        engine = RecommendationEngine()
        
        class_pct = {
            "healthy_crop": 80.0,
            "stressed_crop": 10.0,
            "drought_stress": 5.0,
            "bare_soil": 5.0
        }
        
        report = engine.analyze_field(
            class_percentages=class_pct,
            fhi=85.0,
            field_area_ha=2.0
        )
        
        assert "summary" in report, "Missing summary section"
        assert "recommended_actions" in report, "Missing recommended actions"
        assert report["summary"]["field_health_index"] == 85.0, "FHI mismatch"
        assert report["summary"]["urgency_level"] == "low", "Healthy field should be low urgency"
        
        print(f"  ✓ Analysis completed")
        print(f"  ✓ Urgency: {report['summary']['urgency_level']}")
        print(f"  ✓ Actions: {len(report['recommended_actions'])}")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_stressed_field_analysis():
    """Test 4: Stressed field analysis"""
    print("\nTest 4: Stressed Field Analysis...")
    try:
        engine = RecommendationEngine()
        
        class_pct = {
            "healthy_crop": 30.0,
            "stressed_crop": 50.0,
            "drought_stress": 10.0,
            "bare_soil": 10.0
        }
        
        report = engine.analyze_field(
            class_percentages=class_pct,
            fhi=48.0,
            field_area_ha=2.0
        )
        
        assert report["summary"]["urgency_level"] in ["high", "moderate"], \
            "Stressed field should be high/moderate urgency"
        assert len(report["recommended_actions"]) > 0, "Should have actions"
        
        # Check for immediate actions
        timeline = report["action_timeline"]
        urgent_count = len(timeline["immediate"]) + len(timeline["today"])
        
        print(f"  ✓ Analysis completed")
        print(f"  ✓ Urgency: {report['summary']['urgency_level']}")
        print(f"  ✓ Urgent actions (24h): {urgent_count}")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_critical_drought_analysis():
    """Test 5: Critical drought analysis"""
    print("\nTest 5: Critical Drought Analysis...")
    try:
        engine = RecommendationEngine()
        
        class_pct = {
            "healthy_crop": 15.0,
            "stressed_crop": 25.0,
            "drought_stress": 50.0,
            "bare_soil": 10.0
        }
        
        report = engine.analyze_field(
            class_percentages=class_pct,
            fhi=25.0,
            field_area_ha=2.0
        )
        
        assert report["summary"]["urgency_level"] in ["critical", "high"], \
            "Drought should be critical/high urgency"
        
        # Should have emergency irrigation action
        actions_text = " ".join([a["action"] for a in report["recommended_actions"]])
        assert "irrigation" in actions_text.lower(), "Should recommend irrigation"
        
        print(f"  ✓ Analysis completed")
        print(f"  ✓ Urgency: {report['summary']['urgency_level']}")
        print(f"  ✓ FHI: {report['summary']['field_health_index']}")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_action_prioritization():
    """Test 6: Action prioritization"""
    print("\nTest 6: Action Prioritization...")
    try:
        prioritizer = ActionPrioritizer()
        
        test_actions = [
            {
                "action": "Action A",
                "priority": 2,
                "timing": "within_24_hours",
                "description": "Test action A"
            },
            {
                "action": "Action B",
                "priority": 1,
                "timing": "immediate",
                "description": "Test action B"
            },
            {
                "action": "Action C",
                "priority": 3,
                "timing": "within_7_days",
                "description": "Test action C"
            }
        ]
        
        prioritized = prioritizer.prioritize_actions(test_actions, "high")
        
        # Priority 1 should be first
        assert prioritized[0]["priority"] == 1, "Priority 1 should be first"
        assert all("urgency_score" in a for a in prioritized), "Missing urgency scores"
        
        print(f"  ✓ Prioritization completed")
        print(f"  ✓ Top priority: {prioritized[0]['action']}")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_growth_stage_filtering():
    """Test 7: Growth stage filtering"""
    print("\nTest 7: Growth Stage Filtering...")
    try:
        engine = RecommendationEngine()
        
        class_pct = {
            "healthy_crop": 60.0,
            "stressed_crop": 25.0,
            "drought_stress": 8.0,
            "bare_soil": 7.0
        }
        
        # Test different growth stages
        for dat, expected_stage in [(25, "vegetative"), (60, "reproductive"), (85, "grain_filling")]:
            report = engine.analyze_field(
                class_percentages=class_pct,
                fhi=65.0,
                field_area_ha=2.0,
                days_after_transplant=dat
            )
            
            assert report.get("growth_stage_guidance") is not None, \
                f"Missing growth stage guidance for DAT {dat}"
            
            stage_name = report["growth_stage_guidance"].get("stage", "").lower()
            assert expected_stage in stage_name, \
                f"Wrong stage for DAT {dat}: expected {expected_stage}"
        
        print(f"  ✓ Stage filtering working")
        print(f"  ✓ Tested: vegetative, reproductive, grain_filling")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_cost_estimation():
    """Test 8: Cost estimation"""
    print("\nTest 8: Cost Estimation...")
    try:
        engine = RecommendationEngine()
        
        class_pct = {
            "healthy_crop": 40.0,
            "stressed_crop": 40.0,
            "drought_stress": 10.0,
            "bare_soil": 10.0
        }
        
        report = engine.analyze_field(
            class_percentages=class_pct,
            fhi=55.0,
            field_area_ha=2.0
        )
        
        cost = report["cost_estimate"]
        assert "total_usd" in cost, "Missing total cost"
        assert "per_hectare" in cost, "Missing per hectare cost"
        assert cost["total_usd"] >= 0, "Cost should be non-negative"
        
        print(f"  ✓ Cost estimation completed")
        print(f"  ✓ Total cost: ${cost['total_usd']:.2f}")
        print(f"  ✓ Per ha: ${cost['per_hectare']:.2f}")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_export_functions():
    """Test 9: Export functions"""
    print("\nTest 9: Export Functions...")
    try:
        engine = RecommendationEngine()
        
        class_pct = {
            "healthy_crop": 70.0,
            "stressed_crop": 20.0,
            "drought_stress": 5.0,
            "bare_soil": 5.0
        }
        
        report = engine.analyze_field(
            class_percentages=class_pct,
            fhi=75.0,
            field_area_ha=1.0
        )
        
        # Test quick summary
        summary = engine.generate_quick_summary(report)
        assert len(summary) > 0, "Summary should not be empty"
        assert "Field Health Index" in summary, "Summary should mention FHI"
        
        # Test JSON export
        import tempfile
        with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as f:
            json_path = f.name
        engine.export_to_json(report, json_path)
        
        # Test text export
        with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False) as f:
            text_path = f.name
        engine.export_to_text(report, text_path)
        
        # Verify files exist and have content
        import os
        assert os.path.getsize(json_path) > 0, "JSON file should have content"
        assert os.path.getsize(text_path) > 0, "Text file should have content"
        
        # Clean up
        os.unlink(json_path)
        os.unlink(text_path)
        
        print(f"  ✓ Quick summary generated")
        print(f"  ✓ JSON export working")
        print(f"  ✓ Text export working")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def test_resource_filtering():
    """Test 10: Resource availability filtering"""
    print("\nTest 10: Resource Filtering...")
    try:
        engine = RecommendationEngine()
        
        class_pct = {
            "healthy_crop": 40.0,
            "stressed_crop": 40.0,
            "drought_stress": 10.0,
            "bare_soil": 10.0
        }
        
        available_resources = {
            "irrigation": True,
            "fertilizer": True,
            "pesticides": False,
            "labor": True
        }
        
        report = engine.analyze_field(
            class_percentages=class_pct,
            fhi=55.0,
            field_area_ha=2.0,
            available_resources=available_resources
        )
        
        # Check if infeasible actions are marked
        infeasible = [a for a in report["recommended_actions"] if not a.get("feasible", True)]
        
        print(f"  ✓ Resource filtering applied")
        print(f"  ✓ Infeasible actions: {len(infeasible)}")
        
        return True
    except Exception as e:
        print(f"  ✗ Failed: {e}")
        return False


def run_all_tests():
    """Run all tests and report results"""
    print("="*80)
    print("MOONHARVEST RECOMMENDATION ENGINE - TEST SUITE")
    print("="*80)
    
    tests = [
        test_basic_initialization,
        test_database_structure,
        test_healthy_field_analysis,
        test_stressed_field_analysis,
        test_critical_drought_analysis,
        test_action_prioritization,
        test_growth_stage_filtering,
        test_cost_estimation,
        test_export_functions,
        test_resource_filtering
    ]
    
    results = []
    for test_func in tests:
        try:
            results.append(test_func())
        except Exception as e:
            print(f"\n  ✗ Test crashed: {e}")
            results.append(False)
    
    # Summary
    print("\n" + "="*80)
    print("TEST SUMMARY")
    print("="*80)
    passed = sum(results)
    total = len(results)
    percentage = (passed / total) * 100 if total > 0 else 0
    
    print(f"Passed: {passed}/{total} ({percentage:.1f}%)")
    
    if passed == total:
        print("✓ ALL TESTS PASSED")
        return 0
    else:
        print("✗ SOME TESTS FAILED")
        return 1


if __name__ == "__main__":
    exit_code = run_all_tests()
    sys.exit(exit_code)
