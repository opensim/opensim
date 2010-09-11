using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using Prebuild.Core.Attributes;
using Prebuild.Core.Interfaces;
using Prebuild.Core.Utilities;

namespace Prebuild.Core.Nodes
{
    [DataNode("DatabaseProject")]
    public class DatabaseProjectNode : DataNode
    {
        string name;
        string path;
        string fullpath;
        Guid guid = Guid.NewGuid();
        readonly List<AuthorNode> authors = new List<AuthorNode>();
        readonly List<DatabaseReferenceNode> references = new List<DatabaseReferenceNode>();

        public Guid Guid
        {
            get { return guid; }
        }

        public string Name
        {
            get { return name; }
        }

        public string Path
        {
            get { return path; }
        }

        public string FullPath
        {
            get { return fullpath; }
        }

        public IEnumerable<DatabaseReferenceNode> References
        {
            get { return references; }
        }

        public override void Parse(XmlNode node)
        {
            name = Helper.AttributeValue(node, "name", name);
            path = Helper.AttributeValue(node, "path", name);

            try
            {
                fullpath = Helper.ResolvePath(path);
            }
            catch
            {
                throw new WarningException("Could not resolve Solution path: {0}", path);
            }

            Kernel.Instance.CurrentWorkingDirectory.Push();

            try
            {
                Helper.SetCurrentDir(fullpath);

                if (node == null)
                {
                    throw new ArgumentNullException("node");
                }

                foreach (XmlNode child in node.ChildNodes)
                {
                    IDataNode dataNode = Kernel.Instance.ParseNode(child, this);

                    if (dataNode == null)
                        continue;

                    if (dataNode is AuthorNode)
                        authors.Add((AuthorNode)dataNode);
                    else if (dataNode is DatabaseReferenceNode)
                        references.Add((DatabaseReferenceNode)dataNode);
                }
            }
            finally
            {
                Kernel.Instance.CurrentWorkingDirectory.Pop();
            }

            base.Parse(node);
        }
    }
}
