using System;
using System.Collections.Generic;
using System.Numerics;
using PicoGK;

namespace BuildingProject
{
    // --- The Master Blueprint ---
    public class ProjectData
    {
        public string BuildingType { get; set; } = "Maisonette";
        public string DesignStyle { get; set; } = "Organic"; 
        public GlobalConstraintsData GlobalConstraints { get; set; } = new GlobalConstraintsData();
        public SpineData CirculationSpine { get; set; } = new SpineData();
        public List<FloorData> Floors { get; set; } = new List<FloorData>();
    }

    public class GlobalConstraintsData
    {
        public float MandatoryWallThickness_m { get; set; } = 0.3f;
        public float FloorSlabThickness_m { get; set; } = 0.4f;
        public float MinRoomHeight_m { get; set; } = 3.0f;
        public bool NaturalLightOptimization { get; set; } = true;
    }

    public class SpineData
    {
        public bool HasElevator { get; set; } = false;
        public float Width_m { get; set; } = 2.0f;
    }

    public class FloorData
    {
        public int Level { get; set; }
        public string Name { get; set; } = string.Empty;
        public float BaseElevation_m { get; set; }
        public float FloorHeight_m { get; set; } = 3.0f;
        public List<RoomData> Rooms { get; set; } = new List<RoomData>();
    }

    public class RoomData
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public float TargetArea_sqm { get; set; }
        public string ElevationModifier { get; set; } = "Standard"; 
        public FenestrationData Fenestration { get; set; } = new FenestrationData();
    }

    public class FenestrationData
    {
        public WindowData Windows { get; set; } = new WindowData();
    }

    public class WindowData
    {
        public int TargetCount { get; set; } = 0;
        public float Width_m { get; set; } = 1.0f;
        public float Height_m { get; set; } = 1.5f;
        public float SillHeight_m { get; set; } = 0.9f;
    }

    // --- Data Tracking Classes for Rebuilding & Scaling ---
    public class BlockData
    {
        public Vector3 Center;
        public Vector3 Size;
        public int Level;
        public string Primitive = string.Empty;
    }

    public class BridgeData
    {
        public Vector3 Start;
        public Vector3 End;
        public float Thickness;
        public bool IsCurvy;
    }

    // --- The Generator ---
    public class DataDrivenBuildingGenerator
    {
        public static Voxels GenerateBuilding(ProjectData project)
        {
            Mesh positiveMesh = new Mesh(); 
            Mesh negativeMesh = new Mesh(); 

            float wallThickness = project.GlobalConstraints.MandatoryWallThickness_m;
            float slabThickness = project.GlobalConstraints.FloorSlabThickness_m;
            float spineWidth = project.CirculationSpine.Width_m;
            float halfSpine = spineWidth / 2.0f;
            
            float standardRoomDepth = 4.0f; 
            float depthOvershoot = wallThickness * 4.0f; 

            for (int f = 0; f < project.Floors.Count; f++)
            {
                var floor = project.Floors[f];
                bool isTopFloor = (f == project.Floors.Count - 1);

                float zMin = floor.BaseElevation_m; 
                float floorLevel = zMin + slabThickness; 
                float ceilingLevel = floorLevel + floor.FloorHeight_m; 
                float zMax = ceilingLevel + slabThickness; 

                AddBoxToMesh(positiveMesh, 
                    new Vector3(-halfSpine - wallThickness, -10f, zMin), 
                    new Vector3(halfSpine + wallThickness, 10f, zMax)); 
                
                AddBoxToMesh(negativeMesh, 
                    new Vector3(-halfSpine, -10f, floorLevel), 
                    new Vector3(halfSpine, 10f, ceilingLevel));

                if (project.Floors.Count > 1)
                {
                    float coreDepth = 4.0f;
                    float coreYBase = -10f; 

                    AddBoxToMesh(positiveMesh, 
                        new Vector3(-halfSpine - wallThickness, coreYBase - coreDepth - wallThickness, zMin), 
                        new Vector3(halfSpine + wallThickness, coreYBase, zMax));

                    float voidZMax = isTopFloor ? ceilingLevel : zMax; 
                    
                    AddBoxToMesh(negativeMesh, 
                        new Vector3(-halfSpine, coreYBase - coreDepth, floorLevel), 
                        new Vector3(halfSpine, coreYBase, voidZMax));
                }

                float currentXFront = halfSpine + wallThickness;
                float currentXBack = -halfSpine - wallThickness;

                for (int r = 0; r < floor.Rooms.Count; r++)
                {
                    RoomData room = floor.Rooms[r];
                    
                    float roomWidth = room.TargetArea_sqm / standardRoomDepth;
                    
                    float roomFloorLevel = floorLevel;
                    if (room.ElevationModifier == "Sunken") roomFloorLevel -= 0.6f;
                    if (room.ElevationModifier == "Raised") roomFloorLevel += 0.6f;

                    bool isFront = (r % 2 == 0); 
                    
                    if (isFront)
                    {
                        Vector3 solidMin = new Vector3(currentXFront - wallThickness, -standardRoomDepth / 2 - wallThickness, zMin);
                        Vector3 solidMax = new Vector3(currentXFront + roomWidth + wallThickness, standardRoomDepth / 2 + wallThickness, zMax);
                        AddBoxToMesh(positiveMesh, solidMin, solidMax);

                        Vector3 voidMin = new Vector3(currentXFront, -standardRoomDepth / 2, roomFloorLevel);
                        Vector3 voidMax = new Vector3(currentXFront + roomWidth, standardRoomDepth / 2, ceilingLevel);
                        AddBoxToMesh(negativeMesh, voidMin, voidMax);

                        float doorCenterY = 0f; 
                        AddBoxToMesh(negativeMesh, 
                            new Vector3(currentXFront - depthOvershoot, doorCenterY - 0.5f, roomFloorLevel), 
                            new Vector3(currentXFront + depthOvershoot, doorCenterY + 0.5f, roomFloorLevel + 2.1f));

                        GenerateWindows(negativeMesh, room.Fenestration.Windows, 
                            currentXFront, currentXFront + roomWidth, 
                            standardRoomDepth / 2, true, roomFloorLevel, wallThickness);

                        currentXFront += roomWidth + wallThickness;
                    }
                    else
                    {
                        Vector3 solidMin = new Vector3(currentXBack - roomWidth - wallThickness, -standardRoomDepth / 2 - wallThickness, zMin);
                        Vector3 solidMax = new Vector3(currentXBack + wallThickness, standardRoomDepth / 2 + wallThickness, zMax);
                        AddBoxToMesh(positiveMesh, solidMin, solidMax);

                        Vector3 voidMin = new Vector3(currentXBack - roomWidth, -standardRoomDepth / 2, roomFloorLevel);
                        Vector3 voidMax = new Vector3(currentXBack, standardRoomDepth / 2, ceilingLevel);
                        AddBoxToMesh(negativeMesh, voidMin, voidMax);

                        float doorCenterY = 0f; 
                        AddBoxToMesh(negativeMesh, 
                            new Vector3(currentXBack - depthOvershoot, doorCenterY - 0.5f, roomFloorLevel), 
                            new Vector3(currentXBack + depthOvershoot, doorCenterY + 0.5f, roomFloorLevel + 2.1f));

                        GenerateWindows(negativeMesh, room.Fenestration.Windows, 
                            currentXBack - roomWidth, currentXBack, 
                            -standardRoomDepth / 2, false, roomFloorLevel, wallThickness);

                        currentXBack -= (roomWidth + wallThickness);
                    }
                }
            }

            Voxels voxHouse = new Voxels(positiveMesh);
            Voxels voxVoids = new Voxels(negativeMesh);
            
            voxHouse.BoolSubtract(voxVoids);

            if (project.DesignStyle == "Organic")
            {
                float r = wallThickness / 2.0f; 
                voxHouse.Offset(r);       
                voxHouse.Offset(-r * 2f); 
                voxHouse.Offset(r);       
            }

            return voxHouse;
        }

        private static void GenerateWindows(Mesh negativeMesh, WindowData winData, float startX, float endX, float exteriorY, bool isFront, float roomZ, float wallThickness)
        {
            if (winData.TargetCount <= 0) return;

            float roomWidth = endX - startX;
            float spacing = roomWidth / (winData.TargetCount + 1);

            for (int i = 1; i <= winData.TargetCount; i++)
            {
                float winCenterX = startX + (spacing * i);
                float winZBottom = roomZ + winData.SillHeight_m;
                float winZTop = winZBottom + winData.Height_m;

                float yMin = isFront ? exteriorY - wallThickness * 2f : exteriorY - wallThickness * 2f;
                float yMax = isFront ? exteriorY + wallThickness * 2f : exteriorY + wallThickness * 2f;

                AddBoxToMesh(negativeMesh, 
                    new Vector3(winCenterX - (winData.Width_m / 2f), yMin, winZBottom), 
                    new Vector3(winCenterX + (winData.Width_m / 2f), yMax, winZTop));
            }
        }

        // --- NEW: Primitive Geometry Helpers for varied massing ---

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

            m.nAddTriangle(v0, v3, v2); m.nAddTriangle(v0, v2, v1); 
            m.nAddTriangle(v4, v5, v6); m.nAddTriangle(v4, v6, v7); 
            m.nAddTriangle(v0, v1, v5); m.nAddTriangle(v0, v5, v4); 
            m.nAddTriangle(v3, v7, v6); m.nAddTriangle(v3, v6, v2); 
            m.nAddTriangle(v0, v4, v7); m.nAddTriangle(v0, v7, v3); 
            m.nAddTriangle(v1, v2, v6); m.nAddTriangle(v1, v6, v5); 
        }

        public static void AddCylinderToMesh(Mesh m, Vector3 center, float radius, float height)
        {
            int segments = 12;
            float zMin = center.Z - height / 2f;
            float zMax = center.Z + height / 2f;
            int centerBottom = m.nAddVertex(new Vector3(center.X, center.Y, zMin));
            int centerTop = m.nAddVertex(new Vector3(center.X, center.Y, zMax));
            
            int[] bottomVerts = new int[segments];
            int[] topVerts = new int[segments];
            
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (float)Math.PI * 2f / segments;
                float x = center.X + radius * (float)Math.Cos(angle);
                float y = center.Y + radius * (float)Math.Sin(angle);
                bottomVerts[i] = m.nAddVertex(new Vector3(x, y, zMin));
                topVerts[i] = m.nAddVertex(new Vector3(x, y, zMax));
            }
            
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                m.nAddTriangle(centerBottom, bottomVerts[next], bottomVerts[i]);
                m.nAddTriangle(centerTop, topVerts[i], topVerts[next]);
                m.nAddTriangle(bottomVerts[i], bottomVerts[next], topVerts[next]);
                m.nAddTriangle(bottomVerts[i], topVerts[next], topVerts[i]);
            }
        }

        public static void AddHexPrismToMesh(Mesh m, Vector3 center, float radius, float height)
        {
            int segments = 6;
            float zMin = center.Z - height / 2f;
            float zMax = center.Z + height / 2f;
            int centerBottom = m.nAddVertex(new Vector3(center.X, center.Y, zMin));
            int centerTop = m.nAddVertex(new Vector3(center.X, center.Y, zMax));
            
            int[] bottomVerts = new int[segments];
            int[] topVerts = new int[segments];
            
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (float)Math.PI * 2f / segments + (float)Math.PI / 6f;
                float x = center.X + radius * (float)Math.Cos(angle);
                float y = center.Y + radius * (float)Math.Sin(angle);
                bottomVerts[i] = m.nAddVertex(new Vector3(x, y, zMin));
                topVerts[i] = m.nAddVertex(new Vector3(x, y, zMax));
            }
            
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                m.nAddTriangle(centerBottom, bottomVerts[next], bottomVerts[i]);
                m.nAddTriangle(centerTop, topVerts[i], topVerts[next]);
                m.nAddTriangle(bottomVerts[i], bottomVerts[next], topVerts[next]);
                m.nAddTriangle(bottomVerts[i], topVerts[next], topVerts[i]);
            }
        }

        public static void AddConeToMesh(Mesh m, Vector3 center, float radius, float height)
        {
            int segments = 12;
            float zMin = center.Z - height / 2f;
            float zMax = center.Z + height / 2f;
            int centerBottom = m.nAddVertex(new Vector3(center.X, center.Y, zMin));
            int top = m.nAddVertex(new Vector3(center.X, center.Y, zMax));
            
            int[] bottomVerts = new int[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (float)Math.PI * 2f / segments;
                float x = center.X + radius * (float)Math.Cos(angle);
                float y = center.Y + radius * (float)Math.Sin(angle);
                bottomVerts[i] = m.nAddVertex(new Vector3(x, y, zMin));
            }
            
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                m.nAddTriangle(centerBottom, bottomVerts[next], bottomVerts[i]);
                m.nAddTriangle(bottomVerts[i], bottomVerts[next], top);
            }
        }

        public static void AddPyramidToMesh(Mesh m, Vector3 center, float radius, float height)
        {
            int segments = 4;
            float zMin = center.Z - height / 2f;
            float zMax = center.Z + height / 2f;
            int centerBottom = m.nAddVertex(new Vector3(center.X, center.Y, zMin));
            int top = m.nAddVertex(new Vector3(center.X, center.Y, zMax));
            
            int[] bottomVerts = new int[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (float)Math.PI * 2f / segments + (float)Math.PI / 4f;
                float x = center.X + radius * (float)Math.Cos(angle);
                float y = center.Y + radius * (float)Math.Sin(angle);
                bottomVerts[i] = m.nAddVertex(new Vector3(x, y, zMin));
            }
            
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                m.nAddTriangle(centerBottom, bottomVerts[next], bottomVerts[i]);
                m.nAddTriangle(bottomVerts[i], bottomVerts[next], top);
            }
        }

        public static void AddSphereToMesh(Mesh m, Vector3 center, float radius, bool hemisphere)
        {
            int latLines = 10;
            int lonLines = 16;
            int endLat = hemisphere ? latLines / 2 : latLines;
            
            int[,] verts = new int[latLines + 1, lonLines + 1];

            for (int lat = 0; lat <= endLat; lat++)
            {
                float theta = lat * (float)Math.PI / latLines;
                float sinTheta = (float)Math.Sin(theta);
                float cosTheta = (float)Math.Cos(theta);
                
                for (int lon = 0; lon <= lonLines; lon++)
                {
                    float phi = lon * 2f * (float)Math.PI / lonLines;
                    float sinPhi = (float)Math.Sin(phi);
                    float cosPhi = (float)Math.Cos(phi);
                    
                    float x = center.X + radius * sinTheta * cosPhi;
                    float y = center.Y + radius * sinTheta * sinPhi;
                    float z = center.Z + radius * cosTheta;
                    
                    verts[lat, lon] = m.nAddVertex(new Vector3(x, y, z));
                }
            }
            
            for (int lat = 0; lat < endLat; lat++)
            {
                for (int lon = 0; lon < lonLines; lon++)
                {
                    int v0 = verts[lat, lon];
                    int v1 = verts[lat + 1, lon];
                    int v2 = verts[lat + 1, lon + 1];
                    int v3 = verts[lat, lon + 1];
                    
                    m.nAddTriangle(v0, v2, v1);
                    m.nAddTriangle(v0, v3, v2);
                }
            }
            
            if (hemisphere)
            {
                int centerBottom = m.nAddVertex(center);
                for (int lon = 0; lon < lonLines; lon++)
                {
                    m.nAddTriangle(centerBottom, verts[endLat, lon + 1], verts[endLat, lon]);
                }
            }
        }

        public static void AddWedgeToMesh(Mesh m, Vector3 min, Vector3 max)
        {
            int v0 = m.nAddVertex(new Vector3(min.X, min.Y, min.Z));
            int v1 = m.nAddVertex(new Vector3(max.X, min.Y, min.Z));
            int v2 = m.nAddVertex(new Vector3(max.X, max.Y, min.Z));
            int v3 = m.nAddVertex(new Vector3(min.X, max.Y, min.Z));
            
            int v4 = m.nAddVertex(new Vector3(max.X, min.Y, max.Z));
            int v5 = m.nAddVertex(new Vector3(max.X, max.Y, max.Z));
            
            m.nAddTriangle(v0, v3, v2); m.nAddTriangle(v0, v2, v1); // Bottom
            m.nAddTriangle(v1, v2, v5); m.nAddTriangle(v1, v5, v4); // Back
            m.nAddTriangle(v0, v1, v4); // Right
            m.nAddTriangle(v3, v2, v5); // Left
            m.nAddTriangle(v0, v4, v5); m.nAddTriangle(v0, v5, v3); // Top Slope
        }

        public static void AddSpecificShapeToMesh(Mesh m, Vector3 center, Vector3 size, string shapeType)
        {
            float radius = Math.Min(size.X, size.Y) / 2f;
            float height = size.Z;

            switch (shapeType.ToLower())
            {
                case "cylinder": 
                    AddCylinderToMesh(m, center, radius, height); 
                    break;
                case "cone": 
                    AddConeToMesh(m, center, radius, height); 
                    break;
                case "sphere": 
                    AddSphereToMesh(m, center, radius, false); 
                    break;
                case "hemisphere": 
                    AddSphereToMesh(m, center - new Vector3(0, 0, height / 2f), radius, true); 
                    break;
                case "wedge": 
                    AddWedgeToMesh(m, center - size / 2f, center + size / 2f); 
                    break;
                case "hex_prism": 
                    AddHexPrismToMesh(m, center, radius, height); 
                    break;
                case "pyramid": 
                    AddPyramidToMesh(m, center, radius, height); 
                    break;
                case "box":
                default:
                    AddBoxToMesh(m, center - size / 2f, center + size / 2f); 
                    break;
            }
        }
        
        // Connects separated volumes with a straight bridge
        private static void AddBridgeToMesh(Mesh m, Vector3 start, Vector3 end, float thickness)
        {
            float halfT = thickness / 2f;
            float bridgeZ = Math.Min(start.Z, end.Z); 

            Vector3 min = new Vector3(
                Math.Min(start.X, end.X) - halfT,
                start.Y - halfT,
                bridgeZ - halfT
            );
            Vector3 max = new Vector3(
                Math.Max(start.X, end.X) + halfT,
                start.Y + halfT,
                bridgeZ + halfT
            );
            AddBoxToMesh(m, min, max);
        }

        // Connects separated volumes with a sinuous, curvy snake-like bridge
        private static void AddCurvyBridgeToMesh(Mesh m, Vector3 start, Vector3 end, float thickness)
        {
            int steps = 15;
            float radius = thickness / 2f;
            float dist = Vector3.Distance(start, end);
            float curveHeight = dist * 0.35f; // Defines how far the bridge arcs sideways

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = Vector3.Lerp(start, end, t);
                
                // Curve heavily on the Y axis to keep it perfectly flat while snaking back and forth
                p.Y += (float)Math.Sin(t * Math.PI) * curveHeight;
                
                AddSphereToMesh(m, p, radius, false);
            }
        }

        // ==============================================================================
        // --- METHOD: generate_inspiration_geometry ---
        // 
        // DESCRIPTION:
        // This is the core procedural blockout engine. It uses a 3D Random Walk / Face-Attachment 
        // algorithm to autonomously build large, conceptual architectural clusters. It calculates
        // an ideal bounding box based on area constraints, spawns an initial volume on Level 1, 
        // and then branches out piece by piece. Once the structural framework is established, 
        // it bakes the primitive shapes into a single watertight voxel mesh using PicoGK.
        //
        // OPTIONS:
        //  * shape: 
        //      - "cubes": Forces all generated primitives to be perfectly uniform cubes.
        //      - "sharp": Uses mixed primitives but avoids voxel smoothing, leaving sharp edges.
        //      - "organic": Applies aggressive dilate/erode smoothing to melt blocks together.
        //      - "fluid": Applies a mild organic smoothing logic for slightly rounded structures.
        //  * distribution: 
        //      - "compact": Forces blocks to clump tightly around the initial central core.
        //      - "linear": Forces blocks to build off the most recently placed block (sprawling).
        //      - "distributed": Completely separates blocks in space, connected only by splines.
        //      - "scatter": Standard random face-attachment.
        //      - "hybrid": A chaotic, randomized blend of all structural distribution types.
        //  * n_geometry: The absolute number of 3D primitive blocks to spawn.
        //  * footprint_area: The max target spread (in square meters) to constrain the X/Y bounds.
        //  * stack_limit: The maximum Z-axis height levels allowed (e.g., 2 = max 2 layers stacked).
        //                 Set to 1 to force a strictly horizontal, sprawling flat design.
        //  * allowed_primitives: Custom array string (e.g., ["box", "sphere", "pyramid"]).
        //
        // STRUCTURAL RULES:
        //  * First Layer Extrapolation: It forces the ground floor to hold the vast majority 
        //    (at least 60%) of the mass. Any block generated on Level 1 is structurally elongated 
        //    to ensure its base perfectly touches the ground plane (Z=0).
        //  * Level 2 Elongation: If the stack limit is > 3, blocks on Level 2 are also elongated 
        //    downward, acting as massive foundation pillars for towering architectures.
        // ==============================================================================
        public static (Voxels building, Voxels baseGeometry, float minX, float maxX, float minY, float maxY, List<BlockData> blocks, List<BridgeData> bridges, float baseVolSize, string shape) generate_inspiration_geometry(
            string shape, 
            string distribution, 
            int n_geometry, 
            float footprint_area, 
            int stack_limit = 3, 
            List<string> allowed_primitives = null)
        {
            shape = shape.ToLower();
            distribution = distribution.ToLower();

            if (allowed_primitives == null || allowed_primitives.Count == 0)
            {
                allowed_primitives = new List<string> { "box" };
            }

            Mesh bMesh = new Mesh();
            if (n_geometry <= 0) return (new Voxels(), new Voxels(), 0f, 0f, 0f, 0f, new List<BlockData>(), new List<BridgeData>(), 0f, shape);

            Random rand = new Random(); 

            float maxRadius = (float)Math.Sqrt(footprint_area) / 2.0f; 
            float baseVolSize = (float)Math.Sqrt(footprint_area) / (float)Math.Sqrt(n_geometry); 
            baseVolSize = Math.Max(2.0f, Math.Min(baseVolSize, 10.0f)); 

            // Tracks the layout for later re-scaling and rebuilding
            List<BlockData> blocks = new List<BlockData>();
            List<BridgeData> bridges = new List<BridgeData>();
            
            // 1. Initial Volume Placement
            Vector3 startSize = Vector3.Zero;
            if (shape == "cubes")
            {
                float side = baseVolSize * (0.5f + (float)rand.NextDouble());
                startSize = new Vector3(side, side, side);
            }
            else
            {
                startSize = new Vector3(
                    baseVolSize * (0.5f + (float)rand.NextDouble()),
                    baseVolSize * (0.5f + (float)rand.NextDouble()),
                    baseVolSize * (0.5f + (float)rand.NextDouble())
                );
            }

            Vector3 startPos = new Vector3(0, 0, startSize.Z / 2f);
            string startPrimitive = shape == "cubes" ? "box" : allowed_primitives[rand.Next(allowed_primitives.Count)].ToLower();
            
            blocks.Add(new BlockData { Center = startPos, Size = startSize, Level = 1, Primitive = startPrimitive }); 
            
            if (shape == "cubes") 
            {
                AddBoxToMesh(bMesh, startPos - startSize / 2f, startPos + startSize / 2f);
            }
            else 
            {
                AddSpecificShapeToMesh(bMesh, startPos, startSize, startPrimitive);
            }

            // 2. Face-Attached Growth Algorithm
            for (int i = 1; i < n_geometry; i++)
            {
                bool placed = false;
                int attempts = 0;
                Vector3 newPos = Vector3.Zero;
                Vector3 newSize = Vector3.Zero;
                int newLevel = 1;
                
                string currentDistribution = distribution;
                if (distribution == "hybrid")
                {
                    int mode = rand.Next(4);
                    if (mode == 0) currentDistribution = "compact";
                    else if (mode == 1) currentDistribution = "linear";
                    else if (mode == 2) currentDistribution = "distributed";
                    else currentDistribution = "scatter"; 
                }

                // Ensure the first layer contains the vast majority of the geometry
                int level1Count = 0;
                for (int j = 0; j < blocks.Count; j++)
                {
                    if (blocks[j].Level == 1) level1Count++;
                }
                
                // If less than 60% of the target geometry is on the ground floor, FORCE it to stay on Level 1
                bool forceLevel1 = (stack_limit <= 1) || (level1Count < n_geometry * 0.6f);

                List<BlockData> validSources = new List<BlockData>();
                if (forceLevel1) 
                {
                    for (int j = 0; j < blocks.Count; j++) 
                    {
                        if (blocks[j].Level == 1) validSources.Add(blocks[j]);
                    }
                } 
                else 
                {
                    validSources = blocks;
                }

                var source = validSources[0];

                while (!placed && attempts < 50)
                {
                    if (currentDistribution == "compact")
                    {
                        int index = (int)(Math.Pow(rand.NextDouble(), 3) * validSources.Count);
                        source = validSources[index];
                    }
                    else if (currentDistribution == "linear")
                    {
                        int index = validSources.Count - 1 - (int)(Math.Pow(rand.NextDouble(), 3) * Math.Min(3, validSources.Count));
                        source = validSources[Math.Max(0, index)];
                    }
                    else
                    {
                        source = validSources[rand.Next(validSources.Count)]; 
                    }
                    
                    if (shape == "cubes")
                    {
                        float side = baseVolSize * (0.3f + (float)rand.NextDouble() * 1.5f);
                        newSize = new Vector3(side, side, side);
                    }
                    else
                    {
                        newSize = new Vector3(
                            baseVolSize * (0.3f + (float)rand.NextDouble() * 1.5f),
                            baseVolSize * (0.3f + (float)rand.NextDouble() * 1.5f),
                            baseVolSize * (0.3f + (float)rand.NextDouble() * 1.5f)
                        );
                    }

                    // Strict Z-Axis Stacking Constraint (Avoid dir = 4 if forcing Level 1 expansion)
                    int maxDir = forceLevel1 ? 4 : 5;
                    int dir = rand.Next(maxDir); 

                    // Temporary level tracking depending on stack direction
                    int tempLevel = source.Level + (dir == 4 ? 1 : 0);

                    float gap = 0f;
                    if (currentDistribution == "distributed") {
                        dir = rand.Next(2); // STRICTLY limit branching to +X (0) or -X (1)
                        gap = baseVolSize * (0.5f + (float)rand.NextDouble() * 1.5f);
                    }

                    if (dir == 0) newPos = source.Center + new Vector3((source.Size.X + newSize.X) / 2f + gap, 0, 0);
                    else if (dir == 1) newPos = source.Center + new Vector3(-(source.Size.X + newSize.X) / 2f - gap, 0, 0);
                    else if (dir == 2) newPos = source.Center + new Vector3(0, (source.Size.Y + newSize.Y) / 2f + gap, 0);
                    else if (dir == 3) newPos = source.Center + new Vector3(0, -(source.Size.Y + newSize.Y) / 2f - gap, 0);
                    else if (dir == 4) newPos = source.Center + new Vector3(0, 0, (source.Size.Z + newSize.Z) / 2f + gap);

                    if (currentDistribution != "distributed")
                    {
                        float slideFactor = currentDistribution == "compact" ? 0.15f : 0.6f;
                        if (dir == 0 || dir == 1) {
                            newPos.Y += (float)(rand.NextDouble() - 0.5) * source.Size.Y * slideFactor;
                            newPos.Z += (float)(rand.NextDouble() - 0.5) * source.Size.Z * slideFactor;
                        } else if (dir == 2 || dir == 3) {
                            newPos.X += (float)(rand.NextDouble() - 0.5) * source.Size.X * slideFactor;
                            newPos.Z += (float)(rand.NextDouble() - 0.5) * source.Size.Z * slideFactor;
                        } else if (dir == 4) {
                            newPos.X += (float)(rand.NextDouble() - 0.5) * source.Size.X * slideFactor;
                            newPos.Y += (float)(rand.NextDouble() - 0.5) * source.Size.Y * slideFactor;
                        }
                    }

                    // Enforce Ground Floor Base
                    if (newPos.Z - newSize.Z / 2f < 0) {
                        newPos.Z = newSize.Z / 2f; 
                    }

                    bool validPlacement = true;
                    if (currentDistribution == "distributed")
                    {
                        foreach (var b in blocks)
                        {
                            if (Math.Abs(newPos.X - b.Center.X) < (newSize.X + b.Size.X) / 2f + 0.1f &&
                                Math.Abs(newPos.Y - b.Center.Y) < (newSize.Y + b.Size.Y) / 2f + 0.1f &&
                                Math.Abs(newPos.Z - b.Center.Z) < (newSize.Z + b.Size.Z) / 2f + 0.1f)
                            {
                                validPlacement = false;
                                break;
                            }
                        }
                    }

                    float maxAllowedZ = stack_limit * baseVolSize;

                    if (validPlacement && 
                        Math.Abs(newPos.X) + newSize.X / 2f <= maxRadius &&
                        Math.Abs(newPos.Y) + newSize.Y / 2f <= maxRadius &&
                        newPos.Z + newSize.Z / 2f <= maxAllowedZ)
                    {
                        placed = true;
                        newLevel = tempLevel; // Inherit the successfully placed stack level
                    }
                    attempts++;
                }

                // Fallback placement if constrained
                if (!placed)
                {
                    source = validSources[rand.Next(validSources.Count)];
                    newSize = new Vector3(baseVolSize, baseVolSize, baseVolSize); 
                    
                    if (forceLevel1) {
                        // Push sideways on the ground to prevent stacking and force wide base
                        newPos = source.Center + new Vector3((source.Size.X + newSize.X) / 2f, 0, 0);
                        newPos.Z = newSize.Z / 2f; 
                        newLevel = 1; 
                    } else {
                        // Stack upwards normally
                        newPos = source.Center + new Vector3(0, 0, (source.Size.Z + newSize.Z) / 2f);
                        newLevel = source.Level + 1; // Creates new level
                    }
                    currentDistribution = "scatter"; 
                }

                string selectedPrimitive = shape == "cubes" ? "box" : allowed_primitives[rand.Next(allowed_primitives.Count)].ToLower();

                // --- NEW STRUCTURAL ELONGATION LOGIC ---
                // We elongate under TWO conditions:
                // 1. It is a Level 1 placement (ensures all objects in the massive ground layer firmly touch Z=0 even if offset by distributed logic)
                // 2. It is a Level 2 placement and stack_limit > 3 (creates towering foundation columns)
                bool shouldElongate = (newLevel == 1) || (stack_limit > 3 && newLevel == 2);

                if (shouldElongate)
                {
                    bool isFlat = shape == "cubes" || 
                                  selectedPrimitive == "box" || 
                                  selectedPrimitive == "cylinder" || 
                                  selectedPrimitive == "cone" || 
                                  selectedPrimitive == "hex_prism" || 
                                  selectedPrimitive == "pyramid" || 
                                  selectedPrimitive == "wedge";

                    if (isFlat)
                    {
                        float topZ = newPos.Z + newSize.Z / 2f;
                        float bottomZ = 0f; // Force base down to ground level Z=0
                        newSize.Z = topZ - bottomZ;
                        newPos.Z = bottomZ + newSize.Z / 2f; // Adjust center point to perfectly accommodate new structural height
                    }
                }

                blocks.Add(new BlockData { Center = newPos, Size = newSize, Level = newLevel, Primitive = selectedPrimitive });
                
                if (shape == "cubes") 
                {
                    AddBoxToMesh(bMesh, newPos - newSize / 2f, newPos + newSize / 2f);
                }
                else 
                {
                    AddSpecificShapeToMesh(bMesh, newPos, newSize, selectedPrimitive);
                }

                if (currentDistribution == "distributed" && placed)
                {
                    // Trigger new curvy splines for organic designs
                    if (shape == "organic" || shape == "fluid") 
                    {
                        bridges.Add(new BridgeData { Start = source.Center, End = newPos, Thickness = baseVolSize * 0.35f, IsCurvy = true });
                        AddCurvyBridgeToMesh(bMesh, source.Center, newPos, baseVolSize * 0.35f);
                    } 
                    else 
                    {
                        bridges.Add(new BridgeData { Start = source.Center, End = newPos, Thickness = baseVolSize * 0.25f, IsCurvy = false });
                        AddBridgeToMesh(bMesh, source.Center, newPos, baseVolSize * 0.25f);
                    }
                }
            }

            // --- CREATE BASE SLAB GEOMETRY ---
            // Find the absolute maximum extents in the XY plane to perfectly fit the generated footprint
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            foreach (var b in blocks)
            {
                float halfX = b.Size.X / 2f;
                float halfY = b.Size.Y / 2f;

                minX = Math.Min(minX, b.Center.X - halfX);
                maxX = Math.Max(maxX, b.Center.X + halfX);
                minY = Math.Min(minY, b.Center.Y - halfY);
                maxY = Math.Max(maxY, b.Center.Y + halfY);
            }

            float slabThickness = Math.Max(1.0f, baseVolSize * 0.2f);
            
            // Generate the rectangular footprint explicitly sitting below the ground plane (Z = 0)
            Mesh baseMesh = new Mesh();
            AddBoxToMesh(baseMesh, new Vector3(minX, minY, -slabThickness), new Vector3(maxX, maxY, 0f));
            Voxels baseGeometry = new Voxels(baseMesh);

            // 3. PicoGK Voxel Operations (Melts all primitives & splines into cohesive shapes)
            Voxels finalGeom = new Voxels(bMesh);

            if (shape == "fluid")
            {
                float r = baseVolSize * 0.15f;
                finalGeom.Offset(r);
                finalGeom.Offset(-r * 2f);
                finalGeom.Offset(r);
            }
            else if (shape == "organic")
            {
                float r = baseVolSize * 0.45f; 
                finalGeom.Offset(r);
                finalGeom.Offset(-r * 1.5f);
                finalGeom.Offset(r * 0.5f);
            }

            // --- VOXEL CLEANUP ---
            // Clean out any microscopic stray voxels that might have been created while creating the final geometry.
            // A quick boolean opening operation (Erode then Dilate) destroys floating voxel artifacts 
            // without altering the main structural footprint!
            finalGeom.Offset(-0.05f); 
            finalGeom.Offset(0.05f);

            return (finalGeom, baseGeometry, minX, maxX, minY, maxY, blocks, bridges, baseVolSize, shape);
        }

        // --- NEW: Rebuild Building with Custom Scaling and Offset ---
        public static Voxels RebuildBuilding(List<BlockData> blocks, List<BridgeData> bridges, float scaleZ, float shiftX, float shiftZ, string shape, float baseVolSize)
        {
            Mesh bMesh = new Mesh();

            foreach (var b in blocks)
            {
                // Multiply Z bounds by scale factor, shift X and Z locally 
                Vector3 newCenter = new Vector3(b.Center.X + shiftX, b.Center.Y, (b.Center.Z * scaleZ) + shiftZ);
                Vector3 newSize = new Vector3(b.Size.X, b.Size.Y, b.Size.Z * scaleZ);
                
                AddSpecificShapeToMesh(bMesh, newCenter, newSize, b.Primitive);
            }

            foreach (var br in bridges)
            {
                Vector3 newStart = new Vector3(br.Start.X + shiftX, br.Start.Y, (br.Start.Z * scaleZ) + shiftZ);
                Vector3 newEnd = new Vector3(br.End.X + shiftX, br.End.Y, (br.End.Z * scaleZ) + shiftZ);

                if (br.IsCurvy)
                    AddCurvyBridgeToMesh(bMesh, newStart, newEnd, br.Thickness);
                else
                    AddBridgeToMesh(bMesh, newStart, newEnd, br.Thickness);
            }

            Voxels finalGeom = new Voxels(bMesh);

            if (shape == "fluid")
            {
                float r = baseVolSize * 0.15f;
                finalGeom.Offset(r);
                finalGeom.Offset(-r * 2f);
                finalGeom.Offset(r);
            }
            else if (shape == "organic")
            {
                float r = baseVolSize * 0.45f; 
                finalGeom.Offset(r);
                finalGeom.Offset(-r * 1.5f);
                finalGeom.Offset(r * 0.5f);
            }

            return finalGeom;
        }

        // --- NEW: Generate Dynamic Floor Slabs with Boolean Cut & Hole Cleanup! ---
        public static Voxels GenerateFloorSlabs(Voxels interiorVolume, float minX, float maxX, float minY, float maxY, int numFloors, float ceilingHeight)
        {
            Voxels finalFloorSlabs = new Voxels();
            float thickness = 0.1524f; // Exactly 6 inches
            float cleanupRadius = 2.0f; // Radius to morphological close unwanted internal gaps/holes

            // Process each floor individually to prevent vertical merging during cleanup
            for (int i = 0; i <= numFloors; i++)
            {
                // Z=0 is the lowest point of the generated geometry (sitting exactly on top of the base slab)
                float zBottom = i * ceilingHeight;
                float zTop = zBottom + thickness;
                
                // Creates a flat rectangular plane matching the perfectly fitted top cross-sectional footprint
                Mesh slabMesh = new Mesh();
                AddBoxToMesh(slabMesh, new Vector3(minX, minY, zBottom), new Vector3(maxX, maxY, zTop));
                
                Voxels singleSlab = new Voxels(slabMesh);
                Voxels boundingBox = new Voxels(slabMesh); // Keep pure copy for flattening

                // 1. Slice the slab using the interior volume to fit inside the walls
                singleSlab.BoolIntersect(interiorVolume);

                // 2. CLEANUP: Morphological Close (Dilate then Erode) 
                // This expands the slab to swallow and fill any unwanted internal holes, then shrinks the boundaries back
                singleSlab.Offset(cleanupRadius);
                singleSlab.Offset(-cleanupRadius);

                // 3. RESTORE FLATNESS: The 3D offset rounded the top and bottom of the slab.
                // We intersect it back with the pure rectangular bounding box to restore the perfect 6-inch flat surfaces!
                singleSlab.BoolIntersect(boundingBox);

                // Add the cleaned slab to the master collection
                finalFloorSlabs.BoolAdd(singleSlab);
            }

            return finalFloorSlabs;
        }

        // --- NEW: Generate Structural Cores (Stairs and Elevators) ---
        public static Voxels GenerateShafts(float minX, float maxX, float minY, float maxY, float height, int numElevators, float stairWidth, float stairLength, float elevWidth, float elevLength)
        {
            Mesh shaftMesh = new Mesh();
            float cx = (minX + maxX) / 2f;
            float cy = (minY + maxY) / 2f;

            // Staircase Shaft positioned centrally
            float halfStairW = stairWidth / 2f;
            float halfStairL = stairLength / 2f;
            AddBoxToMesh(shaftMesh, new Vector3(cx - halfStairW, cy - halfStairL, 0f), new Vector3(cx + halfStairW, cy + halfStairL, height));

            // Elevator Shafts cascading alongside the staircase
            float currentX = cx + halfStairW; 
            float halfElevL = elevLength / 2f;
            for (int i = 0; i < numElevators; i++)
            {
                AddBoxToMesh(shaftMesh, new Vector3(currentX, cy - halfElevL, 0f), new Vector3(currentX + elevWidth, cy + halfElevL, height));
                currentX += elevWidth;
            }

            return new Voxels(shaftMesh);
        }
    }

    // --- Program Execution ---
    class Program
    {
        static void Main(string[] args)
        {
            Library.Go(0.1f, RunTests);
        }

        static void RunTests()
        {
            Console.WriteLine("Starting Interactive Procedural Generation...");

            List<string> organicPalette = new List<string> { 
                "box", 
                "cylinder",
                "sphere",
                "wedge"
            };

            int variations = 100; // Allow up to 100 iterations via terminal commands

            for (int i = 1; i <= variations; i++)
            {
                Console.WriteLine($"\n--- Generating Iteration {i} ---");

                // FIX: All variables MUST be unpacked without discards to avoid "name does not exist" compiler errors!
                var (building, baseGeometry, minX, maxX, minY, maxY, blocks, bridges, baseVolSize, genShape) = DataDrivenBuildingGenerator.generate_inspiration_geometry(
                    shape: "fluid", 
                    distribution: "compact", 
                    n_geometry: 50, 
                    footprint_area: 1200.0f,
                    stack_limit: 5, 
                    allowed_primitives: organicPalette
                );

                // Display current generation in the viewer
                Console.WriteLine("Label: Base Geometry successfully generated to perfectly fit footprint!");
                Library.oViewer().Add(baseGeometry); 
                Library.oViewer().Add(building);

                Console.Write("Choose this ? (yes/next): ");
                string? input = Console.ReadLine()?.Trim().ToLower(); // Safely handled potential null inputs

                if (input == "yes" || input == "y")
                {
                    Console.WriteLine($"User chose iteration {i}");

                    // --- NEW: Add dynamic floor slabs upon selection ---
                    Console.Write("Enter the number of floors to generate: ");
                    string? floorsInput = Console.ReadLine() ?? "";
                    if (!int.TryParse(floorsInput, out int numFloors)) numFloors = 3; // Default to 3

                    Console.Write("Enter the ceiling height in meters (e.g., 3.0): ");
                    string? heightInput = Console.ReadLine() ?? "";
                    if (!float.TryParse(heightInput, out float ceilingHeight)) ceilingHeight = 3.0f; // Default to 3.0m

                    // --- NEW: Structural Core Prompts ---
                    Console.Write("Enter staircase shaft width in meters (e.g., 3.0): ");
                    string? stairWInput = Console.ReadLine() ?? "";
                    if (!float.TryParse(stairWInput, out float stairWidth)) stairWidth = 3.0f;

                    Console.Write("Enter staircase shaft length in meters (e.g., 5.0): ");
                    string? stairLInput = Console.ReadLine() ?? "";
                    if (!float.TryParse(stairLInput, out float stairLength)) stairLength = 5.0f;

                    Console.Write("Add an elevator shaft? (yes/no): ");
                    string? elevInput = Console.ReadLine()?.Trim().ToLower();
                    
                    int numElevators = 0;
                    float elevWidth = 2.5f;
                    float elevLength = 2.5f;

                    if (elevInput == "yes" || elevInput == "y")
                    {
                        Console.Write("Enter the number of elevator shafts: ");
                        string? numElevsStr = Console.ReadLine() ?? "";
                        if (!int.TryParse(numElevsStr, out numElevators)) numElevators = 1;

                        Console.Write("Enter elevator shaft width in meters (e.g., 2.5): ");
                        string? elevWInput = Console.ReadLine() ?? "";
                        if (!float.TryParse(elevWInput, out elevWidth)) elevWidth = 2.5f;

                        Console.Write("Enter elevator shaft length in meters (e.g., 2.5): ");
                        string? elevLInput = Console.ReadLine() ?? "";
                        if (!float.TryParse(elevLInput, out elevLength)) elevLength = 2.5f;
                    }

                    // 1. Z-SCALING: Calculate max geometric bounds to perfectly map floor heights
                    float slabThickness = 0.1524f; // 6 inch slab
                    
                    // The roof slab will be placed at Z = numFloors * ceilingHeight
                    float targetMaxZ = numFloors * ceilingHeight; 
                    
                    // To fit exactly BETWEEN the ground slab and the roof slab:
                    // The geometry must span from top of ground slab (Z = thickness) to bottom of roof slab (Z = targetMaxZ)
                    float targetBuildingHeight = targetMaxZ - slabThickness;

                    float actualMaxZ = 0f;
                    foreach (var b in blocks) actualMaxZ = Math.Max(actualMaxZ, b.Center.Z + (b.Size.Z / 2f));
                    foreach (var br in bridges) actualMaxZ = Math.Max(actualMaxZ, Math.Max(br.Start.Z, br.End.Z) + (br.Thickness / 2f));
                    
                    // Generate Scale and Z Shift parameters
                    float scaleZ = actualMaxZ > 0.01f ? targetBuildingHeight / actualMaxZ : 1.0f;
                    float shiftZ = slabThickness; // Lift the building geometry so it sits ON TOP of the first slab

                    // 2. REBUILD SOLID IN PLACE WITH PERFECT SCALING & Z SHIFT
                    Voxels solidBuilding = DataDrivenBuildingGenerator.RebuildBuilding(blocks, bridges, scaleZ, 0f, shiftZ, genShape, baseVolSize);

                    // 3. CREATE INTERIOR VOID AND HOLLOW SHELL
                    float wallThickness = 0.3f; // 30cm walls
                    Voxels interiorBuilding = new Voxels(solidBuilding);
                    interiorBuilding.Offset(-wallThickness); 

                    Voxels shellBuilding = new Voxels(solidBuilding);
                    shellBuilding.BoolSubtract(interiorBuilding);

                    // 4. GENERATE AND TRIM FLOOR SLABS (in place)
                    // The GenerateFloorSlabs natively adds a roof slab mathematically since the loop bounds are i <= numFloors
                    Voxels floorSlabs = DataDrivenBuildingGenerator.GenerateFloorSlabs(interiorBuilding, minX, maxX, minY, maxY, numFloors, ceilingHeight);

                    // --- 5. GENERATE STRUCTURAL SHAFTS & PUNCH HOLES ---
                    // Generate solid cuboids to act as the cores representing the elevator and stair shafts
                    Voxels coreShafts = DataDrivenBuildingGenerator.GenerateShafts(minX, maxX, minY, maxY, targetMaxZ + slabThickness, numElevators, stairWidth, stairLength, elevWidth, elevLength);
                    
                    // Mathematically punch exact holes through the floor slabs and base geometry!
                    floorSlabs.BoolSubtract(coreShafts);
                    baseGeometry.BoolSubtract(coreShafts);

                    // --- 6. ARCHITECTURAL DOLLHOUSE CUT ---
                    // Slices away the front half of the outer shell. 
                    // This creates a standard architectural section view, allowing you to perfectly see inside!
                    Mesh sectionCutter = new Mesh();
                    float boundsOut = (maxX - minX) * 2f; 
                    
                    // Slice off everything where Y < 0 (the front half)
                    DataDrivenBuildingGenerator.AddBoxToMesh(sectionCutter, 
                        new Vector3(-boundsOut, -boundsOut, -10f), 
                        new Vector3(boundsOut, 0f, targetMaxZ + 10f));
                    
                    Voxels cutterVoxels = new Voxels(sectionCutter);
                    shellBuilding.BoolSubtract(cutterVoxels);
                    baseGeometry.BoolSubtract(cutterVoxels); // Slice the base slab too so it perfectly matches the architectural cut!

                    // Clear the viewer and show the final architectural presentation
                    Library.oViewer().RemoveAllObjects();
                    
                    Console.WriteLine("Label: Z-Scaled Hollow Architectural Shell (Dollhouse Section Cut) Generated!");
                    Console.WriteLine("Label: Trimmed Interior Slabs Generated (Shaded Blue)!");
                    
                    Library.oViewer().Add(baseGeometry);
                    Library.oViewer().Add(shellBuilding); // Render the sectioned outer shell
                    Library.oViewer().Add(floorSlabs);    // Render the perfectly trimmed inner slabs (with the core holes)

                    break; // Keeps the selected geometry and newly added slabs in the viewer
                }
                
                // Discard the generated geometry when user types "next" to prevent overlapping
                Library.oViewer().Remove(building);
                Library.oViewer().Remove(baseGeometry);
            }
            Console.WriteLine("Generation session complete.");
        }
    }
}