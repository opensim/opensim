#region	BSD	License
/*
Copyright (c) 2004-2005	Matthew	Holmes (matthew@wildfiregames.com),	Dan	Moorehead (dan05a@gmail.com)

Redistribution and use in source and binary	forms, with	or without modification, are permitted
provided that the following	conditions are met:

* Redistributions of source	code must retain the above copyright notice, this list of conditions
  and the following	disclaimer.
* Redistributions in binary	form must reproduce	the	above copyright	notice,	this list of conditions
  and the following	disclaimer in the documentation	and/or other materials provided	with the
  distribution.
* The name of the author may not be	used to	endorse	or promote products	derived	from this software
  without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR	``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
BUT	NOT	LIMITED	TO,	THE	IMPLIED	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A	PARTICULAR PURPOSE
ARE	DISCLAIMED.	IN NO EVENT	SHALL THE AUTHOR BE	LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL	DAMAGES	(INCLUDING,	BUT	NOT	LIMITED	TO,	PROCUREMENT	OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA,	OR PROFITS;	OR BUSINESS	INTERRUPTION) HOWEVER CAUSED AND ON	ANY	THEORY
OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR	TORT (INCLUDING	NEGLIGENCE OR OTHERWISE) ARISING
IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,	EVEN IF	ADVISED	OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#endregion

#region	CVS	Information
/*
 * $Source$
 * $Author: jendave $
 * $Date: 2006-09-20 03:42:51 -0400 (Wed, 20 Sep 2006) $
 * $Revision: 164 $
 */
#endregion

using System;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Nodes;

#if	(DEBUG && _DEBUG_TARGET)
namespace Prebuild.Core.Targets
{
    [Target("debug")]
    public class DebugTarget : ITarget
    {
#region	Fields

        private	Kernel m_Kernel	= null;

#endregion

#region	ITarget	Members

        public void	Write()
        {
            foreach(SolutionNode s in m_Kernel.Solutions)
            {
                Console.WriteLine("Solution	[ {0}, {1} ]", s.Name, s.Path);
                foreach(string file	in s.Files)
{
                    Console.WriteLine("\tFile [	{0}	]",	file);
}

                foreach(ProjectNode	proj in	s.Projects)
                {
                    Console.WriteLine("\tProject [ {0},	{1}. {2} ]", proj.Name,	proj.Path, proj.Language);
                    foreach(string file	in proj.Files)
                        Console.WriteLine("\t\tFile	[ {0} ]", file);
                }
            }
        }

        public void	Clean()
        {
            Console.WriteLine("Not implemented");
        }

        public string Name
        {
            get
            {
                return "debug";
            }
        }

        public Kernel Kernel
        {
            get
            {
                return m_Kernel;
            }
            set
            {
                m_Kernel = value;
            }
        }

#endregion
    }
}
#endif
