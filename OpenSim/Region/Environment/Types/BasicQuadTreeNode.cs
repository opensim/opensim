using System.Collections.Generic;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Types
{
    public class BasicQuadTreeNode
    {
        private List<SceneObjectGroup> m_objects = new List<SceneObjectGroup>();
        private BasicQuadTreeNode[] m_childNodes = null;
        private BasicQuadTreeNode m_parent = null;

        private short m_leftX;
        private short m_leftY;
        private short m_width;
        private short m_height;
        //private int m_quadNumber;
        private string m_quadID;

        public BasicQuadTreeNode(BasicQuadTreeNode parent, string quadID, short leftX, short leftY, short width, short height)
        {
            m_parent = parent;
            m_quadID = quadID;
            m_leftX = leftX;
            m_leftY = leftY;
            m_width = width;
            m_height = height;
            // Console.WriteLine("creating quadtree node " + m_quadID);
        }

        public void AddObject(SceneObjectGroup obj)
        {
            if (m_childNodes == null)
            {
                if (!m_objects.Contains(obj))
                {
                    m_objects.Add(obj);
                }
            }
            else
            {
                if (obj.AbsolutePosition.X < (m_leftX + (m_width / 2)))
                {
                    if (obj.AbsolutePosition.Y < (m_leftY + (m_height / 2)))
                    {
                        m_childNodes[0].AddObject(obj);
                    }
                    else
                    {
                        m_childNodes[2].AddObject(obj);
                    }
                }
                else
                {
                    if (obj.AbsolutePosition.Y < (m_leftY + (m_height / 2)))
                    {
                        m_childNodes[1].AddObject(obj);
                    }
                    else
                    {
                        m_childNodes[3].AddObject(obj);
                    }
                }
            }
        }

        public void Subdivide()
        {
            if (m_childNodes == null)
            {
                m_childNodes = new BasicQuadTreeNode[4];
                m_childNodes[0] = new BasicQuadTreeNode(this, m_quadID + "1/", m_leftX, m_leftY, (short)(m_width / 2), (short)(m_height / 2));
                m_childNodes[1] = new BasicQuadTreeNode(this, m_quadID + "2/", (short)(m_leftX + (m_width / 2)), m_leftY, (short)(m_width / 2), (short)(m_height / 2));
                m_childNodes[2] = new BasicQuadTreeNode(this, m_quadID + "3/", m_leftX, (short)(m_leftY + (m_height / 2)), (short)(m_width / 2), (short)(m_height / 2));
                m_childNodes[3] = new BasicQuadTreeNode(this, m_quadID + "4/", (short)(m_leftX + (m_width / 2)), (short)(m_height + (m_height / 2)), (short)(m_width / 2), (short)(m_height / 2));
            }
            else
            {
                for (int i = 0; i < m_childNodes.Length; i++)
                {
                    m_childNodes[i].Subdivide();
                }
            }
        }

        public List<SceneObjectGroup> GetObjectsFrom(float x, float y)
        {
            if (m_childNodes == null)
            {
                return new List<SceneObjectGroup>(m_objects);
            }
            else
            {
                if (x < m_leftX + (m_width / 2))
                {
                    if (y < m_leftY + (m_height / 2))
                    {
                        return m_childNodes[0].GetObjectsFrom(x, y);
                    }
                    else
                    {
                        return m_childNodes[2].GetObjectsFrom(x, y);
                    }
                }
                else
                {
                    if (y < m_leftY + (m_height / 2))
                    {
                        return m_childNodes[1].GetObjectsFrom(x, y);
                    }
                    else
                    {
                        return m_childNodes[3].GetObjectsFrom(x, y);
                    }
                }
            }
        }

        public List<SceneObjectGroup> GetObjectsFrom(string nodeName)
        {
            if (nodeName == m_quadID)
            {
                return new List<SceneObjectGroup>(m_objects);
            }
            else if (m_childNodes != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    List<SceneObjectGroup> retVal;
                    retVal = m_childNodes[i].GetObjectsFrom(nodeName);
                    if (retVal != null)
                    {
                        return retVal;
                    }
                }
            }
            return null;
        }

        public string GetNodeID(float x, float y)
        {
            if (m_childNodes == null)
            {
                return m_quadID;
            }
            else
            {
                if (x < m_leftX + (m_width / 2))
                {
                    if (y < m_leftY + (m_height / 2))
                    {
                        return m_childNodes[0].GetNodeID(x, y);
                    }
                    else
                    {
                        return m_childNodes[2].GetNodeID(x, y);
                    }
                }
                else
                {
                    if (y < m_leftY + (m_height / 2))
                    {
                        return m_childNodes[1].GetNodeID(x, y);
                    }
                    else
                    {
                        return m_childNodes[3].GetNodeID(x, y);
                    }
                }
            }
        }

        public void Update()
        {
            if (m_childNodes != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    m_childNodes[i].Update();
                }
            }
            else
            {
                List<SceneObjectGroup> outBounds = new List<SceneObjectGroup>();
                foreach (SceneObjectGroup group in m_objects)
                {
                    if (((group.AbsolutePosition.X > m_leftX) && (group.AbsolutePosition.X < (m_leftX + m_width))) && ((group.AbsolutePosition.Y > m_leftY) && (group.AbsolutePosition.Y < (m_leftY + m_height))))
                    {
                        //still in bounds
                    }
                    else
                    {
                        outBounds.Add(group);
                    }
                }

                foreach (SceneObjectGroup removee in outBounds)
                {
                    m_objects.Remove(removee);
                    if (m_parent != null)
                    {
                        m_parent.PassUp(removee);
                    }
                }
                outBounds.Clear();
            }
        }

        public void PassUp(SceneObjectGroup group)
        {
            if (((group.AbsolutePosition.X > m_leftX) && (group.AbsolutePosition.X < (m_leftX + m_width))) && ((group.AbsolutePosition.Y > m_leftY) && (group.AbsolutePosition.Y < (m_leftY + m_height))))
            {
                AddObject(group);
            }
            else
            {
                if (m_parent != null)
                {
                    m_parent.PassUp(group);
                }
            }
        }

        public string[] GetNeighbours(string nodeName)
        {
            string[] retVal = new string[1];
            retVal[0] = "";
            return retVal;
        }
    }
}