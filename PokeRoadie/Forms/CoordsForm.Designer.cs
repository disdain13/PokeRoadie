namespace PokeRoadie.Forms
{
    partial class CoordsForm
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
            this.lblLatitude = new System.Windows.Forms.Label();
            this.lblLongitude = new System.Windows.Forms.Label();
            this.txtLatitude = new System.Windows.Forms.TextBox();
            this.txtLongitude = new System.Windows.Forms.TextBox();
            this.btnSubmit = new System.Windows.Forms.Button();
            this.ErrorPanel = new System.Windows.Forms.Panel();
            this.ErrorLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.CloseErrorButton = new System.Windows.Forms.Button();
            this.ErrorLabel = new System.Windows.Forms.Label();
            this.pnlMain = new System.Windows.Forms.Panel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.ErrorPanel.SuspendLayout();
            this.ErrorLayoutPanel.SuspendLayout();
            this.pnlMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblLatitude
            // 
            this.lblLatitude.AutoSize = true;
            this.lblLatitude.Location = new System.Drawing.Point(23, 55);
            this.lblLatitude.Name = "lblLatitude";
            this.lblLatitude.Size = new System.Drawing.Size(48, 13);
            this.lblLatitude.TabIndex = 1;
            this.lblLatitude.Text = "Latitude:";
            // 
            // lblLongitude
            // 
            this.lblLongitude.AutoSize = true;
            this.lblLongitude.Location = new System.Drawing.Point(23, 94);
            this.lblLongitude.Name = "lblLongitude";
            this.lblLongitude.Size = new System.Drawing.Size(57, 13);
            this.lblLongitude.TabIndex = 2;
            this.lblLongitude.Text = "Longitude:";
            // 
            // txtLatitude
            // 
            this.txtLatitude.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLatitude.Location = new System.Drawing.Point(85, 52);
            this.txtLatitude.MaxLength = 10;
            this.txtLatitude.Name = "txtLatitude";
            this.txtLatitude.Size = new System.Drawing.Size(121, 20);
            this.txtLatitude.TabIndex = 3;
            // 
            // txtLongitude
            // 
            this.txtLongitude.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLongitude.Location = new System.Drawing.Point(85, 91);
            this.txtLongitude.MaxLength = 10;
            this.txtLongitude.Name = "txtLongitude";
            this.txtLongitude.Size = new System.Drawing.Size(121, 20);
            this.txtLongitude.TabIndex = 4;
            // 
            // btnSubmit
            // 
            this.btnSubmit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSubmit.Location = new System.Drawing.Point(146, 179);
            this.btnSubmit.Name = "btnSubmit";
            this.btnSubmit.Size = new System.Drawing.Size(75, 23);
            this.btnSubmit.TabIndex = 5;
            this.btnSubmit.Text = "Submit";
            this.btnSubmit.UseVisualStyleBackColor = true;
            this.btnSubmit.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // ErrorPanel
            // 
            this.ErrorPanel.AutoSize = true;
            this.ErrorPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.ErrorPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ErrorPanel.Controls.Add(this.ErrorLayoutPanel);
            this.ErrorPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.ErrorPanel.Location = new System.Drawing.Point(0, 0);
            this.ErrorPanel.Name = "ErrorPanel";
            this.ErrorPanel.Size = new System.Drawing.Size(233, 45);
            this.ErrorPanel.TabIndex = 7;
            this.ErrorPanel.Visible = false;
            // 
            // ErrorLayoutPanel
            // 
            this.ErrorLayoutPanel.AutoSize = true;
            this.ErrorLayoutPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ErrorLayoutPanel.ColumnCount = 2;
            this.ErrorLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 86.60714F));
            this.ErrorLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13.39286F));
            this.ErrorLayoutPanel.Controls.Add(this.CloseErrorButton, 1, 0);
            this.ErrorLayoutPanel.Controls.Add(this.ErrorLabel, 0, 0);
            this.ErrorLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ErrorLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.ErrorLayoutPanel.Name = "ErrorLayoutPanel";
            this.ErrorLayoutPanel.Padding = new System.Windows.Forms.Padding(5);
            this.ErrorLayoutPanel.RowCount = 1;
            this.ErrorLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.ErrorLayoutPanel.Size = new System.Drawing.Size(231, 43);
            this.ErrorLayoutPanel.TabIndex = 0;
            // 
            // CloseErrorButton
            // 
            this.CloseErrorButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.CloseErrorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.CloseErrorButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CloseErrorButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.CloseErrorButton.Location = new System.Drawing.Point(199, 8);
            this.CloseErrorButton.Name = "CloseErrorButton";
            this.CloseErrorButton.Size = new System.Drawing.Size(24, 27);
            this.CloseErrorButton.TabIndex = 2;
            this.CloseErrorButton.Text = "x";
            this.CloseErrorButton.UseVisualStyleBackColor = true;
            this.CloseErrorButton.Click += new System.EventHandler(this.CloseErrorButton_Click);
            // 
            // ErrorLabel
            // 
            this.ErrorLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.ErrorLabel.AutoEllipsis = true;
            this.ErrorLabel.AutoSize = true;
            this.ErrorLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.ErrorLabel.Location = new System.Drawing.Point(8, 15);
            this.ErrorLabel.Name = "ErrorLabel";
            this.ErrorLabel.Size = new System.Drawing.Size(75, 13);
            this.ErrorLabel.TabIndex = 0;
            this.ErrorLabel.Text = "Error Message";
            // 
            // pnlMain
            // 
            this.pnlMain.Controls.Add(this.lblDescription);
            this.pnlMain.Controls.Add(this.lblLatitude);
            this.pnlMain.Controls.Add(this.txtLongitude);
            this.pnlMain.Controls.Add(this.lblLongitude);
            this.pnlMain.Controls.Add(this.txtLatitude);
            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMain.Location = new System.Drawing.Point(0, 45);
            this.pnlMain.Name = "pnlMain";
            this.pnlMain.Size = new System.Drawing.Size(233, 131);
            this.pnlMain.TabIndex = 8;
            // 
            // lblDescription
            // 
            this.lblDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDescription.Location = new System.Drawing.Point(15, 10);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(206, 31);
            this.lblDescription.TabIndex = 1;
            this.lblDescription.Text = "Please specifiy your starting coordinates below:";
            // 
            // CoordsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(233, 216);
            this.Controls.Add(this.pnlMain);
            this.Controls.Add(this.ErrorPanel);
            this.Controls.Add(this.btnSubmit);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "CoordsForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Starting Coordinates:";
            this.TopMost = true;
            this.ErrorPanel.ResumeLayout(false);
            this.ErrorPanel.PerformLayout();
            this.ErrorLayoutPanel.ResumeLayout(false);
            this.ErrorLayoutPanel.PerformLayout();
            this.pnlMain.ResumeLayout(false);
            this.pnlMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label lblLatitude;
        private System.Windows.Forms.Label lblLongitude;
        private System.Windows.Forms.TextBox txtLatitude;
        private System.Windows.Forms.TextBox txtLongitude;
        private System.Windows.Forms.Button btnSubmit;
        internal System.Windows.Forms.Panel ErrorPanel;
        internal System.Windows.Forms.TableLayoutPanel ErrorLayoutPanel;
        internal System.Windows.Forms.Label ErrorLabel;
        private System.Windows.Forms.Panel pnlMain;
        private System.Windows.Forms.Label lblDescription;
        internal System.Windows.Forms.Button CloseErrorButton;
    }
}