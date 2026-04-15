using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}
