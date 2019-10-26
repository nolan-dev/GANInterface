import sys
import numpy as np
import pandas as pd
import os
import argparse
from cv2 import resize
# Can be called from tool to set the spatial locations to change automatically based on
# feature maps that get written to files the 'dir' argument.  For example, the script can automatically
# detect where the mouth is in order to open or close it.
# example: spatial_map.py --threshold=10 --resize=0.5,2,2x1,23,412 in the tool does the following:
#  1. Writes the feature maps 1, 23, and 412 for layer 2 to a directory
#  2. Calls this script with that directory, along with --threshold=10 --resize=0.25 as parameters
#  3. This script combines the feature maps (giving 2x the weight to feature map 1), and for each
#     location above 10 that location is set to 1 for a map with 0.5x the input resolution (for layer 1 instead of 2)
# Using a python script for stuff like this is slower when run but faster to implement
if __name__ == "__main__":
    parser = argparse.ArgumentParser() # output resolution, average threshold
    parser.add_argument("dir", type=str)
    parser.add_argument("--resize_factor", type=float, default=None,
        help="resize spatial map by this factor")
    parser.add_argument("--threshold", type=float, default=25.0, 
        help="include in spatial map if average is above this")
    args = parser.parse_args()
    fmaps = [os.path.join(args.dir, f) for f in os.listdir(args.dir)]
    data = None
    for fmap_path in fmaps:
        if data is not None:
            data += pd.read_csv(fmap_path, delimiter=',', header=None)
        else:
            data = pd.read_csv(fmap_path, delimiter=',', header=None)
    data = data / len(fmaps)
    fmap_values = data.values
    fmap_values[fmap_values <= args.threshold] = 0.0
    fmap_values[fmap_values > args.threshold] = 1.0
    if args.resize_factor is not None:
        new_size = np.array([fmap_values.shape[1], fmap_values.shape[0]])*args.resize_factor
        fmap_values = resize(fmap_values, tuple(new_size.astype(np.int32)))
    np.savetxt(os.path.join(args.dir, 'spatial_map.csv'), fmap_values, delimiter=',')