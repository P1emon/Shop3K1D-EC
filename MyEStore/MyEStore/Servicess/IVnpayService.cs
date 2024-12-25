using MyEStore.Models;

namespace MyEStore.Servicess
{
    public interface IVnpayService
    {
        string CreatePaymentUrl(HttpContext context, VnPaymentRequestModel model);
        VnPaymentResponseModel PaymentExcute(IQueryCollection collections);
    }
}
