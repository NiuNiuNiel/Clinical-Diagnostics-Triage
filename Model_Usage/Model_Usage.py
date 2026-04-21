from tensorflow.keras.models import load_model
import pandas as pd
import numpy as np
import ast
import sys
import zai
from PIL import Image



user_prompt = sys.argv[1]
files_attached = sys.argv[2:]
with open("API_key.txt", "r") as f:
    API_key = f.read().strip()

GLM_model = zai.ZAI(api_key=API_key)

classes = {"Heartbeat_Abnormality_Model" : ["Normal", 'Supraventricular Ectopic Beats', 'Ventricular Ectopic Beats', 'Fusion Beats', 'Unknown'],
           }


# Model Choosing
model_choosing_context = """
You are the routing agent for an intelligent Clinical Diagnostics Triage Copilot. 
Your primary task is to analyze the user's clinical notes and attached files to determine if specialized AI diagnostic models need to be executed before making a final triage decision.

Available Models:
1. "Heartbeat_Abnormality_Model": 
   - Data Type: 1D signal data (e.g., .csv, .tsv containing ECG readings).
   - Purpose: Detects cardiac arrhythmias.
   - Classes Detected: "Normal", "Supraventricular Ectopic Beats", "Ventricular Ectopic Beats", "Fusion Beats", "Unknown".
   - Routing Logic: Trigger this model if a tabular file is attached and clinical notes mention heart-related symptoms (palpitations, chest pain, irregular rhythm, ECG/EKG).
   
2. "Chest_XRay_Vision_Model": 
   - Data Type: 2D medical imaging (e.g., .png, .jpg, .jpeg chest X-rays).
   - Purpose: Detects pulmonary abnormalities from chest radiographs.
   - Classes Detected: "Normal", "Pneumonia", "Pleural Effusion". 
   - Routing Logic: Trigger this model if an image file is attached and clinical notes mention respiratory issues (severe cough, fever, shortness of breath, lung imaging). 

Review the user's request and the attached files. If a file corresponds to an available model, you must generate an AI node for it.

You MUST respond ONLY with a valid JSON object matching this schema exactly:
{
    "response_summary": "string",
    "requires_model": boolean,
    "AI_nodes": [
        {
            "model_name": "string",
            "modification": "string" or None,
            "file_path": "string",
            "headers": integer or None
        }
    ]
}

Each field is defined as follows:
- "response_summary": A concise summary of the user's request and the intended action.
- "requires_model": A boolean indicating whether a model is required for the task.
- "AI_nodes": A list of objects, where each object represents an individual processing task. This allows the system to process multiple files or apply different models in a single request. Each object in the list must contain the specific configuration for that file.
- "model_name": The name of the model to be used for this specific node.
- "modification": Any modifications to be applied on the data.
    - For Tabular/Signal data (csv,tsv,json,...): Choose ONLY from "forward_fill", "fill_zero", "drop_missing", or None if no cleaning is needed.
    - For Image data (png,jpg,jpeg): Set to None (the backend automatically handles standard tensor reshaping)
- "file_path": The path of the file being processed in this node.
- "headers": An integer indicating the row number to be used as headers for this file, or null if there are no headers.
"""

model_choosing_response = GLM_model.chat.completions.create(
    model="glm-5.1",
    messages=[
        {"role": "system", "content": model_choosing_context},
        {"role": "user", "content": f"User Prompt: {user_prompt}\nAttached Files: {files_attached}"}
    ],
    stream=False
)

model_choosing = ast.literal_eval(model_choosing_response.choises[0].message.content)

print(model_choosing.get("response_summary"))

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
    else:
        raise ValueError("Unsupported file type")

def modification_func(data, modification):
    if modification == "forward_fill":
        return data.ffill()
    elif modification == "fill_zero":
        return data.fillna(0)
    elif modification == "drop_missing":
        return data.dropna()
    else:
        raise ValueError("Unsupported modification type")

def AI_node(model_name, modification, file_path, headers):
    model = load_model(model_name + ".keras")

    file_data = read_file(file_path, headers=headers)

    if modification != None:
        file_data = modification_func(file_data, modification)

    file_name = file_path.split("/")[-1]

    if file_path.split(".")[-1].lower() in ["png", "jpg", "jpeg"]:
        return

    if len(file_data) == 1:
        prediction = model.predict(file_data.to_numpy().reshape(1, -1, 1))[0]
        return {file_name:{"Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"}}

    predictions = model.predict(file_data.values)
    return {file_name:[{"Index":index, "Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"} for index,prediction in enumerate(predictions)]}

print("Utilizing AI nodes...")

if model_choosing["requires_model"]:
    AI_nodes_results = []
    for node in model_choosing["AI_nodes"]:
        AI_nodes_results.append(AI_node(model_name=node["model_name"], modification=node["modification"], file_path=node["file_path"], headers=node["headers"]))



# Notes and Predictions (if any) analysis
print(f"Analysing Clinical Notes {f'together with AI Nodes Results' if model_choosing["requires_model"] else ''} ...")

analysis_context = f"""
You are the final synthesis agent for a Clinical Diagnostics Triage Copilot. 
Your task is to correlate the physician's original clinical notes with the automated findings from specialized AI diagnostic models.

Rules for Triage:
1. Compare the AI's "Prediction" and "Confidence Score" with the patient's reported symptoms.
2. If the AI detects an abnormality with high confidence and symptoms align, escalate the priority.
3. If the AI results are contradictory, ambiguous, or the confidence score is low, flag the record for human review.

Original Physician Notes: {user_prompt}
Attached Files: {files_attached}
"""

if model_choosing["requires_model"]:
    analysis_context += f"\n\nAutomated AI Node Findings:\n{AI_nodes_results}"

analysis_context += """
\nBased on all provided information, you MUST output a final triage decision as a valid JSON object representing an Electronic Health Record (EHR) payload matching this exact schema:
{
    "triage_priority": "string",
    "clinical_summary": "string",
    "recommended_action": "string",
    "flagged_for_human_review": boolean
}

Each field is defined as follows:
- "triage_priority": An integer range from 1 to 5, where 1 indicates the highest priority for immediate attention and 5 indicates the lowest priority for non-urgent cases.
- "clinical_summary": A brief summary of the patient's condition, including key symptoms, relevant medical history, and any critical information that would assist healthcare professionals in understanding the patient's situation quickly and effectively.
- "recommended_action": A clear and concise recommendation for the next steps that medical staff should take based on the analysis of the patient's condition and the AI findings. This could include actions such as "Immediate hospitalization", "Schedule follow-up appointment", "Order additional tests", or "Provide home care instructions".
- "flagged_for_human_review": A boolean value indicating whether the case should be flagged for human review due to ambiguous or contradictory AI findings.
"""

Analysis_response = GLM_model.chat.completions.create(
    model="glm-5.1",
    messages=[
        {"role": "system", "content": analysis_context}
    ],
    stream=False
)

Analysis = ast.literal_eval(Analysis_response.choises[0].message.content)

print(Analysis)