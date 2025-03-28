using BeautySky.Models;
using BeautySky.Models.Vnpay;
using BeautySky.Service;
using BeautySky.Services.Vnpay;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace BeautySky.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly ProjectSwpContext _context;
        private readonly IVnPayService _vnPayService;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public PaymentsController(
            ProjectSwpContext context,
            IVnPayService vnPayService,
            ILogger<PaymentsController> logger,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _context = context;
            _vnPayService = vnPayService;
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
        }

        [HttpPost("create-payment")]
        public async Task<IActionResult> CreatePayment([FromBody] PaymentInformationModel request)
        {
            try
            {
                _logger.LogInformation("Creating payment for Order ID: {OrderId}", request.OrderId);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors });
                }

                var order = await _context.Orders.FindAsync(request.OrderId);
                if (order == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                var paymentUrl = _vnPayService.CreatePaymentUrl(request, HttpContext);
                return Ok(new { success = true, paymentUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment URL");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("payment-callback")]
        public async Task<IActionResult> PaymentCallback([FromQuery] string vnp_ResponseCode, [FromQuery] string vnp_TxnRef)
        {
            try
            {
                var response = _vnPayService.PaymentExecute(Request.Query);
                var orderId = WebUtility.UrlEncode(response.OrderId?.ToString() ?? "");
                var message = WebUtility.UrlEncode(response.OrderDescription ?? "");

                if (response.Success)
                {
                    if (int.TryParse(response.OrderId, out int orderIdInt))
                    {
                        try
                        {
                            var result = await ProcessPaymentTransaction(orderIdInt);
                            if (result.Result is CreatedResult)
                            {
                                _logger.LogInformation($"Payment for Order {orderIdInt} processed successfully via callback");
                                var payment = (result.Result as CreatedResult)?.Value as Payment;
                                return Redirect($"http://localhost:5173/paymentsuccess?orderId={orderId}&paymentId={payment?.PaymentId}");
                            }
                            else if (result.Result is NotFoundResult)
                            {
                                _logger.LogWarning($"Order {orderIdInt} not found during payment processing");
                                return Redirect($"http://localhost:5173/paymentfailed?orderId={orderId}&message={WebUtility.UrlEncode("Không tìm thấy đơn hàng")}");
                            }
                            else if (result.Result is BadRequestResult)
                            {
                                _logger.LogWarning($"Invalid order state for {orderIdInt} during payment processing");
                                return Redirect($"http://localhost:5173/paymentfailed?orderId={orderId}&message={WebUtility.UrlEncode("Đơn hàng không hợp lệ hoặc đã được thanh toán")}");
                            }
                        }
                        catch (Exception procEx)
                        {
                            _logger.LogError(procEx, $"Could not process payment for Order {orderIdInt}: {procEx.Message}");
                            return Redirect($"http://localhost:5173/paymentfailed?orderId={orderId}&message={WebUtility.UrlEncode("Lỗi xử lý thanh toán")}");
                        }
                    }
                }

                return Redirect($"http://localhost:5173/paymentfailed?orderId={orderId}&message={message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment callback");
                return Redirect("http://localhost:5173/paymentfailed?message=" + WebUtility.UrlEncode("Có lỗi xảy ra trong quá trình xử lý thanh toán"));
            }
        }

        [HttpPost("ProcessAndConfirmPayment/{orderId}")]
        public async Task<ActionResult<Payment>> ProcessAndConfirmPayment(int orderId)
        {
            _logger.LogInformation($"Processing payment for Order ID: {orderId}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await ProcessPaymentTransaction(orderId);
                await transaction.CommitAsync();
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing payment for Order ID {orderId}");
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        [HttpPost("test-email")]
        public async Task<IActionResult> TestEmail()
        {
            try
            {
                await _emailService.SendEmailAsync("test@example.com", "Test Email", "<h1>Test thành công!</h1>");
                return Ok("Email sent!");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePayment(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                return NotFound();
            }

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<ActionResult<Payment>> ProcessPaymentTransaction(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                _logger.LogWarning($"Order {orderId} not found.");
                return NotFound("Order not found.");
            }

            if (order.PaymentId != null)
            {
                _logger.LogWarning($"Order {orderId} already has a payment.");
                return BadRequest("Order already has a payment.");
            }

            if (order.UserId == null)
            {
                _logger.LogWarning($"Order {orderId} has no associated User.");
                return BadRequest("Order has no associated User.");
            }

            var payment = await CreatePaymentRecord(order);
            await UpdateOrderStatus(order, payment);

            if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
            {
                var emailBody = GenerateOrderEmailBody(order);
                try
                {
                    await _emailService.SendEmailAsync(order.User.Email, "Đơn hàng của bạn đã được duyệt - BeautySky", emailBody);
                    _logger.LogInformation($"Email sent successfully to {order.User.Email} for Order ID: {orderId}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, $"Error sending email for Order ID: {orderId}");
                }
            }

            _logger.LogInformation($"Payment {payment.PaymentId} processed successfully.");
            return Created($"api/Payments/{payment.PaymentId}", payment);
        }

        private async Task<Payment> CreatePaymentRecord(Order order)
        {
            int paymentTypeId;
            if (order.Payment?.PaymentType?.PaymentTypeId == 1)
            {
                paymentTypeId = 1; // VNPay
            }
            else
            {
                paymentTypeId = 2; // Ship COD
            }

            var payment = new Payment
            {
                UserId = order.UserId,
                PaymentTypeId = paymentTypeId,
                PaymentStatusId = 2, // Confirmed
                PaymentDate = DateTime.Now
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        private async Task UpdateOrderStatus(Order order, Payment payment)
        {
            order.PaymentId = payment.PaymentId;
            order.Status = "Completed";
            _context.Entry(order).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        private string GenerateOrderEmailBody(Order order)
        {
            // Xử lý null và format số
            var formattedAmount = (order.FinalAmount?.ToString("0") ?? "0") + " VND";

            var body = $@"
    <h1>Đơn hàng của bạn đã được duyệt!</h1>
    <p>Xin chào quý khách,</p>
    <p>Cảm ơn bạn đã tin tưởng và đặt hàng tại <strong>BeautySky</strong>. Chúng tôi rất vui thông báo rằng đơn hàng của bạn đã được duyệt thành công.</p>
    
    <h3>Thông tin đơn hàng:</h3>
    <p><strong>Mã đơn hàng:</strong> {order.OrderId}</p>
    <p><strong>Tổng tiền:</strong> {formattedAmount}</p>

    <p>Chúng tôi sẽ xử lý đơn hàng và giao đến bạn trong thời gian sớm nhất có thể. Nếu có bất kỳ thắc mắc nào, vui lòng liên hệ với chúng tôi.</p>

    <h3>Thông tin liên hệ:</h3>
    <p><strong>Công ty TNHH Thương mại FPT</strong></p>
    <p>Địa chỉ: Lô E2a-7, Đường D1, Khu Công Nghệ Cao, Thủ Đức, TP.HCM</p>
    <p>Số điện thoại: 0937748123</p>
    <p>Hotline: (028) 7300 5588</p>
    <p>Email: <a>company.fbeauty@fpt.net.vn</a></p>

    <p>Một lần nữa, BeautySky xin chân thành cảm ơn bạn đã ủng hộ chúng tôi.</p>
    <p>Trân trọng,</p>
    <p><strong>Đội ngũ BeautySky</strong></p>
";

            return body;
        }
    }
}