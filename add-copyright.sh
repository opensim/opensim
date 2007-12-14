#!/bin/sh

TEMPNAME="copyright_script_temp_file"
COPYNAME="copyright_script_copyright_notice"

URL="http://opensimulator.org/"
PROJNAME="OpenSim"

cat > ${COPYNAME} <<EOF
/*
* Copyright (c) Contributors, ${URL}
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the ${PROJNAME} Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS \`\`AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

EOF

has_bom() {
    CHARS=`hexdump -c $1 | head -n1 | cut -d\  -f1-4`
    BOMMARK="0000000 357 273 277"
    if [ "${CHARS}" == "${BOMMARK}" ]; then
	echo 1
    else
	echo 0
    fi
}

for f in `find . -iname "*.cs"`; do
    head -n2 $f | tail -n1 > ${TEMPNAME}
    grep -q Copyright ${TEMPNAME}
    if [ $? == 1 ]; then
	BOMSTATUS=`has_bom $f`
	rm ${TEMPNAME}

	if [ ${BOMSTATUS} == 1 ]; then
	    echo -ne \\0357\\0273\\0277 > ${TEMPNAME}
	fi

	cat ${COPYNAME} >> ${TEMPNAME}

	if [ ${BOMSTATUS} == 1 ]; then
	    cat $f | perl -p -e "s/^\\xEF\\xBB\\xBF//" >> ${TEMPNAME}
	else
	    cat $f >> ${TEMPNAME}
	fi

	mv ${TEMPNAME} $f
    fi
done

rm -f ${COPYNAME} ${TEMPNAME}
