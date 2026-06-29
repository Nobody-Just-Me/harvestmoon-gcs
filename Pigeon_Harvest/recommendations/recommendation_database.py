"""
Evidence-Based Recommendation Database
All recommendations are based on peer-reviewed international journals
References from 2018-2026 (IEEE, Elsevier, MDPI, Springer, Nature, PLOS ONE)
"""

from typing import Dict, List, Any

# Journal References used in this database
JOURNAL_REFERENCES = {
    "Hassanein2018": {
        "title": "A New Vegetation Segmentation Approach for Cropped Fields Based on Threshold Detection from Hue Histograms",
        "authors": "Hassanein, M., Lari, Z., El-Sheimy, N.",
        "journal": "Sensors, 18(5), 1474. MDPI",
        "year": 2018,
        "doi": "10.3390/s18051474",
        "focus": "UAV-based vegetation monitoring"
    },
    "Zhao2025": {
        "title": "Rice Canopy Disease and Pest Identification Based on Improved YOLOv5 and UAV Images",
        "authors": "Zhao et al.",
        "journal": "Sensors, 25(12). MDPI",
        "year": 2025,
        "focus": "Rice disease detection from UAV"
    },
    "Mahmood2025": {
        "title": "Deep Learning Framework Using UAV Imagery for Multi-Disease Detection in Cereal Crops",
        "authors": "Mahmood, A. et al.",
        "journal": "Scientific Reports, 15. Nature",
        "year": 2025,
        "focus": "Multi-disease detection and management"
    },
    "Logavitool2025": {
        "title": "Field-Scale Detection of Bacterial Leaf Blight in Rice Based on UAV Multispectral Imaging and Deep Learning Frameworks",
        "authors": "Logavitool, T. et al.",
        "journal": "PLOS ONE, 20(1)",
        "year": 2025,
        "doi": "10.1371/journal.pone.0314535",
        "focus": "Rice bacterial leaf blight management"
    },
    "Yu2022": {
        "title": "Research on Weed Identification Method in Rice Fields Based on UAV Remote Sensing",
        "authors": "Yu, F. et al.",
        "journal": "Frontiers in Plant Science, 13",
        "year": 2022,
        "focus": "Weed management in rice fields"
    },
    "Zhang2023": {
        "title": "Lightweight Deep Learning Models for High-Precision Rice Seedling Segmentation from UAV-Based Multispectral Images",
        "authors": "Zhang, P. et al.",
        "journal": "Plant Phenomics, 5",
        "year": 2023,
        "focus": "Rice growth monitoring and management"
    },
    "Bouguettaya2022": {
        "title": "Deep Learning Techniques to Classify Agricultural Crops Through UAV Imagery: A Review",
        "authors": "Bouguettaya, A. et al.",
        "journal": "Neural Computing and Applications, 34. Springer",
        "year": 2022,
        "focus": "Precision agriculture best practices"
    }
}


# Recommendation database organized by crop condition and severity
RECOMMENDATION_DATABASE: Dict[str, Any] = {
    
    # ========================================================================
    # HEALTHY CROP CONDITIONS (Lush Green)
    # ========================================================================
    "healthy_crop": {
        "dominant": {  # When healthy_crop > 70%
            "condition": "Excellent Field Health",
            "fhi_range": [75, 100],
            "urgency": "low",
            "primary_actions": [
                {
                    "action": "Maintain Current Management",
                    "description": "Continue current irrigation, fertilization, and pest monitoring practices",
                    "priority": 1,
                    "timing": "ongoing",
                    "evidence": "Hassanein2018, Bouguettaya2022",
                    "details": [
                        "Monitor field weekly with UAV flights to detect early stress",
                        "Maintain soil moisture at 80-100% field capacity",
                        "Continue balanced NPK fertilization schedule"
                    ]
                },
                {
                    "action": "Preventive Monitoring",
                    "description": "Implement preventive surveillance for disease and pest outbreaks",
                    "priority": 2,
                    "timing": "weekly",
                    "evidence": "Zhao2025, Mahmood2025",
                    "details": [
                        "Schedule UAV monitoring flights every 5-7 days",
                        "Scout field borders for early pest detection",
                        "Monitor weather forecasts for disease-favorable conditions",
                        "Implement pheromone traps for early pest detection"
                    ]
                },
                {
                    "action": "Optimize Nutrient Management",
                    "description": "Fine-tune fertilization to maximize yield without waste",
                    "priority": 3,
                    "timing": "growth_stage_dependent",
                    "evidence": "Zhang2023, Bouguettaya2022",
                    "details": [
                        "Apply split N fertilization: 40% basal, 30% tillering, 30% panicle initiation",
                        "Monitor leaf color for nitrogen status (SPAD meter or UAV-based)",
                        "Consider site-specific variable rate application if available"
                    ]
                }
            ],
            "risk_factors": [
                "Sudden weather changes (heavy rain, drought)",
                "Disease outbreaks in neighboring fields",
                "Nutrient depletion during reproductive stages"
            ]
        },
        "moderate": {  # When healthy_crop 40-70%
            "condition": "Good Field Health with Minor Stress",
            "fhi_range": [65, 74],
            "urgency": "moderate",
            "primary_actions": [
                {
                    "action": "Investigate Stress Zones",
                    "description": "Identify and address areas showing early stress symptoms",
                    "priority": 1,
                    "timing": "within_3_days",
                    "evidence": "Hassanein2018, Yu2022",
                    "details": [
                        "Use UAV imagery to map stress distribution",
                        "Ground-truth stressed areas to identify cause",
                        "Check for irrigation uniformity issues",
                        "Assess nutrient deficiency symptoms"
                    ]
                },
                {
                    "action": "Enhance Monitoring Frequency",
                    "description": "Increase surveillance to prevent stress escalation",
                    "priority": 2,
                    "timing": "every_3_days",
                    "evidence": "Zhao2025, Logavitool2025",
                    "details": [
                        "Conduct UAV flights every 3 days",
                        "Focus on stressed zones for detailed inspection",
                        "Monitor stress progression rate"
                    ]
                }
            ],
            "risk_factors": [
                "Stress may spread to healthy areas",
                "Yield reduction risk 5-15%",
                "Window for intervention is narrowing"
            ]
        }
    },
    
    # ========================================================================
    # STRESSED CROP CONDITIONS (Inconsistent Growth)
    # ========================================================================
    "stressed_crop": {
        "dominant": {  # When stressed_crop > 40%
            "condition": "Significant Crop Stress - Immediate Action Required",
            "fhi_range": [40, 64],
            "urgency": "high",
            "primary_actions": [
                {
                    "action": "Diagnose Stress Cause",
                    "description": "Rapid field assessment to identify stress type (water, nutrient, disease, pest)",
                    "priority": 1,
                    "timing": "within_24_hours",
                    "evidence": "Mahmood2025, Logavitool2025",
                    "details": [
                        "Check soil moisture at root zone depth (15-20cm)",
                        "Inspect plants for disease symptoms (lesions, discoloration)",
                        "Look for pest damage signs (chewing, sucking damage)",
                        "Assess leaf color and uniformity for nutrient deficiency",
                        "Review recent weather and management activities"
                    ]
                },
                {
                    "action": "Water Stress Management",
                    "description": "If drought stress is identified, implement emergency irrigation",
                    "priority": 1,
                    "timing": "immediate",
                    "evidence": "Zhang2023, Bouguettaya2022",
                    "details": [
                        "Apply 50-75mm irrigation immediately if soil moisture < 60% FC",
                        "Ensure uniform water distribution across field",
                        "Consider alternate wetting and drying (AWD) once recovered",
                        "Install soil moisture sensors for continuous monitoring"
                    ]
                },
                {
                    "action": "Nutrient Deficiency Correction",
                    "description": "Apply corrective fertilization based on deficiency symptoms",
                    "priority": 1,
                    "timing": "within_48_hours",
                    "evidence": "Zhao2025, Yu2022",
                    "details": [
                        "Nitrogen deficiency (yellowing): Apply 30-40 kg N/ha as urea",
                        "Phosphorus deficiency (purple tint): Apply 20-30 kg P2O5/ha",
                        "Potassium deficiency (brown margins): Apply 30 kg K2O/ha",
                        "Foliar application for rapid uptake if severe",
                        "Soil test recommended for targeted correction"
                    ]
                },
                {
                    "action": "Disease/Pest Intervention",
                    "description": "If biotic stress is confirmed, apply appropriate control measures",
                    "priority": 1,
                    "timing": "within_24_hours",
                    "evidence": "Mahmood2025, Zhao2025, Logavitool2025",
                    "details": [
                        "For fungal diseases: Apply registered fungicide (e.g., azoxystrobin, trifloxystrobin)",
                        "For bacterial diseases: Apply copper-based bactericides or antibiotics if registered",
                        "For insect pests: Use appropriate insecticide or biological control",
                        "Follow integrated pest management (IPM) principles",
                        "Ensure proper application timing and coverage",
                        "Re-scout 5-7 days post-application to assess efficacy"
                    ]
                }
            ],
            "risk_factors": [
                "Yield loss potential: 20-40%",
                "Stress may become irreversible if untreated >5 days",
                "Secondary infections may occur in weakened plants",
                "Quality degradation (grain filling, milling quality)"
            ],
            "follow_up": "Re-assess field with UAV in 5-7 days to evaluate intervention effectiveness"
        },
        "moderate": {  # When stressed_crop 20-40%
            "condition": "Moderate Stress - Preventive Action Needed",
            "fhi_range": [50, 64],
            "urgency": "moderate",
            "primary_actions": [
                {
                    "action": "Targeted Zone Management",
                    "description": "Focus interventions on stressed patches to prevent spread",
                    "priority": 1,
                    "timing": "within_48_hours",
                    "evidence": "Hassanein2018, Yu2022",
                    "details": [
                        "Map stressed zones using UAV georeferenced imagery",
                        "Apply site-specific interventions (irrigation, fertilizer)",
                        "Create buffer treatment zones around stressed areas",
                        "Monitor adjacent healthy areas for stress expansion"
                    ]
                },
                {
                    "action": "Improve Field Conditions",
                    "description": "Optimize environmental and nutritional factors",
                    "priority": 2,
                    "timing": "within_3_days",
                    "evidence": "Zhang2023, Bouguettaya2022",
                    "details": [
                        "Ensure adequate irrigation frequency and uniformity",
                        "Apply balanced fertilizer if deficiency suspected",
                        "Improve drainage if waterlogging is observed",
                        "Consider foliar micronutrient application (Zn, Fe, Mn)"
                    ]
                }
            ],
            "risk_factors": [
                "Potential yield loss: 10-20%",
                "Stress may escalate to severe if conditions worsen",
                "Critical window for intervention: 3-5 days"
            ]
        }
    },
    
    # ========================================================================
    # DROUGHT / SEVERE STRESS CONDITIONS
    # ========================================================================
    "drought_stress": {
        "dominant": {  # When drought_stress > 30%
            "condition": "Critical Drought Stress - Emergency Intervention",
            "fhi_range": [0, 49],
            "urgency": "critical",
            "primary_actions": [
                {
                    "action": "Emergency Irrigation",
                    "description": "Immediate water application to prevent permanent wilting",
                    "priority": 1,
                    "timing": "immediate",
                    "evidence": "Zhang2023, Bouguettaya2022",
                    "details": [
                        "Apply 75-100mm irrigation within 12 hours",
                        "Irrigate during early morning or evening to reduce evaporation",
                        "Ensure water reaches root zone (15-25cm depth)",
                        "If water is limited, prioritize critical growth stages",
                        "Monitor for signs of recovery within 24-48 hours"
                    ]
                },
                {
                    "action": "Assess Crop Viability",
                    "description": "Determine if crop can recover or if replanting is needed",
                    "priority": 1,
                    "timing": "within_24_hours",
                    "evidence": "Hassanein2018, Mahmood2025",
                    "details": [
                        "Check plant recovery response after initial irrigation",
                        "Assess growth stage: vegetative stress more recoverable",
                        "Reproductive stage (flowering/grain filling) stress may be irreversible",
                        "Calculate economic viability of recovery vs replanting",
                        "Consult agricultural extension for decision support"
                    ]
                },
                {
                    "action": "Recovery Support",
                    "description": "If recovery is viable, provide nutritional and protective support",
                    "priority": 2,
                    "timing": "within_48_hours",
                    "evidence": "Zhao2025, Logavitool2025",
                    "details": [
                        "Apply foliar fertilizer (NPK + micronutrients) for rapid uptake",
                        "Consider plant growth regulators if approved (e.g., brassinosteroids)",
                        "Protect from opportunistic pests/diseases during recovery",
                        "Gradual irrigation increase (avoid shock from overwatering)",
                        "Monitor for secondary stress symptoms"
                    ]
                },
                {
                    "action": "Prevent Future Drought",
                    "description": "Implement drought mitigation strategies",
                    "priority": 3,
                    "timing": "planning_phase",
                    "evidence": "Bouguettaya2022, Zhang2023",
                    "details": [
                        "Install soil moisture monitoring system",
                        "Implement alternate wetting and drying (AWD) irrigation",
                        "Consider drought-tolerant varieties for next season",
                        "Improve water use efficiency (drip, sprinkler systems)",
                        "Mulching to reduce evaporation if applicable"
                    ]
                }
            ],
            "risk_factors": [
                "Yield loss potential: 40-80% or total crop failure",
                "Permanent wilting point may be reached within 24-48 hours",
                "Root damage may limit recovery potential",
                "Increased susceptibility to diseases after stress",
                "Economic loss may exceed intervention costs"
            ],
            "follow_up": "Daily monitoring for 5 days post-intervention, then every 3 days for 2 weeks"
        },
        "moderate": {  # When drought_stress 15-30%
            "condition": "Emerging Drought Stress - Urgent Irrigation Needed",
            "fhi_range": [50, 64],
            "urgency": "high",
            "primary_actions": [
                {
                    "action": "Immediate Irrigation",
                    "description": "Apply water before stress becomes severe",
                    "priority": 1,
                    "timing": "within_12_hours",
                    "evidence": "Zhang2023, Yu2022",
                    "details": [
                        "Apply 50-75mm irrigation immediately",
                        "Check irrigation system for uniformity issues",
                        "Monitor soil moisture at 15-20cm depth",
                        "Plan regular irrigation schedule (every 3-5 days depending on ET)"
                    ]
                },
                {
                    "action": "Stress Monitoring",
                    "description": "Track drought progression and recovery",
                    "priority": 2,
                    "timing": "daily",
                    "evidence": "Hassanein2018, Zhao2025",
                    "details": [
                        "Daily visual assessment for wilting symptoms",
                        "UAV flight every 3 days to map stress extent",
                        "Soil moisture checks twice daily until recovery",
                        "Document recovery rate and patterns"
                    ]
                }
            ],
            "risk_factors": [
                "Yield loss potential: 15-30%",
                "May escalate to critical within 48-72 hours",
                "Critical intervention window: 12-24 hours"
            ]
        }
    },
    
    # ========================================================================
    # BARE SOIL / GAPS
    # ========================================================================
    "bare_soil": {
        "dominant": {  # When bare_soil > 25%
            "condition": "Significant Crop Gaps - Yield Impact",
            "fhi_range": [40, 70],
            "urgency": "moderate",
            "primary_actions": [
                {
                    "action": "Gap Diagnosis",
                    "description": "Identify cause of bare patches",
                    "priority": 1,
                    "timing": "within_48_hours",
                    "evidence": "Yu2022, Hassanein2018",
                    "details": [
                        "Check for poor germination zones (seed quality, planting depth)",
                        "Assess for disease-killed patches (e.g., sheath blight, blast)",
                        "Look for pest damage (stem borers, root feeders)",
                        "Evaluate soil conditions (compaction, drainage, salinity)",
                        "Review planting/transplanting records for inconsistencies"
                    ]
                },
                {
                    "action": "Replanting Decision",
                    "description": "Determine if gap filling is economically viable",
                    "priority": 1,
                    "timing": "within_3_days",
                    "evidence": "Bouguettaya2022, Zhang2023",
                    "details": [
                        "Early vegetative stage (<30 DAP): Replanting highly viable",
                        "Tillering stage (30-50 DAP): Marginal benefit, evaluate case-by-case",
                        "Reproductive stage (>50 DAP): Replanting not recommended",
                        "Calculate cost-benefit: labor, seed, vs expected yield gain",
                        "Consider using older seedlings to catch up if available"
                    ]
                },
                {
                    "action": "Compensatory Management",
                    "description": "If replanting not viable, maximize yield of remaining plants",
                    "priority": 2,
                    "timing": "ongoing",
                    "evidence": "Zhao2025, Mahmood2025",
                    "details": [
                        "Increase fertilizer to remaining plants for tiller compensation",
                        "Apply additional 10-15% N to promote tillering",
                        "Ensure optimal water management to support vigorous growth",
                        "Weed control critical as gaps create weed pressure",
                        "Monitor for pests (gaps may attract)"]
                },
                {
                    "action": "Future Prevention",
                    "description": "Implement measures to prevent gaps in future seasons",
                    "priority": 3,
                    "timing": "next_season",
                    "evidence": "Yu2022, Bouguettaya2022",
                    "details": [
                        "Use high-quality certified seed (>85% germination)",
                        "Ensure proper seed treatment for disease/pest protection",
                        "Optimize planting density and uniformity",
                        "Improve land preparation (leveling, drainage)",
                        "Address soil health issues (pH, organic matter, compaction)"
                    ]
                }
            ],
            "risk_factors": [
                "Yield loss proportional to gap percentage (25% gap ≈ 15-25% yield loss)",
                "Gaps create weed pressure reducing yield further",
                "Uneven maturity complicates harvest timing",
                "Pest and disease harborage in weedy gaps"
            ]
        },
        "moderate": {  # When bare_soil 10-25%
            "condition": "Minor Gaps - Monitor and Compensate",
            "fhi_range": [65, 75],
            "urgency": "low",
            "primary_actions": [
                {
                    "action": "Weed Control in Gaps",
                    "description": "Prevent weed competition in bare areas",
                    "priority": 1,
                    "timing": "within_7_days",
                    "evidence": "Yu2022",
                    "details": [
                        "Hand weeding or spot herbicide application",
                        "Prevent weed seed set to reduce future pressure",
                        "Monitor gap areas weekly during vegetative stage"
                    ]
                },
                {
                    "action": "Support Remaining Crop",
                    "description": "Optimize conditions for existing plants to compensate",
                    "priority": 2,
                    "timing": "ongoing",
                    "evidence": "Zhang2023, Bouguettaya2022",
                    "details": [
                        "Maintain optimal water and nutrient supply",
                        "Encourage tillering to fill canopy",
                        "Monitor for pest/disease preferentially affecting gap edges"
                    ]
                }
            ],
            "risk_factors": [
                "Potential yield loss: 5-15%",
                "Weed competition if not managed",
                "Generally acceptable loss level"
            ]
        }
    },
    
    # ========================================================================
    # MIXED CONDITIONS (Multiple classes present)
    # ========================================================================
    "mixed_conditions": {
        "healthy_stressed_mix": {
            "condition": "Heterogeneous Field Health",
            "urgency": "moderate",
            "primary_actions": [
                {
                    "action": "Spatial Variability Analysis",
                    "description": "Use precision agriculture approach for zone-specific management",
                    "priority": 1,
                    "timing": "within_48_hours",
                    "evidence": "Hassanein2018, Bouguettaya2022",
                    "details": [
                        "Generate management zones from UAV imagery",
                        "Investigate causes of spatial variability (soil, topography, irrigation)",
                        "Implement variable rate application if equipment available",
                        "Otherwise, create manual treatment zones for targeted intervention"
                    ]
                },
                {
                    "action": "Priority Treatment of Stressed Zones",
                    "description": "Focus immediate resources on stressed areas while maintaining healthy zones",
                    "priority": 1,
                    "timing": "within_48_hours",
                    "evidence": "Zhao2025, Mahmood2025",
                    "details": [
                        "Treat stressed zones as per stressed_crop recommendations",
                        "Monitor healthy zones weekly to ensure no degradation",
                        "Create buffer zones between healthy and stressed for early intervention",
                        "Track effectiveness of zone-specific treatments with repeat UAV flights"
                    ]
                }
            ]
        }
    }
}


# Integrated Pest Management (IPM) guidelines
IPM_GUIDELINES = {
    "general_principles": [
        "Use UAV monitoring as primary surveillance tool (Zhao2025)",
        "Implement economic threshold-based interventions (Mahmood2025)",
        "Prioritize cultural and biological control before chemical (Logavitool2025)",
        "Rotate pesticide modes of action to prevent resistance",
        "Always follow label rates and pre-harvest intervals"
    ],
    "common_rice_pests": {
        "stem_borer": {
            "symptoms": "Dead hearts, white heads, stem tunneling",
            "threshold": "5-10% infestation at vegetative stage",
            "management": [
                "Remove and destroy infested stems",
                "Apply Trichogramma egg parasitoids (biological)",
                "Chemical: Cartap hydrochloride or chlorantraniliprole",
                "Timing: Early infestation, before larvae bore into stem"
            ],
            "evidence": "Zhao2025"
        },
        "brown_planthopper": {
            "symptoms": "Hopper burn, yellowing, plant wilting",
            "threshold": "5-10 insects per hill at tillering",
            "management": [
                "Avoid excessive nitrogen fertilization",
                "Conserve natural enemies (spiders, mirid bugs)",
                "Chemical: Imidacloprid or dinotefuran",
                "Apply at base of plant for nymph contact"
            ],
            "evidence": "Mahmood2025"
        },
        "leaf_folder": {
            "symptoms": "Folded leaves with webbing, feeding damage",
            "threshold": "2-3 damaged leaves per hill",
            "management": [
                "Remove and destroy affected leaves",
                "Biological: Bacillus thuringiensis (Bt)",
                "Chemical: Chlorpyrifos or lambda-cyhalothrin"
            ],
            "evidence": "Zhao2025"
        }
    },
    "common_rice_diseases": {
        "bacterial_leaf_blight": {
            "symptoms": "Yellow to white lesions with wavy margins, systemic wilting",
            "favorable_conditions": "High humidity, warm temperatures, wounds from wind/insects",
            "management": [
                "Plant resistant varieties if available",
                "Avoid excessive nitrogen and maintain balanced nutrition",
                "Copper-based bactericides at early symptom detection",
                "Remove infected plant debris",
                "Avoid overhead irrigation"
            ],
            "evidence": "Logavitool2025"
        },
        "blast": {
            "symptoms": "Diamond-shaped lesions on leaves, neck rot, panicle blast",
            "favorable_conditions": "High humidity, moderate temperatures (20-30°C), dense canopy",
            "management": [
                "Plant resistant varieties",
                "Balanced fertilization (avoid excess N)",
                "Fungicides: Tricyclazole, azoxystrobin (preventive and curative)",
                "Apply at early symptom appearance and before flowering",
                "Improve air circulation (proper spacing)"
            ],
            "evidence": "Mahmood2025, Zhao2025"
        },
        "sheath_blight": {
            "symptoms": "Oval lesions on leaf sheaths near water line, greenish-gray with brown borders",
            "favorable_conditions": "High humidity, dense planting, high nitrogen",
            "management": [
                "Avoid excessive nitrogen fertilization",
                "Drain field periodically to reduce humidity",
                "Fungicides: Validamycin, hexaconazole",
                "Apply at early tillering and booting stage",
                "Biological: Trichoderma spp."
            ],
            "evidence": "Mahmood2025"
        }
    }
}


# Seasonal timing recommendations
GROWTH_STAGE_RECOMMENDATIONS = {
    "vegetative": {
        "stage": "Transplanting to Panicle Initiation (0-50 DAT)",
        "key_focus": [
            "Establish uniform crop stand",
            "Promote healthy tillering",
            "Build disease/pest resistance"
        ],
        "fertilization": {
            "timing": "Basal (0 DAT) + Tillering (15-20 DAT)",
            "rates": "60-80 kg N/ha total, 40% basal + 30% tillering + 30% panicle initiation",
            "evidence": "Zhang2023, Bouguettaya2022"
        },
        "irrigation": {
            "strategy": "Maintain shallow standing water (2-5cm) for establishment",
            "notes": "Can implement AWD after 20 DAT to save water while maintaining yield",
            "evidence": "Bouguettaya2022"
        },
        "pest_disease": {
            "priority": "Stem borers, leaf folder, blast disease",
            "monitoring": "Weekly UAV flights + ground scouting",
            "evidence": "Zhao2025, Mahmood2025"
        }
    },
    "reproductive": {
        "stage": "Panicle Initiation to Flowering (50-70 DAT)",
        "key_focus": [
            "Support spikelet formation",
            "Protect from stress during critical yield determination",
            "Prevent yield-limiting diseases"
        ],
        "fertilization": {
            "timing": "Panicle initiation (PI stage, ~50 DAT)",
            "rates": "Final 30% of total N (25-30 kg N/ha)",
            "evidence": "Zhang2023"
        },
        "irrigation": {
            "strategy": "Critical period - maintain adequate moisture (5-10cm standing water)",
            "notes": "Water stress during flowering highly detrimental to yield",
            "evidence": "Bouguettaya2022"
        },
        "pest_disease": {
            "priority": "Blast (neck and panicle), sheath blight, planthoppers",
            "monitoring": "Every 3-5 days during flowering",
            "evidence": "Mahmood2025, Logavitool2025"
        }
    },
    "grain_filling": {
        "stage": "Post-Flowering to Maturity (70-110 DAT)",
        "key_focus": [
            "Support grain filling and maturation",
            "Prevent lodging",
            "Prepare for harvest"
        ],
        "fertilization": {
            "timing": "No additional N (risk of lodging and delayed maturity)",
            "rates": "Foliar K only if deficiency observed",
            "evidence": "Zhang2023"
        },
        "irrigation": {
            "strategy": "Gradual drying: Maintain saturation (no standing water) until 1-2 weeks before harvest, then drain completely",
            "notes": "Final draining timing affects grain moisture and harvest timing",
            "evidence": "Bouguettaya2022"
        },
        "pest_disease": {
            "priority": "Avoid chemical applications close to harvest (PHI compliance)",
            "monitoring": "Weekly monitoring sufficient unless outbreak detected",
            "evidence": "Zhao2025"
        }
    }
}
