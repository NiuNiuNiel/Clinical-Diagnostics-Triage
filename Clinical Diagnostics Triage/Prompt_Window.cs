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
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout; // Required for text formatting

namespace Clinical_Diagnostics_Triage
{
    public partial class Prompt_Window : Form
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

        private async Task TriggerFHIRPushAsync(TriagePayload payload)
        {
            try
            {
                rtbChatHistory.SelectionColor = Color.LightSkyBlue;
                rtbChatHistory.AppendText("\n[System]: Initiating FHIR R4 payload push...\n");
                rtbChatHistory.ScrollToCaret();

                // CHANGED: Serialize the entire payload object, not just the list
                string fhirJson = JsonSerializer.Serialize(payload);

                // 2. Save it to a temp file to bypass command line character limits
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempFilePath = Path.Combine(baseDir, "fhir_payload.json");

                File.WriteAllText(tempFilePath, fhirJson);

                await Task.Delay(100);

                // Target the FHIR_R4_Handling.py script
                string scriptPath = Path.Combine(baseDir, "FHIR_R4_Handling.py");

                if (!File.Exists(scriptPath))
                {
                    throw new FileNotFoundException($"Could not find FHIR script at: {scriptPath}");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-u \"{scriptPath}\" \"{tempFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = baseDir
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new Exception("Failed to start the FHIR backend.");

                    // Read output and errors
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string errors = await process.StandardError.ReadToEndAsync();

                    process.WaitForExit();

                    // Print any Python Tracebacks/Errors
                    if (!string.IsNullOrWhiteSpace(errors))
                    {
                        rtbChatHistory.SelectionColor = Color.Tomato;
                        rtbChatHistory.AppendText($"\n[FHIR Script Error]:\n{errors}\n");
                    }

                    // Print the successful output from the Python script
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        rtbChatHistory.SelectionColor = Color.MediumSpringGreen;
                        rtbChatHistory.AppendText($"\n[FHIR Server]:\n{output}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                rtbChatHistory.SelectionColor = Color.Tomato;
                rtbChatHistory.AppendText($"\n[System Error during FHIR Push]: {ex.Message}\n");
            }
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

                        if (payload.AI_nodes_results != null && payload.AI_nodes_results.Count > 0)
                        {
                            DialogResult pushResult = MessageBox.Show(
                                "Triage analysis complete.\n\nDo you want to push the AI diagnostic results to the server via FHIR R4 payload?",
                                "Push FHIR Payload",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (pushResult == DialogResult.Yes)
                            {
                                // CHANGED: Pass the full 'payload' instead of 'payload.AI_nodes_results'
                                await TriggerFHIRPushAsync(payload);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    rtbChatHistory.SelectionColor = Color.Tomato;
                    rtbChatHistory.AppendText($"\nSystem Error: {ex.Message}\n");
                }
                finally
                {
                    // 1. Tell the animation to stop
                    cts.Cancel();

                    // 2. THE FIX: Wait for the animation thread to completely die before moving on
                    try
                    {
                        await animationTask;
                    }
                    catch { } // Ignore the cancellation error, we just want it to stop

                    // 3. NOW it is safe to restore the UI
                    btnSend.Text = "Send"; // Hardcode this to guarantee it resets
                    btnSend.Enabled = true;
                    txtInput.Enabled = true;
                    btnAttach_Note.Enabled = true;
                    bmFile_attachBtn.Enabled = true;
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
            // Force the chat box to scroll down to the newest message
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

        private void Prompt_Window_Load(object sender, EventArgs e)
        {
            // The last two numbers (15, 15) control how round the corners are. 
            btnSend.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, btnSend.Width, btnSend.Height, 15, 15));
            btnAttach_Note.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, btnAttach_Note.Width, btnAttach_Note.Height, 15, 15));

            txtInput.BorderStyle = BorderStyle.None;
            txtInput.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, txtInput.Width, txtInput.Height, 10, 10));
        }

        private void Prompt_Window_Resize(object sender, EventArgs e)
        {
            lblTitle.Left = (this.ClientSize.Width - lblTitle.Width) / 2;
        }

        private void btnReviewLogs_Click(object sender, EventArgs e)
        {
            // Create an instance of your new form
            Review_Activity_Log logForm = new Review_Activity_Log();

            // ShowDialog() freezes the main chat window until they close the log window.
            // If you want them to be able to use both at the same time, use logForm.Show() instead!
            logForm.ShowDialog();
        }

        // ==========================================
        // PDF FONT RESOLVER
        // ==========================================
        public class WindowsSystemFontResolver : PdfSharp.Fonts.IFontResolver
        {
            public byte[] GetFont(string faceName)
            {
                // Map the specific font styles to the actual Windows files
                string fontPath = @"C:\Windows\Fonts\arial.ttf"; // Default to regular Arial

                if (faceName.Contains("Bold")) fontPath = @"C:\Windows\Fonts\arialbd.ttf";
                else if (faceName.Contains("Italic")) fontPath = @"C:\Windows\Fonts\ariali.ttf";

                if (File.Exists(fontPath))
                {
                    return File.ReadAllBytes(fontPath);
                }

                throw new FileNotFoundException($"Could not find font at: {fontPath}");
            }

            public PdfSharp.Fonts.FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                // If the PDF asks for Arial, route it to our files
                if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
                {
                    if (isBold) return new PdfSharp.Fonts.FontResolverInfo("Arial#Bold");
                    if (isItalic) return new PdfSharp.Fonts.FontResolverInfo("Arial#Italic");
                    return new PdfSharp.Fonts.FontResolverInfo("Arial#Regular");
                }

                // Fallback to regular Arial for everything else
                return new PdfSharp.Fonts.FontResolverInfo("Arial#Regular");
            }
        }

        private void btnExportPDF_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(rtbChatHistory.Text))
            {
                MessageBox.Show("There is no chat history to export!", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PDF Document|*.pdf";
                saveDialog.Title = "Save Clinical Session as PDF";
                saveDialog.FileName = $"Clinical_Triage_Session_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Register the font resolver (if not already registered)
                        if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
                        {
                            PdfSharp.Fonts.GlobalFontSettings.FontResolver = new WindowsSystemFontResolver();
                        }

                        // 1. Create a new PDF document
                        PdfDocument document = new PdfDocument();
                        document.Info.Title = "Clinical Diagnostics Triage - Session Export";

                        // 2. Create an empty page
                        PdfPage page = document.AddPage();

                        // 3. Get an XGraphics object for drawing
                        XGraphics gfx = XGraphics.FromPdfPage(page);

                        // ==========================================
                        // NEW: DRAW THE CLINIC ICON / LOGO
                        // ==========================================
                        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.jpeg");
                        int textStartingY = 40; // Default text height if no logo is found

                        if (File.Exists(iconPath))
                        {
                            XImage logo = XImage.FromFile(iconPath);

                            // Draw the image at (X: 40, Y: 40) and resize it to 60x60 pixels
                            gfx.DrawImage(logo, 40, 40, 60, 60);

                            // Push the text starting position down so it doesn't overlap the image!
                            textStartingY = 120;
                        }

                        // 4. Set the font
                        XFont font = new XFont("Arial", 11, XFontStyleEx.Regular);
                        XTextFormatter tf = new XTextFormatter(gfx);

                        // 5. Define the margins and draw the chat history text
                        // Notice we are using 'textStartingY' instead of the hardcoded 40
                        XRect rect = new XRect(40, textStartingY, page.Width - 80, page.Height - textStartingY - 40);
                        tf.DrawString(rtbChatHistory.Text, font, XBrushes.Black, rect, XStringFormats.TopLeft);

                        // 6. Save the document
                        document.Save(saveDialog.FileName);

                        MessageBox.Show("Session successfully exported to PDF!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to generate PDF: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}