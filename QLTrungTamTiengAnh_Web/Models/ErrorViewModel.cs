namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class ErrorViewModel : BaseEntity
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
