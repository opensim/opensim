/*
 * WARNING!: this class is not in the original BulletX
 * By the way it's based on the Bullet btHeightfieldTerrainShape:
 * http://www.continuousphysics.com/Bullet/BulletFull/classbtHeightfieldTerrainShape.html
 *****************************************************************************************
 * 3RD PARTY LICENSE. The next it's the original 3rd party lincense of Bullet:
 * ----------------------------------------------------------------------------
Bullet Continuous Collision Detection and Physics Library
Copyright (c) 2003-2006 Erwin Coumans  http://continuousphysics.com/Bullet/

This software is provided 'as-is', without any express or implied warranty.
In no event will the authors be held liable for any damages arising from the use of this software.
Permission is granted to anyone to use this software for any purpose, 
including commercial applications, and to alter it and redistribute it freely, 
subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
 * ------------------------------------------------------------------------------
*/
using System;
using System.Collections.Generic;
using System.Text;
using MonoXnaCompactMaths;

namespace XnaDevRu.BulletX
{
    public class HeightfieldTerrainShape : ConcaveShape
    {
        private Vector3 _localAabbMin;
        private Vector3 _localAabbMax;
        private Vector3 _localScaling = new Vector3(1f,1f,1f);
        private int _width;
        private int _length;
        private float[] _heightfieldData;
        private float _maxHeight;
        private int _upAxis;
        private bool _useFloatData;
        private bool _flipQuadEdges;
        private bool _useDiamondSubdivision = false;
        private float _defaultCollisionMargin = 0.6f;

        public HeightfieldTerrainShape(int width, int length, float[] heightfieldData, float maxHeight, 
            int upAxis, bool useFloatData, bool flipQuadEdges)
        {
            _width = width;
            _length = length;
            _heightfieldData = heightfieldData;
            _maxHeight = maxHeight;
            _upAxis = upAxis;
            _useFloatData = useFloatData;
            _flipQuadEdges = flipQuadEdges;
            this.Margin = _defaultCollisionMargin;

            float quantizationMargin = 1f;

            //enlarge the AABB to avoid division by zero when initializing the quantization value
            Vector3 clampValue = new Vector3(quantizationMargin, quantizationMargin, quantizationMargin);
            Vector3 halfExtents = new Vector3(0, 0, 0);

            switch (_upAxis)
            {
                case 0:
                    halfExtents.X = _maxHeight;
                    halfExtents.Y = _width;
                    halfExtents.Z = _length;
                    break;
                case 1:
                    halfExtents.X = _width;
                    halfExtents.Y = _maxHeight;
                    halfExtents.Z = _length;
                    break;
                case 2:
                    halfExtents.X = _width;
                    halfExtents.Y = _length;
                    halfExtents.Z = _maxHeight;
                    break;
                default:
                    //need to get valid _upAxis
                    //btAssert(0);
                    throw new Exception("HeightfieldTerrainShape: need to get valid _upAxis");
            }

            halfExtents *= 0.5f;

            _localAabbMin = -halfExtents - clampValue;
            _localAabbMax = halfExtents + clampValue;
            //Vector3 aabbSize = new Vector3();
            //aabbSize = m_localAabbMax - m_localAabbMin;

        }

        protected Vector3 LocalAabbMin 
        { get { return _localAabbMin; } set { _localAabbMin = value; } }
        protected Vector3 LocalAabbMax 
        { get { return _localAabbMax; } set { _localAabbMax = value; } }
        public override string Name
        {
            get
            {
                return "HeightfieldTerrain";
            }
        }
        public override Vector3 LocalScaling
        {
            get
            {
                return _localScaling;
            }
            set
            {
                _localScaling = value;
            }
        }
        public override float Margin
        {
            get
            {
                return base.Margin;
            }
            set
            {
                base.Margin = value;
            }
        }
        public override BroadphaseNativeTypes ShapeType
        {
            get { return BroadphaseNativeTypes.Terrain; }
        }
        public Vector3 HalfExtents
        {
            get
            {
                Vector3 halfExtents = new Vector3();
                switch (_upAxis)
                {
                    case 0:
                        halfExtents.X = 2f;//_maxHeight;
                        halfExtents.Y = _width;
                        halfExtents.Z = _length;
                        break;
                    case 1:
                        halfExtents.X = _width;
                        halfExtents.Y = 2f;// _maxHeight;
                        halfExtents.Z = _length;
                        break;
                    case 2:
                        halfExtents.X = _width;
                        halfExtents.Y = _length;
                        halfExtents.Z = 2f;// _maxHeight;
                        break;
                    default:
                        //need to get valid m_upAxis
                        //btAssert(0);
                        throw new Exception("HeightfieldTerrainShape: need to get valid _upAxis");
                    //break;
                }
                halfExtents *= 0.5f;
                return halfExtents;
            }
        }

        public override void ProcessAllTriangles(ITriangleCallback callback, Vector3 aabbMin, Vector3 aabbMax)
        {
            //(void)callback;
            //(void)aabbMax;
            //(void)aabbMin;

	        //quantize the aabbMin and aabbMax, and adjust the start/end ranges

	        int[] quantizedAabbMin = new int[3];
	        int[] quantizedAabbMax = new int[3];

	        Vector3	localAabbMin = aabbMin * new Vector3(1f/_localScaling.X,1f/_localScaling.Y,1f/_localScaling.Z );
	        Vector3	localAabbMax = aabbMax * new Vector3(1f/_localScaling.X,1f/_localScaling.Y,1f/_localScaling.Z);
        	
	        quantizeWithClamp(ref quantizedAabbMin, localAabbMin);
	        quantizeWithClamp(ref quantizedAabbMax, localAabbMax);
        	
        	

	        int startX=0;
	        int endX=_width-1;
	        int startJ=0;
	        int endJ=_length-1;

	        switch(_upAxis)
	        {
    	        case 0:
			        quantizedAabbMin[1]+=_width/2-1;
			        quantizedAabbMax[1]+=_width/2+1;
			        quantizedAabbMin[2]+=_length/2-1;
			        quantizedAabbMax[2]+=_length/2+1;

			        if (quantizedAabbMin[1]>startX)
				        startX = quantizedAabbMin[1];
			        if (quantizedAabbMax[1]<endX)
				        endX = quantizedAabbMax[1];
			        if (quantizedAabbMin[2]>startJ)
				        startJ = quantizedAabbMin[2];
			        if (quantizedAabbMax[2]<endJ)
				        endJ = quantizedAabbMax[2];
			        break;
    	        case 1:
			        quantizedAabbMin[0]+=_width/2-1;
			        quantizedAabbMax[0]+=_width/2+1;
			        quantizedAabbMin[2]+=_length/2-1;
			        quantizedAabbMax[2]+=_length/2+1;

			        if (quantizedAabbMin[0]>startX)
				        startX = quantizedAabbMin[0];
			        if (quantizedAabbMax[0]<endX)
				        endX = quantizedAabbMax[0];
			        if (quantizedAabbMin[2]>startJ)
				        startJ = quantizedAabbMin[2];
			        if (quantizedAabbMax[2]<endJ)
				        endJ = quantizedAabbMax[2];
			        break;
    	        case 2:
			        quantizedAabbMin[0]+=_width/2-1;
			        quantizedAabbMax[0]+=_width/2+1;
			        quantizedAabbMin[1]+=_length/2-1;
			        quantizedAabbMax[1]+=_length/2+1;

			        if (quantizedAabbMin[0]>startX)
				        startX = quantizedAabbMin[0];
			        if (quantizedAabbMax[0]<endX)
				        endX = quantizedAabbMax[0];
			        if (quantizedAabbMin[1]>startJ)
				        startJ = quantizedAabbMin[1];
			        if (quantizedAabbMax[1]<endJ)
				        endJ = quantizedAabbMax[1];
			        break;
                default:
        		    //need to get valid m_upAxis
                    throw new Exception("HeightfieldTerrainShape: need to get valid _upAxis");
                    //break;
	        }

	        for(int j=startJ; j<endJ; j++)
	        {
		        for(int x=startX; x<endX; x++)
		        {
			        Vector3[] vertices = new Vector3[3];
                    //if (m_flipQuadEdges || (m_useDiamondSubdivision && ((j + x) & 1)))
			        if (_flipQuadEdges || (_useDiamondSubdivision && (((j + x) & 1) > 0)))
			        {
                        //first triangle
                        getVertex(x,j,ref vertices[0]);
                        getVertex(x+1,j,ref vertices[1]);
                        getVertex(x+1,j+1,ref vertices[2]);
                        //callback->processTriangle(vertices,x,j);
                        callback.ProcessTriangle(vertices,x,j);

                        //second triangle
                        getVertex(x,j,ref vertices[0]);
                        getVertex(x+1,j+1,ref vertices[1]);
                        getVertex(x,j+1,ref vertices[2]);
                        //callback->processTriangle(vertices,x,j);
                        callback.ProcessTriangle(vertices, x, j);
			        }
                    else
			        {
                        //first triangle
                        getVertex(x,j,ref vertices[0]);
                        getVertex(x,j+1,ref vertices[1]);
                        getVertex(x+1,j,ref vertices[2]);
                        //callback->processTriangle(vertices,x,j);
                        callback.ProcessTriangle(vertices,x,j);

                        //second triangle
                        getVertex(x+1,j,ref vertices[0]);
                        getVertex(x,j+1,ref vertices[1]);
                        getVertex(x+1,j+1,ref vertices[2]);
                        //callback->processTriangle(vertices,x,j);
                        callback.ProcessTriangle(vertices,x,j);
                    }
		        }
	        }
        }
        public override void GetAabb(Matrix t, out Vector3 aabbMin, out Vector3 aabbMax)
        {
            //aabbMin = new Vector3(-1e30f, -1e30f, -1e30f);
            //aabbMax = new Vector3(1e30f, 1e30f, 1e30f);

            Vector3 halfExtents = (_localAabbMax - _localAabbMin) * _localScaling * 0.5f;

            Vector3 center = t.Translation;
            Vector3 extent = new Vector3(Math.Abs(halfExtents.X), Math.Abs(halfExtents.Y), Math.Abs(halfExtents.Z));
            extent += new Vector3(Margin, Margin, Margin);

            aabbMin = center - extent;
            aabbMax = center + extent;
        }
        public override void CalculateLocalInertia(float mass, out Vector3 inertia)
        {
            //moving concave objects not supported
            inertia = new Vector3();
        }
        public float getHeightFieldValue(int x,int y)
        {
            float  val = 0f;
            if (_useFloatData)
	        {
                val = _heightfieldData[(y * _width) + x];
	        }
            else
	        {
		        //assume unsigned short int
                int heightFieldValue = (int)_heightfieldData[(y * _width) + x];
		        val = heightFieldValue * _maxHeight/65535f;
	        }
	        return val;
        }
        public void getVertex(int x,int y,ref Vector3 vertex)
        {
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= _width) x = _width - 1;
            if (y >= _length) y = _length - 1;
            float height = getHeightFieldValue(x,y);
            switch(_upAxis)
            {
    	        case 0:
                    vertex.X = height;
                    vertex.Y = (- _width/2 ) + x;
                    vertex.Z = (- _length/2 ) + y;
        			break;
                case 1:
                    vertex.X = (- _width/2 ) + x;
                    vertex.Y = height;
                    vertex.Z = (- _length/2 ) + y;
			        break;
            	case 2:
                    vertex.X = (- _width/2 ) + x;
                    vertex.Y = (- _length/2 ) + y;
                    vertex.Z = height;
        			break;
            	default:
        			//need to get valid m_upAxis
                    throw new Exception("HeightfieldTerrainShape: need to get valid _upAxis");
                    //break;
		    }
        	vertex *= _localScaling;
        }
        public void quantizeWithClamp(ref int[] _out,Vector3 point)
        {
	        Vector3 clampedPoint = point;
            MathHelper.SetMax(ref clampedPoint,_localAabbMin);
            MathHelper.SetMin(ref clampedPoint, _localAabbMax);
            Vector3 v = clampedPoint;

	        _out[0] = (int)(v.X);
	        _out[1] = (int)(v.Y);
	        _out[2] = (int)(v.Z);
        	//correct for
        }
    }
}
