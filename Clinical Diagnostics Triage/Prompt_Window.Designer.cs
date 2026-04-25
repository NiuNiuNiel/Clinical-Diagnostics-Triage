namespace Clinical_Diagnostics_Triage
{
    partial class Prompt_Window
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Prompt_Window));
            rtbChatHistory = new RichTextBox();
            txtInput = new TextBox();
            btnSend = new Button();
            btnAttach_Note = new Button();
            btnNewPatient = new Button();
            lblTitle = new Label();
            bmFile_attachBtn = new Button();
            label1 = new Label();
            label2 = new Label();
            btnReviewLogs = new Button();
            btnExportPDF = new Button();
            SuspendLayout();
            // 
            // rtbChatHistory
            // 
            rtbChatHistory.BackColor = Color.FromArgb(30, 30, 30);
            rtbChatHistory.BorderStyle = BorderStyle.None;
            rtbChatHistory.Location = new Point(99, 59);
            rtbChatHistory.Name = "rtbChatHistory";
            rtbChatHistory.ReadOnly = true;
            rtbChatHistory.ScrollBars = RichTextBoxScrollBars.Vertical;
            rtbChatHistory.Size = new Size(687, 339);
            rtbChatHistory.TabIndex = 0;
            rtbChatHistory.Text = "";
            rtbChatHistory.TextChanged += rtbChatHistory_TextChanged;
            // 
            // txtInput
            // 
            txtInput.BackColor = Color.FromArgb(45, 45, 45);
            txtInput.BorderStyle = BorderStyle.FixedSingle;
            txtInput.ForeColor = Color.White;
            txtInput.Location = new Point(51, 397);
            txtInput.Multiline = true;
            txtInput.Name = "txtInput";
            txtInput.PlaceholderText = "Ask Niel, Your Professional Clinical Assistance";
            txtInput.Size = new Size(701, 55);
            txtInput.TabIndex = 1;
            txtInput.TextChanged += txtInput_TextChanged;
            // 
            // btnSend
            // 
            btnSend.BackColor = Color.FromArgb(64, 64, 64);
            btnSend.Cursor = Cursors.Hand;
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.Location = new Point(773, 404);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(95, 33);
            btnSend.TabIndex = 2;
            btnSend.Text = "Send ➤";
            btnSend.UseVisualStyleBackColor = false;
            btnSend.Click += btnSend_Click;
            // 
            // btnAttach_Note
            // 
            btnAttach_Note.BackColor = Color.FromArgb(64, 64, 64);
            btnAttach_Note.Cursor = Cursors.Hand;
            btnAttach_Note.FlatAppearance.BorderSize = 0;
            btnAttach_Note.FlatStyle = FlatStyle.Flat;
            btnAttach_Note.Location = new Point(168, 458);
            btnAttach_Note.Name = "btnAttach_Note";
            btnAttach_Note.Size = new Size(106, 33);
            btnAttach_Note.TabIndex = 3;
            btnAttach_Note.Text = "📎";
            btnAttach_Note.UseVisualStyleBackColor = false;
            btnAttach_Note.Click += btnAttach_Click;
            // 
            // btnNewPatient
            // 
            btnNewPatient.BackColor = Color.FromArgb(64, 64, 64);
            btnNewPatient.Cursor = Cursors.Hand;
            btnNewPatient.FlatAppearance.BorderSize = 0;
            btnNewPatient.FlatStyle = FlatStyle.Flat;
            btnNewPatient.Location = new Point(773, 12);
            btnNewPatient.Name = "btnNewPatient";
            btnNewPatient.Size = new Size(106, 33);
            btnNewPatient.TabIndex = 4;
            btnNewPatient.Text = "New Chat";
            btnNewPatient.UseVisualStyleBackColor = false;
            btnNewPatient.Click += btnNewPatient_Click;
            // 
            // lblTitle
            // 
            lblTitle.Location = new Point(347, 17);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(219, 23);
            lblTitle.TabIndex = 5;
            lblTitle.Text = "Clinical Triage Copilot";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // bmFile_attachBtn
            // 
            bmFile_attachBtn.BackColor = Color.FromArgb(64, 64, 64);
            bmFile_attachBtn.Cursor = Cursors.Hand;
            bmFile_attachBtn.FlatAppearance.BorderSize = 0;
            bmFile_attachBtn.FlatStyle = FlatStyle.Flat;
            bmFile_attachBtn.Location = new Point(411, 458);
            bmFile_attachBtn.Name = "bmFile_attachBtn";
            bmFile_attachBtn.Size = new Size(106, 33);
            bmFile_attachBtn.TabIndex = 7;
            bmFile_attachBtn.Text = "📎";
            bmFile_attachBtn.UseVisualStyleBackColor = false;
            bmFile_attachBtn.Click += bmFile_attachBtn_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(51, 463);
            label1.Name = "label1";
            label1.Size = new Size(88, 19);
            label1.TabIndex = 8;
            label1.Text = "Clinical Note:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(278, 463);
            label2.Name = "label2";
            label2.Size = new Size(101, 19);
            label2.TabIndex = 9;
            label2.Text = "Biomedical File:";
            // 
            // btnReviewLogs
            // 
            btnReviewLogs.BackColor = Color.FromArgb(64, 64, 64);
            btnReviewLogs.Cursor = Cursors.Hand;
            btnReviewLogs.FlatAppearance.BorderSize = 0;
            btnReviewLogs.FlatStyle = FlatStyle.Flat;
            btnReviewLogs.Location = new Point(29, 12);
            btnReviewLogs.Name = "btnReviewLogs";
            btnReviewLogs.Size = new Size(103, 33);
            btnReviewLogs.TabIndex = 10;
            btnReviewLogs.Text = "Log Review";
            btnReviewLogs.UseVisualStyleBackColor = false;
            btnReviewLogs.Click += btnReviewLogs_Click;
            // 
            // btnExportPDF
            // 
            btnExportPDF.BackColor = Color.FromArgb(64, 64, 64);
            btnExportPDF.Cursor = Cursors.Hand;
            btnExportPDF.FlatAppearance.BorderSize = 0;
            btnExportPDF.FlatStyle = FlatStyle.Flat;
            btnExportPDF.Font = new Font("Segoe UI", 7.8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnExportPDF.Location = new Point(577, 459);
            btnExportPDF.Name = "btnExportPDF";
            btnExportPDF.Size = new Size(120, 33);
            btnExportPDF.TabIndex = 13;
            btnExportPDF.Text = "Export To PDF";
            btnExportPDF.UseVisualStyleBackColor = false;
            btnExportPDF.Click += btnExportPDF_Click;
            // 
            // Prompt_Window
            // 
            AutoScaleDimensions = new SizeF(8F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            ClientSize = new Size(900, 518);
            Controls.Add(btnExportPDF);
            Controls.Add(btnReviewLogs);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(bmFile_attachBtn);
            Controls.Add(lblTitle);
            Controls.Add(btnNewPatient);
            Controls.Add(btnAttach_Note);
            Controls.Add(btnSend);
            Controls.Add(txtInput);
            Controls.Add(rtbChatHistory);
            Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ForeColor = Color.WhiteSmoke;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Prompt_Window";
            Text = "Clinical Triage Copilot";
            Load += Prompt_Window_Load;
            Resize += Prompt_Window_Resize;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private RichTextBox rtbChatHistory;
        private TextBox txtInput;
        private Button btnSend;
        private Button btnAttach_Note;
        private Button btnNewPatient;
        private Label lblTitle;
        private Button bmFile_attachBtn;
        private Label label1;
        private Label label2;
        private Button btnReviewLogs;
        private Button btnExportPDF;
    }
}
