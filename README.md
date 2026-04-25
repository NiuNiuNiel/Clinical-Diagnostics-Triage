# Clinical Diagnostics Triage & Workflow Copilot 🩺
**Team Shrooms | UMHackathon 2026**

## ⚠️ IMPORTANT NOTICE FOR EVALUATORS ⚠️
**Please evaluate the `Google_Model` branch.** Due to a failure with the file upload feature in the originally intended `ilmu-glm` API during development, our team pivoted to a robust alternative using Google's API to ensure a fully functional pipeline. 

The **COMPLETED** and **ALTERNATIVE** working project is saved exclusively within the **`Google_Model`** branch. **PLEASE refer to that branch when evaluating our system.** The modular architecture of our system allows it to seamlessly switch back to the Z.AI model once their file uploading feature is resolved.

---

## 📖 Project Overview
Clinical personnel operating in time-pressured environments often manage fragmented diagnostic evidence across disconnected tools—reviewing ECGs in one software, X-rays in another, and manual notes elsewhere. 

The **Clinical Diagnostics Triage & Workflow Copilot** is a standalone Windows desktop application designed to eliminate this fragmentation. It consolidates clinical note intake, LLM-assisted handwritten-note OCR, local model inference for biomedical files (ECG and chest imaging), and AI-assisted triage synthesis into a single session. 

The system evaluates all findings using a strict 90% confidence threshold. If all evidence aligns, it outputs a recommended action and a standardized mock FHIR R4 ClinicalImpression structured output. If model confidence is low or inputs contradict, it explicitly flags the case for **Human Review Required**.

## ✨ Key Features
* **Unified Desktop Interface:** A C# WinForms presentation layer allowing doctors to input typed notes, upload images of handwritten charts, and attach biomedical files (.csv, .png, etc.) in one seamless session.
* **Local Python AI Engine:** A secure local processing backend that handles inference without relying on an internal HTTP API.
* **Intelligent Modality Routing:** Automatically routes attached files to the correct local TensorFlow/Keras models:
  * **Heartbeat Abnormality Model:** 5-class sequence classifier for ECG signal files.
  * **Chest X-Ray Vision Model:** Convolutional neural network for binary classification (Normal vs. Pneumonia).
* **Agentic Synthesis & OCR:** Uses an external LLM to perform OCR on handwritten notes, summarize lengthy clinical histories, and synthesize all evidence into a final triage priority.
* **Safety & Compliance:** Strict contradiction detection and a 90% model confidence requirement ensure the system never makes autonomous triage decisions when uncertain. Generates mock FHIR R4 output for EHR interoperability.

## 🛠️ Tech Stack
* **Frontend:** C# WinForms (.NET Framework)
* **Backend Engine:** Python 3.12 (Local Process Invocation)
* **Local Machine Learning:** TensorFlow & Keras
* **LLM / Agentic Routing:** Google API (Temporary alternative to Z.AI `ilmu-glm-5.1`)
