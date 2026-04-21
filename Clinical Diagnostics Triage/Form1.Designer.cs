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
            rtbChatHistory.Location = new Point(106, 26);
            rtbChatHistory.Name = "rtbChatHistory";
            rtbChatHistory.ReadOnly = true;
            rtbChatHistory.Size = new Size(125, 120);
            rtbChatHistory.TabIndex = 0;
            rtbChatHistory.Text = "";
            rtbChatHistory.TextChanged += rtbChatHistory_TextChanged;
            // 
            // txtInput
            // 
            txtInput.Location = new Point(201, 226);
            txtInput.Multiline = true;
            txtInput.Name = "txtInput";
            txtInput.Size = new Size(125, 34);
            txtInput.TabIndex = 1;
            // 
            // btnSend
            // 
            btnSend.Location = new Point(136, 308);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(94, 29);
            btnSend.TabIndex = 2;
            btnSend.Text = "button1";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            // 
            // btnAttach
            // 
            btnAttach.Location = new Point(340, 310);
            btnAttach.Name = "btnAttach";
            btnAttach.Size = new Size(94, 29);
            btnAttach.TabIndex = 3;
            btnAttach.Text = "📎";
            btnAttach.UseVisualStyleBackColor = true;
            btnAttach.Click += btnAttach_Click;
            // 
            // btnNewPatient
            // 
            btnNewPatient.Location = new Point(567, 314);
            btnNewPatient.Name = "btnNewPatient";
            btnNewPatient.Size = new Size(94, 29);
            btnNewPatient.TabIndex = 4;
            btnNewPatient.Text = "button3";
            btnNewPatient.UseVisualStyleBackColor = true;
            btnNewPatient.Click += btnNewPatient_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(btnNewPatient);
            Controls.Add(btnAttach);
            Controls.Add(btnSend);
            Controls.Add(txtInput);
            Controls.Add(rtbChatHistory);
            Name = "Form1";
            Text = "Form1";
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
