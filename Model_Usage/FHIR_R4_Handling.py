import sys
import json
import os
import datetime
import time


upload_material = json.loads(sys.argv[1])
patient_info = json.loads(sys.argv[2])

mock_triage_url = "http://example.org/fhir/StructureDefinition/triage-priority"
mock_flagged_review_url = "http://example.org/fhir/StructureDefinition/flagged-for-review"


status = upload_material.get("flagged_for_human_review")

pay_load = {
    "resourceType": "ClinicalImpression",
    "status": "in-progress" if status else "completed",
    "subject": {
        "reference": f"Patient/{patient_info.get('ID')}",
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

if upload_material.get("AI_nodes_results"):
    items = []

    for ai_node_result in upload_material.get("AI_nodes_results"):
        item = {
          "reference": ai_node_result.get("reference_ID"),
          "display": f"{ai_node_result.get('model_name')}: {ai_node_result.get('result')}"
        }
        items.append(item)

    pay_load["investigation"] = [
            {
                "code": {
                    "text": "Automated AI Diagnostic Nodes"
                },
                "item": items
            }
        ]

def mock_push_to_fhir_server(payload):
    print(json.dumps(payload))


mock_push_to_fhir_server(pay_load)

current_dir = os.path.dirname(os.path.abspath(__file__))
repo_root = os.path.abspath(os.path.join(current_dir, "..", "..", "..", ".."))

activity_log_dir = os.path.join(repo_root, 'Activity_Log')
os.makedirs(activity_log_dir, exist_ok=True)
    
file_path = os.path.join(activity_log_dir, f"{datetime.date.today()}.tsv")
    
line_count = 0
if os.path.exists(file_path):
    with open(file_path, "r") as log_file:
        line_count = len(log_file.readlines())
            
activity_ID = f"{int(time.time())}_{line_count + 2}_Mock_FHIR_R4_Pushing"
    
with open(file_path, "a+") as log_file:
    log_file.write(f"{activity_ID}\tPushing Mock FHIR R4\t{json.dumps(pay_load)}\n")