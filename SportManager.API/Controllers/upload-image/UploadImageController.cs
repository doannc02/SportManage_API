using Microsoft.AspNetCore.Mvc;

namespace SportManager.API.Controllers.upload_image;

[Route("api/upload-image")]

[ApiController]
public class UploadImageController : ApiControllerBase
{
    private readonly IWebHostEnvironment _env;
    public UploadImageController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> UploadImagesAsync(List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
            return BadRequest("No files uploaded");

        var urls = new List<string>();

        foreach (var file in files)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(_env.WebRootPath, "uploads", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            urls.Add($"/uploads/{fileName}");
        }

        return Ok(urls);
    }

    [HttpDelete]
    public IActionResult DeleteImage([FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("File name is required");

        var filePath = Path.Combine(_env.WebRootPath, "uploads", fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found");

        System.IO.File.Delete(filePath);

        return Ok("File deleted successfully");
    }


}
