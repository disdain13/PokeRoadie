using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PokeRoadie.Forms
{
    public partial class CoordsForm : Form
    {
        private const string keypressFilter = "0123456789.-";

        //private Client _client;
        public double Longitude { get; set; }
        public double Latitude { get; set; }

        public CoordsForm()
        {
            InitializeComponent();
            this.DialogResult = DialogResult.Cancel;
            txtLatitude.KeyPress += OnKeyPress;
            txtLongitude.KeyPress += OnKeyPress;
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLatitude.Text))
            {
                ShowError("Please enter a latitude.");
                txtLatitude.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(txtLongitude.Text))
            {
                ShowError("Please enter a longitude.");
                txtLatitude.Focus();
                return;
            }
            Latitude = Convert.ToDouble(txtLatitude.Text);
            Longitude = Convert.ToDouble(txtLongitude.Text);
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

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = Convert.ToInt32(e.KeyChar) > 31 && !keypressFilter.Contains(e.KeyChar);
        }
    }
}
