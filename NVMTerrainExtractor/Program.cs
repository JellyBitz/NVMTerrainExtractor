using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NVMTerrainExtractor
{
    static class Program
    {
        /// <summary>
        /// Application entry point
        /// </summary>
        static async Task Main(string[] args)
        {
            List<TerrainMesh> terrains = new List<TerrainMesh>();

            // Load files to convert
            string[] filenames = args;
            if(filenames.Length == 0)
            {
                // Find all .nvm files inside current folder
                filenames = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.nvm");
            }

            // Loading terrain
            foreach (var path in filenames)
            {
                Console.WriteLine("Loading " + path);
                var filename = Path.GetFileName(path);

                // Gets the tile map (X,Y) from Region Id
                var match = Regex.Match(filename, "([0-9a-zA-Z]{2})([0-9a-zA-Z]{2}).nvm$");
                if (!match.Success)
                {
                    Console.WriteLine("Error: Region offset cannot be extracted from filename, the terrain won't be extracted ["+filename+"]");
                    continue;
                }

                // Filling some mesh info
                TerrainMesh mesh = new TerrainMesh();
                mesh.Name = filename;
                mesh.OffsetPosY = int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                mesh.OffsetPosX = int.Parse(match.Groups[2].Value,System.Globalization.NumberStyles.HexNumber);

                // Try to read all vertices from the file
                try
                {
                    ReadJMXVNVM1000(path, mesh);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex);
                    continue;
                }

                // Build triangles from vertices like quads/2
                var w = mesh.Width - 1;
                var h = mesh.Height - 1;
                for (int i = 0; i < h; i++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        uint a = (uint)((i * mesh.Width) + j);
                        uint b = a + 1u;
                        uint c = (uint)(a + mesh.Width);
                        mesh.Triangles.Add(new uint[] { a, b, c });
                        mesh.Triangles.Add(new uint[] { b, c, c + 1u });
                    }
                }

                // Add generated mesh
                terrains.Add(mesh);
            }

            // Avoid saving an empty terrain
            if (terrains.Count == 0)
            {
                Console.WriteLine("Error: Not enough .nvm files to create terrain!");
                return;
            }

            Console.WriteLine("Generating \"Terraing.obj\"...");
            // Make sure the printing format will be right
            var en_us = new System.Globalization.CultureInfo("en-us");
            using (var fs = new FileStream("Terrain.obj", FileMode.Create, FileAccess.Write))
            {
                // Generating as a whole object
                var buffer = System.Text.Encoding.ASCII.GetBytes("o Terrain" + Environment.NewLine);
                await fs.WriteAsync(buffer, 0, buffer.Length);

                uint size = 1;
                // Apply terrain center
                foreach (var mesh in terrains)
                {
                    // Just writes an endline
                    buffer = System.Text.Encoding.ASCII.GetBytes(Environment.NewLine);
                    await fs.WriteAsync(buffer, 0, buffer.Length);

                    // Writing mesh as group
                    buffer = System.Text.Encoding.ASCII.GetBytes($"g {mesh.Name}_{mesh.OffsetPosY}x{mesh.OffsetPosX}" + Environment.NewLine);
                    await fs.WriteAsync(buffer, 0, buffer.Length);

                    foreach (var v in mesh.Vertices)
                    {
                        buffer = System.Text.Encoding.ASCII.GetBytes(string.Format(en_us, "v {0} {1} {2}{3}", v.X, v.Y, v.Z, Environment.NewLine));
                        await fs.WriteAsync(buffer, 0, buffer.Length);
                    }
                    foreach (var t in mesh.Triangles)
                    {
                        buffer = System.Text.Encoding.ASCII.GetBytes($"f {t[0] + size} {t[1] + size} {t[2] + size}" + Environment.NewLine);
                        await fs.WriteAsync(buffer, 0, buffer.Length);
                    }
                    size += (uint)(mesh.Height*mesh.Width);
                }
                // Just to know it finishes
                Console.WriteLine("\"Terraing.obj\" created successfully!");
            }
        }
        /// <summary>
        /// Reads the JMXVNVM1000 file format and extract all the required
        /// </summary>
        private static void ReadJMXVNVM1000(string Filename, TerrainMesh Mesh)
        {
            using (var fs = new FileStream(Filename, FileMode.Open, FileAccess.Read))
            {
                using (var br = new BinaryReader(fs))
                {
                    // Header
                    br.SkipRead(12);
                    // Navigation Entries
                    ushort entryCount = br.ReadUInt16();
                    for (ushort i = 0; i < entryCount; i++)
                    {
                        br.SkipRead(30);
                        br.SkipRead(br.ReadUInt16() * 6);
                    }
                    // Navigation Cells
                    uint cellCount = br.ReadUInt32();
                    br.SkipRead(4);
                    for (uint i = 0; i < cellCount; i++)
                    {
                        br.SkipRead(16);
                        br.SkipRead(br.ReadByte() * 2);
                    }
                    // Navigation Region Links
                    br.SkipRead(br.ReadUInt32() * 27);
                    // Navigation Cell Links
                    br.SkipRead(br.ReadUInt32() * 23);
                    // Texture Map
                    br.SkipRead(96 * 96 * 8);
                    // Height Map
                    Mesh.Width = Mesh.Height = 97;
                    for (int y = 0; y < 97; y++)
                    {
                        for (int x = 0; x < 97; x++)
                        {
                            Mesh.Vertices.Add(new Vector3() { X = x * 20 + Mesh.OffsetPosX * 1920, Y = y * 20 + Mesh.OffsetPosY * 1920, Z = br.ReadSingle() });
                        }
                    }
                    // Just skip everything else
                    // br.SkipRead(36);
                    // br.SkipRead(36 * 4);
                }
            }
        }
        /// <summary>
        /// Extension method to skip reading bytes
        /// </summary>
        public static void SkipRead(this BinaryReader BinaryReader, long Count)
        {
            BinaryReader.BaseStream.Seek(Count, SeekOrigin.Current);
        }
    }
    /// <summary>
    /// Represents a vertex in 3D space
    /// </summary>
    class Vector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
    /// <summary>
    /// Mesh from terrain
    /// </summary>
    class TerrainMesh
    {
        public string Name { get; set; }
        public float OffsetPosX { get; set; }
        public float OffsetPosY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public List<uint[]> Triangles { get; } = new List<uint[]>();
    }
}
