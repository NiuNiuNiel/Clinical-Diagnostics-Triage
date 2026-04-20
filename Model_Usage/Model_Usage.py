from tensorflow.keras.models import load_model
import pandas as pd
import numpy as np
import sys

model_name = sys.argv[0]
model = load_model(model_name + ".keras")

classes = {"Heartbeat_Abnormality_Model" : ["Normal", 'Supraventricular Ectopic Beats', 'Ventricular Ectopic Beats', 'Fusion Beats', 'Unknown'],
           }

df = exec(sys.argv[1])

modification = sys.argv[2]
if modification != None:
    exec(modification)

if len(df) == 1:
    prediction = model.predict(df.to_numpy().reshape(1, -1, 1))
    print({"Prediction":classes[model_name][np.argmax(prediction, axis=-1)[0]], "Confident Score":f"{np.max(prediction, axis=-1)[0]*100:.2f}%"})

else:
    predictions = model.predict(df.values)
    print([{"Index":index, "Prediction":classes[model_name][np.argmax(prediction, axis=-1)[0]], "Confident Score":f"{np.max(prediction, axis=-1)[0]*100:.2f}%"} for index,prediction in enumerate(predictions)])