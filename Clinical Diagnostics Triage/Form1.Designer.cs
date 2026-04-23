namespace Clinical_Diagnostics_Triage
{
    partial class Form1
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
            rtbChatHistory = new RichTextBox();
            txtInput = new TextBox();
            btnSend = new Button();
            btnAttach = new Button();
            btnNewPatient = new Button();
            SuspendLayout();
            // 
            // rtbChatHistory
            // 
            rtbChatHistory.BackColor = Color.FromArgb(30, 30, 30);
            rtbChatHistory.BorderStyle = BorderStyle.None;
            rtbChatHistory.Location = new Point(51, 38);
            rtbChatHistory.Name = "rtbChatHistory";
            rtbChatHistory.ReadOnly = true;
            rtbChatHistory.Size = new Size(806, 339);
            rtbChatHistory.TabIndex = 0;
            rtbChatHistory.Text = "";
            rtbChatHistory.TextChanged += rtbChatHistory_TextChanged;
            // 
            // txtInput
            // 
            txtInput.BackColor = Color.FromArgb(45, 45, 45);
            txtInput.BorderStyle = BorderStyle.FixedSingle;
            txtInput.ForeColor = Color.White;
            txtInput.Location = new Point(51, 401);
            txtInput.Multiline = true;
            txtInput.Name = "txtInput";
            txtInput.PlaceholderText = "Ask Niel, Your Professional Clinical Assistance";
            txtInput.Size = new Size(676, 38);
            txtInput.TabIndex = 1;
            txtInput.TextChanged += txtInput_TextChanged;
            // 
            // btnSend
            // 
            btnSend.BackColor = Color.FromArgb(64, 64, 64);
            btnSend.Cursor = Cursors.Hand;
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.Location = new Point(753, 404);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(95, 33);
            btnSend.TabIndex = 2;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = false;
            btnSend.Click += btnSend_Click;
            // 
            // btnAttach
            // 
            btnAttach.BackColor = Color.FromArgb(64, 64, 64);
            btnAttach.Cursor = Cursors.Hand;
            btnAttach.FlatAppearance.BorderSize = 0;
            btnAttach.FlatStyle = FlatStyle.Flat;
            btnAttach.Location = new Point(51, 456);
            btnAttach.Name = "btnAttach";
            btnAttach.Size = new Size(106, 33);
            btnAttach.TabIndex = 3;
            btnAttach.Text = "📎";
            btnAttach.UseVisualStyleBackColor = false;
            btnAttach.Click += btnAttach_Click;
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
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            ClientSize = new Size(900, 518);
            Controls.Add(btnNewPatient);
            Controls.Add(btnAttach);
            Controls.Add(btnSend);
            Controls.Add(txtInput);
            Controls.Add(rtbChatHistory);
            Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ForeColor = Color.WhiteSmoke;
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private RichTextBox rtbChatHistory;
        private TextBox txtInput;
        private Button btnSend;
        private Button btnAttach;
        private Button btnNewPatient;
    }
}
