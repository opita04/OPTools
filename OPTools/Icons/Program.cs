using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Svg;

namespace IconConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            // Look for SVG in the Icons directory (parent of bin/Debug/net8.0)
            var exeDir = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? Directory.GetCurrentDirectory();
            var iconsDir = Path.Combine(exeDir, "..", "..", "..");
            iconsDir = Path.GetFullPath(iconsDir);
            
            // If that doesn't work, try current directory
            if (!File.Exists(Path.Combine(iconsDir, "chip_icon.svg")))
            {
                iconsDir = Directory.GetCurrentDirectory();
            }
            
            var svgPath = Path.Combine(iconsDir, "chip_icon.svg");
            var icoPath = Path.Combine(iconsDir, "chip_icon.ico");
            
            if (!File.Exists(svgPath))
            {
                Console.WriteLine($"Error: SVG file not found: {svgPath}");
                Environment.Exit(1);
            }
            
            try
            {
                ConvertSvgToIco(svgPath, icoPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        static void ConvertSvgToIco(string svgPath, string icoPath)
        {
            var sizes = new[] { 16, 32, 48, 64, 96, 128, 256 };
            var images = new List<Bitmap>();

            Console.WriteLine($"Converting {svgPath} to ICO with sizes: {string.Join(", ", sizes)}");

            // Load SVG document
            var svgDocument = SvgDocument.Open(svgPath);
            svgDocument.Width = new SvgUnit(SvgUnitType.None, 256);
            svgDocument.Height = new SvgUnit(SvgUnitType.None, 256);

            foreach (var size in sizes)
            {
                try
                {
                    // Render SVG to bitmap at specific size
                    svgDocument.Width = new SvgUnit(SvgUnitType.None, size);
                    svgDocument.Height = new SvgUnit(SvgUnitType.None, size);
                    
                    var bitmap = svgDocument.Draw();
                    images.Add(bitmap);
                    Console.WriteLine($"  Created {size}x{size} image");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: Failed to create {size}x{size}: {ex.Message}");
                }
            }

            if (images.Count == 0)
            {
                throw new Exception("No images were created");
            }

            // Save as ICO
            SaveAsIco(images, icoPath);
            Console.WriteLine($"Successfully created ICO file: {icoPath}");

            // Cleanup
            foreach (var img in images)
            {
                img.Dispose();
            }
        }

        static void SaveAsIco(List<Bitmap> images, string icoPath)
        {
            // ICO file format structure
            using (var fs = new FileStream(icoPath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                // ICO header
                writer.Write((ushort)0); // Reserved (must be 0)
                writer.Write((ushort)1); // Type (1 = ICO)
                writer.Write((ushort)images.Count); // Number of images

                var offset = 6 + (images.Count * 16); // Header + directory entries

                // Write directory entries
                foreach (var img in images)
                {
                    byte width = (byte)(img.Width == 256 ? 0 : img.Width);
                    byte height = (byte)(img.Height == 256 ? 0 : img.Height);
                    writer.Write(width);
                    writer.Write(height);
                    writer.Write((byte)0); // Color palette (0 = no palette)
                    writer.Write((byte)0); // Reserved
                    writer.Write((ushort)1); // Color planes
                    writer.Write((ushort)32); // Bits per pixel
                    
                    // Calculate PNG data size
                    using (var pngStream = new MemoryStream())
                    {
                        img.Save(pngStream, ImageFormat.Png);
                        var pngSize = (uint)pngStream.Length;
                        writer.Write(pngSize);
                        writer.Write((uint)offset);
                        offset += (int)pngSize;
                    }
                }

                // Write image data
                foreach (var img in images)
                {
                    using (var pngStream = new MemoryStream())
                    {
                        img.Save(pngStream, ImageFormat.Png);
                        var pngData = pngStream.ToArray();
                        writer.Write(pngData);
                    }
                }
            }
        }
    }
}

