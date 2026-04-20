from tensorflow.keras.models import load_model
import pandas as pd
import numpy as np
import ast
import sys


user_prompt = sys.argv[0]
files_attached = sys.argv[1:]



classes = {"Heartbeat_Abnormality_Model" : ["Normal", 'Supraventricular Ectopic Beats', 'Ventricular Ectopic Beats', 'Fusion Beats', 'Unknown'],
           }



def AI_node(model_name, read_file_arg, modification, file_name):
    model = load_model(model_name + ".keras")

    df = exec(read_file_arg)

    if modification != None:
        exec(modification)

    if len(df) == 1:
        prediction = model.predict(df.to_numpy().reshape(1, -1, 1))[0]
        return {file_name:{"Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"}}

    else:
        predictions = model.predict(df.values)
        return {file_name:[{"Index":index, "Prediction":classes[model_name][np.argmax(prediction, axis=-1)], "Confident Score":f"{np.max(prediction, axis=-1)*100:.2f}%"} for index,prediction in enumerate(predictions)]}