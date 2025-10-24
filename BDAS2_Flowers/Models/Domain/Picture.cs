using System;

namespace BDAS2_Flowers.Models.Domain;
public class Picture
{
    public int PictureId { get; set; }
    public byte[] PictureBytes { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime UploadDate { get; set; }
    public DateTime? ModificationDate { get; set; }
    public int? ProductId { get; set; }
    public int FormatId { get; set; }
}
