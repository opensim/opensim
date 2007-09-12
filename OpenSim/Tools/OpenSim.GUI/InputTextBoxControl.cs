using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace OpenSim.GUI
{
    class InputTextBoxControl:System.Windows.Forms.TextBox
    {
        public InputTextBoxControl()
        {
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(TextInputControl_KeyDown);
        }

  
        private List<string> CommandHistory = new List<string>();
        private bool InHistory = false;
        private int HistoryPosition = -1;

        void TextInputControl_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {

            
            if (e.KeyCode == Keys.Enter && InHistory == false)
            {
                CommandHistory.Add(this.Text);
            }


            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                // if not inside buffer, enter
                // InBuffer = true
                //Console.WriteLine("History: Check");
                if (InHistory == false)
                {
                    if (this.Text != "")
                    {
                        //Console.WriteLine("History: Add");
                        CommandHistory.Add(this.Text);
                        HistoryPosition = CommandHistory.Count;
                    }
                    else
                    {
                        //HistoryPosition = CommandHistory.Count + 1;
                    }
                    //Console.WriteLine("History: InHistory");
                    InHistory = true;
                }

                if (e.KeyCode == Keys.Up)
                    HistoryPosition -= 1;
                if (e.KeyCode == Keys.Down)
                    HistoryPosition += 1;

                if (HistoryPosition > CommandHistory.Count - 1)
                    HistoryPosition = -1;
                if (HistoryPosition < -1)
                    HistoryPosition = CommandHistory.Count - 1;

                //Console.WriteLine("History: Pos: " + HistoryPosition);
                //Console.WriteLine("History: HaveInHistCount: " + CommandHistory.Count);
                if (CommandHistory.Count != 0)
                {
                    if (HistoryPosition != -1)
                    {
                        //Console.WriteLine("History: Getting");
                        //this.Text = CommandHistory.Item(HistoryPosition);
                        this.Text = CommandHistory[HistoryPosition];
                        this.SelectionStart = this.Text.Length;
                        this.SelectionLength = 0;
                    }
                    else
                    {
                        //Console.WriteLine("History: Nothing");
                        this.Text = "";
                    }
                }
                e.Handled = true;
            } else {
                InHistory = false;
                HistoryPosition = -1;
            }
        }


    }
}
