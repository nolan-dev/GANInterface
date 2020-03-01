DO NOT RUN IN A DIRECTORY THAT ALREADY CONTAINS A FOLDER CALLED "stylegan2"
Requires GanStudio v0.2.0+
Usage:
0. cd to the pkl_conversion dir and make sure run_with_docker.sh and download_and_patch.sh are executable (chmod +x [filename])
1. ./run_with_docker.sh pkl_file output_height,output_width
2. Wait a while (performance was not a priority for this)
3. Hopefully out.pb and data_[hash of out.pb] are written to the working directory
4. Rename the out.pb to correspond to the model's name
5. Copy the renamed pb file and the data directory to GanStudio's directory on a Windows machine
6. The tool should be able to load the new pb file on startup.  To use the quality/variation bar a new average latent will need to be generated, and by default there will be no attributes for the model.

Example:
./run_with_docker.sh stylegan2-ffhq-config-f.pkl 1024,1024
pkl file needs to have been generated with "config-f"

This is an EXTREMELY MESSY method of converting from the official stylegan2's model format of python pickles to the tensorflow graph format used by this tool. 
The reason this is so messy is because it requires modifying the stylegan2 graph after it has been saved as a pkl.  If you find a simpler method, please let me know.
It requires linux with docker 19.03+ installed alongside a gpu with nvidia drivers.  It has been tested with the default configuration of GCP's deep learning vm (https://cloud.google.com/deep-learning-vm)

Here's what it does:

1. Starts a docker container with Tensorflow 1.15 with the current directory mounted
2. In that container, execute download_and_patch.sh, which performs the next steps
3. Installs git and a couple python requirements in the docker container
4. Uses git to clone the official stylegan2 code
5. Checks out a specific, tested commit 
6. Load the pkl model and saves the weights
7. Applys a patch to stylegan2 source that:
	a. Modifies the graph to be compatible with the tool
	b. Adds a messy hack that causes run_training.py to save the graph instead of training
8. Runs run_training.py which, due to the patch, saves a graph instead of training
9. Freezes the graph to 'out.pb', the format required by the tool
10. Writes the configuration for the graph to a data dir, named like 'data_[md5 of graph]'
11. Moves the graph to the pkl_conversion dir and deletes the cloned stylegan2 directory

Tested with the church, cat and ffhq models from https://drive.google.com/drive/folders/1yanUI9m4b4PWzR0eurKNq6JR1Bbfbh6L