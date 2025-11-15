using BusinessObject.DTOs.OrdersDto;
using BusinessObject.DTOs.TransactionsDto;
using BusinessObject.Enums;
using BusinessObject.Models;
using BusinessObject.Utilities;

namespace BusinessObject.Mappings
{
    public class OrderProfile : AutoMapper.Profile
    {
        public OrderProfile()
        {
            CreateMap<Order, OrderDto>().ReverseMap();
            CreateMap<Order, OrderWithDetailsDto>();
            CreateMap<CreateOrderDto, Order>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTimeHelper.GetVietnamTime()))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Customer, opt => opt.Ignore())
                .ForMember(dest => dest.Provider, opt => opt.Ignore());

            CreateMap<Order, OrderListDto>()
                .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => $"ORD{src.Id.ToString().Substring(0, 3).ToUpper()}"))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer.Profile.FullName))
                .ForMember(dest => dest.CustomerEmail, opt => opt.MapFrom(src => src.Customer.Email))

                .ForMember(dest => dest.DeliveryAddress, opt => opt.MapFrom(src => src.Customer.Profile.Address))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Customer.Profile.Phone))
                .ForMember(dest => dest.ScheduledDate, opt => opt.MapFrom(src => src.RentalStart ?? DateTime.UtcNow))
                .ForMember(dest => dest.DeliveredDate, opt => opt.MapFrom(src =>
                    (src.Status == OrderStatus.in_use || src.Status == OrderStatus.returned || src.Status == OrderStatus.returned_with_issue || src.Status == OrderStatus.in_transit || src.Status == OrderStatus.returning)
                    ? src.DeliveredDate
                    : (DateTime?)null))
                .ForMember(dest => dest.ReturnDate, opt => opt.MapFrom(src => src.RentalEnd));

            CreateMap<OrderItem, OrderItemListDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.ProductSize, opt => opt.MapFrom(src => src.Product.Size))
                .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => src.Product.Images.FirstOrDefault(i => i.IsPrimary).ImageUrl))
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => src.RentalDays))
                .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => src.TransactionType));

            CreateMap<Transaction, TransactionSummaryDto>();

            CreateMap<Order, OrderFullDetailsDto>();

            CreateMap<Order, OrderDetailsDto>()
            .ForMember(dest => dest.OrderCode, opt => opt.MapFrom(src => $"ORD{src.Id.ToString().Substring(0, 3).ToUpper()}"))
            .ForMember(dest => dest.ProviderId, opt => opt.MapFrom(src => src.ProviderId)) // Map ProviderId for ownership verification
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt)) // Order Placed date
            .ForMember(dest => dest.DeliveredDate, opt => opt.MapFrom(src => src.DeliveredDate)) // Order Shipped date
            .ForMember(dest => dest.RentalStartDate, opt => opt.MapFrom(src => src.RentalStart))
            .ForMember(dest => dest.RentalEndDate, opt => opt.MapFrom(src => src.RentalEnd))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
            // Map Subtotal directly from Order.Subtotal field (chỉ giá thuê/mua, không bao gồm cọc)
            .ForMember(dest => dest.Subtotal, opt => opt.MapFrom(src => src.Subtotal))
            // Set default shipping (can be customized based on business logic)
            .ForMember(dest => dest.Shipping, opt => opt.MapFrom(src => 0m))  // Free shipping for now
            // Set default tax (can be customized based on business logic) 
            .ForMember(dest => dest.Tax, opt => opt.MapFrom(src => 0m))  // No tax for now
            // Map total deposit amount from Order.TotalDeposit field
            .ForMember(dest => dest.TotalDepositAmount, opt => opt.MapFrom(src => src.TotalDeposit))
            // Map discount fields
            .ForMember(dest => dest.DiscountAmount, opt => opt.MapFrom(src => src.DiscountAmount))
            .ForMember(dest => dest.DiscountCodeId, opt => opt.MapFrom(src => src.DiscountCodeId))
            .ForMember(dest => dest.DiscountCodeName, opt => opt.MapFrom(src => src.DiscountCode != null ? src.DiscountCode.Code : null))
            // AutoMapper sẽ tự động map List<OrderItem> sang List<OrderItemDetailsDto> nếu bạn đã định nghĩa mapping cho item
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            // Map từ thông tin Profile của Customer
            .ForMember(dest => dest.ShippingAddress, opt => opt.MapFrom(src => new ShippingAddressDto
            {
                Email = src.CustomerEmail,
                FullName = src.CustomerFullName,
                Phone = src.CustomerPhoneNumber,
                Address = src.DeliveryAddress
            }))
            // Map từ Transaction liên quan (lấy cái đầu tiên làm đại diện)
            .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.Transactions.FirstOrDefault().PaymentMethod))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Transactions.FirstOrDefault().Content));

            // Mapping cho OrderItem -> OrderItemDetailsDto (chi tiết hơn OrderItemListDto)
            CreateMap<OrderItem, OrderItemDetailsDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Product.Size))
                .ForMember(dest => dest.Color, opt => opt.MapFrom(src => src.Product.Color))
                .ForMember(dest => dest.PrimaryImageUrl, opt => opt.MapFrom(src => 
                    src.Product.Images != null && src.Product.Images.Any(i => i.IsPrimary) 
                        ? src.Product.Images.FirstOrDefault(i => i.IsPrimary).ImageUrl 
                        : src.Product.Images != null && src.Product.Images.Any() 
                            ? src.Product.Images.FirstOrDefault().ImageUrl 
                            : "/images/placeholder.jpg"))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.RentalDays, opt => opt.MapFrom(src => src.RentalDays))
                .ForMember(dest => dest.PricePerDay, opt => opt.MapFrom(src => src.DailyRate))
                .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => src.TransactionType))
                .ForMember(dest => dest.DepositPerUnit, opt => opt.MapFrom(src => src.DepositPerUnit))
                .ForMember(dest => dest.RentalCommissionRate, opt => opt.MapFrom(src => src.RentalCommissionRate))
                .ForMember(dest => dest.PurchaseCommissionRate, opt => opt.MapFrom(src => src.PurchaseCommissionRate))
                .ForMember(dest => dest.CommissionAmount, opt => opt.MapFrom(src => src.CommissionAmount));

            // Mapping cho Profile -> ShippingAddressDto
            CreateMap<Models.Profile, ShippingAddressDto>();
        }
    }
}
