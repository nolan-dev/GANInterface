apt-get update
apt-get -y install git
pip3 install -r requirements.txt
git clone https://github.com/NVlabs/stylegan2.git
cd stylegan2
git checkout 4874628c7dfffaae01f89558c476842b475f54d5
cp ../run_conversion.py .
python run_conversion.py ../$1
git apply ../patch.diff
python run_training.py
freeze_graph --input_saved_model_dir=saved_graph --output_graph=out.pb --output_node_names=Gs/G_synthesis/output
cp out.pb ../
cd ..
python add_data_dir.py stylegan2/resolution.txt out.pb
rm -rf stylegan2
