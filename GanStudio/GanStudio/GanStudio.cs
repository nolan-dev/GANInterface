using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using CsvHelper;
using Microsoft.VisualBasic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace GanStudio
{
    [Serializable]
    public struct FmapData
    {
        public List<string> Names { get; set; }
        public List<int> Mults { get; set; }
        public List<float[,]> Selections { get; set; }
        public List<float[]> Mods { get; set; }
        public List<string> Groups { get; set; }
        public List<string> ScriptPaths { get; set; }
        public FmapData(List<string> _names, List<int> _mults, List<float[,]> _selections, List<float[]> _mods, List<string> _groups, List<string> _scriptPath=null)
        {
            Names = _names;
            Mults = _mults;
            Selections = _selections;
            Mods = _mods;
            Groups = _groups;
            ScriptPaths = _scriptPath;
        }
    }
    // A lot of this should be moved to a separate attribute/latent manipulation class
    public partial class GanStudio : Form
    {
        private const string version = "1.1";
        LatentManipulator lm;
        public delegate void BatchGenFormStarted();
        public BatchGenFormStarted batchStartedDelegate;

        public delegate void FmapFormStarted();
        public FmapFormStarted fmapsStartedDelegate;

        public delegate void FmapViewerFormStarted();
        public FmapViewerFormStarted fmapsViewerStartedDelegate;

        public delegate void InterruptApplication();
        public InterruptApplication interruptedDelegate;

        public delegate void Finished();
        public Finished finishedDelegate;

        private const string defaultTitle = "Gan Studio (Alpha)";
        private string _currentImageName;
        private string _prevImagePath = null;
        private string _currentImagePath = null;
        private float[] _currentLatent;
        private float[,] _currentFmapToDraw;
        private List<float[,]> _fmapSelections = new List<float[,]>();
        private List<float[]> _fmapMods = new List<float[]>();
        private List<string> _fmapScripts = new List<string>();
        private List<int> _fmapMults = new List<int>();
        private List<string> _fmapGroupList = new List<string>();

        private Dictionary<int, int> _rowCountToFmapCount = new Dictionary<int, int>
        {
            { 4, 512 },
            { 8, 512 },
            { 16, 512 },
            { 32, 512 },
            { 64, 256 },
            { 128, 128 }
        };
        private int[] _dragPointStart = null;
        private int[] _maxRes = { 512, 256 };
        private bool _showingSpatialMod = false;
        private bool _showingOld = false;
        private bool _showingAdvancedAttributes = false;
        private bool _showingCustomAttributes = false;
        private bool _showingJustFavoriteAttributes = false;
        private bool _showingBasicAttributes = true;
        private bool _allowAttributeDeletion = false;
        private bool _allowAttributeFavoriting = false;
        private List<TrackBar> _attTrackbars = new List<TrackBar>();
        private List<Label> _attLabels = new List<Label>();
        private List<Button> _attButtons = new List<Button>();
        private bool _interrupted = false;
        private bool _batchGenFormStarted = false;
        private bool _fmapViewerFormStarted = false;

        public GanStudio()
        {
            InitializeComponent();
        }

        private float[] ApplyTrackbarsToLatent(float[] latent, float factor = 1.0f,
            bool includeUniqueness = true, bool includeAttributes = true)
        {
            float[] newLatent = latent;

            if (includeUniqueness)
            {
                float normalizedVar = (float)varianceBar.Value / varianceBar.Maximum;
                newLatent = lm.ModUniqueness(newLatent, normalizedVar);
            }
            if (includeAttributes)
            {
                foreach (var trackbar in _attTrackbars)
                {
                    float normalizedVal = factor * (float)trackbar.Value * 3.0f / (trackbar.Maximum);
                    newLatent = lm.ModAttribute(trackbar.Name, normalizedVal, newLatent);
                }
            }
            return newLatent;
        }
        private static string GenerateSampleName()
        {
            return string.Format("sample_{0}.png", Guid.NewGuid());
        }
        private void PopulateFmapPanel()
        {
            int numFmaps = lm.currentFmaps[comboBoxDims.SelectedIndex].Count;

            flowLayoutOutputFmap.Controls.Clear();
            List<Button> fmapButtonList = new List<Button>();
            for (int i = 0; i < numFmaps; i++)
            {
                Button showFmapButton = new Button
                {
                    Text = string.Format("{0}", i),
                    Width = 40,
                };
                int currentIndex = i;
                showFmapButton.Click += (s, ev) =>
                {
                    fmapIndexBox.Text = string.Format("{0}", currentIndex);
                    if (fmapsToViewTextbox.Text.Length > 0)
                    {
                        fmapsToViewTextbox.Text += string.Format(",{0}", currentIndex);
                    }
                    else
                    {
                        fmapsToViewTextbox.Text += string.Format("{0}", currentIndex);
                    }
                    ShowImageAndSpatialSelections(lm.currentFmaps[comboBoxDims.SelectedIndex][currentIndex]);
                };
                fmapButtonList.Add(showFmapButton);
            }
            if (checkBoxSortBySimilarity.Checked || checkBoxExclusiveSelection.Checked)
            {
                if (_fmapSelections.Count == 0 || 
                    _fmapSelections[fmapTabControl.SelectedIndex].Cast<float>().ToArray().GetLength(0) != 
                    lm.currentFmaps[comboBoxDims.SelectedIndex][0].Cast<float>().ToArray().GetLength(0))
                {
                    return;
                }
                List<Tuple<float, int>> magnitudeToIndex = new List<Tuple<float, int>>();
                for (int i = 0; i < numFmaps; i++)
                {
                    if (checkBoxExclusiveSelection.Checked)
                    {
                        magnitudeToIndex.Add(new Tuple<float, int>(SpatialSelectionDotProdExclusive(lm.currentFmaps[comboBoxDims.SelectedIndex][i]), i));
                    }
                    else
                    {
                        magnitudeToIndex.Add(new Tuple<float, int>(SpatialSelectionDotProd(lm.currentFmaps[comboBoxDims.SelectedIndex][i]), i));
                    }
                }
                magnitudeToIndex.Sort((t1, t2) => {
                    return t2.Item1.CompareTo(t1.Item1);
                });
                foreach (Tuple<float, int> tuple in magnitudeToIndex)
                {
                    flowLayoutOutputFmap.Controls.Add(fmapButtonList[tuple.Item2]);
                }
            }
            else if (checkBoxSortByMagnitude.Checked)
            {
                List<Tuple<float, int>> magnitudeToIndex = new List<Tuple<float, int>>();
                for (int i = 0; i < numFmaps; i++)
                {
                    magnitudeToIndex.Add(new Tuple<float, int>(SpatialSelectionMagnitude(lm.currentFmaps[comboBoxDims.SelectedIndex][i]), i));
                }
                magnitudeToIndex.Sort((t1, t2) => {
                    return t2.Item1.CompareTo(t1.Item1);
                });
                foreach (Tuple<float, int> tuple in magnitudeToIndex)
                {
                    flowLayoutOutputFmap.Controls.Add(fmapButtonList[tuple.Item2]);
                }
            }
            else
            {
                for (int i = 0; i < numFmaps; i++)
                {
                    flowLayoutOutputFmap.Controls.Add(fmapButtonList[i]);
                }
            }
        }
        private void ShowImageForLatent(float[] latent)
        {
            if (latent == null)
            {
                MessageBox.Show("Latent is null, have you generated an image?");
                return;
            }

            string fname = GenerateSampleName();
            _currentImageName = fname;

            Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            // This ends up calling the model specified in graph.pb to convert the latent to an image
            float[] newLatent = ApplyTrackbarsToLatent(latent);
            if (newLatent == null)
            {
                MessageBox.Show("Latent is null, have you generated an image?");
                return;
            }
            string samplePath;
            //if (_currentFmapMods.All(m => m == 0))
            //{
            //    samplePath = lm.GenerateImage(newLatent, fname, fmaps: null);
            //}
            //else
            //{
            samplePath = GenerateImage(newLatent, fname, LatentManipulator.SampleDirName);
            if (checkBoxSortBySimilarity.Checked || checkBoxExclusiveSelection.Checked || checkBoxSortByMagnitude.Checked)
            {
                PopulateFmapPanel();
            }
            if (_fmapMults.Count > 0)
            {
                _fmapMults[fmapTabControl.SelectedIndex] = spatialMultBar.Value;
            }
            if (_showingSpatialMod)
            {
                _currentFmapToDraw = _fmapSelections[fmapTabControl.SelectedIndex];
                pictureBox1.Refresh();
            }
            //}
            lm.WriteToLatentFile(fname, newLatent);
            timerLabel.Text = string.Format("{0}s", (double)watch.ElapsedMilliseconds / 1000);
            Image oldImage = pictureBox1.Image;
            try
            {
                pictureBox1.Image = Image.FromFile(samplePath);

            }
            catch (OutOfMemoryException)
            {
                MessageBox.Show(string.Format("File {0} may be corrupted, could not load", samplePath));
            }
            if (oldImage != null)
                oldImage.Dispose();
            _prevImagePath = _currentImagePath;
            _currentImagePath = samplePath;
        }
        private bool PromptForImageToLoad(string dirName)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), dirName);

            // Easiest way to show user all generated images is with the normal 'browse' 
            // explorer dialog, though the user may need to change the icon sizes to large
            DialogResult result = openFileDialog1.ShowDialog();
            if (result != DialogResult.OK)
            {
                return false;
            }

            float[] savedLatent = lm.LoadSavedLatent(openFileDialog1.FileName);
            if (savedLatent == null || savedLatent.Contains(float.NaN))
            {
                MessageBox.Show(string.Format("Could not load {0}", openFileDialog1.FileName));
                return false;
            }
            ResetTrackbars();
            // Could either reset the uniqueness bar or scale the loaded image's uniqueness
            // so that it cancels out with the current setting of the uniqueness bar, otherwise
            // the image displayed will not appear the same as what's loaded
            // This is the second option:
            if (varianceBar.Value == 0)
            {
                varianceBar.Value = varianceBar.Maximum / 2;
            }
            float currentUniqueness = (float)varianceBar.Value / varianceBar.Maximum;
            _currentLatent = lm.ModUniqueness(savedLatent, 1.0f / currentUniqueness);

            StartGeneration();
            ShowImageForLatent(_currentLatent);
            FinishGeneration();
            return true;
        }
        private void EstimateImageQuality()
        {
            // This is a very heuristic guess about the probability an image is high quality
            // it's mostly meant for a user who has spent very little time experimenting and
            // doesn't know moving 'Uniqueness' all the way to the right produces garbage most of the time

            float qualityEstimate;
            float normalizedVar = (float)varianceBar.Value / varianceBar.Maximum;
            qualityEstimate = normalizedVar * 13;
            foreach (var trackbar in _attTrackbars)
            {
                float normalizedVal = Math.Abs((float)trackbar.Value * 3.0f / (trackbar.Maximum));
                if (normalizedVal > 1.0)
                {
                    qualityEstimate += 1;
                }
                if (normalizedVal > 1.5)
                {
                    qualityEstimate += 1;
                }
                if (normalizedVal > 2.0)
                {
                    qualityEstimate += 2;
                }
                if (normalizedVal > 2.5)
                {
                    qualityEstimate += 2;
                }
            }
            if (qualityEstimate >= 10)
            {
                imageQualityLabel.Text = "Very Low";
                imageQualityLabel.ForeColor = System.Drawing.ColorTranslator.FromHtml("#F00000");
            }
            else if (qualityEstimate >= 8)
            {
                imageQualityLabel.Text = "Low";
                imageQualityLabel.ForeColor = System.Drawing.ColorTranslator.FromHtml("#7F0000");
            }
            else if (qualityEstimate >= 6)
            {
                imageQualityLabel.Text = "Medium";
                imageQualityLabel.ForeColor = System.Drawing.ColorTranslator.FromHtml("#000000");
            }
            else if (qualityEstimate >= 3)
            {
                imageQualityLabel.Text = "High";
                imageQualityLabel.ForeColor = System.Drawing.ColorTranslator.FromHtml("#007F00");
            }
            else
            {
                imageQualityLabel.Text = "Maximum";
                imageQualityLabel.ForeColor = System.Drawing.Color.Red;
                imageQualityLabel.ForeColor = System.Drawing.ColorTranslator.FromHtml("#00D000");
            }
        }
        private void ResetTrackbars()
        {
            foreach (var trackbar in _attTrackbars)
            {
                trackbar.Value = 0;
            }
        }
        public void AttributesImport()
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            DialogResult result = openFileDialog1.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            lm.ImportAttributes(openFileDialog1.FileName);

            LoadAttributes();

        }
        private void LoadAttributes()
        {
            // Clear trackbars in UI if they already exist
            if (_attLabels.Count != 0)
            {
                foreach (Label label in _attLabels)
                {
                    this.Controls.Remove(label);
                    label.Dispose();
                }
                _attLabels.Clear();
            }
            if (_attTrackbars.Count != 0)
            {
                foreach (TrackBar bar in _attTrackbars)
                {
                    this.Controls.Remove(bar);
                    bar.Dispose();
                }
                _attTrackbars.Clear();
            }
            if (_attButtons.Count != 0)
            {
                foreach (Button button in _attButtons)
                {
                    this.Controls.Remove(button);
                    button.Dispose();
                }
                _attButtons.Clear();
            }
            foreach (TabPage page in mainTabControl.TabPages)
            {
                if (page.Text.Contains("Attribute"))
                {
                    mainTabControl.TabPages.Remove(page);
                }

            }

            // Re-read attributes from csv files based settings
            lm._attributeToLatent.Clear();
            if (_showingJustFavoriteAttributes)
            {
                if (lm.ReadAttributesFromCsv(lm.FavoriteAttributesPath) == null)
                {
                    MessageBox.Show(string.Format("Could not read {0}", lm.FavoriteAttributesPath));
                }
            }
            else
            {
                if (_showingBasicAttributes)
                {
                    if (lm.ReadAttributesFromCsv(lm.AttributesCsvPath) == null)
                    {
                        MessageBox.Show(string.Format("Could not read {0}", lm.AttributesCsvPath));
                    }
                }
                if (_showingAdvancedAttributes)
                {
                    if (lm.ReadAttributesFromCsv(lm.AdvancedAttributesPath) == null)
                    {
                        MessageBox.Show(string.Format("Could not read {0}", lm.AdvancedAttributesPath));
                    }

                }
                if (_showingCustomAttributes)
                {
                    if (lm.ReadAttributesFromCsv(lm.CustomAttributesPath) == null)
                    {
                        MessageBox.Show(string.Format("Could not read {0}", lm.CustomAttributesPath));
                    }
                }
            }
            List<string> attributes = new List<string>(lm._attributeToLatent.Keys);
            attributes.Sort();

            // Create and display trackbars
            int verticalCutoff = 850;
            if (attributes.Count() > 40)
            {
                verticalCutoff = 850;
            }

            int txtBoxPosition = 10;
            int txtBoxPositionV = 10;

            int horizontalCutoff = 1000;
            TabPage currentPage = new TabPage();
            int pageCounter = 0;
            currentPage.Text = string.Format("Attributes_{0}", pageCounter);
            mainTabControl.TabPages.Add(currentPage);
            foreach (string att in attributes)
            {
                Label newLabel = new Label();
                newLabel.Location = new System.Drawing.Point(txtBoxPosition, txtBoxPositionV);
                int labelWidth = 65;
                newLabel.Size = new System.Drawing.Size(labelWidth, 25);
                newLabel.Text = att;
                _attLabels.Add(newLabel);
                currentPage.Controls.Add(newLabel);
                toolTip1.SetToolTip(newLabel, att);
                if (_allowAttributeDeletion)
                {
                    Button delButton = new Button
                    {
                        Text = "del",
                        Width = 30,
                        Location = new Point(txtBoxPosition + 90, txtBoxPositionV)
                    };
                    delButton.Click += (s, e) =>
                    {
                        DialogResult dr = MessageBox.Show(string.Format("Are you sure you want to " +
                            "PERMANENTLY delete {0}?  It may be a good idea to back up the 'data' directory", att),
                        "Confirmation", MessageBoxButtons.YesNo);
                        switch (dr)
                        {
                            case DialogResult.Yes:
                                if (_showingJustFavoriteAttributes)
                                {
                                    lm.DeleteAttributeFromFile(att, lm.FavoriteAttributesPath);
                                }
                                else
                                {
                                    lm.DeleteAttributeFromFile(att, lm.AttributesCsvPath);
                                    lm.DeleteAttributeFromFile(att, lm.AdvancedAttributesPath);
                                    lm.DeleteAttributeFromFile(att, lm.CustomAttributesPath);
                                }
                                LoadAttributes();
                                break;
                            case DialogResult.No:
                                break;
                        }
                    };
                    _attButtons.Add(delButton);
                    currentPage.Controls.Add(delButton);
                }
                else if (_allowAttributeFavoriting && !_showingJustFavoriteAttributes)
                {
                    Button favButton = new Button
                    {
                        Text = "fav",
                        Width = 30,
                        Location = new Point(txtBoxPosition + 90, txtBoxPositionV)
                    };
                    favButton.Click += (s, e) =>
                    {
                        lm.FavoriteAttribute(att);
                    };
                    _attButtons.Add(favButton);
                    currentPage.Controls.Add(favButton);
                }
                else
                {
                    TrackBar trackBar = new TrackBar();
                    trackBar.Scroll += new System.EventHandler(this.AttBar_Scroll);
                    trackBar.Height = 50;
                    trackBar.Width = 300;
                    trackBar.Minimum = -30;
                    trackBar.Maximum = 30;
                    trackBar.Value = 0;
                    trackBar.TickFrequency = 20;
                    trackBar.Name = att;
                    int horizontalOffset = 65;
                    trackBar.Location = new Point(txtBoxPosition + horizontalOffset, txtBoxPositionV);
                    currentPage.Controls.Add(trackBar);
                    _attTrackbars.Add(trackBar);
                }

                txtBoxPositionV += 50;

                if (txtBoxPositionV > verticalCutoff)
                {
                    txtBoxPositionV = 10;
                    txtBoxPosition += 370;
                }
                if (txtBoxPosition > horizontalCutoff)
                {
                    pageCounter += 1;
                    txtBoxPosition = 10;
                    currentPage = new TabPage();
                    currentPage.Text = string.Format("Attributes_{0}", pageCounter);
                    mainTabControl.TabPages.Add(currentPage);
                }
            }
        }
        private void Form1_Load(object sender, EventArgs el)
        {
            MessageBox.Show("Some things to know: " +
                "\n 1. This tool has only been tested with .NET framework 4.7." +
                "\n 2. It requires a processor with AVX enabled." +
                "\n 3. It is full of bugs.");
            lm = new LatentManipulator();
            Directory.CreateDirectory(LatentManipulator.FavoritesDirName);
            checkBoxDragSelection.Checked = true;
            interruptedDelegate = new InterruptApplication(InterruptedMethod);
            batchStartedDelegate = new BatchGenFormStarted(BatchGenFormStartedMethod);
            fmapsViewerStartedDelegate = new FmapViewerFormStarted(FmapViewerFormStartedMethod);
            
            if (!File.Exists(lm.AttributesCsvPath))
            {
                throw new ArgumentException(string.Format("Initialization failed, Could not find {0} (working dir: {1})",
                    lm.AttributesCsvPath,
                    System.IO.Directory.GetCurrentDirectory()));
            }

            if (Directory.Exists(Path.Combine(lm.DataDir, "saved_fmaps")))
            {
                string[] fileEntries = Directory.GetFiles(Path.Combine(lm.DataDir, "saved_fmaps"));
                LoadSavedFmapTabs(fileEntries);
            }
            lm._attributeToLatent = new SortedDictionary<string, float[]>();
            Directory.CreateDirectory(LatentManipulator.SampleDirName);
            if (lm.ReadAverageLatentFromCsv(lm.AverageLatentCsvPath) == -1)
            {
                throw new Exception("Initialization failed, in ReadAverageLatentFromCsv");
            }
            if (lm.ReadAttributesFromCsv(lm.AttributesCsvPath) == null)
            {
                throw new Exception("Initialization failed, in ReadAttributesFromCsv");
            }
            List<int[]> dims = GetFmapResolutions();
            for (int i = 0; i < dims.Count; i++)
            {
                comboBoxDims.Items.Add(string.Format("{0}x{1}", dims[i][0], dims[i][1]));
            }
            comboBoxDims.Text = (string.Format("{0}x{1}", dims[0][0], dims[0][1]));

            LoadAttributes();
            saveLocation.Text = Path.Combine(System.IO.Directory.GetCurrentDirectory(), LatentManipulator.SampleDirName);
            EstimateImageQuality();

            pictureBox1.Paint += PictureBox1_Paint;
        }
        private void Generate_button_Click(object sender, EventArgs e)
        {
            StartGeneration();
            float[] intermediate = lm.GenerateNewIntermediateLatent();
            _currentLatent = intermediate;
            ShowImageForLatent(_currentLatent);
            FinishGeneration();
        }
        private void LoadButton_Click(object sender, EventArgs e)
        {
            PromptForImageToLoad(LatentManipulator.SampleDirName);
        }
        private void UpdateImage_Click(object sender, EventArgs e)
        {
            StartGeneration();
            ShowImageForLatent(_currentLatent);
            FinishGeneration();
        }
        private void AttBar_Scroll(object sender, EventArgs e)
        {
            EstimateImageQuality();
        }
        private void VarianceBar_Scroll(object sender, EventArgs e)
        {
            EstimateImageQuality();
        }
        private void ResetButton_Click(object sender, EventArgs e)
        {
            ResetTrackbars();
        }
        private void Favorite_Click(object sender, EventArgs e)
        {
            if (_currentImageName == null)
            {
                MessageBox.Show("Need to generate an image to favorite");
                return;
            }
            // Copy the current image to the favorites directory (ensures that the noise is saved)
            // and copy the latent to a separate favorites files that doesn't get deleted when other
            // samples are cleared
            string favoritesDirPath = Path.Combine(System.IO.Path.GetDirectoryName(
                LatentManipulator.SampleDirName), LatentManipulator.FavoritesDirName);
            File.Copy(Path.Combine(LatentManipulator.SampleDirName, _currentImageName),
                Path.Combine(favoritesDirPath, _currentImageName));

            using (TextWriter writer = new StreamWriter(lm.FavoritesLatentPath,
                true, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer);
                float[] newLatent = ApplyTrackbarsToLatent(_currentLatent);
                Debug.Assert(newLatent != null, "should have identified no activate image earlier");
                csv.WriteRecord(new LatentForFilename(_currentImageName, lm._graphHashStr, string.Join(":", newLatent)));
                csv.NextRecord();
                csv.Flush();
            }
        }
        private void DeleteSamplesFromDir(string path)
        {
            var files = Directory.GetFiles(path, "*sample_*.png");
            foreach (string file in files)
            {
                if (Path.GetFileName(file) != _currentImageName)
                    System.IO.File.Delete(file);
            }
        }
        private void DeleteSamples_Click(object sender, EventArgs e)
        {
            string sampleDir = LatentManipulator.SampleDirName;
            DialogResult dr = MessageBox.Show(string.Format("Are you sure you want to" +
                " delete samples in {0} and their latent codes in {1}?", sampleDir, lm.LatentCsvPath), "Confirmation",
                MessageBoxButtons.YesNo);
            switch (dr)
            {
                case DialogResult.Yes:
                    DeleteSamplesFromDir(sampleDir);
                    var dirs = Directory.GetDirectories(sampleDir, "batch_*");
                    foreach (string dir in dirs)
                    {
                        DeleteSamplesFromDir(dir);
                        System.IO.Directory.Delete(dir);
                    }
                    System.IO.File.Delete(lm.LatentCsvPath);
                    break;
                case DialogResult.No:
                    break;
            }
        }
        private void ChangeBase_Click(object sender, EventArgs e)
        {
            float[] newLatent = ApplyTrackbarsToLatent(_currentLatent);
            if (newLatent == null)
            {
                MessageBox.Show("Generate an image to switch the base to the generated image");
                return;
            }
            lm._averageAll = newLatent;
            ResetTrackbars();
        }
        private void ResetBase_Click(object sender, EventArgs e)
        {
            lm.ResetAverage();
        }
        private void AdvancedAtts_Click(object sender, EventArgs e)
        {
            if (!_showingAdvancedAttributes)
            {
                if (!File.Exists(lm.AdvancedAttributesPath))
                {
                    MessageBox.Show(string.Format("Can't find {0}", lm.AdvancedAttributesPath));
                    return;
                }
                _showingAdvancedAttributes = true;
            }
            else
            {
                _showingAdvancedAttributes = false;
            }
            LoadAttributes();
        }
        private void CombineSliders_Click(object sender, EventArgs e)
        {
            string input = Interaction.InputBox("New attribute name", "New attribute name", "my_attribute", -1, -1);
            if (input.Length == 0)
            {
                return;
            }

            // Check if attribute name already exists
            if (lm._attributeToLatent.Keys.Contains(input))
            {
                MessageBox.Show("Duplicate name, enter a different one");
                return;
            }
            if (File.Exists(lm.CustomAttributesPath))
            {
                using (StreamReader textReader = new StreamReader(lm.CustomAttributesPath))
                {
                    var csvr = new CsvReader(textReader);

                    csvr.Configuration.Delimiter = ",";
                    csvr.Configuration.HasHeaderRecord = false;
                    var records = csvr.GetRecords<RawAttribute>();

                    foreach (RawAttribute record in records)
                    {
                        if (record.Name == input)
                        {
                            MessageBox.Show("Duplicate name, enter a different one");
                            return;
                        }
                    }
                }
            }

            // Get modifier for new attribute
            float[] newAttribute = null;
            foreach (var trackbar in _attTrackbars)
            {
                float normalizedVal = (float)trackbar.Value * 3.0f / (trackbar.Maximum);
                if (newAttribute == null)
                {
                    newAttribute = lm._attributeToLatent[trackbar.Name].Select(x => x * normalizedVal).ToArray();
                }
                else
                {

                    newAttribute = lm.ModAttribute(trackbar.Name, normalizedVal, newAttribute);
                }
            }

            // Write modifier to custom attributes file
            using (TextWriter writer = new StreamWriter(lm.CustomAttributesPath,
                true, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer);
                csv.WriteRecord(new LatentForFilename(input, lm._graphHashStr, string.Join(":", newAttribute)));
                csv.NextRecord();
                csv.Flush();
            }
            if (_showingCustomAttributes)
            {
                LoadAttributes();
            }
        }
        private void ToggleCustom_Click(object sender, EventArgs e)
        {
            if (!_showingCustomAttributes && !File.Exists(lm.CustomAttributesPath))
            {
                MessageBox.Show(string.Format("Can't find {0}, have you " +
                    "created a custom attribute?", lm.CustomAttributesPath));
                return;
            }

            _showingCustomAttributes = !_showingCustomAttributes;
            LoadAttributes();
        }
        private void ToggleFavs_Click(object sender, EventArgs e)
        {
            if (!_showingJustFavoriteAttributes && !File.Exists(lm.FavoriteAttributesPath))
            {
                MessageBox.Show(string.Format("Can't find {0}, have you " +
                    "saved a attribute?", lm.FavoriteAttributesPath));
                return;
            }
            _showingJustFavoriteAttributes = !_showingJustFavoriteAttributes;
            LoadAttributes();
        }
        private Tuple<int, int> BatchGeneratePrompt(bool needsLatent = true)
        {
            if (needsLatent && _currentLatent == null)
            {
                MessageBox.Show("Need image to generate a batch based off of");
                return null;
            }
            string inputBatch = Interaction.InputBox("Images to generate", "Batch Gen prompt", "5", -1, -1);
            if (inputBatch.Length == 0)
            {
                return null;
            }
            if (!Int32.TryParse(inputBatch, out int batchSize))
            {
                MessageBox.Show(string.Format("{0} could not be parsed", batchSize));
                return null;
            }

            //string inputThread = Interaction.InputBox("Threads to use (more will use" +
            //    " more system resources, but finish faster)", "Threads",
            //    string.Format("{0}", Environment.ProcessorCount-1), -1, -1);
            //if (inputThread.Length == 0)
            //{
            //    return null;
            //}

            //if (!Int32.TryParse(inputThread, out int threadCount))
            //{
            //    MessageBox.Show(string.Format("{0} could not be parsed", inputThread));
            //    return null;
            //}
            //if (threadCount <= 0)
            //{
            //    MessageBox.Show("Need at least one thread");
            //}
            //if (threadCount >  8)
            //{
            //    DialogResult dr = MessageBox.Show(string.Format("That's a lot of threads, it may " +
            //        "slow down your computer.  Continue?", AdvancedAttributesPath),
            //        "Confirmation",
            //        MessageBoxButtons.YesNo);
            //    switch (dr)
            //    {
            //        case DialogResult.No:
            //            return null;
            //    }
            //}
            return Tuple.Create(batchSize, 1);
        }
        private string GenerateDirForBatch()
        {
            string batchDir = string.Format("batch_{0}", Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(LatentManipulator.SampleDirName, batchDir));
            return Path.Combine(LatentManipulator.SampleDirName, batchDir);
        }
        void DisableControls(Control con, int currentDepth)
        {
            if (currentDepth == 0)
            {
                return;
            }
            foreach (Control c in con.Controls)
            {
                DisableControls(c, currentDepth - 1);
            }
            if (con.GetType() == typeof(Button) && con.Name != "togglePrev")
            {
                con.Enabled = false;
            }
        }
        private void StartGeneration()
        {
            this.Text = "Generating...";
            DisableControls(this, 2);
        }
        void EnableControls(Control con, int currentDepth)
        {
            if (currentDepth == 0)
            {
                return;
            }
            foreach (Control c in con.Controls)
            {
                EnableControls(c, currentDepth - 1);
            }
            if (con.GetType() == typeof(Button) && con.Name != "togglePrev")
            {
                con.Enabled = true;
            }
        }
        private void FinishGeneration()
        {
            EnableControls(this, 2);
            this.Text = defaultTitle;
        }
        private bool BatchGeneration(int batchSize, int threadCount, Action<int, string> CustomGenerateImage,
            SortedDictionary<string, float[]> fnameToLatent)
        {

            StartGeneration();
            if (minimizeDuringBatch.Checked == true)
            {
                this.WindowState = FormWindowState.Minimized;
            }

            string batchDir = GenerateDirForBatch();

            Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            BatchCreation creationForm = new BatchCreation(this);
            Thread creationThread = new Thread(() => creationForm.ShowDialog());
            creationThread.Start();

            // Hack to wait for batch gen form to be created, probably a better way to do this
            for (int j = 0; j < 100; j++)
            {
                Thread.Sleep(100);
                if (_batchGenFormStarted)
                {
                    break;
                }
            }
            creationForm.Invoke((Action)(() => creationForm.dirDelegate(batchDir)));
            for (int i = 0; i < batchSize; i++)
            {
                if (_batchGenFormStarted)
                    creationForm.Invoke((Action)(() => creationForm.textDelegate(string.Format("{0} / {1}", i + 1, batchSize))));
                if (_interrupted)
                {
                    break;
                }
                CustomGenerateImage(i, batchDir);
            }
            if (!_interrupted && Directory.GetFiles(batchDir).Count() != batchSize)
            {
                MessageBox.Show("Sorry, something went wrong with the batch generation, please try again");
            }
            if (_batchGenFormStarted)
                creationForm.Invoke(creationForm.finishedDelegate);
            _batchGenFormStarted = false;
            //Parallel.For(0, batchSize, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, GenereateImage);

            foreach (KeyValuePair<string, float[]> item in fnameToLatent)
            {
                lm.WriteToLatentFile(item.Key, item.Value);
            }
            _interrupted = false;
            timerLabel.Text = string.Format("{0}s", (double)watch.ElapsedMilliseconds / 1000);
            if (!dontPopup.Checked)
            {
                Process.Start(batchDir);
                //PromptForImageToLoad(batchDir);
            }
            FinishGeneration();
            return true;
        }
        private void BatchGen_Click(object sender, EventArgs e)
        {
            Tuple<int, int> batchAndThreads = BatchGeneratePrompt();
            if (batchAndThreads == null)
            {
                return;
            }
            int batchSize = batchAndThreads.Item1;
            int threadCount = batchAndThreads.Item2;
            float factor = (float)varianceBar.Value / varianceBar.Maximum;
            // This should really be parallelized, hopefully it is when you're reading this and this 
            // comment was accidentally left in.  Most efficient parallelization method may be letting tensorflow handle
            // things with a batch input
            if (batchSize <= 1)
            {
                MessageBox.Show("Need batch size of > 1 (just click Generate New for a single image)");
                return;
            }

            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                float[] intermediate = lm.GenerateNewIntermediateLatent();

                float[] newLatent = ApplyTrackbarsToLatent(intermediate);
                //float[] newLatent = ModUniqueness(intermediate, factor);
                //float[] newLatent = VectorCombine(initialLatent, intermediate, factor, 1.0f - factor);
                string sampleName = string.Format("{0}_{1}", i, GenerateSampleName());

                GenerateImage(newLatent, sampleName, batchDir);
                fnameToLatent.Add(sampleName, newLatent);
            };
            BatchGeneration(batchSize, threadCount, CustomGenerateImage, fnameToLatent);
        }
        private void BatchGenAttrs_Click(object sender, EventArgs e)
        {
        }
        private void AllowAttDel_Click(object sender, EventArgs e)
        {
            _allowAttributeDeletion = !_allowAttributeDeletion;
            LoadAttributes();
        }
        private void ToggleFavoriting_Click(object sender, EventArgs e)
        {
            _allowAttributeFavoriting = !_allowAttributeFavoriting;
            LoadAttributes();
        }
        private void ExportAtt_Click(object sender, EventArgs e)
        {
            string input = Interaction.InputBox("Attribute to export (overwrites file):",
                "Export", "my_attribute", -1, -1);
            if (input.Length == 0)
            {
                return;
            }

            // Check if attribute name already exists
            if (!lm._attributeToLatent.Keys.Contains(input))
            {
                MessageBox.Show("Unable to find attribute.  It must appear as a trackbar," +
                    " hover over attribute label for tooltip with full name");
                return;
            }

            StreamWriter writer;
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            saveFileDialog1.RestoreDirectory = true;
            saveFileDialog1.FileName = string.Format("export-{0}.csv", input);
            saveFileDialog1.Filter = "Csv files (*.csv)|*.csv|All files (*.*)|*.*";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                writer = new StreamWriter(saveFileDialog1.OpenFile());
                if (writer != null)
                {
                    var csv = new CsvWriter(writer);
                    csv.WriteRecord(new LatentForFilename(input, lm._graphHashStr, string.Join(":", lm._attributeToLatent[input])));
                    csv.NextRecord();
                    csv.Flush();
                    writer.Close();
                }
            }
        }
        private void ImportAttr_Click(object sender, EventArgs e)
        {
            AttributesImport();
        }
        private Tuple<float[], float[]> GetLatentsToInterpolate()
        {
            if (!PromptForImageToLoad(LatentManipulator.SampleDirName))
            {
                return null;
            }
            float[] imageLatent1 = ApplyTrackbarsToLatent(_currentLatent, includeAttributes: false);
            if (!PromptForImageToLoad(LatentManipulator.SampleDirName))
            {
                return null;
            }
            float[] imageLatent2 = ApplyTrackbarsToLatent(_currentLatent, includeAttributes: false);
            return Tuple.Create(imageLatent1, imageLatent2);
        }
        private void TogglePrev_Click(object sender, EventArgs e)
        {
            if (!_showingOld)
            {
                if (_prevImagePath == null)
                {
                    return;
                }
                DisableControls(this, 2);

                Image oldImage = pictureBox1.Image;
                pictureBox1.Image = Image.FromFile(_prevImagePath);
                if (oldImage != null)
                    oldImage.Dispose();
                _showingOld = true;
            }
            else
            {
                EnableControls(this, 2);

                Image oldImage = pictureBox1.Image;
                pictureBox1.Image = Image.FromFile(_currentImagePath);
                if (oldImage != null)
                    oldImage.Dispose();
                _showingOld = false;
            }
        }
        private void InterruptedMethod()
        {
            _interrupted = true;
        }
        private void BatchGenFormStartedMethod()
        {
            _batchGenFormStarted = true;
        }
        private void FmapViewerFormStartedMethod()
        {
            _fmapViewerFormStarted = true;
        }
        private void SaveLocation_Click(object sender, EventArgs e)
        {
            Process.Start(LatentManipulator.SampleDirName);
        }
        private void ToggleBasic_Click(object sender, EventArgs e)
        {
            _showingBasicAttributes = !_showingBasicAttributes;
            LoadAttributes();
        }
        private Dictionary<string, float> GetAttributesToModify()
        {
            Dictionary<string, float> attributeNames = new Dictionary<string, float>();
            foreach (var trackbar in _attTrackbars)
            {
                if (trackbar.Value != 0.0f)
                {
                    attributeNames.Add(trackbar.Name, (float)trackbar.Value / (float)trackbar.Maximum);
                }
            }
            return attributeNames;
        }
        private void BatchFromAttributes(bool combinatorial = false)
        {
            bool slidePastZero = true;
            Dictionary<string, float> attributesToModify = GetAttributesToModify();
            Dictionary<string, int> numImagesForAttribute = new Dictionary<string, int>();

            if (_currentLatent == null)
            {
                return;
            }
            if (attributesToModify.Count == 0)
            {
                return;
            }
            DialogResult dr = MessageBox.Show("Slide past 0?", "Confirmation",
                MessageBoxButtons.YesNo);
            switch (dr)
            {
                case DialogResult.No:
                    slidePastZero = false;
                    break;
            }
            Dictionary<string, List<float[]>> changesForAttrbiute = new Dictionary<string, List<float[]>>();
            foreach (string attributeName in attributesToModify.Keys)
            {
                string inputBatch = Interaction.InputBox(string.Format("Images to generate for {0}", attributeName),
                    "Combinatoric Gen prompt", "2", -1, -1);
                if (!Int32.TryParse(inputBatch, out int batchSize))
                {
                    MessageBox.Show(string.Format("{0} could not be parsed", batchSize));
                    return;
                }
                if (batchSize != 0)
                {
                    int i = 0;
                    if (slidePastZero)
                    {
                        i = -batchSize;
                    }
                    List<float[]> attChanges = new List<float[]>();
                    for (; i < batchSize; i++)
                    {
                        float normalizedVal = (float)attributesToModify[attributeName] * 3.0f * i / (batchSize - 1);
                        if (slidePastZero)
                        {
                            normalizedVal = (float)attributesToModify[attributeName] * 3.0f * i / (batchSize - 1);
                        }

                        attChanges.Add(lm._attributeToLatent[attributeName].Select(x => x * normalizedVal).ToArray());
                    }
                    changesForAttrbiute.Add(attributeName, attChanges);
                }
                else
                {
                    // pre-apply
                }
            }
            float[] startLatent = ApplyTrackbarsToLatent(_currentLatent, includeAttributes: false);
            List<Tuple<float[], List<string>>> startCombination = new List<Tuple<float[], List<string>>>
            {
                new Tuple<float[], List<string>>(startLatent, new List<string>())
            };
            List<Tuple<float[], List<string>>> changes;
            if (combinatorial)
            {
                changes = lm.BuildCombinations(startCombination, new Stack<string>(attributesToModify.Keys),
                    changesForAttrbiute);
            }
            else
            {
                changes = lm.BuildSequences(startCombination, new Stack<string>(attributesToModify.Keys),
                    changesForAttrbiute);
            }
            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                string sampleName = string.Format("{0}_{1}", String.Join("_", changes[i].Item2.ToArray()), GenerateSampleName());

                GenerateImage(changes[i].Item1, sampleName, batchDir);
                fnameToLatent.Add(sampleName, changes[i].Item1);
            }
            BatchGeneration(changes.Count, 1, CustomGenerateImage, fnameToLatent);
        }
        private void NewLatentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Tuple<int, int> batchAndThreads = BatchGeneratePrompt();
            if (batchAndThreads == null)
            {
                return;
            }
            int batchSize = batchAndThreads.Item1;
            int threadCount = batchAndThreads.Item2;
            float factor = (float)varianceBar.Value / varianceBar.Maximum;
            // This should really be parallelized, hopefully it is when you're reading this and this 
            // comment was accidentally left in.  Most efficient parallelization method may be letting tensorflow handle
            // things with a batch input
            if (batchSize <= 1)
            {
                MessageBox.Show("Need batch size of > 1 (just click Generate New for a single image)");
                return;
            }

            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                float[] intermediate = lm.GenerateNewIntermediateLatent();

                float[] newLatent = ApplyTrackbarsToLatent(intermediate);
                //float[] newLatent = ModUniqueness(intermediate, factor);
                //float[] newLatent = VectorCombine(initialLatent, intermediate, factor, 1.0f - factor);
                string sampleName = string.Format("{0}_{1}", i, GenerateSampleName());

                GenerateImage(newLatent, sampleName, batchDir);
                fnameToLatent.Add(sampleName, newLatent);
            };
            BatchGeneration(batchSize, threadCount, CustomGenerateImage, fnameToLatent);
        }
        private void CombinatoricToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BatchFromAttributes(true);
        }
        private void SpectrumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Tuple<int, int> batchAndThreads = BatchGeneratePrompt();
            if (batchAndThreads == null)
            {
                return;
            }
            int batchSize = batchAndThreads.Item1;
            int threadCount = batchAndThreads.Item2;
            if (batchSize <= 1)
            {
                MessageBox.Show("Need batch size of > 1 (just click Generate New for a single image)");
                return;
            }

            float[] endLatent = ApplyTrackbarsToLatent(_currentLatent, -1.0f);
            DialogResult dr = MessageBox.Show("Slide past 0?", "Confirmation",
                MessageBoxButtons.YesNo);
            switch (dr)
            {
                case DialogResult.No:
                    endLatent = ApplyTrackbarsToLatent(_currentLatent, 0.0f);
                    break;
            }

            float[] startLatent = ApplyTrackbarsToLatent(_currentLatent);

            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                float factor = i * 1.0f / (batchSize - 1);
                float[] newLatent = LatentManipulator.VectorCombine(startLatent, endLatent, factor, 1.0f - factor);
                string sampleName = string.Format("{0}_{1}", i, GenerateSampleName());
                GenerateImage(newLatent, sampleName, batchDir);
                fnameToLatent.Add(sampleName, newLatent);
            }
            BatchGeneration(batchSize, threadCount, CustomGenerateImage, fnameToLatent);
        }

        private void SequentialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BatchFromAttributes(false);
        }
        private List<int[]> GetFmapResolutions()
        {
            int[] baseResolution = { 8, 4 };
            int nLayers = 6;
            List<int[]> dims = new List<int[]>();
            for (int i = 0; i < nLayers; i++)
            {
                dims.Add(new int[] { baseResolution[0] * Convert.ToInt32(Math.Pow(2, i)),
                    baseResolution[1] * Convert.ToInt32(Math.Pow(2, i)) });
            }


            //lm.GetShapeForTensor("Res8/adaptive_instance_norm_1/add_1:0"))
            //lm.GetShapeForTensor("Res16/adaptive_instance_norm_1/add_1:0"))
            //lm.GetShapeForTensor("Res32/adaptive_instance_norm_1/add_1:0"))
            //lm.GetShapeForTensor("Res64/adaptive_instance_norm_1/add_1:0"))
            //lm.GetShapeForTensor("Res128/adaptive_instance_norm_1/add_1:0"))
            //lm.GetShapeForTensor("Res256/adaptive_instance_norm_1/add_1:0"))
            return dims;
        }
        private void SpatialMod_Click(object sender, EventArgs e)
        {
            pictureBox1.Image = Image.FromFile(_currentImagePath);
            Refresh();
            _showingSpatialMod = !_showingSpatialMod;
            if (_showingSpatialMod)
            {
                _currentFmapToDraw = _fmapSelections[fmapTabControl.SelectedIndex];
                pictureBox1.Refresh();
            }
        }
        private int[] PositionToFmapSelectionPoint(int posX, int posY)
        {
            int rectHeight = _maxRes[0] / _fmapSelections[fmapTabControl.SelectedIndex].GetLength(0);
            int rectWidth = _maxRes[1] / _fmapSelections[fmapTabControl.SelectedIndex].GetLength(1);
            return new int[2] { posX / rectWidth, posY / rectHeight };
        }
        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            PictureBox pictureBox = sender as PictureBox;
            //using (Graphics g = pictureBox.CreateGraphics())
            //{
            DrawFmap(_currentFmapToDraw, pictureBox, e.Graphics);
        }
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
        private void ShowImageAndSpatialSelections(float[,] fmap)
        {
            if (_currentImagePath != null)
            {
                pictureBox1.Image = Image.FromFile(_currentImagePath);
                pictureBox1.Invalidate();
                pictureBox1.Refresh();
                _currentFmapToDraw = fmap;
                pictureBox1.Refresh();
            }
        }
        private void SelectPoint(int[] point, MouseButtons button)
        {
            if (button == MouseButtons.Left && Control.ModifierKeys != Keys.Shift)
            {
                if (checkBoxNoSelectionOverlap.Checked)
                {
                    _fmapSelections[fmapTabControl.SelectedIndex][point[1], point[0]] = 1.0f;
                }
                else
                {
                    _fmapSelections[fmapTabControl.SelectedIndex][point[1], point[0]] += 1.0f;
                }
            }
            else if (button == MouseButtons.Left)
            {
                _fmapSelections[fmapTabControl.SelectedIndex][point[1], point[0]] = 0.0f;
            }
            else if (button  == MouseButtons.Right)
            {
                if (checkBoxNoSelectionOverlap.Checked)
                {
                    _fmapSelections[fmapTabControl.SelectedIndex][point[1], point[0]] = -1.0f;
                }
                else
                {
                    _fmapSelections[fmapTabControl.SelectedIndex][point[1], point[0]] -= 1.0f;
                }
            }
        }
        private void PictureBox1_Click(object sender, EventArgs e)
        {
            if (fmapTabControl.TabCount > 0 && !checkBoxDragSelection.Checked)
            {
                var mouseEventArgs = e as MouseEventArgs;
                SelectPoint(PositionToFmapSelectionPoint(mouseEventArgs.X, mouseEventArgs.Y), mouseEventArgs.Button);
                _showingSpatialMod = true;
                ShowImageAndSpatialSelections(_fmapSelections[fmapTabControl.SelectedIndex]);
                //MouseButtons.Right
                //if (mouseEventArgs != null) textBox1.Text = "X= " + mouseEventArgs.X + " Y= " + mouseEventArgs.Y;
            }
        }
        private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_showingSpatialMod && _fmapSelections.Count > 0)
            {
                ShowImageAndSpatialSelections(_fmapSelections[fmapTabControl.SelectedIndex]);
            }
        }
        private void FmapsToMod_Click(object sender, EventArgs e)
        {
            if (_fmapMods.Count > 0)
            {
                AddSelectorPages();
            }
        }
        private void RandomizeFmaps_Click(object sender, EventArgs e)
        {
            if (_fmapMods.Count > 0)
            {
                Random random = new Random();
                float total = 0.0f;
                for (int i = 0; i < _fmapMods[fmapTabControl.SelectedIndex].GetLength(0); i++)
                {
                    float converted = Convert.ToSingle(random.NextDouble() - 0.5);
                    total += converted;
                    _fmapMods[fmapTabControl.SelectedIndex][i] = converted;
                }
                for (int i = 0; i < _fmapMods[fmapTabControl.SelectedIndex].GetLength(0); i++)
                {
                    _fmapMods[fmapTabControl.SelectedIndex][i] /= total;
                }
            }
        }
        private void ResetFmaps_Click(object sender, EventArgs e)
        {
            _fmapMods[fmapTabControl.SelectedIndex] = new float[_fmapMods[fmapTabControl.SelectedIndex].GetLength(0)];
        }
        private void ResetSpatial_Click(object sender, EventArgs e)
        {
            _fmapSelections[fmapTabControl.SelectedIndex] = new float[_fmapSelections[fmapTabControl.SelectedIndex].GetLength(0),
                _fmapSelections[fmapTabControl.SelectedIndex].GetLength(1)];
            pictureBox1.Refresh();
        }
        private List<float[,,]> SpatialMapToFmaps(bool useCurrentTab=true)
        {
            if (fmapTabControl.TabCount > 0)
            {
                SortedDictionary<int, float[,,]> fmapDict = new SortedDictionary<int, float[,,]>();
                List<int> activeTabs = GetActiveTabs();
                foreach(int tab in activeTabs)
                {
                    int channels = _fmapMods[tab].GetLength(0);
                    string scriptPath = _fmapScripts[tab];
                    if (scriptPath != "")
                    {
                        //RunFmapScript(scriptPath, tab);
                    }
                    int height = _fmapSelections[tab].GetLength(0);
                    int width = _fmapSelections[tab].GetLength(1);
                    float[,,] result;
                    if (fmapDict.ContainsKey(width))
                    {
                        result = fmapDict[width];
                    }
                    else
                    {
                        result = new float[height, width, channels];
                    }
                    for (int i = 0; i < channels; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                if (tab == fmapTabControl.SelectedIndex && useCurrentTab)
                                {
                                    result[j, k, i] += _fmapSelections[tab][j, k] * _fmapMods[tab][i] * spatialMultBar.Value;
                                }
                                else
                                {
                                    result[j, k, i] += _fmapSelections[tab][j, k] * _fmapMods[tab][i] * _fmapMults[tab];
                                }
                            }
                        }
                    }
                    fmapDict[width] = result;
                }
                return fmapDict.Values.ToList();
            }
            return null;
        }
        private void AxisalignedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Tuple<int, int> batchAndThreads = BatchGeneratePrompt();
            if (batchAndThreads == null)
            {
                return;
            }
            int batchSize = batchAndThreads.Item1;
            int threadCount = 1;
            // This should really be parallelized, hopefully it is when you're reading this and this 
            // comment was accidentally left in.  Most efficient parallelization method may be letting tensorflow handle
            // things with a batch input
            if (batchSize <= 1)
            {
                MessageBox.Show("Need batch size of > 1 (just click Generate New for a single image)");
                return;
            }

            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {

                float[] newLatent = ApplyTrackbarsToLatent(_currentLatent);
                //float[] newLatent = ModUniqueness(intermediate, factor);
                //float[] newLatent = VectorCombine(initialLatent, intermediate, factor, 1.0f - factor);
                string nameEnd = GenerateSampleName();
                string sampleName = string.Format("{0}_p_{1}", i, nameEnd);
                _fmapMods[fmapTabControl.SelectedIndex] = new float[_fmapMods[fmapTabControl.SelectedIndex].GetLength(0)];
                _fmapMods[fmapTabControl.SelectedIndex][i] = 1.0f; //spatialMultBar.Value;
                GenerateImage(newLatent, sampleName, batchDir);
                sampleName = string.Format("{0}_n_{1}", i, nameEnd);
                _fmapMods[fmapTabControl.SelectedIndex][i] = -1.0f; //spatialMultBar.Value;
                GenerateImage(newLatent, sampleName, batchDir);
            };
            BatchGeneration(batchSize, threadCount, CustomGenerateImage, fnameToLatent);
        }
        private void SpectrumToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Tuple<int, int> batchAndThreads = BatchGeneratePrompt();
            if (batchAndThreads == null)
            {
                return;
            }
            int batchSize = batchAndThreads.Item1;
            int threadCount = batchAndThreads.Item2;
            if (batchSize <= 1)
            {
                MessageBox.Show("Need batch size of > 1 (just click Generate New for a single image)");
                return;
            }

            float startValue = spatialMultBar.Value;
            float endValue = -1 * spatialMultBar.Value;
            DialogResult dr = MessageBox.Show("Slide past 0?", "Confirmation",
                MessageBoxButtons.YesNo);
            switch (dr)
            {
                case DialogResult.No:
                    endValue = 0.0f;
                    break;
            }


            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                float factor = i * 1.0f / (batchSize - 1);
                float newValue = startValue * (1 - factor) + endValue * factor;
                spatialMultBar.Value = (int)newValue;
                string sampleName = string.Format("{0}_{1}", i, GenerateSampleName());
                GenerateImage(ApplyTrackbarsToLatent(_currentLatent), sampleName, batchDir);
            }
            BatchGeneration(batchSize, threadCount, CustomGenerateImage, fnameToLatent);
        }
        private void RegenerateImagesFromCSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SortedDictionary<string, float[]> fnameToLatent = lm.GetNameToLatentMap();
            List<string> attributes = new List<string>(fnameToLatent.Keys);
            DialogResult dr = MessageBox.Show(string.Format("This will generate {0} images " +
                "from {1} and {2}, to {2} it may take a very long time. Continue?",
                attributes.Count, lm.LatentCsvPath, lm.FavoritesLatentPath, LatentManipulator.SampleDirName),
                "Confirmation", MessageBoxButtons.YesNo);
            switch (dr)
            {
                case DialogResult.Yes:
                    void CustomGenerateImage(int i, string batchDir)
                    {
                        string sampleName = string.Format("{0}_{1}", i, GenerateSampleName());
                        GenerateImage(fnameToLatent[attributes[i]], attributes[i], batchDir);
                    };
                    BatchGeneration(attributes.Count, 1, CustomGenerateImage, fnameToLatent);
                    break;
                case DialogResult.No:
                    break;
            }
        }
        private void ApplyCurrentSlidersToDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Ensure the files that map file names to latents exist
            if (!File.Exists(lm.LatentCsvPath) && !File.Exists(lm.FavoritesLatentPath))
            {
                MessageBox.Show(string.Format("Could not find {0} or {1}",
                    lm.LatentCsvPath, lm.FavoritesLatentPath));
                return;
            }
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = Path.Combine(Directory.GetCurrentDirectory(), LatentManipulator.SampleDirName);
                DialogResult result = fbd.ShowDialog();

                List<Tuple<string, float[]>> oldLatents = new List<Tuple<string, float[]>>();
                SortedDictionary<string, float[]> fnameToLatent = lm.GetNameToLatentMap();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    string[] paths = Directory.GetFiles(fbd.SelectedPath);
                    foreach (string path in paths)
                    {
                        string fileName = Path.GetFileName(path);
                        int missing = 0;

                        float[] latent = lm.ExtractLatentFromImage(path);
                        if (latent != null)
                        {
                            oldLatents.Add(new Tuple<string, float[]>(fileName, latent));
                        }
                        else
                        {
                            if (fnameToLatent.ContainsKey(fileName))
                            {
                                oldLatents.Add(new Tuple<string, float[]>(fileName, fnameToLatent[fileName]));
                            }
                            else
                            {
                                missing++;
                            }
                        }
                    }
                    if (oldLatents.Count != paths.Length)
                    {
                        System.Windows.Forms.MessageBox.Show(string.Format("Updating {0} of {1} files ", oldLatents.Count, paths.Length), "Update");
                    }
                }
                else
                {
                    return;
                }
                void CustomGenerateImage(int i, string batchDir)
                {
                    Tuple<string, float[]> nameAndLatent = oldLatents[i];
                    float[] oldLatent = nameAndLatent.Item2;
                    string fileName = nameAndLatent.Item1;
                    float[] newLatent = ApplyTrackbarsToLatent(oldLatent, 1.0f, false);
                    fileName = Regex.Replace(fileName, "[a-f0-9]{8}[-][a-f0-9]{4}[-][a-f0-9]{4}[-][a-f0-9]{4}[-][a-f0-9]{12}", Guid.NewGuid().ToString());
                    GenerateImage(newLatent, fileName, batchDir);
                    fnameToLatent.Add(fileName, newLatent);
                }
                BatchGeneration(oldLatents.Count, 1, CustomGenerateImage, fnameToLatent);
            }
        }
        private void InterpolateBetweenTwoImagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Tuple<int, int> result = BatchGeneratePrompt(false);
            int batchSize = result.Item1;
            Tuple<float[], float[]> images = GetLatentsToInterpolate();
            if (images == null)
            {
                return;
            }
            float[] imageLatent1 = images.Item1;
            float[] imageLatent2 = images.Item2;

            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                float factor = i * 1.0f / (batchSize - 1);
                float[] newLatent = LatentManipulator.VectorCombine(imageLatent1, imageLatent2, factor, 1.0f - factor);
                string sampleName = string.Format("{0}_{1}", i, GenerateSampleName());
                GenerateImage(newLatent, sampleName, batchDir);
                fnameToLatent.Add(sampleName, newLatent);
            }
            BatchGeneration(batchSize, 1, CustomGenerateImage, fnameToLatent);
        }
        private void AddInterpolationAsCustomAttributeToolStripMenuItem_Click(object sender, EventArgs e)
        {

            string input = Interaction.InputBox("New attribute name", "New attribute name", "my_attribute", -1, -1);
            if (input.Length == 0)
            {
                return;
            }

            // Check if attribute name already exists
            if (lm._attributeToLatent.Keys.Contains(input))
            {
                MessageBox.Show("Duplicate name, enter a different one");
                return;
            }

            if (File.Exists(lm.CustomAttributesPath))
            {
                using (StreamReader textReader = new StreamReader(lm.CustomAttributesPath))
                {
                    var csvr = new CsvReader(textReader);

                    csvr.Configuration.Delimiter = ",";
                    csvr.Configuration.HasHeaderRecord = false;
                    var records = csvr.GetRecords<RawAttribute>();

                    foreach (RawAttribute record in records)
                    {
                        if (record.Name == input)
                        {
                            MessageBox.Show("Duplicate name, enter a different one");
                            return;
                        }
                    }
                }
            }
            Tuple<float[], float[]> images = GetLatentsToInterpolate();
            if (images == null)
            {
                return;
            }
            float[] imageLatent1 = images.Item1;
            float[] imageLatent2 = images.Item2;
            float[] modifier = LatentManipulator.VectorCombine(imageLatent2, imageLatent1, 1.0f, -1.0f);

            // Write modifier to custom attributes file
            using (TextWriter writer = new StreamWriter(lm.CustomAttributesPath,
                true, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer);
                csv.WriteRecord(new LatentForFilename(input, lm._graphHashStr, string.Join(":", modifier)));
                csv.NextRecord();
                csv.Flush();
            }
            if (_showingCustomAttributes)
            {
                LoadAttributes();
            }
        }
        private void AddTab_Click(object sender, EventArgs e)
        {
            _fmapMults.Add(spatialMultBar.Value);
            _fmapScripts.Add("");
            _fmapGroupList.Add(string.Format("{0}", fmapTabControl.TabPages.Count));
            string[] selectedItem = comboBoxDims.Items[comboBoxDims.SelectedIndex].ToString().Split('x');
            int rowCount = Int32.Parse(selectedItem[0]);
            int columnCount = Int32.Parse(selectedItem[1]);
            _fmapSelections.Add(new float[rowCount, columnCount]);
            _fmapMods.Add(new float[_rowCountToFmapCount[columnCount]]);

            TabPage newPage = new TabPage();
            newPage.Text = comboBoxDims.Text;
            fmapTabControl.TabPages.Add(newPage);
            fmapTabControl.SelectedIndex = fmapTabControl.TabCount - 1;
        }
        private void RemoveTab_Click(object sender, EventArgs e)
        {
            _fmapMods.RemoveAt(fmapTabControl.SelectedIndex);
            _fmapScripts.RemoveAt(fmapTabControl.SelectedIndex);
            _fmapSelections.RemoveAt(fmapTabControl.SelectedIndex);
            _fmapMults.RemoveAt(fmapTabControl.SelectedIndex);
            _fmapGroupList.RemoveAt(fmapTabControl.SelectedIndex);
            fmapTabControl.TabPages.Remove(fmapTabControl.SelectedTab);
        }
        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                comboBoxDims.SelectedIndex = (int)Math.Log(_fmapSelections[fmapTabControl.SelectedIndex].GetLength(1), 2) - 2;
            }
            catch(System.ArgumentOutOfRangeException)
            {

            }
            if (_fmapMults.Count > 0)
            {
                spatialMultBar.Value = _fmapMults[fmapTabControl.SelectedIndex];
                scriptPathBox.Text = _fmapScripts[fmapTabControl.SelectedIndex];
                groupTextBox.Text = _fmapGroupList[fmapTabControl.SelectedIndex];
                ShowImageAndSpatialSelections(_fmapSelections[fmapTabControl.SelectedIndex]);
            }
        }
        private void ResetMult_Click(object sender, EventArgs e)
        {
            spatialMultBar.Value = 1;
        }
        private void RenameTab_Click(object sender, EventArgs e)
        {
            string inputName = Interaction.InputBox("Tab Name",
                "Tab name prompt", fmapTabControl.SelectedTab.Text, -1, -1);
            fmapTabControl.SelectedTab.Text = inputName;
        }
        private void SaveTabs_Click(object sender, EventArgs e)
        {
            string inputName = Interaction.InputBox("Save name",
                "Save name prompt", string.Format("{0}_tabdata.bin", _currentImageName), -1, -1);
            List<string> tabNames = new List<string>();
            foreach (TabPage t in fmapTabControl.TabPages)
            {
                tabNames.Add(t.Text);
            }
            FmapData fd = new FmapData(tabNames, _fmapMults, _fmapSelections, _fmapMods, _fmapGroupList, _fmapScripts);
            FileStream fout = new FileStream(inputName, FileMode.Create, FileAccess.Write, FileShare.None);
            using (fout)
            {
                BinaryFormatter b = new BinaryFormatter();
                b.Serialize(fout, fd);
            }
        }
        private void LoadTabs(string filename)
        {
            FileStream fin = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.None);
            using (fin)
            {
                BinaryFormatter b = new BinaryFormatter();
                FmapData newData = (FmapData)b.Deserialize(fin);
                _fmapMults = _fmapMults.Concat(newData.Mults).ToList();
                _fmapSelections = _fmapSelections.Concat(newData.Selections).ToList();
                if (newData.ScriptPaths == null)
                {
                    for(int i = 0; i < _fmapSelections.Count; i++)
                    {
                        _fmapScripts.Add("");
                    }
                }
                else
                {
                    _fmapScripts = _fmapScripts.Concat(newData.ScriptPaths).ToList();
                }
                _fmapMods = _fmapMods.Concat(newData.Mods).ToList();
                _fmapGroupList = _fmapGroupList.Concat(newData.Groups).ToList();
                for (int i = 0; i < newData.Names.Count; i++)
                {
                    TabPage newPage = new TabPage();
                    newPage.Text = newData.Names[i];
                    fmapTabControl.TabPages.Add(newPage);
                    fmapTabControl.SelectedIndex = fmapTabControl.TabCount - 1;
                }
            }
        }
        private void LoadTabs_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.InitialDirectory = Directory.GetCurrentDirectory();

            // Easiest way to show user all generated images is with the normal 'browse' 
            // explorer dialog, though the user may need to change the icon sizes to large
            DialogResult result = openFileDialog1.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            LoadTabs(openFileDialog1.FileName);
        }
        private void LoadSavedFmapTabs(string[] paths)
        {
            foreach (string tabFilePath in paths)
            {
                LoadTabs(tabFilePath);
            }
        }
        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (fmapTabControl.TabCount > 0 && checkBoxDragSelection.Checked)
            {
                var mouseEventArgs = e as MouseEventArgs;
                _dragPointStart = PositionToFmapSelectionPoint(mouseEventArgs.X, mouseEventArgs.Y);
            }
        }
        private List<int[]> GetPixelsSelectedInRectangle(int[] point1, int[] point2)
        {
            List<int[]> result = new List<int[]>();
            int minX = Math.Max(Math.Min(point1[0], point2[0]), 0);
            int maxX = Math.Min(Math.Max(point1[0], point2[0]), _fmapSelections[fmapTabControl.SelectedIndex].GetLength(1)-1);
            int minY = Math.Max(Math.Min(point1[1], point2[1]), 0);
            int maxY = Math.Min(Math.Max(point1[1], point2[1]), _fmapSelections[fmapTabControl.SelectedIndex].GetLength(0)-1);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    result.Add(new int[] { x, y });
                }
            }
            return result;
        }
        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {

            if (fmapTabControl.TabCount > 0 && checkBoxDragSelection.Checked && _dragPointStart != null)
            {
                var mouseEventArgs = e as MouseEventArgs;
                int[] dragPointEnd = PositionToFmapSelectionPoint(mouseEventArgs.X, mouseEventArgs.Y);
                foreach(int[] point in GetPixelsSelectedInRectangle(_dragPointStart, dragPointEnd))
                {
                    SelectPoint(point, mouseEventArgs.Button);
                }
                ShowImageAndSpatialSelections(_fmapSelections[fmapTabControl.SelectedIndex]);
            }
        }
        private void GroupTextBox_TextChanged(object sender, EventArgs e)
        {
            _fmapGroupList[fmapTabControl.SelectedIndex] = groupTextBox.Text;
        }
        private List<int> GetActiveTabs()
        {
            List<int> activeTabs = new List<int>();
            for(int i = 0; i < fmapTabControl.TabPages.Count; i++)
            {
                if (_fmapMults[i] == 0.0f || (_fmapScripts[i] == "" && (_fmapMods[i].All(m => m == 0) || _fmapSelections[i].Cast<float>().ToArray().All(u => u == 0))))
                {
                    continue;
                }
                activeTabs.Add(i);
            }
            return activeTabs;
        }
        private List<int> GetScriptTabs()
        {
            List<int> scriptTabs = new List<int>();
            for (int i = 0; i < fmapTabControl.TabPages.Count; i++)
            {
                if (_fmapScripts[i] == "" || (_fmapMods[i].All(m => m == 0) || _fmapMults[i] == 0.0f))
                {
                    continue;
                }
                scriptTabs.Add(i);
            }
            return scriptTabs;
        }
        // format: [[1, 4], [1, 0]] means two samples, one with index 0 tab set to 1, index 1 tab set to 4, one with index 1 tab set to 0 (index 0 still 1)
        // non recursive example:
        // combined_list = []
        //for lst in input_list:
        //    if combined_list != []:
        //        combined_list_new = []
        //        for item in lst:
        //            for sublst in combined_list:
        //                sublst = sublst.copy()
        //                sublst.append(item)
        //                combined_list_new.append(sublst)
        //        combined_list = combined_list_new
        //    else:
        //        combined_list = [[v] for v in lst]
        private List<List<int>> BuildFmapCombinations(List<List<int>> multsForTab)
        {
            List<List<int>> combinedList = new List<List<int>>();
            foreach (List<int> mults in multsForTab)
            {
                if (combinedList.Count != 0)
                {
                    List<List<int>> newList = new List<List<int>>();
                    foreach (int mult in mults)
                    {
                        foreach (List<int> subList in combinedList)
                        {
                            List<int> clonedSubList = new List<int>(subList);
                            clonedSubList.Add(mult);
                            newList.Add(clonedSubList);
                        }
                    }
                    combinedList = new List<List<int>>(newList);
                }
                else
                {
                    foreach (int mult in mults)
                    {
                        combinedList.Add(new List<int>() { mult });
                    }
                }
            }
            return combinedList;
        }
        private void CombinatoricToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            List<int> activeTabs = GetActiveTabs();
            List<List<int>> multsForTabs = new List<List<int>>();
            List<List<int>> counterForTabs = new List<List<int>>();
            foreach (int tabIndex in activeTabs)
            {
                string inputBatch = Interaction.InputBox(string.Format("Images to generate for {0}", fmapTabControl.TabPages[tabIndex].Text),
                    "Combinatoric Gen prompt", "2", -1, -1);
                if (!Int32.TryParse(inputBatch, out int batchSize))
                {
                    MessageBox.Show(string.Format("{0} could not be parsed", batchSize));
                    return;
                }
                if (batchSize != 0)
                {
                    List<int> mults = new List<int>();
                    List<int> counters = new List<int>();
                    int startValue = -1 * _fmapMults[tabIndex];
                    int endValue = _fmapMults[tabIndex];
                    DialogResult dr = MessageBox.Show("Slide past 0?", "Confirmation",
                        MessageBoxButtons.YesNo);
                    switch (dr)
                    {
                        case DialogResult.No:
                            startValue = 0;
                            break;
                    }
                    for(int i = 0; i <= batchSize-1; i++)
                    {
                        mults.Add(startValue + i * (endValue-startValue) / (batchSize-1));
                        counters.Add(i);
                    }
                    multsForTabs.Add(mults);
                    counterForTabs.Add(counters);
                }
            }
            List<List<int>> combinatoricMults = BuildFmapCombinations(multsForTabs);
            List<List<int>> combinatoricCounters = BuildFmapCombinations(counterForTabs);

            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                List<int> multsForTab = new List<int>(_fmapMults);
                int tabIndex = 0;
                foreach (int tab in activeTabs)
                {
                    multsForTab[tab] = combinatoricMults[i][tabIndex];
                    tabIndex++;
                }
                List<int> countersForTab = combinatoricCounters[i];
                List<int> fmapMultsTmp = _fmapMults;
                _fmapMults = multsForTab;
                string fname = "";
                tabIndex = 0;
                foreach (int tab in activeTabs)
                {
                    fname += fmapTabControl.TabPages[tab].Text + string.Format("-{0}_", countersForTab[tabIndex]);
                    tabIndex++;
                }
                string sampleName = string.Format("{0}_{1}", fname, GenerateSampleName());
                GenerateImage(ApplyTrackbarsToLatent(_currentLatent), sampleName, batchDir, fmapModsUseCurrentTab: false);
                _fmapMults = fmapMultsTmp;
            }
            BatchGeneration(combinatoricMults.Count, 1, CustomGenerateImage, fnameToLatent);
        }
        private float SpatialSelectionDotProd(float[,] fmap)
        {
            float[] fmapFlattened = fmap.Cast<float>().ToArray();
            float[] selectionsFlattened = _fmapSelections[fmapTabControl.SelectedIndex].Cast<float>().ToArray();
            return fmapFlattened.Select((x, index) => x * selectionsFlattened[index]).Sum();
        }
        private float SpatialSelectionDotProdExclusive(float[,] fmap)
        {
            float[] fmapFlattened = fmap.Cast<float>().ToArray();
            float[] selectionsFlattened = _fmapSelections[fmapTabControl.SelectedIndex].Cast<float>().ToArray();
            return fmapFlattened.Select((x, index) =>
            {
                if (selectionsFlattened[index] == 0)
                {
                    return -Math.Abs(x);
                }
                else
                {
                    return x * selectionsFlattened[index] * fmapFlattened.GetLength(0);
                }
            }
            ).Sum();
        }
        private float SpatialSelectionMagnitude(float[,] fmap)
        {
            float[] fmapFlattened = fmap.Cast<float>().ToArray();
            return fmapFlattened.Select((x, index) => Math.Abs(x)).Sum();
        }
        public const int FMAPS_PER_PAGE = 128;
        private void AddSelectorPages()
        {
            foreach(TabPage page in tabControlFmapChannels.TabPages)
            {
                if (page.Text.Contains("Input"))
                {
                    tabControlFmapChannels.TabPages.Remove(page);
                }
            }
            int nonZeroMods = 0;
            foreach (float mod in _fmapMods[fmapTabControl.SelectedIndex])
            {
                if (mod != 0.0f)
                {
                    nonZeroMods++;
                }
            }
            for (int i = 0; i < _fmapMods[fmapTabControl.SelectedIndex].GetLength(0) / FMAPS_PER_PAGE; i++)
            {
                TabPage newPage = new TabPage();
                tabControlFmapChannels.TabPages.Add(newPage);
                FlowLayoutPanel panel = new FlowLayoutPanel();
                panel.Height = newPage.Height;
                panel.Width = newPage.Width;
                newPage.Controls.Add(panel);
                for (int j = i * FMAPS_PER_PAGE; j < (i+1)* FMAPS_PER_PAGE; j++)
                {
                    if (checkBoxViewNonZeroOnly.Checked && _fmapMods[fmapTabControl.SelectedIndex][j] == 0.0f)
                    {
                        continue;
                    }
                    Label newLabel = new Label();
                    newLabel.Size = new System.Drawing.Size(25, 20);
                    newLabel.Text = string.Format("{0}", j);
                    panel.Controls.Add(newLabel);
                    TextBox newBox = new TextBox();
                    newBox.Size = new System.Drawing.Size(40, 20);
                    newBox.Text = string.Format("{0}", _fmapMods[fmapTabControl.SelectedIndex][j]);
                    int newBoxIndex = j;
                    newBox.TextChanged += (s, e) =>
                    {
                        float val;
                        if (!Single.TryParse(newBox.Text, out val))
                        {
                            val = 0.0f;
                        }
                        _fmapMods[fmapTabControl.SelectedIndex][newBoxIndex] = val;
                    };
                    panel.Controls.Add(newBox);
                }
                newPage.Text = string.Format("Input {0}-{1}", i * FMAPS_PER_PAGE, (i + 1) * FMAPS_PER_PAGE);
            }
            //TabPage newPage = new TabPage();
        }
        private void ButtonFmapsToTab_Click(object sender, EventArgs e)
        {
            //_fmapMults.Add(spatialMultBar.Value);
            //_fmapGroupList.Add(string.Format("{0}", fmapTabControl.TabPages.Count));
            //string[] selectedItem = comboBoxDims.Items[comboBoxDims.SelectedIndex].ToString().Split('x');
            //int rowCount = Int32.Parse(selectedItem[0]);
            //int columnCount = Int32.Parse(selectedItem[1]);
            //_fmapSelections.Add(new float[rowCount, columnCount]);


            //_fmapMods.Add(new float[_rowCountToFmapCount[columnCount]]);

            //TabPage newPage = new TabPage();
            //newPage.Text = comboBoxDims.Text;
            //fmapTabControl.TabPages.Add(newPage);
            //fmapTabControl.SelectedIndex = fmapTabControl.TabCount - 1;
        }
        private void ButtonFmapAssign_Click(object sender, EventArgs e)
        {
            if (!Single.TryParse(fmapValueBox.Text, out float val))
            {
                return;
            }
            if (!Int32.TryParse(fmapIndexBox.Text, out int index))
            {
                return;
            }
            if (fmapTabControl.SelectedIndex == -1 || index >= _fmapMods[fmapTabControl.SelectedIndex].GetLength(0))
            {
                return;
            }
            _fmapMods[fmapTabControl.SelectedIndex][index] = val;
        }
        private List<Tuple<int, float[,], float>> ParseSelectedFmaps(string text, List<float[,]> candidateFmaps)
        {
            List<Tuple<int, float[,], float>> selectedFmaps = new List<Tuple<int, float[,], float>>();

            foreach (string fmapSelection in text.Split(','))
            {
                string[] range = fmapSelection.Split('-');
                
                if (range.Length == 1)
                {
                    string[] multAndFmap = range[0].Split('x');
                    float multiplier = 1.0f;
                    string fmapStr = range[0];
                    if (multAndFmap.Length == 2)
                    {
                        float.TryParse(multAndFmap[0], out multiplier);
                        fmapStr = multAndFmap[1];
                    }
                    if (int.TryParse(fmapStr, out int selection) && selection < candidateFmaps.Count)
                    {
                         selectedFmaps.Add(new Tuple<int, float[,], float>(selection, candidateFmaps[selection], multiplier));
                    }
                }
                else if (range.Length == 2)
                {
                    if (int.TryParse(range[0], out int start) && int.TryParse(range[1], out int finish) &&
                        start < finish && finish < candidateFmaps.Count)
                    {
                        for (int i = start; i <= finish; i++)
                        {
                            selectedFmaps.Add(new Tuple<int, float[,], float>(i, candidateFmaps[i], 1.0f));
                        }
                    }

                }
            }
            return selectedFmaps;
        }
        private FmapViewer StartViewerForm()
        {
            FmapViewer viewerForm = new FmapViewer(this);
            Thread creationThread = new Thread(() => viewerForm.ShowDialog());
            creationThread.Start();

            // Hack to wait for form to be created, probably a better way to do this
            for (int j = 0; j < 100; j++)
            {
                Thread.Sleep(100);
                if (_fmapViewerFormStarted)
                {
                    break;
                }
            }
            _fmapViewerFormStarted = false;
            return viewerForm;
        }
        private void ViewMultipleMaps_Click(object sender, EventArgs e)
        {
            FmapViewer viewerForm = StartViewerForm();

            List<float[,]> candidateFmaps;
            if (checkBoxViewAveraged.Checked)
            {
                candidateFmaps = lm.AverageFmaps();
            }
            else
            {
                candidateFmaps = lm.currentFmaps[comboBoxDims.SelectedIndex];
            }
            foreach (Tuple<int, float[,], float> fmap in ParseSelectedFmaps(fmapsToViewTextbox.Text, candidateFmaps))
            {
                viewerForm.Invoke((Action)(() => viewerForm.addFmapDelegate(_currentImagePath, fmap.Item2, fmap.Item1)));
            }
        }
        private void ResetAverageButton_Click(object sender, EventArgs e)
        {
            lm.ResetFmapsSum();
        }
        private void ViewHistoryButton_Click(object sender, EventArgs e)
        {
            FmapViewer viewerForm = StartViewerForm();
            foreach (Tuple<string, List<float[,]>> imageAndFmaps in lm.imageAndFmapsRecord)
            {
                if (int.TryParse(fmapsToViewTextbox.Text.Split(',')[0], out int fmap))
                {
                    viewerForm.Invoke((Action)(() => viewerForm.addFmapDelegate(imageAndFmaps.Item1, imageAndFmaps.Item2[fmap], fmap)));
                }
            }
        }
        private void DelLastButton_Click(object sender, EventArgs e)
        {
            string[] splitFmaps = fmapsToViewTextbox.Text.Split(',');
            fmapsToViewTextbox.Text = String.Join(",", splitFmaps.Take(splitFmaps.Count() - 1).ToArray());
        }
        private void DelFirstButton_Click(object sender, EventArgs e)
        {
            fmapsToViewTextbox.Text = String.Join(",", fmapsToViewTextbox.Text.Split(',').Skip(1).ToArray());
        }
        private float[,] ReadSpatialMap(string path)
        {
            string[] lines = null;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch(System.IO.FileNotFoundException)
            {
                MessageBox.Show(string.Format("{0} could not be read, script may have failed", path));
                return null;
            }
            int rows = lines.GetLength(0);
            int columns = lines[0].Split(',').GetLength(0);
            float[,] spatialMap = new float[rows, columns];
            for (int i = 0; i < rows; i++)
            {
                string[] strRow = lines[i].Split(',');
                for (int j = 0; j < columns; j++)
                {
                    spatialMap[i, j] = float.Parse(strRow[j]);
                }
            }
            return spatialMap;
        }
        private void RunFmapScript(string script, int index)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            string pythonPath = Environment.GetEnvironmentVariable("PYTHONPATH");
            string[] scriptAndFmaps = script.Split(',');
            if (!int.TryParse(scriptAndFmaps[1], out int selectedLayer))
            {
                return;
            }
            if (pythonPath == null || lm.currentFmaps == null || lm.currentFmaps.Count <= selectedLayer)
            {
                return;
            }
            start.FileName = "python.exe";
            string dirName = WriteFmaps(ParseSelectedFmaps(
                String.Join(",", scriptAndFmaps.Skip(2).ToArray()), 
                lm.currentFmaps[selectedLayer]));
            start.Arguments = string.Format("{0} {1}", Path.Combine(LatentManipulator.ScriptsDirName, scriptAndFmaps[0]), dirName);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            //int layerIndex = (int)Math.Log(_fmapSelections[index].GetLength(1), 2) - 2;
            // Right now only get fmaps for single layer
            using (Process process = Process.Start(start))
            {
                //using (StreamReader reader = process.StandardOutput)
                //{
                //    string result = reader.ReadToEnd();
                //}
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    MessageBox.Show(string.Format("Script failed with exit code {0}", process.ExitCode));
                }
                else
                {
                    string spatialMapPath = Path.Combine(dirName, "spatial_map.csv");
                    if (File.Exists(spatialMapPath))
                    {
                        float[,] spatialMap = ReadSpatialMap(spatialMapPath);
                        if (spatialMap != null)
                        {
                            _fmapSelections[index] = spatialMap;
                        }
                    }
                }
            }
            Directory.Delete(dirName, true);
        }
        private string WriteFmaps(List<Tuple<int, float[,], float>> fmaps)
        {
            string dirName = string.Format("fmaps_{0}", Guid.NewGuid());
            Directory.CreateDirectory(dirName);
            for (int fmapIndex = 0; fmapIndex < fmaps.Count; fmapIndex++)
            {
                string path = Path.Combine(dirName, string.Format("{0}.csv", fmaps[fmapIndex].Item1));
                using (TextWriter writer = new StreamWriter(path,
                    true, System.Text.Encoding.UTF8))
                {
                    var csv = new CsvWriter(writer);
                    for (int i = 0; i < fmaps[fmapIndex].Item2.GetLength(0); i++)
                    {
                        string[] column = new string[fmaps[fmapIndex].Item2.GetLength(1)];
                        for (int j = 0; j < fmaps[fmapIndex].Item2.GetLength(1); j++)
                        {
                            column[j] = string.Format("{0}", fmaps[fmapIndex].Item2[i, j] * fmaps[fmapIndex].Item3);
                        }
                        foreach (string entry in column)
                        {
                            csv.WriteField(entry);
                        }
                        csv.NextRecord();
                        csv.Flush();
                    }
                }
            }
            return dirName;
        }
        private void ScriptPathBox_TextChanged(object sender, EventArgs e)
        {
            if (fmapTabControl.SelectedIndex != -1)
            {
                _fmapScripts[fmapTabControl.SelectedIndex] = scriptPathBox.Text;
            }
            
        }
        private void RunScriptButton_Click(object sender, EventArgs e)
        {
            string scriptPath = _fmapScripts[fmapTabControl.SelectedIndex];
            RunFmapScript(scriptPath, fmapTabControl.SelectedIndex);
        }
        private string GenerateImage(float[] latent, string fname, string dir, bool fmapModsUseCurrentTab = true)
        {
            int outLayer = -1;
            if (radioButtonCurrentTabFmaps.Checked)
            {
                outLayer = comboBoxDims.SelectedIndex;
            }
            List<float[,,]> fmapMods = null;
            if (!checkBoxAutoRunScript.Checked)
            {
                fmapMods = SpatialMapToFmaps(useCurrentTab: fmapModsUseCurrentTab);
            }
            string path = lm.GenerateImage(latent, fname, dir, fmapMods: fmapMods,
                outLayer: outLayer, outAllLayers: checkBoxAutoRunScript.Checked || radioButtonAllFmaps.Checked,
                doRecordFmaps: checkBoxRecordFmaps.Checked);

            if (checkBoxAutoRunScript.Checked)
            {
                if (fmapTabControl.TabCount > 0)
                {
                    List<int> scriptTabs = GetScriptTabs();
                    foreach (int tab in scriptTabs)
                    {
                        RunFmapScript(_fmapScripts[tab], tab);
                    }
                }
                path = lm.GenerateImage(latent, fname, dir, 
                    fmapMods: SpatialMapToFmaps(useCurrentTab: fmapModsUseCurrentTab),
                    outLayer: outLayer, doRecordFmaps: checkBoxRecordFmaps.Checked);
            }
            return path;
        }
        private void NewLatentCombinatoricToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Tuple<int, int> batchAndThreads = BatchGeneratePrompt();
            if (batchAndThreads == null)
            {
                return;
            }
            int newLatents = batchAndThreads.Item1;
            if (newLatents <= 1)
            {
                MessageBox.Show("Need batch size of > 1 (just click Generate New for a single image)");
                return;
            }

            List<float[]> intermediates = new List<float[]>();
            for (int i = 0; i < newLatents; i++)
            {
                intermediates.Add(lm.GenerateNewIntermediateLatent());
            }

            List<int> activeTabs = GetActiveTabs();
            List<List<int>> multsForTabs = new List<List<int>>();
            List<List<int>> counterForTabs = new List<List<int>>();
            foreach (int tabIndex in activeTabs)
            {
                string inputBatch = Interaction.InputBox(string.Format("Images to generate for {0}", fmapTabControl.TabPages[tabIndex].Text),
                    "Combinatoric Gen prompt", "2", -1, -1);
                if (!Int32.TryParse(inputBatch, out int batchSize))
                {
                    MessageBox.Show(string.Format("{0} could not be parsed", batchSize));
                    return;
                }
                if (batchSize != 0)
                {
                    List<int> mults = new List<int>();
                    List<int> counters = new List<int>();
                    int startValue = -1 * _fmapMults[tabIndex];
                    int endValue = _fmapMults[tabIndex];
                    DialogResult dr = MessageBox.Show("Slide past 0?", "Confirmation",
                        MessageBoxButtons.YesNo);
                    switch (dr)
                    {
                        case DialogResult.No:
                            startValue = 0;
                            break;
                    }
                    for (int i = 0; i <= batchSize - 1; i++)
                    {
                        mults.Add(startValue + i * (endValue - startValue) / (batchSize - 1));
                        counters.Add(i);
                    }
                    multsForTabs.Add(mults);
                    counterForTabs.Add(counters);
                }
            }
            List<List<int>> combinatoricMults = BuildFmapCombinations(multsForTabs);
            List<List<int>> combinatoricCounters = BuildFmapCombinations(counterForTabs);

            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            void CustomGenerateImage(int i, string batchDir)
            {
                int combinatoricIndex = i % combinatoricMults.Count;
                int batchIndex = i / combinatoricMults.Count;
                List<int> multsForTab = new List<int>(_fmapMults);
                int tabIndex = 0;
                foreach (int tab in activeTabs)
                {
                    multsForTab[tab] = combinatoricMults[combinatoricIndex][tabIndex];
                    tabIndex++;
                }
                List<int> countersForTab = combinatoricCounters[combinatoricIndex];
                List<int> fmapMultsTmp = _fmapMults;
                _fmapMults = multsForTab;
                string fname = "";
                tabIndex = 0;
                foreach (int tab in activeTabs)
                {
                    fname += fmapTabControl.TabPages[tab].Text + string.Format("-{0}_", countersForTab[tabIndex]);
                    tabIndex++;
                }
                string sampleName = string.Format("{0}_{1}", fname, GenerateSampleName());
                GenerateImage(ApplyTrackbarsToLatent(intermediates[batchIndex]), sampleName, batchDir, fmapModsUseCurrentTab: false);
                _fmapMults = fmapMultsTmp;
            }
            BatchGeneration(combinatoricMults.Count * intermediates.Count, 1, CustomGenerateImage, fnameToLatent);
        }
        private void ButtonSetToZero_Click(object sender, EventArgs e)
        {
            spatialMultBar.Value = 0;
            _fmapMults[fmapTabControl.SelectedIndex] = 0;
        }
        private void SpatialMultBar_Scroll(object sender, EventArgs e)
        {
            _fmapMults[fmapTabControl.SelectedIndex] = spatialMultBar.Value;
        }
    }
}

