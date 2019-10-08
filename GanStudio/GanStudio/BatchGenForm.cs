using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GanStudio
{
    public partial class BatchCreation : Form
    {
        public GanStudio MyParent { get; set; }

        public delegate void SetProgressText(string value);
        public SetProgressText textDelegate;

        public delegate void SetDirectory(string dir);
        public SetDirectory dirDelegate;

        public delegate void Finished();
        public Finished finishedDelegate;

        string _batchDir;
        public BatchCreation(GanStudio parent)
        {
            InitializeComponent();
            textDelegate = new SetProgressText(SetProgressTextMethod);
            finishedDelegate = new Finished(FinishedMethod);
            dirDelegate = new SetDirectory(SetDirMethod);
            MyParent = parent;
            MyParent.Invoke(MyParent.batchStartedDelegate);
        }
        private void SetDirMethod(string dir)
        {
            _batchDir = dir;
        }
        private void SetProgressTextMethod(string value)
        {
            this.genImageProgress.Text = value;
        }
        private void FinishedMethod()
        {
            this.Close();
        }

        private void InterruptButton_Click(object sender, EventArgs e)
        {
            Invoke(MyParent.interruptedDelegate);
        }

        private void KillButton_Click(object sender, EventArgs e)
        {
            Environment.Exit(-1);
        }

        private void GenImageProgress_Click(object sender, EventArgs e)
        {
            Process.Start(_batchDir);
        }
    }
}
