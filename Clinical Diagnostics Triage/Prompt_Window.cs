using ReaLTaiizor.Controls;
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
    public partial class Prompt_Window : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
        private const int EM_GETSCROLLPOS = 0x04DD;
        private const int EM_SETSCROLLPOS = 0x04DE;

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
        private string attachedNotePath = string.Empty;
        private List<string> attachedBiomedicalFiles = new List<string>();
        private bool isFirstPrompt = true;

        public Prompt_Window()
        {
            InitializeComponent();
        }

        // ==========================================
        // THE PYTHON ENGINE (STREAMING REAL-TIME)
        // ==========================================
        private async Task<TriagePayload?> RunPythonBackendAsync(string userPrompt, string notePath, List<string> bmFiles, Action<string> onRealTimeLog)
        {
            return await Task.Run(async () =>
            {
                string pythonExePath = "python";
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = Path.Combine(baseDir, "Model_Usage.py");

                if (!File.Exists(scriptPath))
                    throw new FileNotFoundException($"Could not find script at: {scriptPath}. Ensure the project was built successfully.");

                string safePrompt = string.IsNullOrWhiteSpace(userPrompt) ? "None" : userPrompt.Replace("\"", "\\\"");
                string safeNoteFile = string.IsNullOrWhiteSpace(notePath) ? "None" : notePath;

                // Build the arguments for sys.argv[3:]
                string bmFilesArgs = "None";
                if (bmFiles != null && bmFiles.Count > 0)
                {
                    List<string> quotedFiles = new List<string>();
                    foreach (string f in bmFiles)
                    {
                        quotedFiles.Add($"\"{f}\"");
                    }
                    bmFilesArgs = string.Join(" ", quotedFiles);
                }

                // Construct the final Python command
                string arguments = $"-u \"{scriptPath}\" \"{safePrompt}\" \"{safeNoteFile}\" {bmFilesArgs}";        

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonExePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, // We must read this if it's set to true
                    CreateNoWindow = true,
                    WorkingDirectory = baseDir
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new Exception("Failed to start the AI backend.");

                    StringBuilder jsonRaw = new StringBuilder();
                    bool isCapturingOutput = false;

                    // Read standard output asynchronously
                    while (!process.StandardOutput.EndOfStream)
                    {
                        string? line = await process.StandardOutput.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.Contains("<Thinking Process>"))
                        {
                            string step = line.Replace("<Thinking Process>", "").Replace("</Thinking Process>", "").Trim();
                            if (!string.IsNullOrWhiteSpace(step))
                            {
                                onRealTimeLog?.Invoke(step);
                            }
                        }
                        else if (line.Contains("<Output>"))
                        {
                            isCapturingOutput = true;
                            string content = line.Replace("<Output>", "");

                            if (content.Contains("</Output>"))
                            {
                                jsonRaw.AppendLine(content.Replace("</Output>", ""));
                                isCapturingOutput = false;
                                break;
                            }
                            else
                            {
                                jsonRaw.AppendLine(content);
                            }
                        }
                        else if (isCapturingOutput)
                        {
                            if (line.Contains("</Output>"))
                            {
                                jsonRaw.AppendLine(line.Replace("</Output>", ""));
                                isCapturingOutput = false;
                                break;
                            }
                            else
                            {
                                jsonRaw.AppendLine(line);
                            }
                        }
                    }

                    // FIX 2: Capture any fatal Python crashes/Tracebacks
                    string errors = await process.StandardError.ReadToEndAsync();

                    process.WaitForExit();

                    // If Python crashed before spitting out JSON, throw the Python traceback to the UI
                    if (!string.IsNullOrWhiteSpace(errors) && string.IsNullOrEmpty(jsonRaw.ToString().Trim()))
                    {
                        throw new Exception($"Python execution failed:\n{errors}");
                    }

                    string finalJsonString = jsonRaw.ToString().Trim();
                    if (!string.IsNullOrEmpty(finalJsonString))
                    {
                        try
                        {
                            return JsonSerializer.Deserialize<TriagePayload>(finalJsonString);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Failed to parse JSON output: {ex.Message}\nRaw: {finalJsonString}");
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
            string noteFile = attachedNotePath;
            List<string> bmFiles = new List<string>(attachedBiomedicalFiles); // Take a snapshot

            if (string.IsNullOrWhiteSpace(prompt) && string.IsNullOrWhiteSpace(noteFile))
            {
                MessageBox.Show("Please enter clinical notes or attach a note document before sending.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. Lock UI (Make sure to lock BOTH attach buttons)
            btnSend.Enabled = false;
            txtInput.Enabled = false;
            btnAttach_Note.Enabled = false;
            bmFile_attachBtn.Enabled = false;
            string originalButtonText = btnSend.Text;

            if (isFirstPrompt)
            {
                string titleText = prompt.Length > 35 ? prompt.Substring(0, 35) + "..." : prompt;
                lblTitle.Text = titleText;
                isFirstPrompt = false;
            }

            // 2. Print User Input
            rtbChatHistory.SelectionColor = Color.LightSkyBlue;
            rtbChatHistory.AppendText($"\nPhysician: ");

            rtbChatHistory.SelectionColor = Color.WhiteSmoke;
            rtbChatHistory.AppendText($"{prompt}\n");

            if (!string.IsNullOrEmpty(noteFile))
            {
                rtbChatHistory.SelectionColor = Color.MediumPurple;
                rtbChatHistory.AppendText($"[Note Included: {Path.GetFileName(noteFile)}]\n");
            }

            foreach (string f in bmFiles)
            {
                rtbChatHistory.SelectionColor = Color.SkyBlue;
                rtbChatHistory.AppendText($"[Biomedical Data Included: {Path.GetFileName(f)}]\n");
            }

            // Prepare the Chat History for the thinking process
            rtbChatHistory.SelectionColor = Color.Silver; // Changed from DarkGray for dark mode visibility
            rtbChatHistory.AppendText("\n--------------------\nThinking...\n");

            // 3. Setup streaming and animation
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Task animationTask = AnimateButtonAsync(cts.Token);

                try
                {
                    bool isFirstThinkingStep = true;
                    Action<string> updateChatRealTime = null;

                    updateChatRealTime = (stepText) =>
                    {
                        if (rtbChatHistory.InvokeRequired)
                        {
                            rtbChatHistory.Invoke(new Action(() => updateChatRealTime(stepText)));
                            return;
                        }

                        // Make the real-time streamed steps Silver instead of DarkGray
                        rtbChatHistory.SelectionColor = Color.Silver;

                        if (!isFirstThinkingStep)
                        {
                            rtbChatHistory.AppendText("     |\n");
                        }

                        rtbChatHistory.SelectionColor = Color.WhiteSmoke;
                        rtbChatHistory.AppendText($"{stepText}\n");
                        rtbChatHistory.ScrollToCaret();

                        isFirstThinkingStep = false;
                    };

                    // 4. Run Backend
                    var payload = await RunPythonBackendAsync(prompt, noteFile, bmFiles, updateChatRealTime);

                    // Stop animation
                    cts.Cancel();

                    // 5. Print Final Results
                    if (payload != null)
                    {
                        rtbChatHistory.SelectionColor = Color.DarkGray;
                        rtbChatHistory.AppendText("--------------------\n");

                        rtbChatHistory.SelectionColor = Color.WhiteSmoke;
                        rtbChatHistory.AppendText("\nOutput:\n");

                        // Priority coloring
                        rtbChatHistory.SelectionColor = payload.triage_priority == 1 ? Color.Tomato : Color.Gold;
                        rtbChatHistory.AppendText($"Priority: Level {payload.triage_priority}\n");

                        // FIX: Explicitly set WhiteSmoke before the Summary
                        rtbChatHistory.SelectionColor = Color.WhiteSmoke;
                        rtbChatHistory.AppendText($"Summary: {payload.clinical_summary}\n");

                        // FIX: Explicitly set WhiteSmoke before the Reason
                        rtbChatHistory.SelectionColor = Color.WhiteSmoke;
                        rtbChatHistory.AppendText($"Reason: {payload.triage_reason}\n");

                        // FIX: Explicitly set WhiteSmoke before the Action
                        rtbChatHistory.SelectionColor = Color.WhiteSmoke;
                        rtbChatHistory.AppendText($"Action: {payload.recommended_action}\n");

                        // Human Intervention Warning
                        if (payload.flagged_for_human_review)
                        {
                            rtbChatHistory.SelectionColor = Color.Tomato;
                            rtbChatHistory.AppendText("\n⚠️ HUMAN INTERVENTION REQUIRED ⚠️\n");
                            rtbChatHistory.AppendText("Manual physician review is strictly required due to flagged ambiguity.\n");
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
                    btnAttach_Note.Enabled = true;
                    bmFile_attachBtn.Enabled = true; // Unlock the new button
                    txtInput.Clear();

                    // Clear both trackers
                    attachedNotePath = string.Empty;
                    attachedBiomedicalFiles.Clear();
                }
            }
        }

        private void btnAttach_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                // Strictly clinical notes
                openFileDialog.Filter = "Clinical Notes|*.txt;*.docx;*.pdf";
                openFileDialog.Title = "Attach Clinical Note Document";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    attachedNotePath = openFileDialog.FileName;
                    rtbChatHistory.SelectionColor = Color.MediumSpringGreen;
                    rtbChatHistory.AppendText($"\n[System]: Attached Clinical Note -> {Path.GetFileName(attachedNotePath)}\n");
                    rtbChatHistory.SelectionColor = rtbChatHistory.ForeColor;
                }
            }
        }

        private void bmFile_attachBtn_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                // Strictly biomedical models
                openFileDialog.Filter = "Biomedical Data|*.csv;*.tsv;*.json;*.png;*.jpg;*.jpeg";
                openFileDialog.Title = "Attach Biomedical Data (ECG, X-Ray, etc.)";
                openFileDialog.Multiselect = true; // Allows attaching multiple AI nodes at once

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (string file in openFileDialog.FileNames)
                    {
                        if (!attachedBiomedicalFiles.Contains(file))
                        {
                            attachedBiomedicalFiles.Add(file);
                            rtbChatHistory.SelectionColor = Color.SkyBlue;
                            rtbChatHistory.AppendText($"\n[System]: Attached Biomedical Data -> {Path.GetFileName(file)}\n");
                        }
                    }
                    rtbChatHistory.SelectionColor = rtbChatHistory.ForeColor;
                }
            }
        }

        private void btnNewPatient_Click(object sender, EventArgs e)
        {
            currentSessionHistory.Clear();
            rtbChatHistory.Clear();
            txtInput.Clear();

            // Clear both variables
            attachedNotePath = string.Empty;
            attachedBiomedicalFiles.Clear();

            isFirstPrompt = true;
            lblTitle.Text = "Clinical Copilot";
        }

        private void rtbChatHistory_TextChanged(object sender, EventArgs e)
        {
            // 1. Force the chat box to scroll down to the newest message
            rtbChatHistory.SelectionStart = rtbChatHistory.Text.Length;
            rtbChatHistory.ScrollToCaret();

            // 2. Calculate exactly how tall the entire chat history is right now
            int totalLines = rtbChatHistory.GetLineFromCharIndex(rtbChatHistory.TextLength) + 1;
            int totalHeight = totalLines * rtbChatHistory.Font.Height;

            // 3. Update the custom scroll bar's maximum size
            if (totalHeight > rtbChatHistory.Height)
            {
                // Add a little extra padding so it doesn't get stuck
                poisonVScrollBar1.Maximum = totalHeight + 50;

                // Grab the new scroll position and snap the bar to the bottom
                Point currentScroll = new Point();
                SendMessage(rtbChatHistory.Handle, EM_GETSCROLLPOS, 0, ref currentScroll);

                if (currentScroll.Y <= poisonVScrollBar1.Maximum)
                {
                    poisonVScrollBar1.Value = currentScroll.Y;
                }
            }
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
            public int triage_priority { get; set; } // Changed from string to int
            public string clinical_summary { get; set; }
            public string recommended_action { get; set; }
            public string triage_reason { get; set; } // Added new reason field
            public bool flagged_for_human_review { get; set; }
            public List<AiNodeResult> AI_nodes_results { get; set; } // Added AI results list
        }

        public class AiNodeResult
        {
            public string file_name { get; set; }
            public string model_name { get; set; }
            public string reference_ID { get; set; }
            public JsonElement result { get; set; } // JsonElement handles both dictionaries and arrays dynamically
        }

        private void txtInput_TextChanged(object sender, EventArgs e)
        {
            // Force the text box to scroll down and follow the cursor as they type
            txtInput.ScrollToCaret();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // The last two numbers (15, 15) control how round the corners are. 
            // Increase them for pill-shaped buttons, decrease them for subtle curves.

            // 1. Round the Send Button
            btnSend.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, btnSend.Width, btnSend.Height, 15, 15));

            // 2. Round the Attach Button
            btnAttach_Note.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, btnAttach_Note.Width, btnAttach_Note.Height, 15, 15));

            // 3. Round the Text Box
            // NOTE: WinForms text boxes must have BorderStyle set to None for this to look good!
            txtInput.BorderStyle = BorderStyle.None;
            txtInput.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, txtInput.Width, txtInput.Height, 10, 10));
        }

        private void poisonVScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            // Get the exact pixel value of where the custom scroll bar was dragged
            Point scrollTarget = new Point(0, poisonVScrollBar1.Value);

            // Force the text box to move to that exact pixel
            SendMessage(rtbChatHistory.Handle, EM_SETSCROLLPOS, 0, ref scrollTarget);
        }

        private void rtbChatHistory_VScroll(object sender, EventArgs e)
        {
            // Ask Windows exactly where the text box is currently scrolled to
            Point currentScroll = new Point();
            SendMessage(rtbChatHistory.Handle, EM_GETSCROLLPOS, 0, ref currentScroll);

            // Safely update the custom scroll bar to match that exact position
            if (currentScroll.Y >= poisonVScrollBar1.Minimum && currentScroll.Y <= poisonVScrollBar1.Maximum)
            {
                poisonVScrollBar1.Value = currentScroll.Y;
            }
        }
    }
}