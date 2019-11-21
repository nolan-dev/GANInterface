using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace GanTools
{
    // from https://social.msdn.microsoft.com/Forums/vstudio/en-US/d13392f0-3b87-4908-b04e-f0bcf834409f/cannot-marshal-parameter-2-there-is-no-marshaling-support-for-nested-arrays?forum=csharpgeneral
    class JaggedArrayMarshaler : ICustomMarshaler
    {
        static ICustomMarshaler GetInstance(string cookie)
        {
            return new JaggedArrayMarshaler();
        }
        // Only one class is created when calling the external function, so have to keep track of all GCHandle Allocs to avoid memory leaks
        List<GCHandle[]> handleList = null;
        List<GCHandle> bufferList = null;
        Array[] array;
        public void CleanUpManagedData(object ManagedObj)
        {
        }
        public void CleanUpNativeData(IntPtr pNativeData)
        {
            if (bufferList != null)
            {
                foreach (GCHandle buffer in bufferList)
                {
                    buffer.Free();
                }

            }
            if (bufferList != null)
            {
                foreach (GCHandle[] handles in handleList)
                {
                    foreach (GCHandle handle in handles)
                    {
                        handle.Free();
                    }
                }
            }
            handleList = null;
            bufferList = null;
        }
        public int GetNativeDataSize()
        {
            return 4;
        }
        public IntPtr MarshalManagedToNative(object ManagedObj)
        {
            if (handleList == null)
            {
                handleList = new List<GCHandle[]>();
            }
            if (bufferList == null)
            {
                bufferList = new List<GCHandle>();
            }
            array = (Array[])ManagedObj;
            GCHandle[] handles = new GCHandle[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                handles[i] = GCHandle.Alloc(array[i], GCHandleType.Pinned);
            }
            IntPtr[] pointers = new IntPtr[handles.Length];
            for (int i = 0; i < handles.Length; i++)
            {
                pointers[i] = handles[i].AddrOfPinnedObject();
            }
            GCHandle buffer = GCHandle.Alloc(pointers, GCHandleType.Pinned);
            handleList.Add(handles);
            bufferList.Add(buffer);
            return buffer.AddrOfPinnedObject();
        }
        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            // doesn't get called (todo: lookup)
            //return array;
            return null;
        }
    }
    public struct TensorData
    {
        public string Name { get; set; }
        public float[] Data { get; set; }
        public int NumDims { get; set; }
        public int[] Dims { get; set; }
        public TensorData(string _name, float[] _data, int _numDims, int[] _dims)
        {
            Name = _name;
            Data = _data;
            NumDims = _numDims;
            Dims = _dims;
        }
    }
    public class GanModelInterface
    {
        public const string DefaultGraphPath = "graph.pb";
        //[DllImport("TensorflowInterface.dll")]
        //public static extern int image_from_latent(float[] latent, string input_tensor_name, string out_path,
        //    int fmap_channels, int fmap_height, int fmap_width, float[, ,] fmaps, string fmap_name);
        [DllImport("TensorflowInterface.dll")]
        public static extern int image_and_fmaps_from_latent(int num_inputs, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(JaggedArrayMarshaler))]float[][] inputs, 
            int[] input_num_dims,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(JaggedArrayMarshaler))]int[][] input_dims, 
            string[] input_names,
            int num_outputs,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(JaggedArrayMarshaler))]float[][] outputs, 
            int[] output_num_dims,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(JaggedArrayMarshaler))]int[][] output_dims, string[] output_names, string out_path);

        //public static extern int image_and_fmaps_from_latent(float[] latent, string input_tensor_name, string out_path,
        //    string fmap_name, float[,,] output_fmaps, float[,,] input_fmaps, int fmap_height, int fmap_width, int use_input_fmaps);

        [DllImport("TensorflowInterface.dll")]
        public static extern int generate_intermediate_latent(float[] out_intermediate_latent);

        [DllImport("TensorflowInterface.dll")]
        public static extern int initialize_global_session(string graph_path);

        [DllImport("TensorflowInterface.dll")]
        public static extern void close_global_session();

        private readonly string _graphPath;

        public GanModelInterface()
        {
            _graphPath = DefaultGraphPath;
            Initialize();
        }
        public GanModelInterface(string __graph_path, string __sample_dir, string __latent_file, 
            string __attribute_csv, string __average_latent_file)
        {
            _graphPath = __graph_path;
            Initialize();
        }
        private void Initialize()
        {
            if (!File.Exists(_graphPath))
            {
                throw new ArgumentException(string.Format("Initialization failed, Could not find {0} (working dir: {1})", 
                    _graphPath, 
                    System.IO.Directory.GetCurrentDirectory()));
            }
            if (initialize_global_session(_graphPath) == -1)
            {
                throw new Exception("Initialization failed, in initialize_global_session");
            }
        }
        public float[] GenerateNewIntermediateLatent()
        {
            float[] newIntermediate = new float[512];
            generate_intermediate_latent(newIntermediate);
            return newIntermediate;
        }
        public void CloseSession()
        {
            close_global_session();
        }
        public void GenerateImageFromIntermediate(List<TensorData> inputs, ref List<TensorData> outputs, string samplePath)
        {
            //int fmapChannels;
            //int fmapHeight;
            //int fmapWidth;

            //fmapHeight = fmaps.GetLength(0);
            //fmapWidth = fmaps.GetLength(1);
            //fmapChannels = fmaps.GetLength(2);

            //float[][] input_tensors, int[][] input_tensor_dims, string[] input_names,
            //float[][] output_tensors, int[][] output_tensor_dims, string[] output_names, string out_path, int use_input_fmaps
            //if (image_from_latent(latent, "intermediate_latent", samplePath, 
            //    fmapChannels, fmapHeight, fmapWidth, fmaps, layerName) == -1)

            //float[,,] outFmaps = new float[fmapHeight, fmapWidth, fmapChannels];
            //int use_input_fmaps = 0;  // BOOL is an int in c
            //if (fmapInputs)
            //{
            //    use_input_fmaps = 1;
            //}


            //int num_inputs, float[][] inputs, int[] input_num_dims, int[][] input_dims, string[] input_names,
            //int num_outputs, float[][] outputs, int[] output_num_dims, int[][] output_dims, string[] output_names, string out_path
            int[] inputNumDims = new int[inputs.Count];
            string[] inputNames = new string[inputs.Count];
            int[][] inputDims = new int[inputs.Count][];
            float[][] inputData = new float[inputs.Count][];
            int i = 0;
            foreach (TensorData tdata in inputs)
            {
                inputNumDims[i] = tdata.NumDims;
                inputNames[i] = tdata.Name;
                inputDims[i] = tdata.Dims;
                inputData[i] = tdata.Data;
                i++;
            }

            int[] outputNumDims = new int[outputs.Count];
            string[] outputNames = new string[outputs.Count];
            int[][] outputDims = new int[outputs.Count][];
            float[][] outputData = new float[outputs.Count][];
            i = 0;
            foreach (TensorData tdata in outputs)
            {
                outputNumDims[i] = tdata.NumDims;
                outputNames[i] = tdata.Name;
                outputDims[i] = tdata.Dims;
                outputData[i] = tdata.Data;
                i++;
            }
            // don't pass arrays as 'out' variables to native: out variables are for pass by reference, so out array is a pointer to a pointer
            if (image_and_fmaps_from_latent(inputs.Count, inputData, inputNumDims, inputDims, inputNames,
                outputs.Count, outputData, outputNumDims, outputDims, outputNames, samplePath) == -1)
            {
                throw new Exception("Image from latent failed");
            }
        }
        //public void GenerateImageFromIntermediate(float[] latent, string samplePath, float[, ,] fmaps, string layerName, out float[, ,] fmapsOut, bool fmapInputs)
        //{
        //    int fmapChannels;
        //    int fmapHeight;
        //    int fmapWidth;

        //    fmapHeight = fmaps.GetLength(0);
        //    fmapWidth = fmaps.GetLength(1);
        //    fmapChannels = fmaps.GetLength(2);
            
        //    //if (image_from_latent(latent, "intermediate_latent", samplePath, 
        //    //    fmapChannels, fmapHeight, fmapWidth, fmaps, layerName) == -1)
        //    float [, ,] outFmaps = new float[fmapHeight, fmapWidth, fmapChannels];
        //    // don't pass arrays as 'out' variables to native: out variables are for pass by reference, so out array is a pointer to a pointer
        //    int use_input_fmaps = 0;  // BOOL is an int in c
        //    if (fmapInputs)
        //    {
        //        use_input_fmaps = 1;
        //    }
        //    if (image_and_fmaps_from_latent(latent, "intermediate_latent", samplePath, layerName, outFmaps, fmaps, fmapHeight, fmapWidth, use_input_fmaps) == -1)
        //    {
        //        throw new Exception("Image from latent failed");
        //    }
        //    fmapsOut = outFmaps;
        //}
        public string GetGraphPath()
        {
            return _graphPath;
        }
    }
}
