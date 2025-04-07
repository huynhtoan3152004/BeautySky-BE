using BeautySky.Library;
using BeautySky.Models;
using BeautySky.Models.Vnpay;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BeautySky.Services.Vnpay
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<VnPayService> _logger;
        private readonly ProjectSwpContext _dbContext; // Thêm DbContext của bạn vào đây để truy vấn DB

        public VnPayService(IConfiguration configuration, ILogger<VnPayService> logger, ProjectSwpContext dbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _dbContext = dbContext;
        }

        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            try
            {
                var order = _dbContext.Orders.FirstOrDefault(o => o.OrderId == model.OrderId);

                if (order == null || order.Status == "Completed")
                {
                    throw new Exception("Đơn hàng không tồn tại hoặc đã được xử lý.");
                }

                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_configuration["TimeZoneId"]);
                var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
                var pay = new VnPayLibrary();

                long amountInVnd = (long)Math.Round(model.Amount * 100, 0);

                // 🔹 Tạo TxnRef mới bằng cách thêm timestamp vào OrderID
                string transactionId = $"{model.OrderId}_{DateTime.UtcNow.Ticks}";

                pay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
                pay.AddRequestData("vnp_Command", "pay");
                pay.AddRequestData("vnp_TmnCode", _configuration["Vnpay:TmnCode"]);
                pay.AddRequestData("vnp_Amount", amountInVnd.ToString());
                pay.AddRequestData("vnp_BankCode", "");
                pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
                pay.AddRequestData("vnp_CurrCode", "VND");
                pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
                pay.AddRequestData("vnp_Locale", "vn");
                pay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang: {model.OrderId}");
                pay.AddRequestData("vnp_OrderType", "other");
                pay.AddRequestData("vnp_ReturnUrl", _configuration["Vnpay:PaymentBackReturnUrl"]);
                pay.AddRequestData("vnp_TxnRef", transactionId);

                var paymentUrl = pay.CreateRequestUrl(_configuration["Vnpay:BaseUrl"], _configuration["Vnpay:HashSecret"]);

                _logger.LogInformation("Payment URL created: {PaymentUrl}", paymentUrl);

                return paymentUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo URL thanh toán VNPay");
                throw;
            }
        }


        public PaymentResponseModel PaymentExecute(IQueryCollection collections)
        {
            var pay = new VnPayLibrary();
            var response = pay.GetFullResponseData(collections, _configuration["Vnpay:HashSecret"]);
            //var vnp_TxnRef = collections["vnp_TxnRef"];
            //try
            //{
            //    if (!string.IsNullOrEmpty(response.OrderId))
            //    {
            //        // Tách OrderId từ TxnRef
            //        string[] txnRefParts = response.OrderId.Split('_');
            //        if (txnRefParts.Length > 0 && int.TryParse(txnRefParts[0], out int orderIdFromTxnRef))
            //        {
            //            // Tìm đơn hàng trong cơ sở dữ liệu
            //            var order = _dbContext.Orders.FirstOrDefault(o => o.OrderId == orderIdFromTxnRef);

            //            if (order != null)
            //            {
            //                // Kiểm tra trạng thái của đơn hàng trước khi duyệt
            //                if (order.Status != "Pending")
            //                {
            //                    _logger.LogWarning($"Order {order.OrderId} has already been processed. Skipping update.");
            //                    return response;
            //                }

            //                // Cập nhật trạng thái đơn hàng theo kết quả từ VnPay
            //                if (response.VnPayResponseCode == "00")
            //                {
            //                    order.Status = "Completed";
            //                }
            //                else
            //                {
            //                    order.Status = "Failed";
            //                }

            //                // Lưu thông tin cập nhật
            //                _dbContext.Orders.Update(order);
            //                _dbContext.SaveChanges();
            //            }
            //            else
            //            {
            //                _logger.LogWarning($"Order {orderIdFromTxnRef} not found.");
            //            }
            //        }
            //        else
            //        {
            //            _logger.LogError($"Invalid OrderId: {response.OrderId}. Unable to parse OrderId.");
            //        }
            //    }
            //    else
            //    {
            //        _logger.LogError("OrderId is missing in the VnPay response.");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Error updating order status after VnPay payment.");
            //}

            return response;
        }

    }
}
