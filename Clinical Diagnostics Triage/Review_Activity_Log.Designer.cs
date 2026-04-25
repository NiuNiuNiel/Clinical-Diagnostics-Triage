namespace Clinical_Diagnostics_Triage
{
    partial class Review_Activity_Log
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Review_Activity_Log));
            lblTitle = new Label();
            dgvLogs = new DataGridView();
            btnExportRow = new Button();
            cmbDateSelect = new ComboBox();
            label1 = new Label();
            ((System.ComponentModel.ISupportInitialize)dgvLogs).BeginInit();
            SuspendLayout();
            // 
            // lblTitle
            // 
            lblTitle.ForeColor = Color.Snow;
            lblTitle.Location = new Point(334, 29);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(219, 23);
            lblTitle.TabIndex = 6;
            lblTitle.Text = "Export Activity Log";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dgvLogs
            // 
            dgvLogs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvLogs.Location = new Point(52, 75);
            dgvLogs.Name = "dgvLogs";
            dgvLogs.RowHeadersWidth = 51;
            dgvLogs.Size = new Size(818, 408);
            dgvLogs.TabIndex = 7;
            // 
            // btnExportRow
            // 
            btnExportRow.BackColor = Color.Gray;
            btnExportRow.FlatAppearance.BorderSize = 0;
            btnExportRow.FlatStyle = FlatStyle.Flat;
            btnExportRow.ForeColor = SystemColors.ButtonFace;
            btnExportRow.Location = new Point(391, 518);
            btnExportRow.Name = "btnExportRow";
            btnExportRow.Size = new Size(94, 29);
            btnExportRow.TabIndex = 8;
            btnExportRow.Text = "Export";
            btnExportRow.UseVisualStyleBackColor = false;
            btnExportRow.Click += new System.EventHandler(this.btnExportRow_Click);
            // 
            // cmbDateSelect
            // 
            cmbDateSelect.BackColor = Color.Gray;
            cmbDateSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDateSelect.FlatStyle = FlatStyle.Flat;
            cmbDateSelect.ForeColor = SystemColors.Window;
            cmbDateSelect.FormattingEnabled = true;
            cmbDateSelect.Location = new Point(719, 29);
            cmbDateSelect.Name = "cmbDateSelect";
            cmbDateSelect.Size = new Size(151, 28);
            cmbDateSelect.TabIndex = 10;
            cmbDateSelect.SelectedIndexChanged += new System.EventHandler(this.cmbDateSelect_SelectedIndexChanged);
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = SystemColors.ButtonFace;
            label1.Location = new Point(621, 32);
            label1.Name = "label1";
            label1.Size = new Size(92, 20);
            label1.TabIndex = 11;
            label1.Text = "Select Date: ";
            // 
            // Review_Activity_Log
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            ClientSize = new Size(914, 600);
            Controls.Add(label1);
            Controls.Add(cmbDateSelect);
            Controls.Add(btnExportRow);
            Controls.Add(dgvLogs);
            Controls.Add(lblTitle);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 4, 3, 4);
            Name = "Review_Activity_Log";
            Text = "Activity Log";
            this.Load += new System.EventHandler(this.Review_Activity_Log_Load);
            ((System.ComponentModel.ISupportInitialize)dgvLogs).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblTitle;
        private DataGridView dgvLogs;
        private Button btnExportRow;
        private ComboBox cmbDateSelect;
        private Label label1;
    }
}