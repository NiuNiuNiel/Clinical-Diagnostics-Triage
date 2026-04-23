from tensorflow.keras.models import load_model
import pandas as pd
import numpy as np
import sys
import zai
from PIL import Image
import requests
import json
from operator import itemgetter
import time
import datetime
import os
import PyPDF2
import docx


def activity_logger(ID, _type, details):
    os.makedirs("Activity_Log", exist_ok=True)
    
    file_path = f"Activity_Log/{datetime.date.today()}.csv"
    
    line_count = 0
    if os.path.exists(file_path):
        with open(file_path, "r") as log_file:
            line_count = len(log_file.readlines())
            
    activity_ID = f"{int(time.time())}_{line_count + 2}_{ID}"
    
    with open(file_path, "a+") as log_file:
        log_file.write(f"{activity_ID}, {_type}, {details}\n")

    if _type == "AI_Node_Execution":
        return activity_ID

def read_file(file_path, headers):
    file_type = file_path.split(".")[-1].lower()

    if file_type == "csv":
        return pd.read_csv(file_path, header=headers)
    elif file_type == "xlsx":
        return pd.read_excel(file_path, header=headers)
    elif file_type == "tsv":
        return pd.read_csv(file_path, sep="\t", header=headers)
    elif file_type == "json":
        return pd.read_json(file_path)
    elif file_type in ["png", "jpg", "jpeg"]:
        image = Image.open(file_path)
        return np.asarray(image)
    elif file_type == "txt":
        with open(file_path, "r", encoding="utf-8") as f:
            return f.read()
    elif file_type == "pdf":
        text = ""
        with open(file_path, "rb") as f:
            reader = PyPDF2.PdfReader(f)
            for page in reader.pages:
                extracted = page.extract_text()
                if extracted:
                    text += extracted + "\n"
        return text
        
    elif file_type == "docx":
        doc = docx.Document(file_path)
        return "\n".join([para.text for para in doc.paragraphs])
        
    elif file_type == "doc":
        raise ValueError("Legacy .doc files are not supported. Please use .docx or .pdf.")
    else:
        raise ValueError("Unsupported file type")

def modification_func(data, modification):
    if modification == "forward_fill":
        return data.ffill()
    elif modification == "fill_zero":
        return data.fillna(0)
    elif modification == "drop_missing":
        return data.dropna()
    elif modification == None:
        return data
    else:
        raise ValueError("Unsupported modification type")

def AI_node(model_name, modifications, file_path, headers):
    model = load_model(model_name + ".keras")

    file_data = read_file(file_path, headers=headers)

    if modifications != None:
        for modification in modifications:
            file_data = modification_func(file_data, modification)

    file_name = file_path.split("/")[-1]

    result = {
        "file_name": file_name,
        "model_name": model_name,
        "reference_ID": f"{model_name}_Result"
        }

    if file_path.split(".")[-1].lower() in ["png", "jpg", "jpeg"]:
        prediction = model.predict(file_data.reshape(1, file_data.shape[0], file_data.shape[1], file_data.shape[2]))[0]
        result["result"] = {"Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"}
        return result

    if len(file_data) == 1:
        prediction = model.predict(file_data.to_numpy().reshape(1, -1, 1))[0]
        result["result"] = {"Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"}
        return result

    predictions = model.predict(file_data.values)
    result["result"] = [{"Index":index, "Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"} for index,prediction in enumerate(predictions)]

    return result



user_prompt = sys.argv[1]

file_as_prompt = sys.argv[2]

if file_as_prompt != "None":
    user_prompt += "\n" + read_file(file_as_prompt, headers=None)


files_attached = sys.argv[3:]

url = "https://api.z.ai/api/paas/v4/files"
mime_map = {
  "png": "image/png",
  "jpeg": "image/jpeg",
  "jpg": "image/jpeg",
  "doc": "application/msword",
  "docx": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "txt": "text/plain",
  "pdf": "application/pdf",
  "csv": "text/csv",
  "tsv": "text/tab-separated-values",
  "json": "application/json"
}


with open("API_key.txt", "r") as f:
    API_key = f.read().strip()

headers = {"Authorization": f"Bearer {API_key}"}

file_IDs = []

if files_attached[0] != "None":
    for file in files_attached:
        file_type = file.split(".")[-1].lower()

        with open(file, "rb") as f:
            files = {
                "file": (file, f, mime_map.get(file_type))
            }
            data = {
                "purpose": "agent"
            }
    
            response = requests.post(url, headers=headers, files=files, data=data)

        file_IDs.append(response.json()["id"])

GLM_model = zai.ZAI(api_key=API_key)

classes = {"Heartbeat_Abnormality_Model" : ['Normal', 'Supraventricular Ectopic Beats', 'Ventricular Ectopic Beats', 'Fusion Beats', 'Unknown'],
           "Chest_XRay_Vision_Model" : ['Normal', 'Pneumonia'],
           }


# Model Choosing
model_choosing_context = """
You are the routing agent for an intelligent Clinical Diagnostics Triage Copilot. 
Your primary task is to analyze the user's clinical notes and attached files (if any) to determine if specialized AI diagnostic models need to be executed before making a final triage decision.

Available Models:
1. "Heartbeat_Abnormality_Model": 
   - Data Type: 1D signal data (e.g., .csv, .tsv containing ECG readings).
   - Purpose: Detects cardiac arrhythmias.
   - Classes Detected: "Normal", "Supraventricular Ectopic Beats", "Ventricular Ectopic Beats", "Fusion Beats", "Unknown".
   - Routing Logic: Trigger this model if a tabular file is attached and clinical notes mention heart-related symptoms (palpitations, chest pain, irregular rhythm, ECG/EKG).
   
2. "Chest_XRay_Vision_Model": 
   - Data Type: 2D medical imaging (e.g., .png, .jpg, .jpeg chest X-rays).
   - Purpose: Detects the presence of pneumonia from chest radiographs.
   - Classes Detected: "Normal", "Pneumonia". 
   - Routing Logic: Trigger this model if an image file is attached and clinical notes mention respiratory issues (severe cough, fever, shortness of breath, lung imaging).

Review the user's request and the attached files. If a file corresponds to an available model, you must generate an AI node for it.

You MUST respond ONLY with a valid JSON object matching this schema exactly:
{
    "response_summary": "string",
    "requires_model": boolean,
    "AI_nodes": [
        {
            "model_name": "string",
            "modifications": ["string"] or None,
            "file_path": "string",
            "headers": integer or None
        }
    ]
}

Each field is defined as follows:
- "response_summary": A concise summary of the user's request and the intended action.
- "requires_model": A boolean indicating whether a model is required for the task.
    - True if there is a suitable AI node.
    - False if there is no suitable AI nodes or no files has been attached.
- "AI_nodes": A list of objects, where each object represents an individual processing task. This allows the system to process multiple files or apply different models in a single request. Each object in the list must contain the specific configuration for that file.
- "model_name": The name of the model to be used for this specific node.
- "modifications": A list of modifications to be applied on the data.
    - For Tabular/Signal data (csv,tsv,json,...): 
        - "forward_fill" to fill missing values with the last valid observation,
        - "fill_zero" to fill missing values with zero,
        - "drop_missing" to drop rows with missing values.
        - None if no cleaning is needed.
    - For Image data (png,jpg,jpeg): Set to None (the backend automatically handles standard tensor reshaping)
- "file_path": The path of the file being processed in this node.
- "headers": An integer indicating the row number to be used as headers for this file, or None if there are no headers.
"""

try:
    if len(file_IDs)  == 0:
        model_choosing_response = GLM_model.chat.completions.create(
            model="glm-5.1",
            messages=[
                {"role": "system", "content": model_choosing_context},
                {"role": "user", "content": f"User Prompt: {user_prompt}"}
            ],
            stream=False
        )
    else:
        model_choosing_response = GLM_model.chat.completions.create(
            model="glm-5.1",
            messages=[
                {"role": "system", "content": model_choosing_context},
                {"role": "user", "content": f"User Prompt: {user_prompt}"}
            ],
            tools=[
                {
                    "type": "retrieval",
                    "retrieval": {
                        "file_ids": file_IDs 
                    }
                }
            ],
            stream=False
        )

    activity_logger(ID="Model_Choosing", _type="Model_Choosing_API_Call", details=f"Model choosing response: {model_choosing_response.choices[0].message.content}")
except zai.ZAIError.TokenLimitError as e:
    raise RuntimeError("Token limit exceeded") from e
except Exception as e:
    raise RuntimeError("API call failed") from e

model_choosing = json.loads(model_choosing_response.choices[0].message.content)

print(model_choosing.get("response_summary"))

if model_choosing["requires_model"]:
    print("Utilizing AI nodes...")
    AI_nodes_results = []
    for node in model_choosing["AI_nodes"]:
        result = AI_node(model_name=node["model_name"], modifications=node["modifications"], file_path=node["file_path"], headers=node["headers"])
        result["reference_ID"] = activity_logger(ID=result["reference_ID"], _type="AI_Node_Execution", details=f"Executed {node['model_name']} on {node['file_path']} with result: {result}")
        AI_nodes_results.append(result)
        


# Notes and Predictions (if any) analysis
print(f"Analysing Clinical Notes {f'together with AI Nodes Results' if model_choosing['requires_model'] else ''} ...")

analysis_context = f"""
You are the final synthesis agent for a Clinical Diagnostics Triage Copilot.
{'''
Your task is to correlate the physician's original clinical notes with the automated findings from specialized AI diagnostic models.

Rules for Triage:
1. Compare the AI's "Prediction" and "Confidence Score" with the patient's reported symptoms.
2. If the AI detects an abnormality with atleast 90% confidence and symptoms align, escalate the priority.
3. If the AI results are contradictory, ambiguous, or the confidence score is lower than 90%, flag the record for human review.
''' if model_choosing["requires_model"] else
'''
Analyze the physician's notes. Based on the symptoms and information provided, determine the appropriate triage priority.
'''}

Original Physician Notes: {user_prompt}
"""

if model_choosing["requires_model"]:
    node_summary = "\n".join([f"{n['file_name']}: {n['result']}" for n in AI_nodes_results])
    analysis_context += f"\n\nAutomated AI Node Findings:\n{node_summary}"

analysis_context += """
\nBased on all provided information, you MUST output a final triage decision as a valid JSON object representing an Electronic Health Record (EHR) payload matching this exact schema:
{
    "triage_priority": integer,
    "clinical_summary": "string",
    "recommended_action": "string",
    "triage_reason": "string",
    "flagged_for_human_review": boolean
}

Each field is defined as follows:
- "triage_priority": An integer range from 1 to 5, where 1 indicates the highest priority for immediate attention and 5 indicates the lowest priority for non-urgent cases.
- "clinical_summary": A brief summary of the patient's condition, including key symptoms, relevant medical history, and any critical information that would assist healthcare professionals in understanding the patient's situation quickly and effectively.
- "recommended_action": A clear and concise recommendation for the next steps that medical staff should take based on the analysis of the patient's condition and the AI findings. This could include actions such as "Immediate hospitalization", "Schedule follow-up appointment", "Order additional tests", or "Provide home care instructions".
- "triage_reason": A clear explanation of the rationale behind the triage decision. This should synthesize the patient's reported symptoms with the automated AI node findings, explicitly stating *why* a specific priority was assigned (e.g., "The clinical notes indicate acute chest pain, and the Heartbeat_Abnormality_Model confirmed Ventricular Ectopic Beats with 98% confidence, necessitating immediate escalation.").
- "flagged_for_human_review": A boolean value indicating whether the case should be flagged for human review due to ambiguous or contradictory AI findings.
"""

try:
    if len(file_IDs)  == 0:
        Analysis_response = GLM_model.chat.completions.create(
        model="glm-5.1",
            messages=[
                {"role": "system", "content": analysis_context}
            ],
            stream=False
        )
    else:
        Analysis_response = GLM_model.chat.completions.create(
            model="glm-5.1",
            messages=[
                {"role": "system", "content": analysis_context}
            ],
            tools=[
                {
                    "type": "retrieval",
                    "retrieval": {
                        "file_ids": file_IDs 
                    }
                }
            ],
            stream=False
        )

    activity_logger(ID="Final_Analysis", _type="Final_Analysis_API_Call", details=f"Final analysis response: {Analysis_response.choices[0].message.content}")
except zai.ZAIError.TokenLimitError as e:
    raise RuntimeError("Token limit exceeded") from e
except Exception as e:
    raise RuntimeError("API call failed") from e

Analysis = json.loads(Analysis_response.choices[0].message.content)

if model_choosing["requires_model"]:
    Analysis["AI_nodes_results"] = AI_nodes_results

print(json.dumps(Analysis))