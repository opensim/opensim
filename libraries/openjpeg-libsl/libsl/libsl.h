

#ifndef LIBSL_H
#define LIBSL_H


struct LibslImage
{
	unsigned char* encoded;
	int length;

	unsigned char* decoded;
	int width;
	int height;
	int components;
};

#ifdef WIN32
#define DLLEXPORT extern "C" __declspec(dllexport)
#else
#define DLLEXPORT extern "C"
#endif

// uncompresed images are raw RGBA 8bit/channel
DLLEXPORT bool LibslEncode(LibslImage* image, bool lossless);
DLLEXPORT bool LibslDecode(LibslImage* image);
DLLEXPORT bool LibslAllocEncoded(LibslImage* image);
DLLEXPORT bool LibslAllocDecoded(LibslImage* image);
DLLEXPORT void LibslFree(LibslImage* image);


#endif
