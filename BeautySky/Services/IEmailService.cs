using System.Threading.Tasks;
namespace BeautySky.Services

{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
