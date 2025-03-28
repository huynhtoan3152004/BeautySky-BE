﻿using BeautySky.Library;
using BeautySky.Models;
using BeautySky.Models.Vnpay;
using BeautySky.Services.Vnpay;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net;
using System.Threading.Tasks;

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

        public PaymentsController(
            ProjectSwpContext context,
            IVnPayService vnPayService,
            ILogger<PaymentsController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _vnPayService = vnPayService;
            _logger = logger;
            _configuration = configuration;
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

                // Kiểm tra đơn hàng
                var order = await _context.Orders.FindAsync(request.OrderId);
                if (order == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                // Tạo URL thanh toán bằng VnPayService
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
                            // Sử dụng ProcessPaymentTransaction đã có
                            var result = await ProcessPaymentTransaction(orderIdInt);

                            if (result.Result is CreatedResult)
                            {
                                _logger.LogInformation($"Payment for Order {orderIdInt} processed successfully via callback");
                                // Chuyển hướng đến trang thành công với thông tin payment
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

                // Thanh toán thất bại hoặc bị hủy
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
            var order = await _context.Orders.FindAsync(orderId);
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

            _logger.LogInformation($"Payment {payment.PaymentId} processed successfully.");
            return Created($"api/Payments/{payment.PaymentId}", payment);
        }
        private async Task<Payment> CreatePaymentRecord(Order order)
        {
            // Kiểm tra phương thức thanh toán của đơn hàng, xử lý an toàn
            int paymentTypeId;

            // Kiểm tra null và gán giá trị mặc định
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

    }
}