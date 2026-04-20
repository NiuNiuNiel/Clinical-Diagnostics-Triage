from tensorflow.keras.models import load_model
import pandas as pd
import numpy as np
import sys

model_name = sys.argv[0]
model = load_model(model_name + ".keras")

classes = {"Heartbeat_Abnormility_Model" : ["Normal", 'S', 'V', 'F', 'Q'],
           }

df = exec(sys.argv[1])

modification = sys.argv[2]
if modification != None:
    exec(modification)

sample = df.to_numpy()

prediction = model.predict(sample.reshape(1, -1, 1))

print({"Prediction":classes[model_name][np.argmax(prediction, axis=-1)[0]], "Confident Score":f"{np.max(prediction, axis=-1)[0]*100:.2f}%"})