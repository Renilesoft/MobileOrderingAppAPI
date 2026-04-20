using ConcessionTrackerAPI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConcessionTrackerAPI.Repositories
{
    public interface IUserRepository
    {
        Task<bool> EmailExistsAsync(string email);
        Task<object?> CreateUserAsync(CTUser user, string fcmToken, string uuid);
        Task<CTUser?> GetUserByEmailAsync(string email);
        Task<List<MarketInfo>> GetMarketsByCityAsync(string city);
        Task<MarketConcessionResponse?> GetConcessionsByMarketAsync(string marketName);
        //Task<List<ItemResponse>?> GetItemsByConcessionAsync(string concessionName);
        Task<List<CategoryResponse>?> GetCategoriesByConcessionAsync(string concessionName);

        /*Task<List<ConcessionSearchResult>> SearchConcessionsByItemKeywordAsync(string keyword);*/

        Task<List<string>> SearchConcessionsByItemKeywordAsync(string keyword);

        Task<CTUser?> ValidateUserLoginAsync(string email, string plainPassword);

        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);

        Task<(CTUser? user, string message)> ValidateUserCredentialsAsync(
            string email,
            string plainPassword,
            string fcmToken, string uuid);

        Task LogAppLoginAsync(int userId, string email, string password, string fcmToken);


        Task<bool> LogoutUserAsync(string email, string fcmToken, string uuid);

        Task<object> SocialLoginAsync(SocialLoginRequest request);

        Task<GetItemsResponse?> GetItemsByConcessionAsync(GetItemsRequest request);

        Task<List<ItemResponse>?> GetItemsByCategoryAsync(GetItemsByCategoryRequest request);

        Task<List<FoodModifierResponse>?> GetFoodModifiersAsync(int concessionId);

        Task<bool> SaveItemAsync(SaveItemRequest request);

        Task<List<SavedItemMarketResponse>> GetSavedItemsByMarketAsync(int marketId, int userId);

        Task<int?> CreateOrderAsync(CreateOrderRequest request);

        Task<List<ActiveOrderResponse>> GetActiveOrdersAsync(int marketId, int userId);

        Task<List<ItemCategoryResponse>> GetItemCategoriesByMarketAsync(string marketName);

        Task<List<ConcessionByItemResponse>> GetConcessionsByItemIdAsync(int itemId);

        Task<List<ConcessionByCategoryResponse>> GetConcessionsByMarketAndCategoryAsync(int marketId, int categoryId);

        Task<bool> DeleteOrderItemAsync(int concessionId, int orderNo, int itemId, int customerId);

        Task<bool> UnsaveItemAsync(UnsaveItemRequest request);

        Task<PaymentSettingsResponse?> GetPaymentSettingsAsync(int concessionId);

        //Task<bool> ConfirmPaymentAsync(ConfirmPaymentRequest request);

        Task<bool> ConfirmPaymentAsync(ConfirmPaymentRequest request);

        Task<bool> AddFoodModifierAsync(AddFoodModifierRequest request);

        Task<bool?> GetFoodModifierStatusAsync(int concessionId, int orderNo, int customerId, int itemId, int foodModifierId);

        Task<bool> RemoveFoodModifierAsync(int concessionId, int orderNo, int customerId, int itemId, int foodModifierId);

        Task<bool> ConfirmFreeOrderAsync(ConfirmPaymentRequest request);

        Task<List<CustomerOrderResponse>> GetCustomerOrdersAsync(int marketId, int customerId);

        Task<OrderDetailsGroupedResponse?> GetOrderDetailsAsync(int concessionId, long orderNumber);

        Task<List<PromotionResponse>> GetPromotionsByMarketAsync(int marketId);

        Task<List<ComboResponse>> GetCombosAsync(int concessionId);

        Task<ComboDetailsResponse?> GetComboDetailsAsync(int concessionId, long comboId);

        Task<UserResponse?> GetUserByIdAsync(int userId);

        Task<bool> UploadUserImageAsync(int userId, IFormFile file);

        Task<UserImageResponse?> GetUserImageAsync(int userId);

        Task<bool> UpdatePhoneNumberAsync(int userId, string phoneNumber);




    }
}


