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
        private async Task<(TriagePayload? Payload, string ThinkingLogs)> RunPythonBackendAsync(string userPrompt, string filePath)
        {
            return await Task.Run(() =>
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string scriptExePath = Path.Combine(appDirectory, "Model_Usage.exe");

                if (!File.Exists(scriptExePath))
                {
                    throw new FileNotFoundException($"Could not find the backend at: {scriptExePath}");
                }

                string arguments = $"\"{userPrompt.Replace("\"", "\\\"")}\" \"{filePath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = scriptExePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = appDirectory
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new Exception("Failed to start the AI backend.");

                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(errors) && !output.Contains("triage_priority"))
                    {
                        throw new Exception("Backend Error: " + errors);
                    }

                    int jsonStartIndex = output.IndexOf('{');
                    int jsonEndIndex = output.LastIndexOf('}');

                    string thinkingLogs = "";

                    if (jsonStartIndex != -1 && jsonEndIndex != -1)
                    {
                        // 1. Extract everything printed BEFORE the JSON starts
                        if (jsonStartIndex > 0)
                        {
                            thinkingLogs = output.Substring(0, jsonStartIndex).Trim();
                        }

                        // 2. Extract the JSON
                        string cleanJson = output.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                        TriagePayload payload = JsonSerializer.Deserialize<TriagePayload>(cleanJson);

                        // Return BOTH!
                        return (payload, thinkingLogs);
                    }

                    return (null, output.Trim());
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
                // 1. Wait for Python to finish and get BOTH the logs and the result
                var aiResponse = await RunPythonBackendAsync(prompt, file);

                // 2. Print the Python "Thinking" Prints first
                if (!string.IsNullOrWhiteSpace(aiResponse.ThinkingLogs))
                {
                    rtbChatHistory.SelectionColor = Color.Gray;
                    rtbChatHistory.AppendText($"\n[System Output]:\n{aiResponse.ThinkingLogs}\n");
                }

                // 3. Print the Final Formatted Result
                if (aiResponse.Payload != null)
                {
                    rtbChatHistory.SelectionColor = Color.Black;
                    rtbChatHistory.AppendText("\n--- DIAGNOSTIC TRIAGE ALERT ---\n");

                    rtbChatHistory.SelectionColor = aiResponse.Payload.triage_priority == "1" || aiResponse.Payload.triage_priority.ToLower().Contains("high") ? Color.Red : Color.Orange;
                    rtbChatHistory.AppendText($"Priority: {aiResponse.Payload.triage_priority}\n");

                    rtbChatHistory.SelectionColor = Color.Black;
                    rtbChatHistory.AppendText($"Summary: {aiResponse.Payload.clinical_summary}\n");
                    rtbChatHistory.AppendText($"Action: {aiResponse.Payload.recommended_action}\n");

                    if (aiResponse.Payload.flagged_for_human_review)
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
