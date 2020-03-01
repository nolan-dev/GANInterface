# Copyright (c) 2019, NVIDIA Corporation. All rights reserved.
#
# This work is made available under the Nvidia Source Code License-NC.
# To view a copy of this license, visit
# https://nvlabs.github.io/stylegan2/license.html

import argparse
import numpy as np
import PIL.Image
#import dnnlib
#import dnnlib.tflib as tflib
import re
import sys
import dnnlib.tflib as tflib

import pretrained_networks
#----------------------------------------------------------------------------
import tensorflow as tf
def to_png(x, nchw_to_nhwc):
    return tf.image.encode_png(tflib.convert_images_to_uint8(x, nchw_to_nhwc=nchw_to_nhwc)[0], name="output")

def save_weights(network_pkl):
    print('Loading networks from "%s"...' % network_pkl)
    _G, _D, Gs = pretrained_networks.load_networks(network_pkl)
    noise_vars = [var for name, var in Gs.components.synthesis.vars.items() if name.startswith('noise')]
    with open("resolution.txt", "w") as f:
        f.write(",".join([str(i) for i in Gs.output_shape[-2:]]))

    #Gs_kwargs = dnnlib.EasyDict()
    #Gs_kwargs.output_transform = dict(func=to_png, nchw_to_nhwc=True) #dict(func=tflib.convert_images_to_uint8, nchw_to_nhwc=True)
    #Gs_kwargs.randomize_noise = False

    rnd = np.random.RandomState(0)
    z = rnd.randn(1, *Gs.input_shape[1:]) # [minibatch, component]
    tflib.set_vars({var: rnd.randn(*var.shape.as_list()) for var in noise_vars}) # [height, width]
    #try:
    #    images = Gs.run(z, None, **Gs_kwargs) # [minibatch, height, width, channel]
    #except TypeError:
    #    pass


    saver = tf.train.Saver(var_list=tf.trainable_variables())
    saver.save(sess=tf.get_default_session(), save_path="save_weights")

    """
    tf.saved_model.simple_save(tf.get_default_session(), "saved_graph",
                               inputs={t.name: t for t in Gs.input_templates},#tf.get_default_graph().get_tensor_by_name("G_synthesis/dlatents_in:0")},
                               outputs={"output": tf.get_default_graph().get_tensor_by_name("Gs/_Run/output:0")}) # {t.name: t for t in Gs.output_templates})
    """

#----------------------------------------------------------------------------
def main():
    parser = argparse.ArgumentParser(
        description='''StyleGAN2 generator.

Run 'python %(prog)s <subcommand> --help' for subcommand help.''',
        formatter_class=argparse.RawDescriptionHelpFormatter
    )


    parser.add_argument('network', help='Network pickle filename')

    args = parser.parse_args()

    save_weights(args.network)

#----------------------------------------------------------------------------

if __name__ == "__main__":
    main()

#----------------------------------------------------------------------------
