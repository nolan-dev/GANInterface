// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"
#ifdef TENSORFLOWINTERFACE_EXPORTS
#define TENSORFLOWINTERFACE_API __declspec(dllexport)
#else
#define TENSORFLOWINTERFACE_API __declspec(dllimport)
#endif

__declspec(dllexport) int image_and_fmaps_from_latent(int num_inputs, float** inputs, int* input_num_dims, int** input_dims, const char** input_names,
	int num_outputs, float** outputs, int* output_num_dims, int** output_dims, const char** output_names, const char* out_path);
//__declspec(dllexport) int image_and_fmaps_from_latent(float* latent, const char* input_tensor_name, const char* out_path, const char* fmap_name, float* output_fmaps,
//	float* input_fmaps, int fmap_height, int fmap_width, BOOL use_input_fmaps);

__declspec(dllexport) int generate_intermediate_latent(float* out_intermediate_latent);

__declspec(dllexport) int initialize_global_session(const char* graph_path);

__declspec(dllexport) void close_global_session();


#endif //PCH_H
