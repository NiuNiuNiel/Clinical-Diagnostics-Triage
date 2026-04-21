using System.Diagnostics;
using System.Text.Json;

namespace Clinical_Diagnostics_Triage
{
    public partial class Form1 : Form
    {
        private List<ChatMessage> currentSessionHistory = new List<ChatMessage>();
        private string attachedFilePath = string.Empty;
        public Form1()
        {
            InitializeComponent();
        }

        // ==========================================
        // THE PYTHON ENGINE (BACKGROUND TASK)
        // ==========================================
        // Notice the '?' added here to fix that green line warning!
        private async Task<TriagePayload?> RunPythonBackendAsync(string userPrompt, string filePath)
        {
            return await Task.Run(() =>
            {
                // 1. DYNAMICALLY find the Model_Usage folder in your Solution
                string baseDir = Application.StartupPath;
                DirectoryInfo dirInfo = new DirectoryInfo(baseDir);

                // Walk up the folder tree until we find the "Model_Usage" folder
                while (dirInfo != null && !Directory.Exists(Path.Combine(dirInfo.FullName, "Model_Usage")))
                {
                    dirInfo = dirInfo.Parent;
                }

                if (dirInfo == null)
                {
                    throw new Exception("Could not locate the Model_Usage project folder in the Solution.");
                }

                string pythonProjectFolder = Path.Combine(dirInfo.FullName, "Model_Usage");
                string pythonScriptPath = Path.Combine(pythonProjectFolder, "Model_Usage.py");

                if (!File.Exists(pythonScriptPath))
                {
                    throw new FileNotFoundException($"Could not find your Python script at: {pythonScriptPath}");
                }

                // 2. PASTE THE EXACT PATH TO YOUR PYTHON 3.12 HERE
                // (You can get this from that Python Environments window you found!)
                string pythonExePath = @"C:\Users\weisi\AppData\Local\Programs\Python\Python312\python.exe";

                if (!File.Exists(pythonExePath))
                {
                    throw new FileNotFoundException($"Could not find python.exe at: {pythonExePath}");
                }

                // 3. Format the arguments. 
                // Notice the script path goes FIRST, then the prompt, then the file.
                string arguments = $"\"{pythonScriptPath}\" \"{userPrompt.Replace("\"", "\\\"")}\" \"{filePath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonExePath,       // We MUST run python.exe
                    Arguments = arguments,          // And pass the .py script as the argument
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,

                    // This forces Python to run inside the Model_Usage folder, so it finds api_key.txt!
                    WorkingDirectory = pythonProjectFolder
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new Exception("Failed to start the Python process.");

                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    // If Python crashed and didn't give us a triage_priority, throw the exact Python error
                    if (!string.IsNullOrEmpty(errors) && !output.Contains("triage_priority"))
                    {
                        throw new Exception("Python Error: " + errors);
                    }

                    // Extract the JSON payload from the terminal output
                    int jsonStartIndex = output.IndexOf('{');
                    int jsonEndIndex = output.LastIndexOf('}');

                    if (jsonStartIndex != -1 && jsonEndIndex != -1)
                    {
                        string cleanJson = output.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                        return JsonSerializer.Deserialize<TriagePayload>(cleanJson);
                    }

                    return null;
                }
            });
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            string prompt = txtInput.Text;
            string file = attachedFilePath;

            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Please enter clinical notes before sending.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. Lock the UI so the user can't spam the button
            btnSend.Enabled = false;
            txtInput.Enabled = false;
            btnAttach.Enabled = false;

            // 2. Print the doctor's message to the chat
            rtbChatHistory.SelectionColor = Color.DarkSlateGray;
            rtbChatHistory.AppendText($"\nPhysician:\n{prompt}\n");
            if (!string.IsNullOrEmpty(file))
            {
                rtbChatHistory.AppendText($"[File Included: {Path.GetFileName(file)}]\n");
            }

            rtbChatHistory.SelectionColor = Color.Gray;
            rtbChatHistory.AppendText("\nCopilot: Analyzing medical data and routing to AI nodes...\n");

            try
            {
                // 3. Send the data to your Python backend
                TriagePayload aiResult = await RunPythonBackendAsync(prompt, file);

                // 4. Print the AI's response to the chat
                if (aiResult != null)
                {
                    rtbChatHistory.SelectionColor = Color.Black;
                    rtbChatHistory.AppendText("\n--- DIAGNOSTIC TRIAGE ALERT ---\n");

                    // Color code the priority
                    rtbChatHistory.SelectionColor = aiResult.triage_priority == "1" || aiResult.triage_priority.ToLower().Contains("high") ? Color.Red : Color.Orange;
                    rtbChatHistory.AppendText($"Priority: {aiResult.triage_priority}\n");

                    rtbChatHistory.SelectionColor = Color.Black;
                    rtbChatHistory.AppendText($"Summary: {aiResult.clinical_summary}\n");
                    rtbChatHistory.AppendText($"Action: {aiResult.recommended_action}\n");

                    // Check the safety guardrail
                    if (aiResult.flagged_for_human_review)
                    {
                        rtbChatHistory.SelectionColor = Color.Red;
                        rtbChatHistory.AppendText("\n⚠️ SYSTEM WARNING: FLAGGED FOR HUMAN REVIEW ⚠️\nData was ambiguous or contradictory.\n");
                    }
                }
            }
            catch (Exception ex)
            {
                rtbChatHistory.SelectionColor = Color.Red;
                rtbChatHistory.AppendText($"\nSystem Error: {ex.Message}\n");
            }
            finally
            {
                // 5. Unlock the UI and clear the input box
                btnSend.Enabled = true;
                txtInput.Enabled = true;
                btnAttach.Enabled = true;
                txtInput.Clear();
                attachedFilePath = string.Empty; // Clear the file so it isn't sent twice by mistake
            }
        }

        private void btnAttach_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Medical Data|*.csv;*.tsv;*.json;*.png;*.jpg;*.jpeg|All files (*.*)|*.*";
                openFileDialog.Title = "Attach Patient File (ECG or X-Ray)";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    attachedFilePath = openFileDialog.FileName;

                    // Show a blue confirmation in the chat window
                    rtbChatHistory.SelectionColor = Color.Blue;
                    rtbChatHistory.AppendText($"\n[System]: Attached file -> {Path.GetFileName(attachedFilePath)}\n");
                    rtbChatHistory.SelectionColor = rtbChatHistory.ForeColor; // Reset color back to normal
                }
            }
        }

        private void btnNewPatient_Click(object sender, EventArgs e)
        {
            currentSessionHistory.Clear();
            rtbChatHistory.Clear();
            txtInput.Clear();
            attachedFilePath = string.Empty;
        }

        // This handles the chat history memory
        public class ChatMessage
        {
            public string Sender { get; set; }
            public string Content { get; set; }
            public DateTime Timestamp { get; set; }
        }

        // This matches the JSON coming from your Python AI
        public class TriagePayload
        {
            public string triage_priority { get; set; }
            public string clinical_summary { get; set; }
            public string recommended_action { get; set; }
            public bool flagged_for_human_review { get; set; }
        }

        private void rtbChatHistory_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
