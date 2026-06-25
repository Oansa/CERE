using System;
using PicoGK;

namespace BuildingProject
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize PicoGK. 
            // The first parameter (e.g., 0.1f) is the voxel size in millimeters/meters. 
            // Smaller numbers mean higher resolution but longer computation times.
            Library.Go(0.1f, RunBuildingGeneration);
        }

        static void RunBuildingGeneration()
{
    Console.WriteLine("Generating building geometry... this might take a moment.");

   Voxels myBuilding = BuildingGenerator.GenerateBuilding(
        baseArea: 1000.0f,     
        numberOfRooms: 70,    
        floors: 6,            
        isBoxy: true,        
        isOrganic: false,      
        roomHeight: 3.0f,
        explosionOffset: 0.0f,
        wallThickness: 0.3f, 
        slabThickness: 0.4f,
        bisectCrossSection: false 
    );
    // Fixed API call using the approach in your attached script
    Library.oViewer().Add(myBuilding);

    Console.WriteLine("Generation complete. Check the PicoGK Viewer.");
}
    }
}