using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Quick standalone script to convert the PNG to ICO
// Run with: dotnet-script create_ico.csx
// Or just use the PNG directly and reference it in the project

var pngPath = args.Length > 0 ? args[0] : "Assets/tray.png";
var icoPath = args.Length > 1 ? args[1] : "Assets/tray.ico";

using var src = new Bitmap(pngPath);

// Create multi-size ICO (256, 48, 32, 16)
int[] sizes = [256, 48, 32, 16];
using var ms = new System.IO.MemoryStream();

// ICO header
var writer = new System.IO.BinaryWriter(ms);
writer.Write((short)0);  // reserved
writer.Write((short)1);  // type: icon
writer.Write((short)sizes.Length);  // count

var imagesData = new List<byte[]>();
foreach (var size in sizes)
{
    using var resized = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(resized);
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.DrawImage(src, 0, 0, size, size);
    using var imgMs = new System.IO.MemoryStream();
    resized.Save(imgMs, ImageFormat.Png);
    imagesData.Add(imgMs.ToArray());
}

// Write directory entries
int offset = 6 + sizes.Length * 16;
for (int i = 0; i < sizes.Length; i++)
{
    var sz = sizes[i];
    writer.Write((byte)(sz >= 256 ? 0 : sz));  // width
    writer.Write((byte)(sz >= 256 ? 0 : sz));  // height
    writer.Write((byte)0);  // color count
    writer.Write((byte)0);  // reserved
    writer.Write((short)1);  // planes
    writer.Write((short)32); // bit count
    writer.Write(imagesData[i].Length);
    writer.Write(offset);
    offset += imagesData[i].Length;
}
foreach (var data in imagesData)
    writer.Write(data);

System.IO.File.WriteAllBytes(icoPath, ms.ToArray());
Console.WriteLine($"Created {icoPath}");
