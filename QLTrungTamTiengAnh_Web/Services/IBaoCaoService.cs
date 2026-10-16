using QLTrungTamTiengAnh_Web.Controllers;

namespace QLTrungTamTiengAnh_Web.Services
{
    public interface IBaoCaoService
    {
        Task<IReadOnlyList<BaoCaoBranchOptionDto>> GetBranchOptionsAsync(CancellationToken cancellationToken = default);

        Task<BaoCaoTongHopDto> GetReportAsync(
            BaoCaoFilterDto? filter,
            CancellationToken cancellationToken = default);
    }
}
