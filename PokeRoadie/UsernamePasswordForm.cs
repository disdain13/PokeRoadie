using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PokeRoadie
{
    public partial class UsernamePasswordForm : Form
    {
        //private Client _client;
        public string Username { get; set; }
        public string Password { get; set; }
        public string AuthType { get; set; }

        public UsernamePasswordForm()
        {
            InitializeComponent();
            this.DialogResult = DialogResult.Cancel;
            AuthType = "Google";
        }
        //public UsernamePasswordForm(Client client)
        //    : base ()
        //{
        //    _client = client;
            
        //}

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                ShowError("Please enter a username.");
                txtUsername.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                ShowError("Please enter a password.");
                txtUsername.Focus();
                return;
            }
            Username = txtUsername.Text;
            Password = txtPassword.Text;
            AuthType = rbGoogle.Checked ? "Google" : "Ptc";
            this.DialogResult = DialogResult.OK;
        }

        private void CloseErrorButton_Click(object sender, EventArgs e)
        {
            ErrorPanel.Visible = false;
        }

        void ShowError(string message)
        {
            if (String.IsNullOrWhiteSpace(message)) return;
            if (!String.IsNullOrWhiteSpace(ErrorLabel.Text)) ErrorLabel.Text += Environment.NewLine;
            ErrorLabel.Text += message;
            if (!ErrorPanel.Visible) ErrorPanel.Visible = true;
        }


    }
}
