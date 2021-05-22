#!/bin/sh
cd bin
mono --aot=mcpu=native,bind-to-runtime-version -O=all Nini.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all DotNetOpen*.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all Ionic.Zip.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all Newtonsoft.Json.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all C5.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all CSJ2K.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all Npgsql.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all RestSharp.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all Mono*.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all MySql*.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all OpenMetaverse*.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all OpenSim*.dll
mono --aot=mcpu=native,bind-to-runtime-version -O=all OpenSim*.exe
mono --aot=mcpu=native,bind-to-runtime-version -O=all Robust*.exe
cd ..