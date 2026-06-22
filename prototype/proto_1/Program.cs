using System;
using System.Numerics;
using PicoGK;

namespace HouseGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize PicoGK with a 5mm voxel resolution for large architectural scales.
            try { Library.Go(5.0f, GenerateHouseTask); }
            catch (Exception e) { Console.WriteLine($"Execution failed: {e.Message}"); }
        }

        public static void GenerateHouseTask()
        {
            // --- Design Dimensions ---
            float width = 5000f;
            float depth = 4000f;
            float baseHeight = 200f;
            float roofHeight = 200f;
            float pillarHeight = 3000f;
            float pillarRadius = 150f;

            float zStart = baseHeight;
            float zEnd = baseHeight + pillarHeight;

            // --- 1. OPTIMIZED: Generate Base & Roof in a single pass ---
            Mesh houseMesh = new Mesh();
            
            // Build Base geometry
            AddBoxToMesh(houseMesh, new Vector3(0, 0, 0), new Vector3(width, depth, baseHeight));
            // Build Roof geometry
            AddBoxToMesh(houseMesh, new Vector3(0, 0, zEnd), new Vector3(width, depth, zEnd + roofHeight));

            // Voxelize both at once (Much faster than BoolAdd)
            Voxels voxHouse = new Voxels(houseMesh);

            // --- 2. Generate All Pillars ---
            Lattice latPillars = new Lattice();
            latPillars.AddBeam(new Vector3(0, 0, zStart), new Vector3(0, 0, zEnd), pillarRadius, pillarRadius, false);
            latPillars.AddBeam(new Vector3(width, 0, zStart), new Vector3(width, 0, zEnd), pillarRadius, pillarRadius, false);
            latPillars.AddBeam(new Vector3(0, depth, zStart), new Vector3(0, depth, zEnd), pillarRadius, pillarRadius, false);
            latPillars.AddBeam(new Vector3(width, depth, zStart), new Vector3(width, depth, zEnd), pillarRadius, pillarRadius, false);

            // Merge pillars with the main structure
            voxHouse.BoolAdd(new Voxels(latPillars));

            // --- 3. Render to Viewer ---
            Library.oViewer().Add(voxHouse);
        }


      public static void AddBoxToMesh(Mesh m, Vector3 min, Vector3 max)
        {
            int v0 = m.nAddVertex(new Vector3(min.X, min.Y, min.Z));
            int v1 = m.nAddVertex(new Vector3(max.X, min.Y, min.Z));
            int v2 = m.nAddVertex(new Vector3(max.X, max.Y, min.Z));
            int v3 = m.nAddVertex(new Vector3(min.X, max.Y, min.Z));
            int v4 = m.nAddVertex(new Vector3(min.X, min.Y, max.Z));
            int v5 = m.nAddVertex(new Vector3(max.X, min.Y, max.Z));
            int v6 = m.nAddVertex(new Vector3(max.X, max.Y, max.Z));
            int v7 = m.nAddVertex(new Vector3(min.X, max.Y, max.Z));

            // CCW Winding for outwardly facing normals
            m.nAddTriangle(v0, v2, v1); m.nAddTriangle(v0, v3, v2); // Bottom
            m.nAddTriangle(v4, v5, v6); m.nAddTriangle(v4, v6, v7); // Top
            m.nAddTriangle(v0, v1, v5); m.nAddTriangle(v0, v5, v4); // Front
            m.nAddTriangle(v3, v6, v2); m.nAddTriangle(v3, v7, v6); // Back
            m.nAddTriangle(v0, v4, v7); m.nAddTriangle(v0, v7, v3); // Left
            m.nAddTriangle(v1, v2, v6); m.nAddTriangle(v1, v6, v5); // Right
        }
    }
}