import sys
import numpy as np
import pandas as pd
import os
from PIL import Image
max_res_h = 1080
max_res_w = 500
if __name__ == "__main__":
    fmaps = [os.path.join(sys.argv[1], f) for f in os.listdir(sys.argv[1])]
    all_fmaps = []
    for fmap_path in fmaps:
        all_fmaps.append(pd.read_csv(fmap_path, delimiter=',', header=None).values)
    res_h, res_w = all_fmaps[0].shape
    image_columns = max_res_w // res_w
    image_rows = max_res_h // res_h
    for i in range(0, image_columns-(len(all_fmaps) % image_columns)):
        all_fmaps.append(np.zeros([res_h, res_w]))
    num_fmaps = len(all_fmaps)
    num_screens = 1 + (num_fmaps // (image_rows * image_columns))
    
    combined_images = None
    for row in range(0, num_fmaps // image_columns):
        current_row = np.concatenate(all_fmaps[row*image_columns:(row+1)*image_columns], axis=1)
        
        if combined_images is None:
            combined_images = current_row
        else:
            combined_images = np.concatenate([combined_images, current_row], axis=0)
    img = Image.fromarray((((combined_images - combined_images.min()) / (combined_images.max() - combined_images.min())) * 255).astype(np.uint8))
    img.save('fmaps.png')
    img.show()