# FRECS (ShareIt)

FRECS is a specialized Fashion/Product Rental E-Commerce System connecting Providers with Customers for seamless rental transactions. Built with modern .NET technologies, it ensures secure payments and efficient management of rental lifecycles.

## 🚀 Key Features

*   **Role-Based Access**: Specialized interfaces for Admins, Staff, Providers, and Customers.
*   **Rental Management**: End-to-end flow from posting items to booking and returning.
*   **Payment Integration**: Secure transactions via VNPay and Sepay.
*   **Real-time Updates**: Status tracking for orders and requests.

## 👥 System Accounts (Roles & Credentials)

| STT | Role | Username / Email | Password | Description |
| :--- | :--- | :--- | :--- | :--- |
| 1 | **Admin** | `quavi0710@gmail.com` | `Vinh123@` | Quản trị viên cao nhất, quản lý users, hệ thống. |
| 2 | **Staff** | `tnguyen0uu@gmail.com` | `Nguyen111@` | Nhân viên vận hành, duyệt bài đăng, xử lý báo cáo. |
| 3 | **Provider** | `badat201103@gmail.com` | `@Badat123` | Người cung cấp dịch vụ/sản phẩm (đăng bài cho thuê). |
| 4 | **Customer** | `daoha20102003@gmail.com` | `Hadanhdao123@` | Khách hàng (tìm kiếm, đặt thuê/mua). |
| 5 | **Guest** | *(No Login)* | - | Khách vãng lai, chỉ xem thông tin công khai. |

## 🛠 Tech Stack

*   **Backend**: ASP.NET Core Web API (.NET 8.0)
*   **Frontend**: ASP.NET Core Web App (.NET 8.0)
*   **Database**: SQL Server
*   **Cloud Services**: Cloudinary (Image Storage)
*   **Payment Gateways**: VNPay, Sepay

## 🔧 Getting Started

### Prerequisites

*   .NET 8.0 SDK
*   SQL Server
*   Docker (optional)

### Installation & Run

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/daohd2003/FRECS.git
    ```

2.  **Configuration:**
    Update the `appsettings.json` files in `ShareItAPI` and `ShareItFE` with your database connection strings and API keys.

3.  **Run with Visual Studio:**
    *   Open `ShareIt.sln`.
    *   Set **ShareItAPI** and **ShareItFE** as startup projects.
    *   Run the application (F5).

4.  **Run with Docker:**
    ```bash
    docker-compose up --build
    ```
