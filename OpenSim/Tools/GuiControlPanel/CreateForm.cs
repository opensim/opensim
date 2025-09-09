using System;
using System.Windows.Forms;
using OpenSim.Tools.GuiControlPanel.Models;
using OpenSim.Tools.GuiControlPanel.Services;

namespace OpenSim.Tools.GuiControlPanel
{
    public partial class CreateForm : Form
    {
        private readonly OpenSimManager m_openSimManager;

        public CreateForm(OpenSimManager openSimManager)
        {
            InitializeComponent();
            m_openSimManager = openSimManager;
        }

        private void createButton_Click(object sender, EventArgs e)
        {
            var simName = simNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(simName))
            {
                MessageBox.Show("Simulation name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // In a real application, we would collect more configuration data
            // and create a proper .ini file. For now, we'll just create a dummy
            // file to demonstrate the concept.
            var configPath = System.IO.Path.Combine(m_openSimManager.OpenSimPath, "bin", "Regions", $"{simName}.ini");
            try
            {
                System.IO.File.WriteAllText(configPath, $"[{simName}]\n");
                MessageBox.Show($"Simulation '{simName}' created successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating simulation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
