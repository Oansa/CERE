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
            // --- Design Dimensions (Converted to mm) ---
            float width = 5000f;       // 5 meters
            float depth = 4000f;       // 4 meters
            float baseHeight = 200f;   // 20 cm
            float roofHeight = 200f;   // 20 cm
            float pillarHeight = 3000f;// 3 meters
            float pillarRadius = 150f; // 15 cm

            // --- 1. Generate the Base ---
            BBox3 boxBase = new BBox3(
                new Vector3(0, 0, 0),
                new Vector3(width, depth, baseHeight)
            );
            Voxels voxHouse = new Voxels(new Mesh(boxBase));

            // --- 2. Generate All Pillars ---
            Lattice latPillars = new Lattice();
            float zStart = baseHeight;
            float zEnd = baseHeight + pillarHeight;
            
            // Placing pillars precisely at the bounding box corners. 
            // (Note: To keep pillars strictly inside the floor plan, you would offset the X/Y by the pillarRadius)
            latPillars.AddBeam(new Vector3(0, 0, zStart), new Vector3(0, 0, zEnd), pillarRadius, pillarRadius, false);
            latPillars.AddBeam(new Vector3(width, 0, zStart), new Vector3(width, 0, zEnd), pillarRadius, pillarRadius, false);
            latPillars.AddBeam(new Vector3(0, depth, zStart), new Vector3(0, depth, zEnd), pillarRadius, pillarRadius, false);
            latPillars.AddBeam(new Vector3(width, depth, zStart), new Vector3(width, depth, zEnd), pillarRadius, pillarRadius, false);

            voxHouse.BoolAdd(new Voxels(latPillars));

            // --- 3. Generate the Roof ---
            BBox3 boxRoof = new BBox3(
                new Vector3(0, 0, zEnd),
                new Vector3(width, depth, zEnd + roofHeight)
            );
            voxHouse.BoolAdd(new Voxels(new Mesh(boxRoof)));

            // --- 4. Render to Viewer ---
            Library.oViewer().Add(voxHouse);
        }
    }
}