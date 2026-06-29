"""
MoonHarvest Recommendation Engine
Evidence-based agronomic recommendations for rice crop management
Based on international peer-reviewed journals (2018-2026)
"""

from .recommendation_engine import RecommendationEngine
from .recommendation_database import RECOMMENDATION_DATABASE
from .action_prioritizer import ActionPrioritizer

__all__ = [
    "RecommendationEngine",
    "RECOMMENDATION_DATABASE",
    "ActionPrioritizer",
]

__version__ = "1.0.0"
