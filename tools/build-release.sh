msbuild /t:Restore
msbuild /t:Build  /p:AllowUnsafeBlocks=true /p:Configuration=Release
