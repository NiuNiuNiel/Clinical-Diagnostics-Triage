import sys
import json

upload_material = json.loads(sys.argv[1])
patient_info = json.loads(sys.argv[2])

mock_triage_url = "http://example.org/fhir/StructureDefinition/triage-priority"
mock_flagged_review_url = "http://example.org/fhir/StructureDefinition/flagged-for-review"


status = upload_material.get("flagged_for_human_review")

pay_load = {
    "resourceType": "ClinicalImpression",
    "status": "Incomplete" if status else "Completed",
    "subject": {
        "reference": patient_info.get("ID"),
        "display": patient_info.get("Name")
      },
    "description": upload_material.get("clinical_summary"),
    "summary": upload_material.get("triage_reason"),
    "finding": [
        {
          "itemCodeableConcept": {
            "text": upload_material.get("recommended_action") 
          }
        }
      ],
    "extension": [
        {
          "url": mock_triage_url,
          "valueInteger": upload_material.get("triage_priority")
        },
        {
          "url": mock_flagged_review_url,
          "valueBoolean": upload_material.get("flagged_for_human_review") 
        }
      ]
    }

if not status:
    investiagtions = []

    for ai_node_result in upload_material.get("AI_node_results", []):
        pass
        investiagtions.append(investigations)

    pay_load["investigation"] = investiagtions