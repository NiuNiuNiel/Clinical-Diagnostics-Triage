from pyexpat import model
from tensorflow.keras.models import load_model
import pandas as pd
import numpy as np
import ast
import sys
import zai

user_prompt = sys.argv[1]
files_attached = sys.argv[2:]
with open("API_key.txt", "r") as f:
    API_key = f.read().strip()

GLM_model = zai.ZAI(api_key=API_key)

classes = {"Heartbeat_Abnormality_Model" : ["Normal", 'Supraventricular Ectopic Beats', 'Ventricular Ectopic Beats', 'Fusion Beats', 'Unknown'],
           }


# Model Choosing
model_choosing_context = """



You MUST respond ONLY with a valid JSON object matching this schema exactly:
{
    "response_summary": "string",
    "requires_model": boolean,
    "AI_nodes": [
        {
            "model_name": "string",
            "modification": "string or null",
            "file_path": "string",
            "headers": "integer or null"
        }
    ]
}

Each field is defined as follows:
- "response_summary": A concise summary of the user's request and the intended action.
- "requires_model": A boolean indicating whether a model is required for the task.
- "AI_nodes": A list of objects, where each object represents an individual processing task. This allows the system to process multiple files or apply different models in a single request. Each object in the list must contain the specific configuration for that file.
- "model_name": The name of the model to be used for this specific node.
- "modification": Any modifications to be applied on the data with the variable name "df", E.g., df.replace(parameters), df.map(parameters), df.iloc[parameters] = "<value>", etc. null if no modifications are needed.
- "file_path": The path of the file being processed in this node.
- "headers": An integer indicating the row number to be used as headers for this file, or null if there are no headers.
"""

model_choosing_response = GLM_model.chat.completions.create(
    model="glm-5.1",
    messages=None,
    stream=True
)

model_choosing = ast.literal_eval(model_choosing_response)

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
    else:
        raise ValueError("Unsupported file type")


def AI_node(model_name, modification, file_path, headers):
    model = load_model(model_name + ".keras")

    df = read_file(file_path, headers=headers)

    if modification != None:
        exec(modification)

    file_name = file_path.split("/")[-1]

    if len(df) == 1:
        prediction = model.predict(df.to_numpy().reshape(1, -1, 1))[0]
        return {file_name:{"Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"}}

    predictions = model.predict(df.values)
    return {file_name:[{"Index":index, "Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"} for index,prediction in enumerate(predictions)]}

if model_choosing["requires_model"]:
    AI_nodes_results = []
    for node in model_choosing["AI_nodes"]:
        AI_nodes_results.append(AI_node(model_name=node["model_name"], modification=node["modification"], file_path=node["file_path"], headers=node["headers"]))


# Notes and Predictions (if any) analysis
analysis_context = f"""


"""

if model_choosing["requires_model"]:
    analysis_context += f"AI nodes results:\n{AI_nodes_results}"