// This is the main DLL file.

#include "libsl.h"
extern "C" {
#include "../libopenjpeg/openjpeg.h"
}

#define NULL 0


struct image_wrapper
{
	opj_image* image;

	image_wrapper(int numcmpts, opj_image_cmptparm_t* cmptparms, OPJ_COLOR_SPACE clrspc)
	{
		image = opj_image_create(numcmpts, cmptparms, clrspc);

		if (image == NULL)
			throw "opj_image_create failed";
	}

	image_wrapper(opj_dinfo* dinfo, opj_cio* cio)
	{
		image = opj_decode(dinfo,cio);

		if (image == NULL)
			throw "opj_decode failed";
	}

	~image_wrapper()
	{
		opj_image_destroy(image);
	}
};

struct cinfo_wrapper
{
	opj_cinfo* cinfo;

	cinfo_wrapper(CODEC_FORMAT format)
	{
		cinfo = opj_create_compress(format);

		if (cinfo == NULL)
			throw "opj_create_compress failed";
	}

	~cinfo_wrapper()
	{
		opj_destroy_compress(cinfo);
	}
};

struct dinfo_wrapper
{
	opj_dinfo* dinfo;

	dinfo_wrapper(CODEC_FORMAT format)
	{
		dinfo = opj_create_decompress(format);

		if (dinfo == NULL)
			throw "opj_create_decompress failed";
	}

	~dinfo_wrapper()
	{
		opj_destroy_decompress(dinfo);
	}
};

struct cio_wrapper
{
	opj_cio* cio;

	cio_wrapper(opj_cinfo* cinfo, unsigned char* buffer, int length)
	{
		cio = opj_cio_open((opj_common_ptr)cinfo,buffer,length);

		if (cio == NULL)
			throw "opj_cio_open failed";
	}

	cio_wrapper(opj_dinfo* dinfo, unsigned char* buffer, int length)
	{
		cio = opj_cio_open((opj_common_ptr)dinfo,buffer,length);

		if (cio == NULL)
			throw "opj_cio_open failed";
	}

	~cio_wrapper()
	{
		opj_cio_close(cio);
	}
};

bool LibslAllocEncoded(LibslImage* image)
{
	try
	{
		image->encoded = new unsigned char[image->length];
		image->decoded = 0;
	}

	catch (...)
	{
		return false;
	}

	return true;
}

bool LibslAllocDecoded(LibslImage* image)
{
	try
	{
		image->decoded = new unsigned char[image->width * image->height * image->components];
		image->encoded = 0;
	}

	catch (...)
	{
		return false;
	}

	return true;
}

void LibslFree(LibslImage* image)
{
	if (image->encoded != 0) delete image->encoded;
	if (image->decoded != 0) delete image->decoded;
}


bool LibslEncode(LibslImage* image, bool lossless)
{
	try
	{
		opj_cparameters cparameters;
		opj_set_default_encoder_parameters(&cparameters);
		cparameters.cp_disto_alloc = 1;

		if (lossless)
		{
			cparameters.tcp_numlayers = 1;
			cparameters.tcp_rates[0] = 0;
		}
		else
		{
			cparameters.tcp_numlayers = 6;
			cparameters.tcp_rates[0] = 1280;
			cparameters.tcp_rates[1] = 640;
			cparameters.tcp_rates[2] = 320;
			cparameters.tcp_rates[3] = 160;
			cparameters.tcp_rates[4] = 80;
			cparameters.tcp_rates[5] = 40;
		}

		cparameters.cp_comment = "LL_RGBHM";

		if (image->components > 5)
			return false;
		
		opj_image_comptparm comptparm[5];
		
		for (int i = 0; i < image->components; i++)
		{
			comptparm[i].bpp = 8;
			comptparm[i].prec = 8;
			comptparm[i].sgnd = 0;
			comptparm[i].dx = 1;
			comptparm[i].dy = 1;
			comptparm[i].x0 = 0;
			comptparm[i].y0 = 0;
			comptparm[i].w = image->width;
			comptparm[i].h = image->height;
		}

		image_wrapper cimage(image->components,comptparm,CLRSPC_SRGB);
		cimage.image->x0 = 0;
		cimage.image->y0 = 0;
		cimage.image->x1 = image->width;
		cimage.image->y1 = image->height;

		int dataIndex = 0, compIndex = 0, x, y, c;

		for (y = 0; y < image->height; y++)
		{
			for (x = 0; x < image->width; x++)
			{
				for (c = 0; c < image->components; c++)
					cimage.image->comps[c].data[compIndex] = image->decoded[dataIndex++];

				compIndex++;
			}
		}

		cinfo_wrapper cinfo(CODEC_J2K);
		opj_setup_encoder(cinfo.cinfo,&cparameters,cimage.image);
		cio_wrapper cio(cinfo.cinfo,NULL,0);

		if (!opj_encode(cinfo.cinfo,cio.cio,cimage.image,cparameters.index))
			return false;

		image->length = cio_tell(cio.cio);
		image->encoded = new unsigned char[image->length];
		
		for (int i = 0; i < image->length; i++)
			image->encoded[i] = cio.cio->buffer[i];
		
		return true;
	}

	catch (...)
	{
		return false;
	}
}

bool LibslDecode(LibslImage* image)
{
	opj_dparameters dparameters;
	
	try
	{
		opj_set_default_decoder_parameters(&dparameters);
		dinfo_wrapper dinfo(CODEC_J2K);
		opj_setup_decoder(dinfo.dinfo, &dparameters);
		cio_wrapper cio(dinfo.dinfo,image->encoded,image->length);
		image_wrapper cimage(dinfo.dinfo, cio.cio); // decode happens here

		int dataIndex = 0, compIndex = 0, x, y, c;
		image->width = cimage.image->x1 - cimage.image->x0;
		image->height = cimage.image->y1 - cimage.image->y0;
		image->components = cimage.image->numcomps;
		image->decoded = new unsigned char[image->width*image->height*image->components];
		
		for (y = 0; y < image->height; y++)
		{
			for (x = 0; x < image->width; x++)
			{
				for (c = 0; c < image->components; c++)
					image->decoded[dataIndex++] = cimage.image->comps[c].data[compIndex];

				compIndex++;
			}
		}

		return true;
	}

	catch (...)
	{
		return false;
	}
}
