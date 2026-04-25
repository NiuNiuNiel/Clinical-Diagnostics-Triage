using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;

namespace Clinical_Diagnostics_Triage
{
    public partial class Review_Activity_Log : Form
    {
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public Review_Activity_Log()
        {
            InitializeComponent();
        }

        private void Review_Activity_Log_Load(object sender, EventArgs e)
        {
            StyleDataGridView();

            btnExportRow.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, btnExportRow.Width, btnExportRow.Height, 15, 15));
            btnExportRow.BackColor = Color.FromArgb(45, 45, 48);
            btnExportRow.ForeColor = Color.WhiteSmoke;
            btnExportRow.FlatAppearance.BorderSize = 0;

            // Scan the folder and load the ComboBox first!
            LoadAvailableDates();
        }

        private void StyleDataGridView()
        {
            dgvLogs.EnableHeadersVisualStyles = false;
            dgvLogs.BorderStyle = BorderStyle.None;
            dgvLogs.BackgroundColor = Color.FromArgb(30, 30, 30);
            dgvLogs.GridColor = Color.FromArgb(64, 64, 64);

            dgvLogs.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgvLogs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            dgvLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgvLogs.ColumnHeadersHeight = 40;

            dgvLogs.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            dgvLogs.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvLogs.DefaultCellStyle.Font = new Font("Segoe UI", 10);

            dgvLogs.DefaultCellStyle.SelectionBackColor = Color.MediumPurple;
            dgvLogs.DefaultCellStyle.SelectionForeColor = Color.White;

            dgvLogs.RowHeadersVisible = false;
            dgvLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvLogs.MultiSelect = false;
            dgvLogs.AllowUserToAddRows = false;
            dgvLogs.ReadOnly = true;
            dgvLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgvLogs.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, dgvLogs.Width, dgvLogs.Height, 10, 10));
        }

        // ==========================================
        // DYNAMIC FOLDER SCANNER
        // ==========================================
        public class LogFileItem
        {
            public string DisplayName { get; set; }
            public string FullPath { get; set; }
            public override string ToString() { return DisplayName; }
        }

        private string FindActivityLogFolder()
        {
            // 1. Get current .exe directory (Equivalent to Python's __file__ directory)
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                // 2. Replicate Python's "..", "..", "..", ".." 
                // This climbs exactly 4 levels up: net8.0-windows -> Debug -> bin -> Project -> Solution Root
                DirectoryInfo repoRoot = new DirectoryInfo(currentDir).Parent.Parent.Parent.Parent;

                if (repoRoot != null)
                {
                    // Equivalent to Python's os.path.join(repo_root, 'Activity_Log')
                    string logFolder = Path.Combine(repoRoot.FullName, "Activity_Log");

                    if (Directory.Exists(logFolder))
                    {
                        return logFolder; // Found the exact folder Python is using!
                    }
                }
            }
            catch
            {
                // Silently ignore if the folder structure isn't exactly 4 levels deep 
            }

            // 3. Fallback (Useful for when you eventually publish the final .exe to a doctor's PC)
            return Path.Combine(currentDir, "Activity_Log");
        }

        private void LoadAvailableDates()
        {
            cmbDateSelect.Items.Clear();

            // 1. USE THE NEW RADAR FUNCTION INSTEAD OF THE HARDCODED PATH
            string logFolder = FindActivityLogFolder();

            if (Directory.Exists(logFolder))
            {
                // Directly search for *.tsv files only, sorted newest first
                string[] files = Directory.GetFiles(logFolder, "*.tsv")
                    .OrderByDescending(f => f).ToArray();

                foreach (string file in files)
                {
                    cmbDateSelect.Items.Add(new LogFileItem
                    {
                        DisplayName = Path.GetFileNameWithoutExtension(file),
                        FullPath = file
                    });
                }
            }

            if (cmbDateSelect.Items.Count > 0)
            {
                cmbDateSelect.SelectedIndex = 0;
            }
            else
            {
                cmbDateSelect.Items.Add(new LogFileItem { DisplayName = "No TSV Logs Found", FullPath = "" });
                cmbDateSelect.SelectedIndex = 0;
            }
        }

        // ==========================================
        // DROP DOWN CHANGE EVENT
        // ==========================================
        // NOTE: Double click your ComboBox in the Designer to map this event if it doesn't map automatically!
        private void cmbDateSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            LogFileItem selected = cmbDateSelect.SelectedItem as LogFileItem;
            if (selected != null && !string.IsNullOrEmpty(selected.FullPath))
            {
                LoadActivityLogData(selected.FullPath);
            }
            else
            {
                LoadActivityLogData(null);
            }
        }

        // ==========================================
        // DATA PARSER
        // ==========================================
        private void LoadActivityLogData(string targetFile)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Activity ID");
            dt.Columns.Add("Action Type");
            dt.Columns.Add("Details");

            if (!string.IsNullOrEmpty(targetFile) && File.Exists(targetFile))
            {
                string[] lines = File.ReadAllLines(targetFile);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Strictly split by Tab character (\t), maximum of 3 columns
                    string[] parts = line.Split(new char[] { '\t' }, 3);

                    if (parts.Length == 3)
                    {
                        dt.Rows.Add(parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
                    }
                }
            }
            else
            {
                dt.Rows.Add("SYSTEM", "Info", "No activity logs found.");
            }

            dgvLogs.DataSource = dt;
        }

        // ==========================================
        // SINGLE ROW PDF EXPORT
        // ==========================================
        private void btnExportRow_Click(object sender, EventArgs e)
        {
            if (dgvLogs.SelectedRows.Count == 0 || dgvLogs.SelectedRows[0].Cells[0].Value.ToString() == "SYSTEM")
            {
                MessageBox.Show("Please select a valid activity log row to export.", "Select Row", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string activityId = dgvLogs.SelectedRows[0].Cells[0].Value.ToString();
            string actionType = dgvLogs.SelectedRows[0].Cells[1].Value.ToString();
            string details = dgvLogs.SelectedRows[0].Cells[2].Value.ToString();

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PDF Document|*.pdf";
                saveDialog.Title = "Save Activity Log as PDF";
                saveDialog.FileName = $"Activity_Log_{activityId}.pdf";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
                        {
                            PdfSharp.Fonts.GlobalFontSettings.FontResolver = new Prompt_Window.WindowsSystemFontResolver();
                        }
                        PdfDocument document = new PdfDocument();
                        document.Info.Title = $"Activity Log - {activityId}";
                        PdfPage page = document.AddPage();
                        XGraphics gfx = XGraphics.FromPdfPage(page);

                        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon", "icon.jpeg");
                        int textY = 40;
                        if (File.Exists(iconPath))
                        {
                            XImage logo = XImage.FromFile(iconPath);
                            gfx.DrawImage(logo, 40, 40, 60, 60);
                            textY = 120;
                        }

                        XFont titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
                        XFont headerFont = new XFont("Arial", 12, XFontStyleEx.Bold);
                        XFont regularFont = new XFont("Arial", 11, XFontStyleEx.Regular);
                        XTextFormatter tf = new XTextFormatter(gfx);

                        gfx.DrawString("Clinical Copilot - Activity Log Audit", titleFont, XBrushes.Black, new XPoint(40, textY));
                        textY += 30;

                        gfx.DrawString($"Date Exported: {DateTime.Now}", regularFont, XBrushes.DarkGray, new XPoint(40, textY));
                        textY += 40;

                        gfx.DrawString($"Activity ID: {activityId}", headerFont, XBrushes.Black, new XPoint(40, textY));
                        textY += 25;
                        gfx.DrawString($"Action Type: {actionType}", headerFont, XBrushes.Black, new XPoint(40, textY));
                        textY += 35;

                        XRect detailsRect = new XRect(40, textY, page.Width - 80, page.Height - textY - 40);
                        tf.DrawString($"Raw Output Details:\n\n{details}", regularFont, XBrushes.Black, detailsRect, XStringFormats.TopLeft);

                        document.Save(saveDialog.FileName);
                        MessageBox.Show("Row successfully exported to PDF!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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