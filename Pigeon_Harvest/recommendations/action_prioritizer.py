"""
Action Prioritization System
Ranks and filters recommendations based on field conditions, urgency, and feasibility
"""

from typing import Dict, List, Any, Tuple
from datetime import datetime, timedelta


class ActionPrioritizer:
    """
    Prioritizes and filters agronomic actions based on:
    - Urgency level
    - Resource availability
    - Growth stage
    - Weather conditions
    - Economic feasibility
    """
    
    def __init__(self):
        self.urgency_weights = {
            "critical": 1.0,
            "high": 0.75,
            "moderate": 0.50,
            "low": 0.25
        }
        
        self.timing_to_hours = {
            "immediate": 0,
            "within_12_hours": 12,
            "within_24_hours": 24,
            "within_48_hours": 48,
            "within_3_days": 72,
            "within_7_days": 168,
            "weekly": 168,
            "every_3_days": 72,
            "daily": 24,
            "ongoing": 0,
            "growth_stage_dependent": None,
            "next_season": None,
            "planning_phase": None
        }
    
    def prioritize_actions(
        self,
        actions: List[Dict[str, Any]],
        urgency: str,
        current_conditions: Dict[str, Any] = None
    ) -> List[Dict[str, Any]]:
        """
        Sort actions by priority, urgency, and feasibility
        
        Args:
            actions: List of action dictionaries from recommendation database
            urgency: Overall urgency level (critical, high, moderate, low)
            current_conditions: Optional dict with weather, resources, growth_stage
        
        Returns:
            Sorted list of actions with computed urgency scores
        """
        if current_conditions is None:
            current_conditions = {}
        
        scored_actions = []
        
        for action in actions:
            score = self._compute_action_score(action, urgency, current_conditions)
            action_with_score = action.copy()
            action_with_score["urgency_score"] = score
            action_with_score["deadline"] = self._compute_deadline(action["timing"])
            scored_actions.append(action_with_score)
        
        # Sort by priority (ascending, 1 is highest) then by urgency_score (descending)
        scored_actions.sort(key=lambda x: (x["priority"], -x["urgency_score"]))
        
        return scored_actions
    
    def _compute_action_score(
        self,
        action: Dict[str, Any],
        overall_urgency: str,
        conditions: Dict[str, Any]
    ) -> float:
        """
        Compute urgency score for an action (0.0 to 1.0)
        """
        # Base score from overall urgency
        base_score = self.urgency_weights.get(overall_urgency, 0.5)
        
        # Adjust by action priority (priority 1 is most urgent)
        priority = action.get("priority", 3)
        priority_multiplier = 1.0 / priority
        
        # Adjust by timing
        timing = action.get("timing", "ongoing")
        hours = self.timing_to_hours.get(timing, 0)
        if hours is not None and hours > 0:
            # More urgent if deadline is sooner
            time_multiplier = max(0.5, 1.0 - (hours / 168))  # Normalize to week
        else:
            time_multiplier = 0.5
        
        score = base_score * priority_multiplier * time_multiplier
        return min(1.0, max(0.0, score))
    
    def _compute_deadline(self, timing: str) -> str:
        """
        Convert timing string to human-readable deadline
        """
        hours = self.timing_to_hours.get(timing)
        
        if hours is None:
            return timing.replace("_", " ").title()
        
        if hours == 0:
            return "Now / Ongoing"
        
        deadline = datetime.now() + timedelta(hours=hours)
        
        if hours <= 24:
            return deadline.strftime("Today by %I:%M %p")
        elif hours <= 48:
            return deadline.strftime("Tomorrow by %I:%M %p")
        else:
            return deadline.strftime("%A, %B %d by %I:%M %p")
    
    def filter_by_resources(
        self,
        actions: List[Dict[str, Any]],
        available_resources: Dict[str, bool]
    ) -> List[Dict[str, Any]]:
        """
        Filter actions based on available resources
        
        Args:
            actions: List of actions
            available_resources: Dict like {
                "irrigation": True,
                "fertilizer": True,
                "pesticides": False,
                "labor": True
            }
        
        Returns:
            Filtered list of feasible actions
        """
        # Simple keyword matching for resource requirements
        resource_keywords = {
            "irrigation": ["irrigation", "water", "irrigate"],
            "fertilizer": ["fertilizer", "fertilization", "nutrient", "NPK"],
            "pesticides": ["pesticide", "fungicide", "insecticide", "herbicide"],
            "labor": ["hand", "manual", "scout", "inspect"]
        }
        
        filtered_actions = []
        
        for action in actions:
            description = action.get("description", "").lower()
            details_text = " ".join(action.get("details", [])).lower()
            full_text = description + " " + details_text
            
            # Check if action requires unavailable resources
            requires_unavailable = False
            for resource, available in available_resources.items():
                if not available:
                    keywords = resource_keywords.get(resource, [])
                    if any(kw in full_text for kw in keywords):
                        requires_unavailable = True
                        break
            
            if not requires_unavailable:
                filtered_actions.append(action)
            else:
                # Mark as infeasible but keep in list
                action_copy = action.copy()
                action_copy["feasible"] = False
                action_copy["reason"] = f"Requires {resource} (not available)"
                filtered_actions.append(action_copy)
        
        return filtered_actions
    
    def filter_by_growth_stage(
        self,
        actions: List[Dict[str, Any]],
        days_after_transplant: int
    ) -> List[Dict[str, Any]]:
        """
        Filter/adjust actions based on crop growth stage
        
        Args:
            actions: List of actions
            days_after_transplant: DAT (0-110 for typical rice)
        
        Returns:
            Actions with growth stage feasibility noted
        """
        # Define growth stages
        if days_after_transplant < 50:
            stage = "vegetative"
        elif days_after_transplant < 70:
            stage = "reproductive"
        else:
            stage = "grain_filling"
        
        filtered_actions = []
        
        for action in actions:
            action_copy = action.copy()
            action_copy["growth_stage"] = stage
            
            # Flag stage-specific constraints
            if stage == "grain_filling":
                # No nitrogen fertilization during grain filling
                if "nitrogen" in action.get("description", "").lower():
                    action_copy["warning"] = "Nitrogen application not recommended during grain filling (lodging risk)"
                
                # Chemical applications must respect PHI
                if any(kw in action.get("description", "").lower() for kw in ["pesticide", "fungicide", "insecticide"]):
                    action_copy["warning"] = "Check Pre-Harvest Interval (PHI) before chemical application"
            
            if stage == "reproductive":
                # Water stress highly critical
                if "irrigation" in action.get("description", "").lower():
                    action_copy["note"] = "CRITICAL: Reproductive stage is most sensitive to water stress"
            
            filtered_actions.append(action_copy)
        
        return filtered_actions
    
    def generate_action_timeline(
        self,
        actions: List[Dict[str, Any]]
    ) -> Dict[str, List[Dict[str, Any]]]:
        """
        Organize actions into timeline buckets
        
        Returns:
            Dict with keys: "immediate", "today", "this_week", "this_month", "planning"
        """
        timeline = {
            "immediate": [],  # 0-12 hours
            "today": [],      # 12-24 hours
            "this_week": [],  # 1-7 days
            "this_month": [], # 7-30 days
            "planning": []    # Future season
        }
        
        for action in actions:
            timing = action.get("timing", "ongoing")
            hours = self.timing_to_hours.get(timing)
            
            if hours is None:
                if timing in ["next_season", "planning_phase"]:
                    timeline["planning"].append(action)
                else:
                    timeline["this_week"].append(action)
            elif hours <= 12:
                timeline["immediate"].append(action)
            elif hours <= 24:
                timeline["today"].append(action)
            elif hours <= 168:
                timeline["this_week"].append(action)
            else:
                timeline["this_month"].append(action)
        
        return timeline
    
    def estimate_costs(
        self,
        actions: List[Dict[str, Any]],
        field_area_ha: float = 1.0
    ) -> Dict[str, Any]:
        """
        Rough cost estimation for recommended actions
        
        Args:
            actions: List of actions
            field_area_ha: Field area in hectares
        
        Returns:
            Dict with cost estimates and breakdown
        """
        # Approximate costs (USD per hectare)
        cost_estimates = {
            "irrigation": 10,  # Water + energy per application
            "fertilizer_N": 30,  # Per 50 kg N/ha
            "fertilizer_P": 25,  # Per 30 kg P2O5/ha
            "fertilizer_K": 20,  # Per 30 kg K2O/ha
            "fungicide": 40,  # Per application
            "insecticide": 35,  # Per application
            "herbicide": 25,  # Per application
            "labor_day": 15,  # Per person-day
            "uav_flight": 5,  # Per flight (if service)
        }
        
        total_cost = 0.0
        breakdown = {}
        
        for action in actions:
            description = action.get("description", "").lower()
            details_text = " ".join(action.get("details", [])).lower()
            full_text = description + " " + details_text
            
            action_cost = 0.0
            
            # Simple keyword-based cost estimation
            if "irrigation" in full_text or "water" in full_text:
                action_cost += cost_estimates["irrigation"]
            
            if "fertilizer" in full_text or "nitrogen" in full_text:
                action_cost += cost_estimates["fertilizer_N"]
            
            if "phosphorus" in full_text:
                action_cost += cost_estimates["fertilizer_P"]
            
            if "potassium" in full_text:
                action_cost += cost_estimates["fertilizer_K"]
            
            if "fungicide" in full_text:
                action_cost += cost_estimates["fungicide"]
            
            if "insecticide" in full_text or "pesticide" in full_text:
                action_cost += cost_estimates["insecticide"]
            
            if "herbicide" in full_text or "weed" in full_text:
                action_cost += cost_estimates["herbicide"]
            
            if "scout" in full_text or "inspect" in full_text or "hand" in full_text:
                action_cost += cost_estimates["labor_day"]
            
            if "uav" in full_text or "monitoring" in full_text:
                action_cost += cost_estimates["uav_flight"]
            
            if action_cost > 0:
                action_name = action.get("action", "Unknown")
                breakdown[action_name] = round(action_cost * field_area_ha, 2)
                total_cost += action_cost * field_area_ha
        
        return {
            "total_usd": round(total_cost, 2),
            "per_hectare": round(total_cost / field_area_ha, 2),
            "breakdown": breakdown,
            "note": "Estimates only. Actual costs vary by region, suppliers, and market conditions."
        }
