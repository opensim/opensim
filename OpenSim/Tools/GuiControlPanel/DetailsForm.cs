using System;
using System.Windows.Forms;
using OpenSim.Tools.GuiControlPanel.Models;

namespace OpenSim.Tools.GuiControlPanel
{
    public partial class DetailsForm : Form
    {
        public DetailsForm(SimInstance instance)
        {
            InitializeComponent();

            if (instance != null)
            {
                nameLabel.Text = instance.Name;
                statusLabel.Text = instance.StatusText;
                pidLabel.Text = instance.ProcessId.ToString();
                startTimeLabel.Text = instance.StartTime?.ToString() ?? "N/A";
                uptimeLabel.Text = instance.UptimeText;
                configPathLabel.Text = instance.ConfigPath;
                errorMessageLabel.Text = instance.ErrorMessage;
            }
        }
    }
}
