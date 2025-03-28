using System.Threading.Tasks;
namespace BeautySky.Service

{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
