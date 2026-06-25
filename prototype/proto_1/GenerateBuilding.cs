using PicoGK;
using System;
using System.Numerics;

public class BuildingGenerator
{
    public static Voxels GenerateBuilding(
        float baseArea, 
        int numberOfRooms, 
        int floors, 
        bool isBoxy, 
        bool isOrganic, 
        float roomHeight,
        float explosionOffset = 0f,
        float wallThickness = 0.3f,   
        float slabThickness = 0.4f,   
        bool bisectCrossSection = false) 
    {
        float sideLength = (float)Math.Sqrt(baseArea); 
        float totalFloorHeight = roomHeight + slabThickness;
        float depthOvershoot = wallThickness * 4.0f; 

        Mesh positiveMesh = new Mesh();
        Mesh negativeMesh = new Mesh();

        Random rand = new Random();
        int[] roomsPerFloorArr = new int[floors];
        for (int i = 0; i < numberOfRooms; i++)
        {
            roomsPerFloorArr[rand.Next(floors)]++;
        }

        for (int i = 0; i < floors; i++)
        {
            float zOffset = i * explosionOffset;
            float zMin = (i * totalFloorHeight) + zOffset;
            float zMax = zMin + totalFloorHeight;
            float floorLevel = zMin + slabThickness;
            float renderZMax = bisectCrossSection ? (floorLevel + (roomHeight / 2.0f)) : zMax;

            float hw = sideLength * 0.45f;
            float coreX = -hw; // Anchor shafts to the far left of the hallway

            // 1. Hallway (Central Spine)
            AddBoxToMesh(positiveMesh, new Vector3(-hw - wallThickness, -1.0f - wallThickness, zMin), new Vector3(hw + wallThickness, 1.0f + wallThickness, renderZMax));
            AddBoxToMesh(negativeMesh, new Vector3(-hw, -1.0f, floorLevel), new Vector3(hw, 1.0f, zMax));

            // 2. Elevator & Stairs Core (Left side, acting as specialized rooms)
            if (floors > 1)
            {
                // Elevator (Front Side)
                AddBoxToMesh(positiveMesh, new Vector3(coreX - wallThickness, 1.0f, zMin), new Vector3(coreX + 2.5f + wallThickness, 1.0f + wallThickness + 2.5f + wallThickness, renderZMax));
                AddBoxToMesh(negativeMesh, new Vector3(coreX, 1.0f + wallThickness, zMin), new Vector3(coreX + 2.5f, 1.0f + wallThickness + 2.5f, zMax));
                // Elevator Door punching through the new wall
                AddBoxToMesh(negativeMesh, new Vector3(coreX + 0.5f, 1.0f - (depthOvershoot/2), floorLevel), new Vector3(coreX + 1.5f, 1.0f + wallThickness + (depthOvershoot/2), floorLevel + 2.1f));

                // Stairs (Back Side)
                AddBoxToMesh(positiveMesh, new Vector3(coreX - wallThickness, -1.0f - wallThickness - 3.5f - wallThickness, zMin), new Vector3(coreX + 3.5f + wallThickness, -1.0f, renderZMax));
                AddBoxToMesh(negativeMesh, new Vector3(coreX, -1.0f - wallThickness - 3.5f, zMin), new Vector3(coreX + 3.5f, -1.0f - wallThickness, zMax));
                // Stairs Door punching through the new wall
                AddBoxToMesh(negativeMesh, new Vector3(coreX + 1.0f, -1.0f - wallThickness - (depthOvershoot/2), floorLevel), new Vector3(coreX + 2.0f, -1.0f + (depthOvershoot/2), floorLevel + 2.1f));
            }

            // 3. Room Distribution
            int roomsOnThisFloor = roomsPerFloorArr[i];
            
            // Offset starting positions so rooms don't overlap the shafts
            float currentXFront = floors > 1 ? coreX + 2.5f + wallThickness : coreX;
            float currentXBack = floors > 1 ? coreX + 3.5f + wallThickness : coreX;
            float roomWidth = 4.0f;
            float roomDepth = 4.0f;

            for (int r = 0; r < roomsOnThisFloor; r++)
            {
                if (r % 2 == 0)
                {
                    // Back Rooms (Shifted by -wallThickness)
                    AddBoxToMesh(positiveMesh, new Vector3(currentXBack - wallThickness, -1.0f - wallThickness - roomDepth - wallThickness, zMin), new Vector3(currentXBack + roomWidth + wallThickness, -1.0f, renderZMax));
                    AddBoxToMesh(negativeMesh, new Vector3(currentXBack, -1.0f - wallThickness - roomDepth, floorLevel), new Vector3(currentXBack + roomWidth, -1.0f - wallThickness, zMax));
                    
                    // Door
                    AddBoxToMesh(negativeMesh, new Vector3(currentXBack + 1.0f, -1.0f - wallThickness - (depthOvershoot/2), floorLevel), new Vector3(currentXBack + 2.0f, -1.0f + (depthOvershoot/2), floorLevel + 2.1f));
                    currentXBack += roomWidth + wallThickness;
                }
                else
                {
                    // Front Rooms (Shifted by +wallThickness)
                    AddBoxToMesh(positiveMesh, new Vector3(currentXFront - wallThickness, 1.0f, zMin), new Vector3(currentXFront + roomWidth + wallThickness, 1.0f + wallThickness + roomDepth + wallThickness, renderZMax));
                    AddBoxToMesh(negativeMesh, new Vector3(currentXFront, 1.0f + wallThickness, floorLevel), new Vector3(currentXFront + roomWidth, 1.0f + wallThickness + roomDepth, zMax));
                    
                    // Door
                    AddBoxToMesh(negativeMesh, new Vector3(currentXFront + 1.0f, 1.0f - (depthOvershoot/2), floorLevel), new Vector3(currentXFront + 2.0f, 1.0f + wallThickness + (depthOvershoot/2), floorLevel + 2.1f));
                    currentXFront += roomWidth + wallThickness;
                }
            }

            // 4. Ground Floor Main Entrance
            if (i == 0)
            {
                // Extra long cut to ensure it breaches the front of the building
                float doorOuterBoundary = 1.0f + wallThickness + roomDepth + wallThickness + 2.0f; 
                AddBoxToMesh(negativeMesh, new Vector3(-1.5f, 1.0f - (depthOvershoot / 2), floorLevel), new Vector3(1.5f, doorOuterBoundary, floorLevel + 3.0f));
            }
        }

        Voxels voxHouse = new Voxels(positiveMesh);
        Voxels voxVoids = new Voxels(negativeMesh);
        
        voxHouse.BoolSubtract(voxVoids);

        return voxHouse;
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

        m.nAddTriangle(v0, v2, v1); m.nAddTriangle(v0, v3, v2); 
        m.nAddTriangle(v4, v5, v6); m.nAddTriangle(v4, v6, v7); 
        m.nAddTriangle(v0, v1, v5); m.nAddTriangle(v0, v5, v4); 
        m.nAddTriangle(v3, v6, v2); m.nAddTriangle(v3, v7, v6); 
        m.nAddTriangle(v0, v4, v7); m.nAddTriangle(v0, v7, v3); 
        m.nAddTriangle(v1, v2, v6); m.nAddTriangle(v1, v6, v5); 
    }
}