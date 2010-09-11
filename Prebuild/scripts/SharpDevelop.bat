@rem Generates a combine (.cmbx) and a set of project files (.prjx) 
@rem for SharpDevelop (http://icsharpcode.net/OpenSource/SD/Default.aspx)
cd ..
Prebuild.exe /target sharpdev /file prebuild.xml /build NET_1_1 /pause
