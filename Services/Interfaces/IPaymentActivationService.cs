using SaaSForge.Api.Models;

namespace SaaSForge.Api.Services.Interfaces
{
    public interface IPaymentActivationService
    {
        Task ActivateSubscriptionFromOrderAsync(PaymentOrder order);
    }
}
