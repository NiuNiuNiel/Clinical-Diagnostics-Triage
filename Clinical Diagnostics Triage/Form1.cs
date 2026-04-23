using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clinical_Diagnostics_Triage
{
    public partial class Form1 : Form
    {
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse, // How curved the width is (Higher = rounder)
            int nHeightEllipse // How curved the height is (Higher = rounder)
        );
        private List<ChatMessage> currentSessionHistory = new List<ChatMessage>();
        private string attachedFilePath = string.Empty;

        public Form1()
        {
            InitializeComponent();
        }

        // ==========================================
        // THE PYTHON ENGINE (STREAMING REAL-TIME)
        // ==========================================
        private async Task<TriagePayload?> RunPythonBackendAsync(string userPrompt, string filePath, Action<string> onRealTimeLog)
        {
            return await Task.Run(async () =>
            {
                string pythonExePath = @"C:\Users\weisi\AppData\Local\Programs\Python\Python312\python.exe";
                if (!File.Exists(pythonExePath)) throw new FileNotFoundException($"Could not find Python at: {pythonExePath}");

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo dirInfo = new DirectoryInfo(baseDir);
                while (dirInfo != null && !Directory.Exists(Path.Combine(dirInfo.FullName, "Model_Usage")))
                {
                    dirInfo = dirInfo.Parent;
                }
                if (dirInfo == null) throw new Exception("Could not locate the Model_Usage project folder.");

                string pythonProjectFolder = Path.Combine(dirInfo.FullName, "Model_Usage");
                string scriptPath = Path.Combine(pythonProjectFolder, "Model_Usage.py");
                if (!File.Exists(scriptPath)) throw new FileNotFoundException($"Could not find script at: {scriptPath}");

                string safePrompt = string.IsNullOrWhiteSpace(userPrompt) ? "None" : userPrompt.Replace("\"", "\\\"");
                string safeFile = string.IsNullOrWhiteSpace(filePath) ? "None" : filePath;
                string arguments = $"\"{scriptPath}\" \"{safePrompt}\" \"{safeFile}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonExePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = pythonProjectFolder
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new Exception("Failed to start the AI backend.");

                    StringBuilder rawJson = new StringBuilder();
                    bool isJsonBlock = false;

                    while (!process.StandardOutput.EndOfStream)
                    {
                        string? line = await process.StandardOutput.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.Contains("{")) isJsonBlock = true;

                        if (isJsonBlock)
                        {
                            rawJson.AppendLine(line);
                        }
                        else
                        {
                            onRealTimeLog?.Invoke(line);
                        }
                    }

                    string errors = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(errors) && rawJson.Length == 0) throw new Exception("Backend Error: " + errors);

                    if (rawJson.Length > 0)
                    {
                        string cleanJson = rawJson.ToString().Replace("'", "\"").Replace("True", "true").Replace("False", "false").Replace("None", "null");
                        int start = cleanJson.IndexOf('{');
                        int end = cleanJson.LastIndexOf('}');
                        if (start != -1 && end != -1)
                        {
                            cleanJson = cleanJson.Substring(start, end - start + 1);
                            return JsonSerializer.Deserialize<TriagePayload>(cleanJson);
                        }
                    }
                    return null;
                }
            });
        }

        // ==========================================
        // UI ANIMATION TASK (BUTTON PULSE)
        // ==========================================
        private async Task AnimateButtonAsync(CancellationToken token)
        {
            string baseText = "Analyzing";
            int dots = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    dots = (dots + 1) % 4;
                    string currentText = baseText + new string('.', dots);

                    if (btnSend.InvokeRequired)
                    {
                        btnSend.Invoke(new Action(() => btnSend.Text = currentText));
                    }
                    else
                    {
                        btnSend.Text = currentText;
                    }

                    await Task.Delay(400, token);
                }
            }
            catch (TaskCanceledException) { }
        }

        // ==========================================
        // UI EVENT HANDLERS
        // ==========================================
        private async void btnSend_Click(object sender, EventArgs e)
        {
            string prompt = txtInput.Text;
            string file = attachedFilePath;

            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Please enter clinical notes before sending.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. Lock UI
            btnSend.Enabled = false;
            txtInput.Enabled = false;
            btnAttach.Enabled = false;
            string originalButtonText = btnSend.Text;

            // 2. Print User Input
            rtbChatHistory.SelectionColor = Color.LightSkyBlue;
            rtbChatHistory.AppendText($"\nPhysician:\n{prompt}\n");

            if (!string.IsNullOrEmpty(file))
            {
                rtbChatHistory.SelectionColor = Color.MediumPurple;
                rtbChatHistory.AppendText($"[File Included: {Path.GetFileName(file)}]\n");
            }

            rtbChatHistory.SelectionColor = Color.DarkGray;
            rtbChatHistory.AppendText("\nCopilot: System routing initiated...\n");

            // 3. Setup streaming and animation
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Task animationTask = AnimateButtonAsync(cts.Token);

                try
                {
                    Action<string> updateChatRealTime = (logText) =>
                    {
                        if (rtbChatHistory.InvokeRequired)
                        {
                            rtbChatHistory.Invoke(new Action(() =>
                            {
                                rtbChatHistory.SelectionColor = Color.DarkGray;
                                rtbChatHistory.AppendText($"[System Output]: {logText}\n");
                                rtbChatHistory.ScrollToCaret();
                            }));
                        }
                        else
                        {
                            rtbChatHistory.SelectionColor = Color.DarkGray;
                            rtbChatHistory.AppendText($"[System Output]: {logText}\n");
                            rtbChatHistory.ScrollToCaret();
                        }
                    };

                    // 4. Run Backend
                    var payload = await RunPythonBackendAsync(prompt, file, updateChatRealTime);

                    // Stop animation
                    cts.Cancel();

                    // 5. Print Results
                    if (payload != null)
                    {
                        rtbChatHistory.SelectionColor = Color.WhiteSmoke;
                        rtbChatHistory.AppendText("\n--- DIAGNOSTIC TRIAGE ALERT ---\n");

                        rtbChatHistory.SelectionColor = payload.triage_priority == "1" || payload.triage_priority.ToLower().Contains("high") ? Color.Tomato : Color.Gold;
                        rtbChatHistory.AppendText($"Priority: {payload.triage_priority}\n");

                        rtbChatHistory.SelectionColor = Color.WhiteSmoke;
                        rtbChatHistory.AppendText($"Summary: {payload.clinical_summary}\n");
                        rtbChatHistory.AppendText($"Action: {payload.recommended_action}\n");

                        if (payload.flagged_for_human_review)
                        {
                            rtbChatHistory.SelectionColor = Color.Tomato;
                            rtbChatHistory.AppendText("\n⚠️ SYSTEM WARNING: FLAGGED FOR HUMAN REVIEW ⚠️\nData was ambiguous or contradictory.\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    cts.Cancel();
                    rtbChatHistory.SelectionColor = Color.Tomato;
                    rtbChatHistory.AppendText($"\nSystem Error: {ex.Message}\n");
                }
                finally
                {
                    // 6. Restore UI
                    btnSend.Text = originalButtonText;
                    btnSend.Enabled = true;
                    txtInput.Enabled = true;
                    btnAttach.Enabled = true;
                    txtInput.Clear();
                    attachedFilePath = string.Empty;
                }
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
                    rtbChatHistory.SelectionColor = Color.MediumSpringGreen;
                    rtbChatHistory.AppendText($"\n[System]: Attached file -> {Path.GetFileName(attachedFilePath)}\n");
                    rtbChatHistory.SelectionColor = rtbChatHistory.ForeColor;
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

        private void rtbChatHistory_TextChanged(object sender, EventArgs e)
        {
            // Auto-scroll to bottom as text is added
            rtbChatHistory.SelectionStart = rtbChatHistory.Text.Length;
            rtbChatHistory.ScrollToCaret();
        }

        // ==========================================
        // DATA MODELS
        // ==========================================
        public class ChatMessage
        {
            public string Sender { get; set; }
            public string Content { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class TriagePayload
        {
            public string triage_priority { get; set; }
            public string clinical_summary { get; set; }
            public string recommended_action { get; set; }
            public bool flagged_for_human_review { get; set; }
        }

        private void txtInput_TextChanged(object sender, EventArgs e)
        {
            // 1. Define your size limits
            int minHeight = 35;  // Height for 1 row of text
            int maxHeight = 100; // Max height before it stops growing (approx 4-5 rows)
            int padding = 10;    // Extra space so text isn't cramped

            // 2. Calculate how many lines of text there are
            int lines = txtInput.GetLineFromCharIndex(txtInput.TextLength) + 1;

            // Calculate the needed height (approx 20 pixels per line depending on font size)
            int neededHeight = (lines * 20) + padding;

            // 3. Apply the dynamic height
            if (neededHeight <= minHeight)
            {
                txtInput.Height = minHeight;
                txtInput.ScrollBars = ScrollBars.None;
            }
            else if (neededHeight >= maxHeight)
            {
                txtInput.Height = maxHeight;
                txtInput.ScrollBars = ScrollBars.Vertical; // Turn on scrolling when max limit is hit
            }
            else
            {
                txtInput.Height = neededHeight;
                txtInput.ScrollBars = ScrollBars.None;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // The last two numbers (15, 15) control how round the corners are. 
            // Increase them for pill-shaped buttons, decrease them for subtle curves.

            // 1. Round the Send Button
            btnSend.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, btnSend.Width, btnSend.Height, 15, 15));

            // 2. Round the Attach Button
            btnAttach.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, btnAttach.Width, btnAttach.Height, 15, 15));

            // 3. Round the Text Box
            // NOTE: WinForms text boxes must have BorderStyle set to None for this to look good!
            txtInput.BorderStyle = BorderStyle.None;
            txtInput.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, txtInput.Width, txtInput.Height, 10, 10));
        }
    }
}