using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GanStudio
{
    public partial class FmapsForm : Form
    {
        public GanStudio MyParent { get; set; }

        public delegate void SetFmapsValues(float[] value);
        public SetFmapsValues fmapSetDelegate;

        public delegate void Finished();
        public Finished finishedDelegate;
        private float[] fmapsVals;
        private const int FMAPS_PER_PAGE = 64;
        public void Fmaps_Load(object sender, EventArgs el)
        {

        }
        public FmapsForm(GanStudio parent)
        {
            InitializeComponent();
            fmapSetDelegate = new SetFmapsValues(SetFmapValuesMethod);
            finishedDelegate = new Finished(FinishedMethod);

            MyParent = parent;
            MyParent.Invoke(MyParent.fmapsStartedDelegate);
        }
        private void SetFmapValuesMethod(float[] values)
        {
            fmapsVals = values;
            for (int i = 0; i < values.GetLength(0) / FMAPS_PER_PAGE; i++)
            {
                TabPage newPage = GetPageWithSelectors(i * FMAPS_PER_PAGE, FMAPS_PER_PAGE);
                newPage.Text = string.Format("{0}-{1}", i * FMAPS_PER_PAGE, (i + 1) * FMAPS_PER_PAGE);
                tabControl1.TabPages.Add(newPage);
            }
        }
        private void FinishedMethod()
        {
            this.Close();
        }
        private TabPage GetPageWithSelectors(int offset, int count)
        {
            TabPage newPage = new TabPage();
            int wrapHorizontal = 600;
            int posX = 50;
            int posY = 50;
            for (int i = offset; i < offset+count; i++)
            {
                Label newLabel = new Label();
                newLabel.Location = new System.Drawing.Point(posX, posY);
                newLabel.Size = new System.Drawing.Size(40, 20);
                newLabel.Text = string.Format("{0}", i);
                //_attLabels.Add(newLabel);
                newPage.Controls.Add(newLabel);
                TextBox newBox = new TextBox();
                newBox.Location = new System.Drawing.Point(posX, posY+25);
                newBox.Size = new System.Drawing.Size(55, 20);
                newBox.Text = string.Format("{0}", fmapsVals[i]);
                int newBoxIndex = i;
                newBox.TextChanged += (s, e) =>
                 {
                     float val;
                     if (!Single.TryParse(newBox.Text, out val))
                     {
                         val = 0.0f;
                     }
                     fmapsVals[newBoxIndex] = val;
                     //MyParent.Invoke((Action)(() => MyParent.fmapsSetDelegate(fmapsVals)));
                 };
                newPage.Controls.Add(newBox);
                if (posX < wrapHorizontal)
                {
                    posX += 60;
                }
                else
                {
                    posX = 50;
                    posY += 80;
                }
            }
            return newPage;
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
