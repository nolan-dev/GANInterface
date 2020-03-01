import hashlib
import argparse
import math
import os
if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("resolution_file", type=str)
    parser.add_argument("graph_file", type=str)

    args = parser.parse_args()
    with open(args.resolution_file, "r") as f:
        res_h, res_w =  [int(n) for n in f.read().split(",")]
    
    hasher = hashlib.md5()
    with open(args.graph_file, 'rb') as f:
        hasher.update(f.read())
    dir_name = "data_%s" % hasher.hexdigest()
    if not os.path.exists(dir_name):
        os.makedirs(dir_name)
    num_layers = int(math.log(res_w, 2) - 3)
    h_to_w_ratio = float(res_h)/float(res_w)
    
    layer_input_list = []
    layer_output_list = []
    for i in range(1, num_layers+1):
        width = 2 ** (i+2)
        height = int(h_to_w_ratio * width)
        if (width <= 64):
            fmaps = 512
        else:
            fmaps = int(512 // (width/64))
        layer_str = """        <layer%d>
            <name>Gs/G_synthesis/%dx%d/FmapInput</name>
            <dims>1, %d, %d, %d</dims>
        </layer%d>""" % (i, width, height, width, height, fmaps, i)
        layer_input_list.append(layer_str)
        layer_str = """        <layer%d>
            <name>Gs/G_synthesis/%dx%d/Conv1/Conv2D</name>
            <dims>1, %d, %d, %d</dims>
        </layer%d>""" % (i, width, height, width, height, fmaps, i)
        layer_output_list.append(layer_str)

    
    base_config_str = """<model>
    <resolution>
        <height>%d</height>
        <width>%d</width>
    </resolution>
    <input_latent_name></input_latent_name>
    <intermediate_latent_name>Gs/G_mapping/Dense7/mul_2</intermediate_latent_name>
    <output_name>Gs/G_synthesis/output</output_name>
    <input_layers>\n%s
    </input_layers>
    <output_layers>\n%s
    </output_layers>
</model>"""
    config_str = base_config_str % (res_h, res_w, 
        "\n".join(layer_input_list), 
        "\n".join(layer_output_list))
    with open(os.path.join(dir_name, "config.xml"), "w") as f:
        f.write(config_str)
