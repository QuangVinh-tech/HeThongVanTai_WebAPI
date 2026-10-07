using System.Collections.Generic;
using System.Threading.Tasks;
using HeThongVanTai.Models.Domain;

namespace HeThongVanTai.Repositories
{
    public interface IImageRepository
    {
        Task<Image> Upload(Image image);
        Task<IEnumerable<Image>> GetAll();
    }
}