namespace PokemonGo.RocketAPI.Console
{
    partial class UsernamePasswordForm
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
            this.gbAuthType = new System.Windows.Forms.GroupBox();
            this.rbPtc = new System.Windows.Forms.RadioButton();
            this.rbGoogle = new System.Windows.Forms.RadioButton();
            this.lblUsername = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnLogin = new System.Windows.Forms.Button();
            this.ErrorPanel = new System.Windows.Forms.Panel();
            this.ErrorLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.CloseErrorButton = new System.Windows.Forms.Button();
            this.ErrorLabel = new System.Windows.Forms.Label();
            this.pnlMain = new System.Windows.Forms.Panel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.gbAuthType.SuspendLayout();
            this.ErrorPanel.SuspendLayout();
            this.ErrorLayoutPanel.SuspendLayout();
            this.pnlMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbAuthType
            // 
            this.gbAuthType.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbAuthType.Controls.Add(this.rbPtc);
            this.gbAuthType.Controls.Add(this.rbGoogle);
            this.gbAuthType.Location = new System.Drawing.Point(12, 53);
            this.gbAuthType.Name = "gbAuthType";
            this.gbAuthType.Size = new System.Drawing.Size(280, 54);
            this.gbAuthType.TabIndex = 0;
            this.gbAuthType.TabStop = false;
            this.gbAuthType.Text = "Authentication Type";
            // 
            // rbPtc
            // 
            this.rbPtc.AutoSize = true;
            this.rbPtc.Location = new System.Drawing.Point(103, 22);
            this.rbPtc.Name = "rbPtc";
            this.rbPtc.Size = new System.Drawing.Size(64, 17);
            this.rbPtc.TabIndex = 1;
            this.rbPtc.TabStop = true;
            this.rbPtc.Text = "Website";
            this.rbPtc.UseVisualStyleBackColor = true;
            // 
            // rbGoogle
            // 
            this.rbGoogle.AutoSize = true;
            this.rbGoogle.Checked = true;
            this.rbGoogle.Location = new System.Drawing.Point(22, 22);
            this.rbGoogle.Name = "rbGoogle";
            this.rbGoogle.Size = new System.Drawing.Size(59, 17);
            this.rbGoogle.TabIndex = 0;
            this.rbGoogle.TabStop = true;
            this.rbGoogle.Text = "Google";
            this.rbGoogle.UseVisualStyleBackColor = true;
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(21, 121);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(58, 13);
            this.lblUsername.TabIndex = 1;
            this.lblUsername.Text = "Username:";
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(21, 160);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(56, 13);
            this.lblPassword.TabIndex = 2;
            this.lblPassword.Text = "Password:";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(85, 118);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(194, 20);
            this.txtUsername.TabIndex = 3;
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(83, 157);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Size = new System.Drawing.Size(194, 20);
            this.txtPassword.TabIndex = 4;
            // 
            // btnLogin
            // 
            this.btnLogin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLogin.Location = new System.Drawing.Point(217, 243);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(75, 23);
            this.btnLogin.TabIndex = 5;
            this.btnLogin.Text = "Login";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
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
            this.ErrorPanel.Size = new System.Drawing.Size(304, 45);
            this.ErrorPanel.TabIndex = 7;
            this.ErrorPanel.Visible = false;
            // 
            // ErrorLayoutPanel
            // 
            this.ErrorLayoutPanel.AutoSize = true;
            this.ErrorLayoutPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ErrorLayoutPanel.ColumnCount = 2;
            this.ErrorLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 89.72603F));
            this.ErrorLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10.27397F));
            this.ErrorLayoutPanel.Controls.Add(this.CloseErrorButton, 1, 0);
            this.ErrorLayoutPanel.Controls.Add(this.ErrorLabel, 0, 0);
            this.ErrorLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ErrorLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.ErrorLayoutPanel.Name = "ErrorLayoutPanel";
            this.ErrorLayoutPanel.Padding = new System.Windows.Forms.Padding(5);
            this.ErrorLayoutPanel.RowCount = 1;
            this.ErrorLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.ErrorLayoutPanel.Size = new System.Drawing.Size(302, 43);
            this.ErrorLayoutPanel.TabIndex = 0;
            // 
            // CloseErrorButton
            // 
            this.CloseErrorButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.CloseErrorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.CloseErrorButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CloseErrorButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.CloseErrorButton.Location = new System.Drawing.Point(273, 8);
            this.CloseErrorButton.Name = "CloseErrorButton";
            this.CloseErrorButton.Size = new System.Drawing.Size(21, 27);
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
            this.pnlMain.Controls.Add(this.gbAuthType);
            this.pnlMain.Controls.Add(this.lblUsername);
            this.pnlMain.Controls.Add(this.txtPassword);
            this.pnlMain.Controls.Add(this.lblPassword);
            this.pnlMain.Controls.Add(this.txtUsername);
            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMain.Location = new System.Drawing.Point(0, 45);
            this.pnlMain.Name = "pnlMain";
            this.pnlMain.Size = new System.Drawing.Size(304, 192);
            this.pnlMain.TabIndex = 8;
            // 
            // lblDescription
            // 
            this.lblDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDescription.Location = new System.Drawing.Point(15, 10);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(277, 24);
            this.lblDescription.TabIndex = 1;
            this.lblDescription.Text = "Please specifiy your login information below:";
            // 
            // UsernamePasswordForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(304, 280);
            this.Controls.Add(this.pnlMain);
            this.Controls.Add(this.ErrorPanel);
            this.Controls.Add(this.btnLogin);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "UsernamePasswordForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Authentication";
            this.TopMost = true;
            this.gbAuthType.ResumeLayout(false);
            this.gbAuthType.PerformLayout();
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

        private System.Windows.Forms.GroupBox gbAuthType;
        private System.Windows.Forms.RadioButton rbPtc;
        private System.Windows.Forms.RadioButton rbGoogle;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Button btnLogin;
        internal System.Windows.Forms.Panel ErrorPanel;
        internal System.Windows.Forms.TableLayoutPanel ErrorLayoutPanel;
        internal System.Windows.Forms.Label ErrorLabel;
        private System.Windows.Forms.Panel pnlMain;
        private System.Windows.Forms.Label lblDescription;
        internal System.Windows.Forms.Button CloseErrorButton;
    }
}