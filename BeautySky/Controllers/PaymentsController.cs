using BeautySky.Models;
using BeautySky.Models.Vnpay;
using BeautySky.Services;
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
                // Xử lý phản hồi từ VnPay để nhận thông tin thanh toán
                var response = _vnPayService.PaymentExecute(Request.Query);
                _logger.LogInformation("VNPay Callback Params: {Params}", string.Join(", ", Request.Query.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                var orderId = response.OrderId?.ToString() ?? ""; 
                var message = response.OrderDescription ?? "";

                // Kiểm tra nếu thanh toán thành công
                if (response.Success && response.VnPayResponseCode == "00" && !string.IsNullOrEmpty(orderId))
                {
                    if (int.TryParse(orderId, out int orderIdInt))
                    {
                        try
                        {
                            // Tự động duyệt đơn hàng VnPay, không cần frontend cung cấp orderId
                            var result = await ConfirmPaymentVnPay(orderIdInt);

                            if (result is OkObjectResult okResult) // Nếu xử lý thành công
                            {
                                _logger.LogInformation($"Payment for Order {orderIdInt} processed successfully via callback");
                                var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.OrderId == orderIdInt);
                                var payment = ((dynamic)okResult.Value).paymentId;
                                return Ok(new { success = true, orderId = order.OrderId, paymentId = payment, message = "Thanh cong roi"});
                            }
                            else if (result is NotFoundResult) // Nếu không tìm thấy đơn hàng
                            {
                                _logger.LogWarning($"Order {orderIdInt} not found during payment processing");
                                return Redirect($"http://localhost:5173/paymentfailed?orderId={orderId}&message={WebUtility.UrlEncode("Không tìm thấy đơn hàng")}");
                            }
                            else if (result is BadRequestResult) // Nếu đơn hàng không hợp lệ
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

                // Nếu thanh toán thất bại hoặc thông tin không hợp lệ
                return Redirect($"http://localhost:5173/paymentfailed?orderId={orderId}&message={WebUtility.UrlEncode(message)}");
            }
            catch (Exception ex)
            {
                // Xử lý lỗi tổng quát
                _logger.LogError(ex, "Error processing payment callback");
                return Redirect("http://localhost:5173/paymentfailed?message=" + WebUtility.UrlEncode("Có lỗi xảy ra trong quá trình xử lý thanh toán"));
            }
        }



        [HttpPost("confirm-payment/{orderId}")]
        public async Task<IActionResult> ConfirmPaymentVnPay(int orderId)
        {
            _logger.LogInformation($"Processing and confirming payment for Order ID: {orderId}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.OrderId == orderId);

                // Kiểm tra xem đơn hàng có tồn tại không
                if (order == null)
                {
                    return NotFound("Đơn hàng không tồn tại.");
                }

                // Kiểm tra trạng thái của đơn hàng
                if (order.Status != "Pending")
                {
                    return BadRequest("Đơn hàng không thể duyệt vì không phải trạng thái Pending.");
                }

                // Kiểm tra nếu đã có thanh toán
                if (order.PaymentId != null)
                {
                    return BadRequest("Đơn hàng này đã có thanh toán.");
                }

                // Tạo thông tin thanh toán mới
                var payment = new Payment
                {
                    UserId = order.UserId,
                    PaymentTypeId = 1, // VnPay
                    PaymentStatusId = 2, // Confirmed (Thanh toán đã xác nhận)
                    PaymentDate = DateTime.Now
                };

                // Lưu vào database
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Cập nhật Order với PaymentId
                order.PaymentId = payment.PaymentId;
                order.Status = "Completed";

                var user = await _context.Users.FindAsync(order.UserId);
                if (user != null)
                {
                    user.Point += 1; // Hoặc tính điểm theo yêu cầu của bạn
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }

                _context.Entry(order).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                // Gửi email nếu có email
                if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
                {
                    var emailBody = GenerateOrderEmailBody(order);
                    try
                    {
                        await _emailService.SendEmailAsync(order.User.Email, "Thanh toán thành công - BeautySky", emailBody);
                        _logger.LogInformation($"Email đã gửi thành công đến {order.User.Email} cho đơn hàng ID: {orderId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Lỗi khi gửi email cho đơn hàng ID: {orderId}");
                    }
                }
                else
                {
                    _logger.LogWarning($"User của đơn hàng ID {orderId} không có email, không thể gửi thông báo.");
                }

                return Ok(new { success = true, message = "Thanh toán qua VnPay đã được xác nhận và đơn hàng đã được duyệt", paymentId = payment.PaymentId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing payment for Order ID {orderId}");
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        [HttpPost("ProcessAndConfirmPayment/{orderId}")]
        public async Task<ActionResult<Payment>> ProcessAndConfirmPayment(int orderId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found.");
                }

                var user = await _context.Users.FindAsync(order.UserId);
                if (user != null)
                {
                    user.Point += 1; // Cộng điểm cho user
                    _context.Users.Update(user);
                }

                // Gọi transaction xử lý thanh toán
                var result = await ProcessPaymentTransaction(orderId);
                if (result.Result is ObjectResult objectResult && objectResult.StatusCode == 500)
                {
                    throw new Exception("Payment processing failed.");
                }

                await _context.SaveChangesAsync(); // Lưu các thay đổi trước khi commit
                await transaction.CommitAsync(); // Commit transaction sau khi thành công

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

        [HttpPost("confirm-delivery/{orderId}")]
        public async Task<IActionResult> ConfirmDelivery(int orderId)
        {
            _logger.LogInformation($"Confirming delivery for Order ID: {orderId}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.Payment)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                // Kiểm tra đơn hàng tồn tại
                if (order == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                // Kiểm tra trạng thái đơn hàng
                if (order.Status != "Completed")
                {
                    return BadRequest(new { success = false, message = "Đơn hàng chưa được thanh toán hoặc không ở trạng thái phù hợp" });
                }

                // Cập nhật trạng thái đơn hàng
                order.Status = "Delivered";
                order.DeliveryDate = DateTime.Now;

                // Cộng thêm điểm cho user khi giao hàng thành công
                //if (order.User != null)
                //{
                //    var orderTotal = order.FinalAmount ?? 0;
                //    var pointsEarned = (int)(orderTotal / 100000); // Cứ mỗi 100,000 VNĐ được 1 điểm
                //    order.User.Point += pointsEarned;

                //    _logger.LogInformation($"Added {pointsEarned} points to User ID: {order.User.UserId}");
                //}

                await _context.SaveChangesAsync();

                // Gửi email thông báo giao hàng thành công
                if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
                {
                    var emailBody = GenerateOrderEmailBody(order);
                    try
                    {
                        await _emailService.SendEmailAsync(
                            order.User.Email,
                            "Đơn hàng đã giao thành công - BeautySky",
                            emailBody
                        );
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, $"Error sending delivery confirmation email for Order ID: {orderId}");
                        // Không throw exception vì đây không phải lỗi nghiêm trọng
                    }
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Đơn hàng đã được giao thành công",
                    order = new
                    {
                        orderId = order.OrderId,
                        status = order.Status,
                        deliveryDate = order.DeliveryDate,
                        pointsEarned = order.User?.Point
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error confirming delivery for Order ID {orderId}");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xử lý giao hàng" });
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
            var user = await _context.Users.FindAsync(order.UserId);
            if (user != null)
            {
                user.Point += 1; // Hoặc tính điểm theo yêu cầu của bạn
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
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