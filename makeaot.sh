#!/bin/sh
cd bin
mono --aot -O=all Nini.dll
mono --aot -O=all DotNetOpen*.dll
mono --aot -O=all Ionic.Zip.dll
mono --aot -O=all Newtonsoft.Json.*.dll
mono --aot -O=all C5.dll
mono --aot -O=all CSJ2K.dll
mono --aot -O=all Npgslq.dll
mono --aot -O=all RestSharp.dll
mono --aot -O=all Mono*.dll
mono --aot -O=all MuSql*.dll
mono --aot -O=all OpenMetaverse*.dll
mono --aot -O=all OpenSim*.dll
mono --aot -O=all OpenSim*.exe
mono --aot -O=all Robust*.exe
cd ..