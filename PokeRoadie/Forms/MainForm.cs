using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;

namespace PokeRoadie.Forms
{
    public partial class MainForm : Form
    {
        private PokeRoadieClient Client;
        private GMapControl Map;
        public MainForm(PokeRoadieClient client)
        {

            InitializeComponent();
            Client = client;
            Map = new GMapControl();

            Controls.Add(Map);

            // Initialize map:
            // Use google provider
            Map.MapProvider = GoogleMapProvider.Instance;

            // Get tiles from server only
            Map.Manager.Mode = AccessMode.ServerOnly;

            // Do not use proxy
            GMapProvider.WebProxy = null;

            // Zoom min/max
            Map.CenterPen = new Pen(Color.Red, 2);
            Map.MinZoom = trackBar.Maximum = 1;
            Map.MaxZoom = trackBar.Maximum = 20;

            // Set zoom
            trackBar.Value = 17;
            Map.Zoom = trackBar.Value;
        }

        private void trackBar_Scroll(object sender, EventArgs e)
        {
            Map.Zoom = trackBar.Value;
        }

        private void tmrUpdate_Tick(object sender, EventArgs e)
        {
            Map.Position = new PointLatLng(Client.CurrentLatitude, Client.CurrentLongitude);
        }
    }
}
