# MoonHarvest Recommendation Engine

## Overview

Evidence-based agronomic recommendation system for rice crop management using UAV-based health monitoring. All recommendations are derived from peer-reviewed international journals (2018-2026) from IEEE, Elsevier, MDPI, Springer, Nature, and PLOS ONE.

## Features

- **Evidence-Based**: Every recommendation linked to peer-reviewed journal publications
- **Prioritized Actions**: Automatic prioritization based on urgency, timing, and feasibility
- **Growth Stage Aware**: Recommendations adapt to crop development stage
- **Cost Estimation**: Rough cost estimates for recommended interventions
- **IPM Integration**: Integrated Pest Management guidelines following international best practices
- **Multi-format Export**: JSON and human-readable text reports

## Architecture

```
recommendations/
├── __init__.py                    # Package initialization
├── recommendation_database.py     # Evidence-based recommendation rules
├── action_prioritizer.py          # Action ranking and filtering logic
├── recommendation_engine.py       # Main analysis engine
├── README.md                      # This file
└── example_usage.py               # Usage examples
```

## Quick Start

### Basic Usage

```python
from recommendations import RecommendationEngine

# Initialize engine
engine = RecommendationEngine()

# Analyze field from detection results
class_percentages = {
    "healthy_crop": 45.2,
    "stressed_crop": 32.1,
    "drought_stress": 15.3,
    "bare_soil": 7.4
}

report = engine.analyze_field(
    class_percentages=class_percentages,
    fhi=58.3,
    field_area_ha=2.5,
    days_after_transplant=35
)

# Print quick summary
print(engine.generate_quick_summary(report))

# Export full report
engine.export_to_text(report, "field_recommendations.txt")
engine.export_to_json(report, "field_recommendations.json")
```

### Advanced Usage with Filters

```python
# With resource availability
available_resources = {
    "irrigation": True,
    "fertilizer": True,
    "pesticides": False,  # No pesticides available
    "labor": True
}

report = engine.analyze_field(
    class_percentages=class_percentages,
    fhi=58.3,
    field_area_ha=2.5,
    days_after_transplant=35,
    available_resources=available_resources
)

# Access specific sections
timeline = report["action_timeline"]
immediate_actions = timeline["immediate"]
cost_estimate = report["cost_estimate"]

print(f"Immediate actions: {len(immediate_actions)}")
print(f"Estimated cost: ${cost_estimate['total_usd']:.2f}")
```

## Integration with MoonHarvest Detection

### From moonharvest_detect.py

```python
from recommendations import RecommendationEngine

# After running detection
engine = RecommendationEngine()

# Get class percentages from detection results
class_pct = {
    "healthy_crop": results["healthy_pct"],
    "stressed_crop": results["stressed_pct"],
    "drought_stress": results["drought_pct"],
    "bare_soil": results["soil_pct"]
}

report = engine.analyze_field(
    class_percentages=class_pct,
    fhi=results["field_health_index"],
    field_area_ha=2.0
)

# Save alongside detection results
engine.export_to_json(report, "output/recommendations.json")
engine.export_to_text(report, "output/recommendations.txt")
```

### From moonharvest_v2_4class.py

```python
from recommendations import RecommendationEngine

# After video processing
engine = RecommendationEngine()

# Use average percentages from video summary
report = engine.analyze_field(
    class_percentages=summary["avg_class_pct"],
    fhi=summary["avg_field_health"],
    field_area_ha=1.5
)

# Display quick summary in console
print("\n" + "="*80)
print("AGRONOMIC RECOMMENDATIONS")
print("="*80)
print(engine.generate_quick_summary(report))
print("\nFull report saved to: recommendations.txt")
engine.export_to_text(report, "recommendations.txt")
```

## Report Structure

### JSON Report Schema

```json
{
  "summary": {
    "field_health_index": 58.3,
    "field_status": "Fair - Attention Needed",
    "dominant_condition": "Significant Crop Stress",
    "urgency_level": "high",
    "field_area_ha": 2.5
  },
  "crop_distribution": {
    "healthy_crop": 45.2,
    "stressed_crop": 32.1,
    "drought_stress": 15.3,
    "bare_soil": 7.4
  },
  "primary_diagnosis": {
    "condition": "Significant Crop Stress - Immediate Action Required",
    "urgency": "high",
    "risk_factors": [
      "Yield loss potential: 20-40%",
      "Stress may become irreversible if untreated >5 days"
    ],
    "follow_up": "Re-assess field with UAV in 5-7 days"
  },
  "recommended_actions": [
    {
      "action": "Diagnose Stress Cause",
      "description": "Rapid field assessment...",
      "priority": 1,
      "timing": "within_24_hours",
      "urgency_score": 0.75,
      "deadline": "Tomorrow by 02:30 PM",
      "evidence": "Mahmood2025, Logavitool2025",
      "details": [
        "Check soil moisture at root zone depth (15-20cm)",
        "Inspect plants for disease symptoms"
      ]
    }
  ],
  "action_timeline": {
    "immediate": [...],
    "today": [...],
    "this_week": [...],
    "this_month": [...],
    "planning": [...]
  },
  "cost_estimate": {
    "total_usd": 187.50,
    "per_hectare": 75.00,
    "breakdown": {
      "Emergency Irrigation": 25.00,
      "Nutrient Deficiency Correction": 75.00
    }
  },
  "growth_stage_guidance": {
    "stage": "Vegetative (0-50 DAT)",
    "key_focus": ["Establish uniform crop stand", ...],
    "fertilization": {...},
    "irrigation": {...}
  },
  "ipm_guidelines": {...},
  "evidence_base": {
    "note": "All recommendations based on peer-reviewed journals",
    "primary_references": [...],
    "full_references": {...}
  }
}
```

## Evidence Base

All recommendations are traceable to peer-reviewed publications:

### Primary References

1. **Hassanein et al. (2018)** - *Sensors, MDPI*  
   "A New Vegetation Segmentation Approach for Cropped Fields Based on Threshold Detection from Hue Histograms"  
   DOI: 10.3390/s18051474

2. **Zhao et al. (2025)** - *Sensors, MDPI*  
   "Rice Canopy Disease and Pest Identification Based on Improved YOLOv5 and UAV Images"

3. **Mahmood et al. (2025)** - *Scientific Reports, Nature*  
   "Deep Learning Framework Using UAV Imagery for Multi-Disease Detection in Cereal Crops"

4. **Logavitool et al. (2025)** - *PLOS ONE*  
   "Field-Scale Detection of Bacterial Leaf Blight in Rice Based on UAV Multispectral Imaging"  
   DOI: 10.1371/journal.pone.0314535

5. **Zhang et al. (2023)** - *Plant Phenomics*  
   "Lightweight Deep Learning Models for High-Precision Rice Seedling Segmentation"

6. **Bouguettaya et al. (2022)** - *Neural Computing and Applications*  
   "Deep Learning Techniques to Classify Agricultural Crops Through UAV Imagery: A Review"

7. **Yu et al. (2022)** - *Frontiers in Plant Science*  
   "Research on Weed Identification Method in Rice Fields Based on UAV Remote Sensing"

## Recommendation Categories

### 1. Healthy Crop (Lush Green)
- **Dominant (>70%)**: Maintenance and preventive monitoring
- **Moderate (40-70%)**: Investigate stress zones, enhance monitoring

### 2. Stressed Crop (Inconsistent Growth)
- **Dominant (>40%)**: URGENT - Diagnose cause, corrective action within 24-48 hours
- **Moderate (20-40%)**: Targeted zone management, improve conditions

### 3. Drought Stress (Severe Stress)
- **Dominant (>30%)**: CRITICAL - Emergency irrigation, assess viability
- **Moderate (15-30%)**: Urgent irrigation, daily monitoring

### 4. Bare Soil / Gaps
- **Dominant (>25%)**: Gap diagnosis, replanting decision, compensatory management
- **Moderate (10-25%)**: Weed control, support remaining crop

### 5. Mixed Conditions
- Spatial variability analysis
- Priority treatment of stressed zones
- Zone-specific management

## Action Priority Levels

- **Priority 1**: Most critical, immediate attention required
- **Priority 2**: Important, address within stated timeframe
- **Priority 3**: Beneficial, implement as resources allow

## Urgency Levels

- **Critical**: Immediate action (0-12 hours) - crop survival at risk
- **High**: Within 24-48 hours - significant yield impact
- **Moderate**: Within 3-7 days - preventable yield loss
- **Low**: Ongoing/preventive - maintain current status

## Cost Estimation

Rough cost estimates (USD per hectare) based on typical input costs:
- Irrigation: $10 per application
- Fertilizer (N): $30 per 50 kg N/ha
- Fungicide: $40 per application
- Insecticide: $35 per application
- Labor: $15 per person-day
- UAV monitoring: $5 per flight

**Note**: Actual costs vary by region, suppliers, and market conditions.

## Growth Stage Integration

Recommendations automatically adapt to crop development:

- **Vegetative (0-50 DAT)**: Focus on stand establishment, tillering, early stress management
- **Reproductive (50-70 DAT)**: Critical period for yield determination, prevent stress
- **Grain Filling (70-110 DAT)**: Support grain development, prepare harvest, respect PHI

## IPM Integration

Integrated Pest Management guidelines following international best practices:
- Economic threshold-based interventions
- Prioritize cultural and biological control
- Chemical control as last resort with proper resistance management
- Detailed management protocols for:
  - Common rice pests (stem borer, planthopper, leaf folder)
  - Common rice diseases (bacterial leaf blight, blast, sheath blight)

## Limitations

1. **Cost Estimates**: Rough approximations only, actual costs vary significantly by region
2. **Local Adaptation**: Recommendations may need adjustment for specific varieties, climates, soils
3. **Disease/Pest Identification**: UAV can detect stress but cannot definitively identify specific pathogens - ground truthing required
4. **Economic Context**: Cost-benefit analysis should consider local market prices and yield expectations
5. **Regulatory Context**: Pesticide recommendations must comply with local regulations and registrations

## Future Enhancements

- [ ] Integration with weather forecast APIs for stress prediction
- [ ] Local cost database support for region-specific estimates
- [ ] Multi-language support (Spanish, French, Chinese, Hindi)
- [ ] Mobile app interface for field-side recommendations
- [ ] Historical tracking for season-to-season comparison
- [ ] Machine learning for recommendation refinement based on outcomes
- [ ] Integration with soil testing data
- [ ] Market price integration for cost-benefit analysis

## Citation

If using this recommendation system in research or publications, please cite the underlying methodology paper:

**MoonHarvest Detection Methodology** (In preparation)  
"Evidence-Based Precision Agriculture Recommendations from UAV-Based Crop Health Monitoring: Integration of HSV Segmentation and Deep Learning with Agronomic Decision Support"

## License

Part of the MoonHarvest UAV Crop Monitoring System  
© 2026 MoonHarvest Project

## Contact

For questions, suggestions, or contributions:
- Project: MoonHarvest GCS
- Documentation: See `METODE_DETEKSI_JURNAL.md` for detection methodology
- Integration: See `moonharvest_detect.py` for detection pipeline

---

*Last Updated: 2026-06-28*
