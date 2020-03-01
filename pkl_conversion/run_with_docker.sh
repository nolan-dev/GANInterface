docker run --gpus all -v ${PWD}:/tmp/work tensorflow/tensorflow:1.15.0-gpu-py3 /bin/bash -c "cd /tmp/work; ./download_and_patch.sh $1 $2"
