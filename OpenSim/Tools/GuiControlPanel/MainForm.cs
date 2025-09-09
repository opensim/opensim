using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenSim.Tools.GuiControlPanel.Services;
using OpenSim.Tools.GuiControlPanel.Models;

namespace OpenSim.Tools.GuiControlPanel
{
    public partial class MainForm : Form
    {
        private readonly OpenSimManager m_openSimManager;
        private readonly Timer m_refreshTimer;

        public MainForm()
        {
            InitializeComponent();

            m_openSimManager = new OpenSimManager();

            simulationListView.Columns.Clear();
            simulationListView.Columns.Add("Name", 150);
            simulationListView.Columns.Add("Status", 120);
            simulationListView.Columns.Add("Uptime", 120);
            simulationListView.Columns.Add("Config", 250);

            m_refreshTimer = new Timer();
            m_refreshTimer.Interval = 5000;
            m_refreshTimer.Tick += (s, e) => RefreshSimList();
            m_refreshTimer.Start();

            RefreshSimList();
        }

        private void RefreshSimList()
        {
            simulationListView.Items.Clear();

            var instances = m_openSimManager.GetSimInstances();

            foreach (var instance in instances)
            {
                var item = new ListViewItem(instance.Name);
                item.SubItems.Add(instance.StatusText);
                item.SubItems.Add(instance.UptimeText);
                item.SubItems.Add(instance.ConfigPath);
                item.Tag = instance;
                simulationListView.Items.Add(item);

                if (instance.Status == SimStatus.Running)
                {
                    item.ForeColor = Color.Green;
                }
                else if (instance.Status == SimStatus.Error)
                {
                    item.ForeColor = Color.Red;
                }
            }
        }

        private SimInstance GetSelectedInstance()
        {
            if (simulationListView.SelectedItems.Count > 0)
            {
                return (SimInstance)simulationListView.SelectedItems[0].Tag;
            }
            return null;
        }

        private async void startButton_Click(object sender, EventArgs e)
        {
            var instance = GetSelectedInstance();
            if (instance != null && instance.Status == SimStatus.Stopped)
            {
                await m_openSimManager.StartSimAsync(instance.Name, instance.ConfigPath);
                RefreshSimList();
            }
        }

        private async void stopButton_Click(object sender, EventArgs e)
        {
            var instance = GetSelectedInstance();
            if (instance != null && instance.Status == SimStatus.Running)
            {
                await m_openSimManager.StopSimAsync(instance.Name);
                RefreshSimList();
            }
        }

        private async void restartButton_Click(object sender, EventArgs e)
        {
            var instance = GetSelectedInstance();
            if (instance != null && instance.Status == SimStatus.Running)
            {
                await m_openSimManager.RestartSimAsync(instance.Name);
                RefreshSimList();
            }
        }

        private void detailsButton_Click(object sender, EventArgs e)
        {
            var instance = GetSelectedInstance();
            if (instance != null)
            {
                var detailsForm = new DetailsForm(instance);
                detailsForm.ShowDialog();
            }
        }

        private void createButton_Click(object sender, EventArgs e)
        {
            var createForm = new CreateForm(m_openSimManager);
            createForm.ShowDialog();
            RefreshSimList();
        }
    }
}
