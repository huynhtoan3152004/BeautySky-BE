# BeatySkyShop Backend - Skincare E-commerce API
<div align="center">
     <picture>
    <img alt="BeautySky Logo" src="BeautySky-BE\logo.png" width="200">
  </picture>
</div> 

<div align="center">
  <h1>BeatySkyShop - Backend 🚀</h1>
  
  <!-- [![API Status](https://img.shields.io/website?url=<API_DOCS_URL>&label=api)](<API_DOCS_URL>) -->
  [![Documentation](https://img.shields.io/badge/documentation-swagger-green.svg)](<API_DOCS_URL>)
</div>

Backend API service for BeatySkyShop, a skincare e-commerce system managing product sales, customer profiles, orders, payments, promotions, and more.

## 🌐 API Documentation

- API Base URL: `http://localhost:7112`

## ✨ Key Features

- JWT-based authentication & role-based access control
- User profile and order history management
- Secure payment integration using VNPAY
- Amazon S3 for image storage
- Skin type assessment quiz processing
- Personalized skincare regimen system
- Product recommendation engine
- Product comparison functionality
- Order management (from placement to completion)
- Promotions, loyalty programs, and discount management
- Customer ratings and feedback system
- Admin dashboard & reports
- Security measures (rate limiting, input validation, SQL injection prevention)

## 🛠 Tech Stack

- **Backend Framework:** ASP.NET Web API
- **Database:** Microsoft SQL Server
- **Authentication:** JWT (JSON Web Tokens)
- **Cloud Storage:** Amazon S3
- **Payment Processing:** VNPAY
- **Documentation:** Swagger/OpenAPI

## 🚀 Getting Started

### Prerequisites

- .NET SDK: 8.0.*
- Database: Microsoft SQL Server
- Amazon S3 Account
- VNPAY Developer Account

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/huynhtoan3152004/BeautySky-BE.git
   ```

2. Navigate to the project directory:
   ```bash
   cd beautyskyshop-backend
   ```

3. Run database migrations:
   ```bash
   dotnet ef database update
   ```

4. Start the development server:
   ```bash
   dotnet run
   ```
   The API will be available at `http://localhost:7112`

## 🧪 appsettings.json Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "MyDBConnection": ""
  },
  "JWT": {
    "ValidAudience": "User",
    "ValidIssuer": "https://localhost:7112",
    "Secret": ""
  },
  "Authentication": {
    "Google": {
      "ClientId": "",
      "ClientSecret": "_"
    }
  },
  "AWS": {
    "BucketName": "beautysky",
    "AccessKey": "",
    "SecretKey": "",
    "Region": "ap-southeast-2"
  },
  "Vnpay": {
    "TmnCode": "",
    "HashSecret": "",
    "BaseUrl": "",
    "Command": "pay",
    "CurrCode": "VND",
    "Version": "2.1.0",
    "Locale": "vn",
    "PaymentBackReturnUrl": "",
    "UsdToVndRate": 24500
  },
  "TimeZoneId": "SE Asia Standard Time"
}
```


### Key Testing Scenarios

- Authentication and authorization validation
- Order management workflows
- Payment processing (successful and failed transactions)
- Skin type assessment logic
- API rate limiting & security enforcement

## 🔒 Security

This API implements several security measures:
- JWT authentication & role-based authorization
- Request rate limiting
- SQL injection protection
- Secure payment handling
- Input validation
- CORS configuration
- XSS protection


## 👥 Contributing

1. Fork the repository
2. Create your feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add some amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a pull request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

## 🤝 Support

For support and queries:
- Email: `haile170504@gmail.com`
- Email: `huynhhuutoanwork@gmail.com`
- Email: `danhthanh18102004@gmail.com`
- API Issues: Create a GitHub issue

---
## 🙏 Acknowledgments

- Thanks to all contributors who have helped with the API development
- Special thanks to our lecturer [Nguyen The Hoang](https://github.com/doit-now) for guidance
- Appreciation to the open-source community for the amazing tools and libraries
##
<div align="center">
  Built with ❤️ by the BeatySkyShop Backend Team
</div>

