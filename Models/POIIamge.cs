using SQLite;

public class POIImage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int POIId { get; set; }   // liên kết với địa điểm

    public string Url { get; set; } = string.Empty; // URL hoặc tên file ảnh
    public string ImageName { get; set; } = string.Empty;
}