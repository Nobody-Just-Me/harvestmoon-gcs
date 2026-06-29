"""
Main Recommendation Engine
Analyzes field health data and generates evidence-based agronomic recommendations
"""

from typing import Dict, List, Any, Optional
import json

try:
    from .recommendation_database import (
        RECOMMENDATION_DATABASE,
        IPM_GUIDELINES,
        GROWTH_STAGE_RECOMMENDATIONS,
        JOURNAL_REFERENCES
    )
    from .action_prioritizer import ActionPrioritizer
except ImportError:
    # Fallback for direct execution
    import recommendation_database
    import action_prioritizer
    RECOMMENDATION_DATABASE = recommendation_database.RECOMMENDATION_DATABASE
    IPM_GUIDELINES = recommendation_database.IPM_GUIDELINES
    GROWTH_STAGE_RECOMMENDATIONS = recommendation_database.GROWTH_STAGE_RECOMMENDATIONS
    JOURNAL_REFERENCES = recommendation_database.JOURNAL_REFERENCES
    ActionPrioritizer = action_prioritizer.ActionPrioritizer


class RecommendationEngine:
    """
    Core recommendation engine that:
    1. Analyzes crop health distribution from UAV detection
    2. Matches conditions to recommendation database
    3. Prioritizes actions based on urgency and feasibility
    4. Generates structured recommendations with journal citations
    """
    
    def __init__(self):
        self.prioritizer = ActionPrioritizer()
        self.db = RECOMMENDATION_DATABASE
    
    def analyze_field(
        self,
        class_percentages: Dict[str, float],
        fhi: float,
        field_area_ha: float = 1.0,
        days_after_transplant: Optional[int] = None,
        available_resources: Optional[Dict[str, bool]] = None
    ) -> Dict[str, Any]:
        """
        Main analysis function that generates comprehensive recommendations
        
        Args:
            class_percentages: Dict with keys like:
                {
                    "healthy_crop": 45.2,
                    "stressed_crop": 32.1,
                    "drought_stress": 15.3,
                    "bare_soil": 7.4
                }
            fhi: Field Health Index (0-100)
            field_area_ha: Field area in hectares
            days_after_transplant: Crop age for stage-specific recommendations
            available_resources: Dict of resource availability
        
        Returns:
            Comprehensive recommendation report
        """
        # Normalize percentages
        total = sum(class_percentages.values())
        if total > 0:
            normalized_pct = {k: (v / total) * 100 for k, v in class_percentages.items()}
        else:
            normalized_pct = class_percentages
        
        # Identify dominant condition and urgency
        dominant_class = max(normalized_pct, key=normalized_pct.get)
        dominant_pct = normalized_pct[dominant_class]
        
        # Determine overall field status
        field_status = self._determine_field_status(fhi, normalized_pct)
        
        # Get base recommendations
        recommendations = self._get_base_recommendations(
            dominant_class,
            dominant_pct,
            normalized_pct,
            fhi
        )
        
        # Prioritize actions
        all_actions = recommendations.get("primary_actions", [])
        prioritized_actions = self.prioritizer.prioritize_actions(
            all_actions,
            recommendations["urgency"]
        )
        
        # Apply filters
        if days_after_transplant is not None:
            prioritized_actions = self.prioritizer.filter_by_growth_stage(
                prioritized_actions,
                days_after_transplant
            )
        
        if available_resources is not None:
            prioritized_actions = self.prioritizer.filter_by_resources(
                prioritized_actions,
                available_resources
            )
        
        # Generate action timeline
        timeline = self.prioritizer.generate_action_timeline(prioritized_actions)
        
        # Estimate costs
        cost_estimate = self.prioritizer.estimate_costs(prioritized_actions, field_area_ha)
        
        # Get growth stage recommendations
        growth_stage_advice = None
        if days_after_transplant is not None:
            growth_stage_advice = self._get_growth_stage_advice(days_after_transplant)
        
        # Compile comprehensive report
        report = {
            "summary": {
                "field_health_index": fhi,
                "field_status": field_status,
                "dominant_condition": recommendations["condition"],
                "urgency_level": recommendations["urgency"],
                "field_area_ha": field_area_ha
            },
            "crop_distribution": normalized_pct,
            "primary_diagnosis": {
                "condition": recommendations["condition"],
                "urgency": recommendations["urgency"],
                "risk_factors": recommendations.get("risk_factors", []),
                "follow_up": recommendations.get("follow_up", "Monitor field regularly")
            },
            "recommended_actions": prioritized_actions,
            "action_timeline": timeline,
            "cost_estimate": cost_estimate,
            "growth_stage_guidance": growth_stage_advice,
            "ipm_guidelines": self._get_relevant_ipm_guidelines(normalized_pct),
            "evidence_base": self._get_evidence_summary(),
            "generated_at": self._get_timestamp()
        }
        
        return report
    
    def _determine_field_status(
        self,
        fhi: float,
        percentages: Dict[str, float]
    ) -> str:
        """
        Determine overall field health status
        """
        if fhi >= 75:
            return "Excellent"
        elif fhi >= 65:
            return "Good"
        elif fhi >= 50:
            return "Fair - Attention Needed"
        elif fhi >= 35:
            return "Poor - Urgent Action Required"
        else:
            return "Critical - Emergency Intervention"
    
    def _get_base_recommendations(
        self,
        dominant_class: str,
        dominant_pct: float,
        all_percentages: Dict[str, float],
        fhi: float
    ) -> Dict[str, Any]:
        """
        Retrieve base recommendations from database
        """
        # Check for mixed conditions
        significant_classes = [k for k, v in all_percentages.items() if v > 15.0]
        
        if len(significant_classes) > 2:
            # Mixed field condition
            return self._get_mixed_recommendations(all_percentages, fhi)
        
        # Single dominant condition
        class_db = self.db.get(dominant_class, {})
        
        # Determine severity level within class
        if dominant_class == "healthy_crop":
            if dominant_pct > 70:
                level = "dominant"
            else:
                level = "moderate"
        elif dominant_class == "stressed_crop":
            if dominant_pct > 40:
                level = "dominant"
            else:
                level = "moderate"
        elif dominant_class == "drought_stress":
            if dominant_pct > 30:
                level = "dominant"
            else:
                level = "moderate"
        elif dominant_class == "bare_soil":
            if dominant_pct > 25:
                level = "dominant"
            else:
                level = "moderate"
        else:
            level = "moderate"
        
        recommendations = class_db.get(level, {})
        
        # Fallback if no specific level found
        if not recommendations:
            recommendations = {
                "condition": f"{dominant_class.replace('_', ' ').title()} - {dominant_pct:.1f}%",
                "urgency": "moderate",
                "primary_actions": [],
                "risk_factors": []
            }
        
        return recommendations
    
    def _get_mixed_recommendations(
        self,
        percentages: Dict[str, float],
        fhi: float
    ) -> Dict[str, Any]:
        """
        Generate recommendations for mixed field conditions
        """
        stressed_pct = percentages.get("stressed_crop", 0)
        drought_pct = percentages.get("drought_stress", 0)
        
        total_stress = stressed_pct + drought_pct
        
        if total_stress > 40:
            urgency = "high"
        elif total_stress > 25:
            urgency = "moderate"
        else:
            urgency = "low"
        
        mixed_db = self.db.get("mixed_conditions", {}).get("healthy_stressed_mix", {})
        
        # Combine actions from mixed template
        actions = mixed_db.get("primary_actions", [])
        
        # Add specific actions for each significant stress type
        if stressed_pct > 20:
            stress_actions = self.db.get("stressed_crop", {}).get("moderate", {}).get("primary_actions", [])
            actions.extend(stress_actions[:2])  # Top 2 actions
        
        if drought_pct > 15:
            drought_actions = self.db.get("drought_stress", {}).get("moderate", {}).get("primary_actions", [])
            actions.extend(drought_actions[:2])  # Top 2 actions
        
        return {
            "condition": "Mixed Field Health - Multiple Stress Types Present",
            "urgency": urgency,
            "primary_actions": actions,
            "risk_factors": [
                "Multiple stressors may compound yield loss",
                "Prioritize addressing most severe stress first",
                "Monitor for stress interaction effects"
            ],
            "follow_up": "Re-assess with UAV in 3-5 days to track intervention effectiveness"
        }
    
    def _get_growth_stage_advice(self, days_after_transplant: int) -> Dict[str, Any]:
        """
        Get growth stage-specific guidance
        """
        if days_after_transplant < 50:
            stage = "vegetative"
        elif days_after_transplant < 70:
            stage = "reproductive"
        else:
            stage = "grain_filling"
        
        return GROWTH_STAGE_RECOMMENDATIONS.get(stage, {})
    
    def _get_relevant_ipm_guidelines(
        self,
        percentages: Dict[str, float]
    ) -> Dict[str, Any]:
        """
        Get relevant IPM guidelines based on field condition
        """
        # Always return general principles
        ipm = {
            "general_principles": IPM_GUIDELINES["general_principles"]
        }
        
        # Add specific pest/disease info if stress is present
        stressed_pct = percentages.get("stressed_crop", 0)
        if stressed_pct > 15:
            ipm["note"] = "Stress detected. Review common pest and disease symptoms below."
            ipm["common_pests"] = IPM_GUIDELINES["common_rice_pests"]
            ipm["common_diseases"] = IPM_GUIDELINES["common_rice_diseases"]
        
        return ipm
    
    def _get_evidence_summary(self) -> Dict[str, Any]:
        """
        Provide summary of evidence base with journal references
        """
        return {
            "note": "All recommendations are based on peer-reviewed international journals",
            "primary_references": [
                "Hassanein et al. (2018) - Sensors, MDPI",
                "Zhao et al. (2025) - Sensors, MDPI",
                "Mahmood et al. (2025) - Scientific Reports, Nature",
                "Logavitool et al. (2025) - PLOS ONE",
                "Zhang et al. (2023) - Plant Phenomics",
                "Bouguettaya et al. (2022) - Neural Computing and Applications"
            ],
            "full_references": JOURNAL_REFERENCES,
            "methodology": "Recommendations based on UAV-based crop health monitoring combined with agronomic best practices from 2018-2026 peer-reviewed literature"
        }
    
    def _get_timestamp(self) -> str:
        """
        Get current timestamp
        """
        from datetime import datetime
        return datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    def export_to_json(self, report: Dict[str, Any], output_path: str) -> None:
        """
        Export recommendation report to JSON file
        """
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
    
    def export_to_text(self, report: Dict[str, Any], output_path: str) -> None:
        """
        Export recommendation report to human-readable text file
        """
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write("=" * 80 + "\n")
            f.write("MOONHARVEST FIELD HEALTH RECOMMENDATION REPORT\n")
            f.write("Evidence-Based Agronomic Guidance\n")
            f.write("=" * 80 + "\n\n")
            
            # Summary
            f.write("FIELD SUMMARY\n")
            f.write("-" * 80 + "\n")
            summary = report["summary"]
            f.write(f"Field Health Index (FHI): {summary['field_health_index']:.1f}/100\n")
            f.write(f"Field Status: {summary['field_status']}\n")
            f.write(f"Dominant Condition: {summary['dominant_condition']}\n")
            f.write(f"Urgency Level: {summary['urgency_level'].upper()}\n")
            f.write(f"Field Area: {summary['field_area_ha']:.2f} hectares\n")
            f.write(f"Generated: {report['generated_at']}\n\n")
            
            # Crop distribution
            f.write("CROP HEALTH DISTRIBUTION\n")
            f.write("-" * 80 + "\n")
            for class_name, pct in report["crop_distribution"].items():
                display_name = class_name.replace("_", " ").title()
                f.write(f"{display_name:30s}: {pct:5.1f}%\n")
            f.write("\n")
            
            # Primary diagnosis
            f.write("PRIMARY DIAGNOSIS\n")
            f.write("-" * 80 + "\n")
            diag = report["primary_diagnosis"]
            f.write(f"Condition: {diag['condition']}\n")
            f.write(f"Urgency: {diag['urgency'].upper()}\n\n")
            
            if diag.get("risk_factors"):
                f.write("Risk Factors:\n")
                for risk in diag["risk_factors"]:
                    f.write(f"  • {risk}\n")
                f.write("\n")
            
            # Recommended actions
            f.write("RECOMMENDED ACTIONS\n")
            f.write("=" * 80 + "\n\n")
            
            timeline = report["action_timeline"]
            for period, actions in timeline.items():
                if not actions:
                    continue
                
                f.write(f"\n{period.upper().replace('_', ' ')}\n")
                f.write("-" * 80 + "\n")
                
                for i, action in enumerate(actions, 1):
                    f.write(f"\n{i}. {action['action']}\n")
                    f.write(f"   Priority: {action['priority']} | Deadline: {action.get('deadline', 'N/A')}\n")
                    f.write(f"   {action['description']}\n\n")
                    
                    if action.get("details"):
                        f.write("   Action Steps:\n")
                        for detail in action["details"]:
                            f.write(f"   • {detail}\n")
                        f.write("\n")
                    
                    if action.get("evidence"):
                        f.write(f"   Evidence: {action['evidence']}\n")
                    
                    if action.get("warning"):
                        f.write(f"   ⚠ WARNING: {action['warning']}\n")
                    
                    if action.get("note"):
                        f.write(f"   📌 NOTE: {action['note']}\n")
                    
                    f.write("\n")
            
            # Cost estimate
            f.write("\nESTIMATED COSTS\n")
            f.write("-" * 80 + "\n")
            costs = report["cost_estimate"]
            f.write(f"Total Estimated Cost: ${costs['total_usd']:.2f} USD\n")
            f.write(f"Per Hectare: ${costs['per_hectare']:.2f} USD/ha\n\n")
            
            if costs.get("breakdown"):
                f.write("Cost Breakdown:\n")
                for action_name, cost in costs["breakdown"].items():
                    f.write(f"  {action_name:40s}: ${cost:8.2f}\n")
            
            f.write(f"\n{costs.get('note', '')}\n\n")
            
            # Growth stage guidance
            if report.get("growth_stage_guidance"):
                f.write("\nGROWTH STAGE GUIDANCE\n")
                f.write("-" * 80 + "\n")
                stage = report["growth_stage_guidance"]
                f.write(f"Stage: {stage.get('stage', 'N/A')}\n\n")
                
                if stage.get("key_focus"):
                    f.write("Key Focus Areas:\n")
                    for focus in stage["key_focus"]:
                        f.write(f"  • {focus}\n")
                    f.write("\n")
            
            # IPM Guidelines
            ipm = report.get("ipm_guidelines", {})
            if ipm.get("general_principles"):
                f.write("\nINTEGRATED PEST MANAGEMENT (IPM) GUIDELINES\n")
                f.write("-" * 80 + "\n")
                f.write("General Principles:\n")
                for principle in ipm["general_principles"]:
                    f.write(f"  • {principle}\n")
                f.write("\n")
            
            # Evidence base
            f.write("\nEVIDENCE BASE\n")
            f.write("-" * 80 + "\n")
            evidence = report["evidence_base"]
            f.write(f"{evidence['note']}\n\n")
            f.write("Primary References:\n")
            for ref in evidence["primary_references"]:
                f.write(f"  • {ref}\n")
            f.write("\n")
            f.write(f"Methodology: {evidence['methodology']}\n")
            
            f.write("\n" + "=" * 80 + "\n")
            f.write("END OF REPORT\n")
            f.write("=" * 80 + "\n")
    
    def generate_quick_summary(self, report: Dict[str, Any]) -> str:
        """
        Generate a concise 3-4 sentence summary for display
        """
        summary = report["summary"]
        fhi = summary["field_health_index"]
        status = summary["field_status"]
        urgency = summary["urgency_level"]
        
        timeline = report["action_timeline"]
        immediate_actions = len(timeline.get("immediate", []))
        today_actions = len(timeline.get("today", []))
        
        summary_text = f"Field Health Index: {fhi:.1f}/100 ({status}). "
        
        if urgency in ["critical", "high"]:
            summary_text += f"URGENT: {immediate_actions + today_actions} actions required within 24 hours. "
        else:
            summary_text += f"{len(report['recommended_actions'])} recommended actions. "
        
        summary_text += f"Primary diagnosis: {report['primary_diagnosis']['condition']}. "
        
        if report["recommended_actions"]:
            top_action = report["recommended_actions"][0]
            summary_text += f"Priority 1: {top_action['action']}."
        
        return summary_text
