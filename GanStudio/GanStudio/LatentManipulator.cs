using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CsvHelper;
using GanTools;

namespace GanStudio
{
    public class RawAttribute
    {
        public string Name { get; set; }
        public string GraphHash { get; set; }
        public string Increment { get; set; }
    }
    public struct LatentForFilename
    {
        public string Fname { get; set; }
        public string GraphHash { get; set; }
        public string Latent { get; set; }
        public LatentForFilename(string _fname, string _graphhash, string _latent)
        {
            Fname = _fname;
            GraphHash = _graphhash;
            Latent = _latent;
        }
    }
    class LatentManipulator
    {
        private GanModelInterface g;

        private const string version = "1.0";
        // todo: getters and setters
        public string FavoritesLatentPath;
        public string AdvancedAttributesPath;
        public string CustomAttributesPath;
        public string FavoriteAttributesPath;
        public string LatentCsvPath;
        public string AttributesCsvPath;
        public string AverageLatentCsvPath;
        public string AppendedDescription;
        public string DataDir;
        public SortedDictionary<string, float[]> _attributeToLatent;

        public const string FavoritesDirName = "favorites";
        public const string SampleDirName = "portraits";
        public const string ScriptsDirName = "scripts";

        public List<Tuple<string, float[,,]>> _currentNoise = null;  // todo: add multi-map support
        public float[] _averageAll;
        public List<List<float[,]>> currentFmaps;
        public List<float[,]> fmapsSum;
        public List<Tuple<string, List<float[,]>>> imageAndFmapsRecord = new List<Tuple<string, List<float[,]>>>();
        public Dictionary<int, int> widthToIndex = new Dictionary<int, int>();
        public int fmapsTotalSummed = 0;
        public byte[] _graphHash;
        public string _graphHashStr;
        public string _latentTensorName;
        public string _outputTensorName;

        public List<Tuple<string, int[]>> _layersIn = null;
        public List<Tuple<string, int[]>> _layersOut = null;
        public int[] _maxRes = { 256, 256 };
        public LatentManipulator(string graphPath="graph.pb")
        {
            // GanModelInterface is the interface to the model that generates images
            g = new GanModelInterface(__graph_path: graphPath, "", "", "", "");

            AppendedDescription = string.Format("LatentManipulator version {0}. Latent code for GAN model" +
                    " used to generate this image:", version);
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(g.GetGraphPath()))
                {
                    _graphHash = md5.ComputeHash(stream);
                }
            }
            _graphHashStr = BitConverter.ToString(_graphHash).Replace("-", "").ToLowerInvariant();

            DataDir = string.Format("data_{0}", _graphHashStr);
            LoadModelConfig(DataDir);

            FavoritesLatentPath = Path.Combine(DataDir, "favorite_latents.csv");
            AdvancedAttributesPath = Path.Combine(DataDir, "advanced_attributes.csv");
            CustomAttributesPath = Path.Combine(DataDir, "custom_attributes.csv");
            FavoriteAttributesPath = Path.Combine(DataDir, "favorite_attributes.csv");
            LatentCsvPath = Path.Combine(DataDir, "latents.csv");
            AttributesCsvPath = Path.Combine(DataDir, "attributes.csv");
            AverageLatentCsvPath = Path.Combine(DataDir, "average_latent.csv");
            currentFmaps = new List<List<float[,]>>();
        }
        public Dictionary<int, int> GetWidthToFmapCount()
        {
            Dictionary<int, int> widthToFmapCount = new Dictionary<int, int>();
            if (_layersIn != null)
            {
                foreach(Tuple<string, int[]> layer in _layersIn)
                {
                    widthToFmapCount.Add(layer.Item2[2], layer.Item2[3]);
                }
            }
            return widthToFmapCount;
        }

        private void LoadModelConfig(string graphDir)
        {
            _layersIn = new List<Tuple<string, int[]>>();
            _layersOut = new List<Tuple<string, int[]>>();
            XmlDocument doc = null;
            try
            {
                doc = new XmlDocument();
                doc.Load(Path.Combine(graphDir, "config.xml"));
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new Exception("No configuration file found.", e);
            }
            _maxRes = new int[]
            {
                int.Parse(doc.GetElementsByTagName("resolution")[0].FirstChild.InnerText), // height
                int.Parse(doc.GetElementsByTagName("resolution")[0].LastChild.InnerText), // width
            };
            _outputTensorName = doc.GetElementsByTagName("output_name")[0].InnerText;
            _latentTensorName = doc.GetElementsByTagName("intermediate_latent_name")[0].InnerText;
            int count = 0;
            foreach(XmlElement layerElement in doc.GetElementsByTagName("input_layers")[0].ChildNodes)
            {
                // first child: name.  last child: dimensions
                string[] strDims = layerElement.LastChild.InnerText.Split(','); // dims
                int[] dims = new int[]
                {
                    int.Parse(strDims[0]),
                    int.Parse(strDims[1]),
                    int.Parse(strDims[2]),
                    int.Parse(strDims[3]),
                };
                _layersIn.Add(new Tuple<string, int[]>(layerElement.FirstChild.InnerText, dims));
                widthToIndex.Add(dims[2], count);
                count++;
            }
            foreach (XmlElement layerElement in doc.GetElementsByTagName("output_layers")[0].ChildNodes)
            {
                // first child: name.  last child: dimensions
                string[] strDims = layerElement.LastChild.InnerText.Split(','); // dims
                int[] dims = new int[]
                {
                    int.Parse(strDims[0]),
                    int.Parse(strDims[1]),
                    int.Parse(strDims[2]),
                    int.Parse(strDims[3]),
                };
                _layersOut.Add(new Tuple<string, int[]>(layerElement.FirstChild.InnerText, dims));
            }
        }
        public float[] GetAverageLatent()
        {
            return _averageAll;
        }
        public static float[] VectorCombine(float[] v1, float[] v2, float factorV1, float factorV2)
        {
            return v1.Select((x, index) => x * factorV1 + factorV2 * v2[index]).ToArray();
        }
        private static byte[] ExtractBytes(byte[] bytes, int offset, int numBytes)
        {
            byte[] extractedBytes = new byte[numBytes];
            Buffer.BlockCopy(bytes, offset, extractedBytes, 0, numBytes);
            return extractedBytes;
        }
        public float[] ModAttribute(string attribute, float factor, float[] baseLatent)
        {
            return VectorCombine(baseLatent, _attributeToLatent[attribute], 1.0f, factor);
        }
        public float[] ModUniqueness(float[] latent, float factor)
        {
            if (_averageAll == null)
            {
                return latent;
            }
            return VectorCombine(latent, _averageAll, factor, 1.0f - factor);
        }
        public void WriteToLatentFile(string fname, float[] latent)
        {
            if (!File.Exists(LatentCsvPath))
            {
                return;
            }
            using (TextWriter writer = new StreamWriter(LatentCsvPath, true, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer);
                csv.WriteRecord(new LatentForFilename(fname, _graphHashStr, string.Join(":", latent)));
                csv.NextRecord();
                csv.Flush();
            }
        }
        public float[] GenerateNewIntermediateLatent()
        {
            return g.GenerateNewIntermediateLatent(_latentTensorName);
        }
        public float[,,] VectorAdd3D(float[,,] v1, float[,,] v2)
        {
            if (v1.GetLength(0) != v2.GetLength(0) ||
                v1.GetLength(1) != v2.GetLength(1) ||
                v1.GetLength(2) != v2.GetLength(2))
            {
                throw new ArgumentException("v1 and v2 do not have matching dims");
            }
            float[,,] result = new float[v1.GetLength(0),v1.GetLength(1),v1.GetLength(2)];
            for (int i = 0; i < v1.GetLength(0); i++)
            {
                for (int j = 0; j < v1.GetLength(1); j++)
                {
                    for (int k = 0; k < v1.GetLength(2); k++)
                    {
                        result[i, j, k] = v1[i, j, k] + v2[i, j, k];
                    }
                }
            }
            return result;
        }
        public string GenerateImage(float[] latent, string fname, string dir = SampleDirName, List<float[,,]> fmapMods = null, List<int> outLayers = null, bool doRecordFmaps = false)
        {
            string path = Path.Combine(dir, fname);
            if (latent.Contains(float.NaN))
            {
                System.IO.File.WriteAllText(string.Format("{0}.txt", path), "Sorry, something went wrong generating this image");
                return path;
            }
            string fmapsName = "";
            

            // Build session inputs
            List<GanTools.TensorData> inputs = new List<GanTools.TensorData>();
            inputs.Add(new TensorData(_latentTensorName, latent, 2, new int[] { 1, 512 }));
            //fmapMod = VectorAdd3D(fmapMod, _currentFmap.Item2);
            if (fmapMods != null)
            {
                for (int mod = 0; mod < fmapMods.Count; mod++)
                {
                    int width = fmapMods[mod].GetLength(1);
                    int layerIndex = widthToIndex[width]; //(int)Math.Log(width, 2) - 2;

                    fmapsName = _layersIn[layerIndex].Item1;
                    inputs.Add(new TensorData(fmapsName, fmapMods[mod].Cast<float>().ToArray(),
                        4, new int[] { 1, fmapMods[mod].GetLength(0), fmapMods[mod].GetLength(1), fmapMods[mod].GetLength(2) }));
                }
            }

            List<GanTools.TensorData> outputs = new List<GanTools.TensorData>();
            outputs.Add(new TensorData(_outputTensorName, null, 0, null));

            if (outLayers != null)
            {
                foreach(int i in outLayers)
                {
                    fmapsName = _layersOut[i].Item1;
                    int[] dims = _layersOut[i].Item2;
                    float[] newFmap = new float[dims[1] * dims[2] * dims[3]];
                    // ToArray() may copy, in which case this won't work
                    outputs.Add(new TensorData(fmapsName, newFmap, 4, new int[] { 1, dims[1], dims[2], dims[3] }));
                }
            }


            g.GenerateImageFromIntermediate(inputs, ref outputs, path);
            //if (!fmapsUsedAsInput)
            //{
            //    _currentFmap = outMaps;
            //}
            if (outLayers != null)
            {
                if (outLayers.Count == 1)
                {
                    int outLayer = outLayers[0];
                    int[] dims = _layersOut[outLayer].Item2;
                    float[,,] fmapShaped = new float[dims[1], dims[2], dims[3]];
                    Buffer.BlockCopy(outputs[1].Data, 0, fmapShaped, 0, dims[1] * dims[2] * dims[3] * sizeof(float));
                    List<float[,]> currentResFmap = new List<float[,]>();
                    for (int i = 0; i < dims[3]; i++)
                    {
                        float[,] newFmap = new float[dims[1], dims[2]];
                        for(int j = 0; j < dims[1]; j++)
                        {
                            for (int k = 0; k < dims[2]; k++)
                            {
                                newFmap[j, k] = fmapShaped[j, k, i];
                            }
                        }
                        currentResFmap.Add(newFmap);
                    }
                    int oldCount = currentFmaps.Count;
                    for (int i = 0; i <= outLayer - oldCount; i++)
                    {
                        currentFmaps.Add(null);
                    }
                    currentFmaps[outLayer] = currentResFmap;
                    if (doRecordFmaps)
                    {
                        imageAndFmapsRecord.Add(new Tuple<string, List<float[,]>>(path, currentResFmap));
                        if (fmapsSum == null || fmapsSum.Count == 0 || currentResFmap[0].GetLength(0) != fmapsSum[0].GetLength(0))
                        {
                            fmapsSum = currentResFmap;
                        }
                        else
                        {
                            for (int fmap = 0; fmap < currentFmaps.Count; fmap++)
                            {
                                for (int i = 0; i < currentResFmap[0].GetLength(0); i++)
                                {
                                    for (int j = 0; j < currentResFmap[0].GetLength(1); j++)
                                    {
                                        fmapsSum[fmap][i, j] += currentResFmap[fmap][i, j];
                                    }
                                }
                            }
                        }
                        fmapsTotalSummed += 1;
                    }
                }
                else
                {
                    currentFmaps = new List<List<float[,]>>();
                    int outputCounter = 0;
                    for(int layerIndex = 0; layerIndex < _layersOut.Count; layerIndex++)
                    {
                        currentFmaps.Add(null);
                    }
                    foreach (int layerIndex in outLayers)
                    {
                        int[] dims = _layersOut[layerIndex].Item2;
                        float[,,] fmapShaped = new float[dims[1], dims[2], dims[3]];
                        Buffer.BlockCopy(outputs[outputCounter + 1].Data, 0, fmapShaped, 0, dims[1] * dims[2] * dims[3] * sizeof(float));
                        outputCounter += 1;
                        List<float[,]> currentResFmap = new List<float[,]>();
                        for (int i = 0; i < dims[3]; i++)
                        {
                            float[,] newFmap = new float[dims[1], dims[2]];
                            for (int j = 0; j < dims[1]; j++)
                            {
                                for (int k = 0; k < dims[2]; k++)
                                {
                                    newFmap[j, k] = fmapShaped[j, k, i];
                                }
                            }
                            currentResFmap.Add(newFmap);
                        }
                        currentFmaps[layerIndex] = currentResFmap;
                    }
                }
            }
            //if (_currentNoise == null)
            //{
            //    int i = 2;
            //    _currentNoise = new List<Tuple<string, float[,,]>>();
            //    foreach (var noiseTuple in noiseData)
            //    {
            //        float[,,] noiseShaped = new float[noiseTuple.Item2[1], noiseTuple.Item2[2], noiseTuple.Item2[3]];
            //        Buffer.BlockCopy(outputs[i].Data, 0, noiseShaped, 0, noiseTuple.Item2[1] * noiseTuple.Item2[2] * noiseTuple.Item2[3] * sizeof(float));
            //        _currentNoise.Add(new Tuple<string, float[,,]>(noiseTuple.Item1, noiseShaped));
            //        i++;
            //    }
            //}
            AppendLatentToImage(latent, path);
            return path;
        }
        public bool AppendLatentToImage(float[] latent, string path)
        {
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fileStream))
                {
                    string latentStr = string.Join(":", latent);
                    // explaning why this data is present in the png
                    bw.Write(AppendedDescription);
                    bw.Write(string.Join(":", latent));
                    bw.Write(_graphHash);
                    bw.Write(latentStr.Length);
                    bw.Write(0x61726164); // magic identifier
                }
                return true;
            }
            catch (System.IO.IOException)
            {
                return false;
            }
           
        }
        public float[] ExtractLatentFromImage(string path)
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            int fileLength = fileBytes.Length;
            // Format: 
            // Explanitory string
            // Variable length latent code as string
            // 16 bytes: MD5 of graph used to generate this image
            // 4 bytes: length of latent code
            // 4 bytes: magic value (0x61726164)
            int magicNbytes = 4;
            int hashNbytes = 16;
            int latentLenNbytes = 4;

            int magic = BitConverter.ToInt32(fileBytes, fileLength - magicNbytes);
            if (magic != 0x61726164)
            {
                throw new ArgumentException("Bad magic value");
            }
            int latentLength = BitConverter.ToInt32(fileBytes, fileLength - (magicNbytes + latentLenNbytes));
            int latentStart = fileLength - (latentLength + latentLenNbytes + hashNbytes + magicNbytes);
            if (latentStart < 0)
            {
                throw new ArgumentException(string.Format("Bad latent start value {0}", latentStart));
            }
            byte[] graphHash = ExtractBytes(fileBytes, fileLength - (magicNbytes + hashNbytes + latentLenNbytes), hashNbytes);
            if (!graphHash.SequenceEqual(_graphHash))
            {
                //throw new ArgumentException(string.Format("This image was generated by a different model, with graph.pb MD5 {0} instead of {1}",
                //    BitConverter.ToString(graphHash).Replace("-", "").ToLowerInvariant(),
                //    _graphHashStr));
            }
            byte[] latentBytes = fileBytes.Skip(latentStart).Take(latentLength).ToArray();
            String latentString = System.Text.Encoding.Default.GetString(latentBytes);
            float[] latent = Array.ConvertAll(latentString.Split(new char[] { ':' },
                StringSplitOptions.RemoveEmptyEntries), Single.Parse);
            if (latent.Length != 512)
            {
                throw new ArgumentException(string.Format("Bad latent length {0}", latent.Length));
            }
            return latent;
        }
        public SortedDictionary<string, float[]> GetNameToLatentMap()
        {
            // Build a dictionary of all file names record in the the csv files that map the name
            // to the latent code (on disk entries in the code are separated by ':')
            SortedDictionary<string, float[]> fnameToLatent = new SortedDictionary<string, float[]>();
            if (File.Exists(LatentCsvPath))
            {
                using (StreamReader textReader = new StreamReader(LatentCsvPath))
                {
                    var csvr = new CsvReader(textReader);
                    csvr.Configuration.Delimiter = ",";
                    csvr.Configuration.HasHeaderRecord = false;
                    var records = csvr.GetRecords<LatentForFilename>();
                    foreach (LatentForFilename record in records)
                    {
                        if (!fnameToLatent.ContainsKey(record.Fname))
                        {
                            fnameToLatent.Add(record.Fname,
                                Array.ConvertAll(record.Latent.Split(new char[] { ':' },
                                StringSplitOptions.RemoveEmptyEntries), Single.Parse));
                        }
                    }
                }
            }
            if (File.Exists(FavoritesLatentPath))
            {
                using (StreamReader textReader = new StreamReader(FavoritesLatentPath))
                {
                    var csvr = new CsvReader(textReader);
                    csvr.Configuration.Delimiter = ",";
                    csvr.Configuration.HasHeaderRecord = false;
                    var records = csvr.GetRecords<LatentForFilename>();
                    foreach (LatentForFilename record in records)
                    {
                        if (!fnameToLatent.ContainsKey(record.Fname))
                        {
                            fnameToLatent.Add(record.Fname,
                                Array.ConvertAll(record.Latent.Split(new char[] { ':' },
                                StringSplitOptions.RemoveEmptyEntries), Single.Parse));
                        }
                    }
                }
            }
            return fnameToLatent;
        }
        public float[] LoadSavedLatent(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            try
            {
                float[] latent = ExtractLatentFromImage(filePath);
                return latent;
            }
            catch (ArgumentException e)
            {
                Debug.WriteLine(e.Message);
            }
            // Ensure the files that map file names to latents exist
            if (!File.Exists(LatentCsvPath) && !File.Exists(FavoritesLatentPath))
            {
                Debug.WriteLine(string.Format("Could not find {0} or {1}",
                    LatentCsvPath, FavoritesLatentPath));
                return null;
            }

            SortedDictionary<string, float[]> fnameToLatent = GetNameToLatentMap();

            // Check the dictionary for the file name, if it exists return the corresponding latent
            if (!fnameToLatent.ContainsKey(fileName))
            {
                Debug.WriteLine(string.Format("Could not find {0} in {1}", fileName, LatentCsvPath));
                return null;
            }
            else
            {
                return fnameToLatent[fileName];
            }
        }
        public void DeleteAttributeFromFile(string att, string path)
        {
            var tempFile = Path.GetTempFileName();
            var linesToKeep = File.ReadLines(path).Where(l => l.Substring(0, att.Length + 1) != string.Format("{0},", att));
            File.WriteAllLines(tempFile, linesToKeep);
            File.Delete(path);
            File.Move(tempFile, path);
        }
        public void FavoriteAttribute(string att)
        {
            // Write attribute to favorite attributes file
            using (TextWriter writer = new StreamWriter(FavoriteAttributesPath,
                true, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer);
                csv.WriteRecord(new LatentForFilename(att, _graphHashStr, string.Join(":", _attributeToLatent[att])));
                csv.NextRecord();
                csv.Flush();
            }
        }
        public List<float[,]> AverageFmaps()
        {
            List<float[,]> averagedFmaps = new List<float[,]>();
            for (int fmap = 0; fmap < fmapsSum.Count; fmap++)
            {
                averagedFmaps.Add(new float[fmapsSum[0].GetLength(0), fmapsSum[0].GetLength(1)]);
                for (int i = 0; i < fmapsSum[0].GetLength(0); i++)
                {
                    for (int j = 0; j < fmapsSum[0].GetLength(1); j++)
                    {
                        averagedFmaps[fmap][i, j] = fmapsSum[fmap][i, j] / fmapsTotalSummed;
                    }
                }
            }
            return averagedFmaps;
        }
        public void ResetFmapsSum()
        {
            fmapsSum = new List<float[,]>();
            imageAndFmapsRecord = new List<Tuple<string, List<float[,]>>>();
            fmapsTotalSummed = 0;
        }
        public int ReadAverageLatentFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Console.WriteLine("Could not find {0} in {1}", csvPath, System.IO.Directory.GetCurrentDirectory());
                return -1;
            }
            using (StreamReader textReader = new StreamReader(csvPath))
            {
                var csvr = new CsvReader(textReader);
                csvr.Configuration.Delimiter = ",";
                csvr.Configuration.HasHeaderRecord = false;
                var records = csvr.GetRecords<RawAttribute>();
                foreach (RawAttribute record in records)
                {
                    if (record.Name == "all")
                    {
                        _averageAll = Array.ConvertAll(record.Increment.Split(new char[] { ':' },
                            StringSplitOptions.RemoveEmptyEntries), Single.Parse);
                    }

                }
            }
            return 1;
        }
        public List<string> ReadAttributesFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                return null;
            }
            List<string> newAttributes = new List<string>();
            using (StreamReader textReader = new StreamReader(csvPath))
            {
                var csvr = new CsvReader(textReader);
                csvr.Configuration.Delimiter = ",";
                csvr.Configuration.HasHeaderRecord = false;
                var records = csvr.GetRecords<RawAttribute>();
                foreach (RawAttribute record in records)
                {
                    if (_attributeToLatent.ContainsKey(record.Name))
                    {
                        Debug.WriteLine(string.Format("Duplicate attribute name {0}", record.Name));
                        return null;
                    }
                    _attributeToLatent.Add(record.Name, Array.ConvertAll(record.Increment.Split(new char[] { ':' },
                        StringSplitOptions.RemoveEmptyEntries), Single.Parse));
                    newAttributes.Add(record.Name);
                }
            }
            return newAttributes;
        }
        public void ResetAverage()
        {
            ReadAverageLatentFromCsv(AverageLatentCsvPath);
        }
        public void ImportAttributes(string fileName)
        {
            List<string> newAttributes = ReadAttributesFromCsv(fileName);
            if (newAttributes == null)
            {
                return;
            }
            foreach (string newAttribute in newAttributes)
            {
                // Because this is written to custom attribute file, it will be loaded
                // when custom attributes are toggled on
                // Write modifier to custom attributes file
                using (TextWriter writer = new StreamWriter(CustomAttributesPath,
                    true, System.Text.Encoding.UTF8))
                {
                    var csv = new CsvWriter(writer);
                    csv.WriteRecord(new LatentForFilename(newAttribute, _graphHashStr,
                        string.Join(":", _attributeToLatent[newAttribute])));
                    _attributeToLatent.Remove(newAttribute);
                    csv.NextRecord();
                    csv.Flush();
                }
            }
        }
        public List<Tuple<float[], List<string>>> BuildCombinations(List<Tuple<float[], List<string>>> currentLatents, Stack<string> attributesToModify,
            Dictionary<string, List<float[]>> changesForAttribute)
        {
            if (attributesToModify.Count == 0)
            {
                return currentLatents;
            }
            string currentAttribute = attributesToModify.Pop();
            List<Tuple<float[], List<string>>> newLatents = new List<Tuple<float[], List<string>>>();
            foreach (Tuple<float[], List<string>> latentAndName in currentLatents)
            {
                float[] latent = latentAndName.Item1;
                int counter = 0;
                foreach (float[] change in changesForAttribute[currentAttribute])
                {
                    List<string> names = new List<string>(latentAndName.Item2);
                    names.Add(string.Format("{0}-{1}", currentAttribute, counter));
                    Tuple<float[], List<string>> latentNameTuple = new Tuple<float[], List<string>>(
                        VectorCombine(latent, change, 1.0f, 1.0f),
                        names);
                    newLatents.Add(latentNameTuple);
                    counter += 1;
                }
            }
            return BuildCombinations(newLatents, attributesToModify, changesForAttribute);
        }
        public List<Tuple<float[], List<string>>> BuildSequences(List<Tuple<float[], List<string>>> currentLatents, Stack<string> attributesToModify,
            Dictionary<string, List<float[]>> changesForAttribute)
        {
            List<Tuple<float[], List<string>>> newLatents = new List<Tuple<float[], List<string>>>();
            foreach (string currentAttribute in attributesToModify)
            {
                foreach (Tuple<float[], List<string>> latentAndName in currentLatents)
                {
                    float[] latent = latentAndName.Item1;
                    int counter = 0;
                    foreach (float[] change in changesForAttribute[currentAttribute])
                    {
                        List<string> names = new List<string>(latentAndName.Item2);
                        names.Add(string.Format("{0}-{1}", currentAttribute, counter));
                        Tuple<float[], List<string>> latentNameTuple = new Tuple<float[], List<string>>(
                            VectorCombine(latent, change, 1.0f, 1.0f),
                            names);
                        newLatents.Add(latentNameTuple);
                        counter += 1;
                    }
                }
            }
            return newLatents;
        }
    }
}
