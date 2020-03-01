using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GanStudio
{
    public partial class FmapViewer : Form
    {
        List<PictureBox> _picList = new List<PictureBox>();
        List<float[,]> _fmapList = new List<float[,]>();

        public delegate void AddFmapDelegate(string imagePath, float[,] fmap, string fmapIndex);
        public AddFmapDelegate addFmapDelegate;
        bool colorblindMode;

        public FmapViewer(GanStudio parent, bool _colorblindMode = false)
        {
            InitializeComponent();
            addFmapDelegate = new AddFmapDelegate(AddFmapMethod);
            parent.Invoke(parent.fmapsViewerStartedDelegate);
            colorblindMode = _colorblindMode;
        }
        public void AddFmapMethod(string imagePath, float [,] fmap, string fmapIndex)
        {
            string strFmap = string.Format("{0}", fmapIndex);
            GroupBox imageWithCaption = new GroupBox();
            PictureBox pic1 = new PictureBox();
            pic1.Image = Image.FromFile(imagePath);
            pic1.SizeMode = PictureBoxSizeMode.AutoSize;
            pic1.Paint += PictureBox1_Paint;
            pic1.Text = string.Format("{0}", _fmapList.Count);
            if (this.Height < pic1.Height + 100)
            {
                this.Height = pic1.Height + 100;
            }
            imageWithCaption.Height = pic1.Height + 25;
            imageWithCaption.Width = pic1.Width;
            TextBox fmapBox = new TextBox();
            fmapBox.Text = strFmap;
            fmapBox.Location = new System.Drawing.Point(0, pic1.Height + 2);
            fmapBox.Size = new System.Drawing.Size(100, 20);
            imageWithCaption.Controls.Add(pic1);
            imageWithCaption.Controls.Add(fmapBox);
            new ToolTip().SetToolTip(pic1, string.Format("fmap: {0}", strFmap));
            _picList.Add(pic1);
            _fmapList.Add(fmap);
            flowLayoutPanel1.Controls.Add(imageWithCaption);
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
        private void DrawFmap(float[,] fmap, PictureBox pictureBox, Graphics g, float magntiudeMult = 255.0f)
        {
            if (fmap == null)
            {
                return;
            }
            // Draw grid            
            int rectHeight = pictureBox.Height / fmap.GetLength(0);
            int rectWidth = pictureBox.Width / fmap.GetLength(1);
            float maxValue = Math.Max(fmap.Cast<float>().Max(), -1* fmap.Cast<float>().Min());
            for (int row = 0; row < fmap.GetLength(0); row++)
            {
                for (int column = 0; column < fmap.GetLength(1); column++)
                {
                    float magnitude = Math.Abs(magntiudeMult * fmap[row, column] / maxValue);
                    if (magnitude > 0)
                    {
                        int colorMagnitude = (int)Math.Min(magnitude, 255);
                        float lineMagnitude = 1.0f; // magnitude;
                        if (fmap[row, column] > 0)
                        {
                            Rectangle ee = new Rectangle(column * rectWidth, row * rectHeight, rectWidth, rectHeight);
                            if (colorblindMode)
                            {
                                using (Pen pen = new Pen(Color.FromArgb(255 - colorMagnitude, 255 - colorMagnitude, 255), 16.0f * lineMagnitude / (float)Math.Pow(fmap.GetLength(1), 1.5)))
                                {
                                    g.DrawRectangle(pen, ee);
                                }
                            }
                            else
                            {
                                using (Pen pen = new Pen(Color.FromArgb(255 - colorMagnitude, 255, 255 - colorMagnitude), 16.0f * lineMagnitude / (float)Math.Pow(fmap.GetLength(1), 1.5)))
                                {
                                    g.DrawRectangle(pen, ee);
                                }

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
