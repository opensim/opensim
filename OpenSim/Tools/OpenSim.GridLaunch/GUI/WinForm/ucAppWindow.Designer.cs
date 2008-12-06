namespace OpenSim.GridLaunch.GUI.WinForm
{
    partial class ucAppWindow
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ucLogWindow1 = new OpenSim.GridLaunch.GUI.WinForm.ucLogWindow();
            this.ucInputField1 = new OpenSim.GridLaunch.GUI.WinForm.ucInputField();
            this.SuspendLayout();
            // 
            // ucLogWindow1
            // 
            this.ucLogWindow1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ucLogWindow1.Location = new System.Drawing.Point(3, 3);
            this.ucLogWindow1.Name = "ucLogWindow1";
            this.ucLogWindow1.Size = new System.Drawing.Size(232, 132);
            this.ucLogWindow1.TabIndex = 0;
            // 
            // ucInputField1
            // 
            this.ucInputField1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ucInputField1.Location = new System.Drawing.Point(0, 141);
            this.ucInputField1.Name = "ucInputField1";
            this.ucInputField1.Size = new System.Drawing.Size(234, 30);
            this.ucInputField1.TabIndex = 1;
            // 
            // ucAppWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ucInputField1);
            this.Controls.Add(this.ucLogWindow1);
            this.DoubleBuffered = true;
            this.Name = "ucAppWindow";
            this.Size = new System.Drawing.Size(235, 166);
            this.ResumeLayout(false);

        }

        #endregion

        private ucLogWindow ucLogWindow1;
        private ucInputField ucInputField1;
    }
}
