namespace GanStudio
{
    partial class BatchCreation
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
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.genImageProgress = new System.Windows.Forms.Label();
            this.interruptButton = new System.Windows.Forms.Button();
            this.killButton = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(136, 20);
            this.label1.TabIndex = 0;
            this.label1.Text = "Generating image";
            // 
            // genImageProgress
            // 
            this.genImageProgress.AutoSize = true;
            this.genImageProgress.Location = new System.Drawing.Point(154, 9);
            this.genImageProgress.Name = "genImageProgress";
            this.genImageProgress.Size = new System.Drawing.Size(18, 20);
            this.genImageProgress.TabIndex = 1;
            this.genImageProgress.Text = "?";
            this.genImageProgress.Click += new System.EventHandler(this.GenImageProgress_Click);
            // 
            // interruptButton
            // 
            this.interruptButton.Location = new System.Drawing.Point(12, 74);
            this.interruptButton.Name = "interruptButton";
            this.interruptButton.Size = new System.Drawing.Size(103, 73);
            this.interruptButton.TabIndex = 2;
            this.interruptButton.Text = "Interrupt";
            this.toolTip1.SetToolTip(this.interruptButton, "Stop generating images");
            this.interruptButton.UseVisualStyleBackColor = true;
            this.interruptButton.Click += new System.EventHandler(this.InterruptButton_Click);
            // 
            // killButton
            // 
            this.killButton.Location = new System.Drawing.Point(190, 74);
            this.killButton.Name = "killButton";
            this.killButton.Size = new System.Drawing.Size(103, 73);
            this.killButton.TabIndex = 3;
            this.killButton.Text = "Kill Application";
            this.toolTip1.SetToolTip(this.killButton, "Kill Application Immediately");
            this.killButton.UseVisualStyleBackColor = true;
            this.killButton.Click += new System.EventHandler(this.KillButton_Click);
            // 
            // BatchCreation
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(305, 159);
            this.Controls.Add(this.killButton);
            this.Controls.Add(this.interruptButton);
            this.Controls.Add(this.genImageProgress);
            this.Controls.Add(this.label1);
            this.Name = "BatchCreation";
            this.Text = "BatchCreation";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label genImageProgress;
        private System.Windows.Forms.Button interruptButton;
        private System.Windows.Forms.Button killButton;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}