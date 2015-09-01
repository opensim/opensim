/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Drawing;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.World.Warp3DMap
{
    public class Viewport
    {
        private const float DEG_TO_RAD = (float)Math.PI / 180f;
        private static readonly Vector3 UP_DIRECTION = Vector3.UnitZ;

        public Vector3 Position;
        public Vector3 LookDirection;
        public float FieldOfView;
        public float NearPlaneDistance;
        public float FarPlaneDistance;
        public int Width;
        public int Height;
        public bool Orthographic;
        public float OrthoWindowWidth;
        public float OrthoWindowHeight;

        public Viewport(Vector3 position, Vector3 lookDirection, float fieldOfView, float farPlaneDist, float nearPlaneDist, int width, int height)
        {
            // Perspective projection mode
            Position = position;
            LookDirection = lookDirection;
            FieldOfView = fieldOfView;
            FarPlaneDistance = farPlaneDist;
            NearPlaneDistance = nearPlaneDist;
            Width = width;
            Height = height;
        }

        public Viewport(Vector3 position, Vector3 lookDirection, float farPlaneDist, float nearPlaneDist, int width, int height, float orthoWindowWidth, float orthoWindowHeight)
        {
            // Orthographic projection mode
            Position = position;
            LookDirection = lookDirection;
            FarPlaneDistance = farPlaneDist;
            NearPlaneDistance = nearPlaneDist;
            Width = width;
            Height = height;
            OrthoWindowWidth = orthoWindowWidth;
            OrthoWindowHeight = orthoWindowHeight;
            Orthographic = true;
        }

        public Point VectorToScreen(Vector3 v)
        {
            Matrix4 m = GetWorldToViewportMatrix();
            Vector3 screenPoint = v * m;
            return new Point((int)screenPoint.X, (int)screenPoint.Y);
        }

        public Matrix4 GetWorldToViewportMatrix()
        {
            Matrix4 result = GetViewMatrix();
            result *= GetPerspectiveProjectionMatrix();
            result *= GetViewportMatrix();

            return result;
        }

        public Matrix4 GetViewMatrix()
        {
            Vector3 zAxis = -LookDirection;
            zAxis.Normalize();

            Vector3 xAxis = Vector3.Cross(UP_DIRECTION, zAxis);
            xAxis.Normalize();

            Vector3 yAxis = Vector3.Cross(zAxis, xAxis);

            Vector3 position = Position;
            float offsetX = -Vector3.Dot(xAxis, position);
            float offsetY = -Vector3.Dot(yAxis, position);
            float offsetZ = -Vector3.Dot(zAxis, position);

            return new Matrix4(
                xAxis.X, yAxis.X, zAxis.X, 0f,
                xAxis.Y, yAxis.Y, zAxis.Y, 0f,
                xAxis.Z, yAxis.Z, zAxis.Z, 0f,
                offsetX, offsetY, offsetZ, 1f);
        }

        public Matrix4 GetPerspectiveProjectionMatrix()
        {
            float aspectRatio = (float)Width / (float)Height;

            float hFoV = FieldOfView * DEG_TO_RAD;
            float zn = NearPlaneDistance;
            float zf = FarPlaneDistance;

            float xScale = 1f / (float)Math.Tan(hFoV / 2f);
            float yScale = aspectRatio * xScale;
            float m33 = (zf == double.PositiveInfinity) ? -1 : (zf / (zn - zf));
            float m43 = zn * m33;

            return new Matrix4(
                xScale, 0f, 0f, 0f,
                0f, yScale, 0f, 0f,
                0f, 0f, m33, -1f,
                0f, 0f, m43, 0f);
        }

        public Matrix4 GetOrthographicProjectionMatrix(float aspectRatio)
        {
            float w = Width;
            float h = Height;
            float zn = NearPlaneDistance;
            float zf = FarPlaneDistance;

            float m33 = 1 / (zn - zf);
            float m43 = zn * m33;

            return new Matrix4(
                2f / w, 0f, 0f, 0f,
                0f, 2f / h, 0f, 0f,
                0f, 0f, m33, 0f,
                0f, 0f, m43, 1f);
        }

        public Matrix4 GetViewportMatrix()
        {
            float scaleX = (float)Width * 0.5f;
            float scaleY = (float)Height * 0.5f;
            float offsetX = 0f + scaleX;
            float offsetY = 0f + scaleY;

            return new Matrix4(
                scaleX, 0f, 0f, 0f,
                0f, -scaleY, 0f, 0f,
                0f, 0f, 1f, 0f,
                offsetX, offsetY, 0f, 1f);
        }
    }
}
