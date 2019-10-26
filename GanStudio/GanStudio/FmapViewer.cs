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
    public partial class FmapViewer : Form
    {
        List<PictureBox> _picList = new List<PictureBox>();
        List<float[,]> _fmapList = new List<float[,]>();

        public delegate void AddFmapDelegate(string imagePath, float[,] fmap, int fmapIndex);
        public AddFmapDelegate addFmapDelegate;

        public FmapViewer(GanStudio parent)
        {
            InitializeComponent();
            addFmapDelegate = new AddFmapDelegate(AddFmapMethod);
            parent.Invoke(parent.fmapsViewerStartedDelegate);
        }
        public void AddFmapMethod(string imagePath, float [,] fmap, int fmapIndex)
        {
            PictureBox pic1 = new PictureBox();

            pic1.Image = Image.FromFile(imagePath);
            pic1.SizeMode = PictureBoxSizeMode.AutoSize;
            pic1.Paint += PictureBox1_Paint;
            pic1.Text = string.Format("{0}", _fmapList.Count);
            new ToolTip().SetToolTip(pic1, string.Format("fmap: {0}", fmapIndex));
            _picList.Add(pic1);
            _fmapList.Add(fmap);
            flowLayoutPanel1.Controls.Add(pic1);
            //DrawFmap(new float[4, 8], pic1);
        }
        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            PictureBox pictureBox = sender as PictureBox;

            //using (Graphics g = pictureBox.CreateGraphics())
            //{
            DrawFmap(_fmapList[int.Parse(pictureBox.Text)], pictureBox, e.Graphics);
        }
        //private void PictureBox1_Click(object sender, EventArgs e)
        //{
        //    PictureBox pictureBox = sender as PictureBox;
        //    float[,] test = new float[8, 4];
        //    test[0, 0] = 1.0f;
        //}
        private void DrawFmap(float[,] fmap, PictureBox pictureBox, Graphics g, float magntiudeMult = 50.0f)
        {
            if (fmap == null)
            {
                return;
            }
            // Draw grid            
            int rectHeight = pictureBox.Height / fmap.GetLength(0);
            int rectWidth = pictureBox.Width / fmap.GetLength(1);
            for (int row = 0; row < fmap.GetLength(0); row++)
            {
                for (int column = 0; column < fmap.GetLength(1); column++)
                {
                    float magnitude = Math.Abs(magntiudeMult * fmap[row, column]);
                    if (magnitude > 0)
                    {
                        int colorMagnitude = (int)Math.Min(magnitude, 255);
                        float lineMagnitude = magnitude / 255;
                        if (fmap[row, column] > 0)
                        {
                            Rectangle ee = new Rectangle(column * rectWidth, row * rectHeight, rectWidth, rectHeight);
                            using (Pen pen = new Pen(Color.FromArgb(255 - colorMagnitude, 255, 255 - colorMagnitude), 16.0f * lineMagnitude / (float)Math.Pow(fmap.GetLength(1), 1.5)))
                            {
                                g.DrawRectangle(pen, ee);
                            }
                        }
                        else if (fmap[row, column] < 0)
                        {
                            Rectangle ee = new Rectangle(column * rectWidth, row * rectHeight, rectWidth, rectHeight);
                            using (Pen pen = new Pen(Color.FromArgb(255, 255 - colorMagnitude, 255 - colorMagnitude), 16.0f * lineMagnitude / (float)Math.Pow(fmap.GetLength(1), 1.5)))
                            {
                                g.DrawRectangle(pen, ee);
                            }
                        }
                    }
                }
            }
        }

        private void FlowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {
            //foreach (PictureBox picBox in _picList)
            //{
            //    picBox.Refresh();
            //}
        }

        private void FlowLayoutPanel1_Scroll(object sender, ScrollEventArgs e)
        {
            //foreach (PictureBox picBox in _picList)
            //{
            //    picBox.Invalidate();
            //}

        }
    }
}
