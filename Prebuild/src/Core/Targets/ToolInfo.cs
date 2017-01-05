using System;
using System.Collections.Generic;
using System.Text;

namespace Prebuild.Core.Targets
{
    /// <summary>
    ///
    /// </summary>
    public struct ToolInfo
    {
        string name;
        string guid;
        string fileExtension;
        string xmlTag;
        string importProject;

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }

        /// <summary>
        /// Gets or sets the GUID.
        /// </summary>
        /// <value>The GUID.</value>
        public string Guid
        {
            get
            {
                return guid;
            }
            set
            {
                guid = value;
            }
        }

        /// <summary>
        /// Gets or sets the file extension.
        /// </summary>
        /// <value>The file extension.</value>
        public string FileExtension
        {
            get
            {
                return fileExtension;
            }
            set
            {
                fileExtension = value;
            }
        }
        public string LanguageExtension
        {
            get
            {
                switch (this.Name)
                {
                    case "C#":
                        return ".cs";
                    case "VisualBasic":
                        return ".vb";
                    case "Boo":
                        return ".boo";
                    default:
                        return ".cs";
                }
            }
        }
        /// <summary>
        /// Gets or sets the XML tag.
        /// </summary>
        /// <value>The XML tag.</value>
        public string XmlTag
        {
            get
            {
                return xmlTag;
            }
            set
            {
                xmlTag = value;
            }
        }

        /// <summary>
        /// Gets or sets the import project property.
        /// </summary>
        /// <value>The ImportProject tag.</value>
        public string ImportProject
        {
            get
            {
                return importProject;
            }
            set
            {
                importProject = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolInfo"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="guid">The GUID.</param>
        /// <param name="fileExtension">The file extension.</param>
        /// <param name="xml">The XML.</param>
        /// <param name="importProject">The import project.</param>
        public ToolInfo(string name, string guid, string fileExtension, string xml, string importProject)
        {
            this.name = name;
            this.guid = guid;
            this.fileExtension = fileExtension;
            this.xmlTag = xml;
            this.importProject = importProject;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolInfo"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="guid">The GUID.</param>
        /// <param name="fileExtension">The file extension.</param>
        /// <param name="xml">The XML.</param>
        public ToolInfo(string name, string guid, string fileExtension, string xml)
        {
            this.name = name;
            this.guid = guid;
            this.fileExtension = fileExtension;
            this.xmlTag = xml;
            this.importProject = "$(MSBuildBinPath)\\Microsoft." + xml + ".Targets";
        }

        /// <summary>
        /// Equals operator
        /// </summary>
        /// <param name="obj">ToolInfo to compare</param>
        /// <returns>true if toolInfos are equal</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }
            if (obj.GetType() != typeof(ToolInfo))
                return false;

            ToolInfo c = (ToolInfo)obj;
            return ((this.name == c.name) && (this.guid == c.guid) && (this.fileExtension == c.fileExtension) && (this.importProject == c.importProject));
        }

        /// <summary>
        /// Equals operator
        /// </summary>
        /// <param name="c1">ToolInfo to compare</param>
        /// <param name="c2">ToolInfo to compare</param>
        /// <returns>True if toolInfos are equal</returns>
        public static bool operator ==(ToolInfo c1, ToolInfo c2)
        {
            return ((c1.name == c2.name) && (c1.guid == c2.guid) && (c1.fileExtension == c2.fileExtension) && (c1.importProject == c2.importProject) && (c1.xmlTag == c2.xmlTag));
        }

        /// <summary>
        /// Not equals operator
        /// </summary>
        /// <param name="c1">ToolInfo to compare</param>
        /// <param name="c2">ToolInfo to compare</param>
        /// <returns>True if toolInfos are not equal</returns>
        public static bool operator !=(ToolInfo c1, ToolInfo c2)
        {
            return !(c1 == c2);
        }

        /// <summary>
        /// Hash Code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return name.GetHashCode() ^ guid.GetHashCode() ^ this.fileExtension.GetHashCode() ^ this.importProject.GetHashCode() ^ this.xmlTag.GetHashCode();

        }
    }
}
