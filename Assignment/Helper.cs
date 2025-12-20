using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

namespace Assignment;

public class Helper(IWebHostEnvironment en)
{
    // ------------------------------------------------------------------------
    // Photo Upload
    // ------------------------------------------------------------------------

    public string ValidatePhoto(IFormFile photo)
    {
        if (photo == null) return "";

        // 1. Check file size (e.g., max 1MB)
        if (photo.Length > 1024 * 1024) return "File size too large (max 1MB).";

        // 2. Check extension
        var ext = Path.GetExtension(photo.FileName).ToLower();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") return "Only JPG/PNG allowed.";

        return "";
    }

    public string SavePhoto(IFormFile photo, string folder)
    {
        if (photo == null) return null;

        var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(photo.FileName);
        var path = Path.Combine(en.WebRootPath, folder, fileName);

        // Ensure directory exists
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using (var stream = new FileStream(path, FileMode.Create))
        {
            photo.CopyTo(stream);
        }

        return fileName;
    }

    public void DeletePhoto(string fileName, string folder)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        var path = Path.Combine(en.WebRootPath, folder, fileName);
        if (File.Exists(path)) File.Delete(path);
    }

}
