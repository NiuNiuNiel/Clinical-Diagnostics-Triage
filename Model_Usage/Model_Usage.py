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
from pathlib import Path
from transformers import AutoModelForSeq2SeqLM, AutoTokenizer
import google.generativeai as genai



def activity_logger(ID, _type, details):
    activity_log_dir = os.path.join(repo_root, 'Activity_Log')
    os.makedirs(activity_log_dir, exist_ok=True)
    
    file_path = os.path.join(activity_log_dir, f"{datetime.date.today()}.tsv")
    
    line_count = 0
    if os.path.exists(file_path):
        with open(file_path, "r") as log_file:
            line_count = len(log_file.readlines())
            
    activity_ID = f"{int(time.time())}_{line_count + 2}_{ID}"
    
    with open(file_path, "a+") as log_file:
        log_file.write(f"{activity_ID}\t{_type}\t{details}\n")

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
        image = Image.open(file_path).convert("RGB")
        image = image.resize((224, 224))
        
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
        activity_logger(ID="File_Reading", _type="Unsupported_File_Type", details=f"Attempted to read unsupported file type: {file_type} for file {file_path}")
        raise ValueError("Legacy .doc files are not supported. Please use .docx or .pdf.")
    else:
        activity_logger(ID="File_Reading", _type="Unsupported_File_Type", details=f"Attempted to read unsupported file type: {file_type} for file {file_path}")
        raise ValueError("Unsupported file type")

def modification_func(data, modification):
    if modification == "forward_fill":
        return data.ffill()
    elif modification == "fill_zero":
        return data.fillna(0)
    elif modification == "drop_missing":
        return data.dropna()
    elif modification is None:
        return data
    else:
        raise ValueError("Unsupported modification type")

def AI_node(model_name, modifications, file_path, headers):

    if model_name == "Clinical_Note_Summarization_Model":
        model_path = os.path.join(models_dir, "Clinical_Note_Summarization_Model")
        tokenizer = AutoTokenizer.from_pretrained(model_path)
        model = AutoModelForSeq2SeqLM.from_pretrained(model_path)

        inputs = tokenizer(file_path, return_tensors="pt", max_length=512, truncation=True)

        # Generate a summary
        summary_ids = model.generate(inputs["input_ids"], max_length=200)
        summary = tokenizer.decode(summary_ids[0], skip_special_tokens=True)

        return summary

    keras_model_path = os.path.join(models_dir, f"{model_name}.keras")
    model = load_model(keras_model_path)

    file_data = read_file(file_path, headers=headers)

    if modifications is not None:
        for modification in modifications:
            file_data = modification_func(file_data, modification)

    file_name = file_path.split("/")[-1]

    result = {
        "file_name": file_name,
        "model_name": model_name,
        "reference_ID": f"{model_name}_Result"
        }

    if file_path.split(".")[-1].lower() in ["png", "jpg", "jpeg"]:
        tensor = file_data.reshape(1, file_data.shape[0], file_data.shape[1], file_data.shape[2])
        
        prediction = model.predict(tensor)[0]
        result["result"] = {"Prediction": classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score": f"{np.max(prediction, axis=-1)*100:.2f}%"}
        return result

    if len(file_data) == 1:
        prediction = model.predict(file_data.to_numpy().reshape(1, -1, 1))[0]
        result["result"] = {"Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"}
        return result

    predictions = model.predict(file_data.values)
    result["result"] = [{"Index":index, "Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"} for index,prediction in enumerate(predictions)]

    return result

def upload_files(files):
    uploaded_file_objects = []
    for file in files:
        try:
            # The SDK handles uploading and storage automatically
            uploaded_file = genai.upload_file(path=file)
            uploaded_file_objects.append(uploaded_file)
        except Exception as e:
            error_msg = f"Gemini Upload Failed for {os.path.basename(file)}: {e}"
            activity_logger(ID="File_Upload", _type="Upload_Error", details=error_msg)
            raise Exception(error_msg)
            
    return uploaded_file_objects

def estimate_token(text, input_type):
    if input_type == "string":
        return len(text) / 4
    elif input_type in ["csv", "tsv", "json", "docx", "pdf"]:
        return Path(text).stat().st_size / 4
    else:
        return 1445

current_dir = os.path.dirname(os.path.abspath(__file__))
repo_root = os.path.abspath(os.path.join(current_dir, "..", "..", "..", ".."))

api_key_path = os.path.join(repo_root, "Model_Usage", "api_key.txt")
models_dir = os.path.join(repo_root, "Model_Usage", "Models")

context_window = 200000

user_prompt = sys.argv[1]
file_as_prompt = sys.argv[2]
files_attached = sys.argv[3:]

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


with open(api_key_path, "r") as f:
    API_key = f.read().strip()

headers = {"Authorization": f"Bearer {API_key}"}

genai.configure(api_key=API_key)

model_name_gemini = "gemini-2.5-flash"

file_objects = upload_files(files_attached) if files_attached[0] != "None" else []

classes = {"Heartbeat_Abnormality_Model" : ['Normal', 'Supraventricular Ectopic Beats', 'Ventricular Ectopic Beats', 'Fusion Beats', 'Unknown'],
           "Chest_XRay_Vision_Model" : ['Normal', 'Pneumonia'],
           }

if file_as_prompt != "None":
    if file_as_prompt.split(".")[-1].lower() in ["png", "jpg", "jpeg"]:
        print("<Thinking Process>User attached image for clinical note, converting via OCR.</Thinking Process>")

        file_as_prompt_objects = upload_files([file_as_prompt])

        try:
            # We initialize a temporary model just for OCR
            ocr_model = genai.GenerativeModel(model_name_gemini)
            
            ocr_prompt = "You are an OCR model that reads attached hand written clinical note images and return the exact information in one paragraph text without any interpretation. If the note is too messy and can't be converted into a text reply '<<flagged_for_human_review>>'"
            
            # Pass the prompt AND the file object directly in a list
            converted_prompt = ocr_model.generate_content([ocr_prompt, file_as_prompt_objects[0]])
            
            ocr_text = converted_prompt.text.strip()
            
        except Exception as e:
            activity_logger(ID="OCR_Node", _type="OCR_Node_API_Error", details=f"API call failed: {e}")
            raise RuntimeError(f"API call failed: {e}")

        if converted_prompt.choices[0].message.content.strip() == "<<flagged_for_human_review>>":
            activity_logger(ID="OCR_Node", _type="OCR_Node_Flagged_Human_Review", details="The attached clinical note image was flagged for human review due to poor quality.")
            raise RuntimeError("The attached clinical note image is too messy to be converted into text, flagged for human review.")

        user_prompt += "\n" + ocr_text
    else:
        user_prompt += "\n" + read_file(file_as_prompt, headers=None)



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

file_tokens = sum([estimate_token(f, f.split(".")[-1].lower()) for f in files_attached]) if files_attached[0] != "None" else 0

if estimate_token(model_choosing_context, "string") + estimate_token(user_prompt, "string") + file_tokens + 1000 > context_window:
    print("<Thinking Process>Prompt exceeds context window, utilizing Summarization Model.</Thinking Process>")
    user_prompt = AI_node(file_path=user_prompt, model_name="Clinical_Note_Summarization_Model", modifications=None, headers=None)


try:
    # Initialize the model with the System Instruction and force JSON output
    routing_model = genai.GenerativeModel(
        model_name=model_name_gemini,
        system_instruction=model_choosing_context,
        generation_config=genai.GenerationConfig(response_mime_type="application/json")
    )
    
    # Combine the text prompt and the uploaded file objects into one payload
    prompt_payload = [f"User Prompt: {user_prompt}"]
    if len(file_objects) > 0:
        file_names_text = "Attached Files:\n"
        for local_path in files_attached:
            if local_path != "None":
                file_names_text += f"- {os.path.basename(local_path)}\n"
        
        prompt_payload.append(file_names_text)
        prompt_payload.extend(file_objects)

    model_choosing_response = routing_model.generate_content(prompt_payload)
    
    # No need to remove ```json markdown anymore!
    raw_json_response = model_choosing_response.text 
    
    activity_logger(ID="Model_Choosing", _type="Model_Choosing_API_Call", details=f"Response: {raw_json_response}")
    
except Exception as e:
    activity_logger(ID="Model_Choosing", _type="Model_Choosing_API_Error", details=f"API call failed: {e}")
    raise RuntimeError("API call failed") from e

model_choosing = json.loads(raw_json_response)

print("<Thinking Process>" + model_choosing.get("response_summary") + "</Thinking Process>")

if model_choosing["requires_model"]:
    print("<Thinking Process>Utilizing AI nodes...</Thinking Process>")
    file_map = {os.path.basename(f): f for f in files_attached if f != "None"}
    
    AI_nodes_results = []
    for node in model_choosing["AI_nodes"]:
        
        llm_filename = os.path.basename(node["file_path"])
        
        actual_absolute_path = file_map.get(llm_filename, node["file_path"])
        
        result = AI_node(model_name=node["model_name"], modifications=node["modifications"], file_path=actual_absolute_path, headers=node["headers"])
        
        result["reference_ID"] = activity_logger(ID=result["reference_ID"], _type="AI_Node_Execution", details=f"Executed {node['model_name']} on {actual_absolute_path} with result: {result}")
        AI_nodes_results.append(result)
        


# Notes and Predictions (if any) analysis
print(f"<Thinking Process>Analysing Clinical Notes {f'together with AI Nodes Results' if model_choosing['requires_model'] else ''} ...</Thinking Process>")

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
- "triage_priority": 1 = highest urgency, 4 = lowest non-urgent, 5 = reserved for human review flagging only.
- "clinical_summary": A brief summary of the patient's condition, including key symptoms, relevant medical history, and any critical information that would assist healthcare professionals in understanding the patient's situation quickly and effectively.
- "recommended_action": A clear and concise recommendation for the next steps that medical staff should take based on the analysis of the patient's condition and the AI findings. This could include actions such as "Immediate hospitalization", "Schedule follow-up appointment", "Order additional tests", or "Provide home care instructions".
- "triage_reason": A clear explanation of the rationale behind the triage decision. This should synthesize the patient's reported symptoms with the automated AI node findings, explicitly stating *why* a specific priority was assigned (e.g., "The clinical notes indicate acute chest pain, and the Heartbeat_Abnormality_Model confirmed Ventricular Ectopic Beats with 98% confidence, necessitating immediate escalation.").
- "flagged_for_human_review": A boolean value indicating whether the case should be flagged for human review due to ambiguous or contradictory AI findings.
"""

try:
    # Initialize the analysis model, forcing JSON output
    analysis_model = genai.GenerativeModel(
        model_name=model_name_gemini,
        system_instruction=analysis_context,
        generation_config=genai.GenerationConfig(response_mime_type="application/json")
    )

    # Attach the context and files
    analysis_payload = [f"Original Physician Notes: {user_prompt}"]
    if len(file_objects) > 0:
        analysis_payload.extend(file_objects)

    Analysis_response = analysis_model.generate_content(analysis_payload)
    
    raw_analysis_json = Analysis_response.text

    activity_logger(ID="Final_Analysis", _type="Final_Analysis_API_Call", details=f"Final response: {raw_analysis_json}")
    
except Exception as e:
    activity_logger(ID="Final_Analysis", _type="Final_Analysis_API_Error", details=f"API call failed: {e}")
    raise RuntimeError("API call failed") from e

Analysis = json.loads(raw_analysis_json)

if model_choosing["requires_model"]:
    Analysis["AI_nodes_results"] = AI_nodes_results

print("<Output>" + json.dumps(Analysis) + "</Output>")