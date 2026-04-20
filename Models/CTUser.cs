using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ConcessionTrackerAPI.Models
{
    public class CTUser
    {
        public int usr_int_usrid { get; set; }

        public string? usr_vch_emailid { get; set; } = string.Empty;
        public string? usr_vch_name { get; set; } = string.Empty;

        public string? usr_vch_provider { get; set; }
        public string? usr_vch_photo_url { get; set; }
        public string? usr_vch_pswd { get; set; } = default!;

        public string? usr_vch_phoneno { get; set; }


    }

    public class CreateUserRequest
    {
        public string? Name { get; set; } = default!;
        public string? Email { get; set; } = default!;
        public string? Password { get; set; } = default!;
    }

    public class LoginRequest
    {
        public string? Email { get; set; } = string.Empty;
        public string? Password { get; set; } = string.Empty;
        public string? FcmToken { get; set; } = string.Empty;

        public string? uuid { get; set; } = string.Empty;
    }


    public class MarketInfo
    {

        public string? marinfo_vch_city { get; set; }
        public string? marinfo_vch_marketname { get; set; }

    }

    public class MarketRequest
    {
        public string? marketName { get; set; }
    }

    public class ConcessionInfo
    {
        public string? coninfo_vch_conname { get; set; }

        public string? coninfo_vch_dbconnectionstring { get; set; }

    }

    public class MarketConcessionResponse
    {
        public int MarketId { get; set; }
        public List<string> Concessions { get; set; } = new();
    }
    public class ItemResponse
    {
        public int ItemId { get; set; }

        public int CategoryId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public decimal ItemPrice { get; set; }
    }


    public class GetItemsRequest
    {
        public string? ConcessionName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string? UserName { get; set; } = string.Empty;
        public string? UserEmail { get; set; } = string.Empty;
    }

    public class GetItemsResponse
    {
        public int ConcessionId { get; set; }
        public List<ItemResponse> Items { get; set; } = new();
    }

    public class GetCategoriesRequest
    {
        public string? ConcessionName { get; set; } = string.Empty;

    }

    public class CategoryResponse
    {
        [JsonPropertyName("Category ID")]
        public int CategoryId { get; set; }

        [JsonPropertyName("Category Name")]
        public string CategoryName { get; set; } = string.Empty;
    }

    //public class SearchByItemRequest
    //{
    //    public string Keyword { get; set; } = string.Empty;
    //}

    //public class ConcessionSearchResult
    //{
    //    public string ConcessionName { get; set; } = string.Empty;
    //}

    public class SearchConcessionRequest
    {
        public string? Keyword { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        public int UserId { get; set; }

        public string? OldPassword { get; set; } = string.Empty;

        public string? NewPassword { get; set; } = string.Empty;
    }

    [Table("AppLoginDetail")]
    public class AppLoginDetail
    {

        [Key]
        public int apl_int_id { get; set; }

        public int apl_int_usrid { get; set; }

        public string? apl_vch_emailid { get; set; } = string.Empty;

        // 🔥 PASSWORD IS HERE (NOT SKIPPED)
        public string? apl_vch_password { get; set; } = string.Empty;

        public string? apl_vch_fcmtoken { get; set; }

        public string? apl_vch_providertoken { get; set; } = string.Empty;

        public bool apl_bit_loginstatus { get; set; }

        public DateTime apl_dt_logintime { get; set; }

        public DateTime? apl_dt_logouttime { get; set; }

        public string? apl_vch_phoneno { get; set; }

        public string? apl_vch_uuid { get; set; }


    }


    public class SocialLoginRequest
    {
        public string? Email { get; set; } = string.Empty;
        public string? Name { get; set; } = string.Empty;
        public string? Provider { get; set; } = string.Empty;   // google | facebook
        public string? ProviderToken { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; } = string.Empty;
        public string? FcmToken { get; set; } = string.Empty;
        public string? uuid { get; set; } = string.Empty;
    }

    public class SocialLoginResponse
    {
        public int user_id { get; set; }
        public string name { get; set; }
        public string emailid { get; set; }
        public int login_status { get; set; } // 1 = active, 0 = blocked
        public string message { get; set; }
    }

    public class ItemByCategoryResponse
    {
        public string? ItemName { get; set; } = string.Empty;
        public decimal ItemPrice { get; set; }
    }

    public class Item
    {
        [Key]
        public int itm_int_ItemID { get; set; }

        public string? itm_vch_ItemName { get; set; } = string.Empty;

        public decimal itm_mny_ItemPrice { get; set; }

        public int itm_int_CategoryID { get; set; }
    }

    public class GetItemsByCategoryRequest
    {
        public string? ConcessionName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
    }

    public class FoodModifierResponse
    {


        public int FoodModifierId { get; set; }

        public string? FoodModifierName { get; set; } = string.Empty;
    }


    public class GetFoodModifierRequest
    {
        public int ConcessionId { get; set; }   // coninfo_int_conid
    }

    public class SaveItemRequest
    {
        public int ConcessionId { get; set; }

        public int ItemId { get; set; }

        public int CustomerId { get; set; }

        public int CategoryId { get; set; }

        public string? ItemName { get; set; }
        public decimal? ItemPrice { get; set; }
    }
    public class SavedItemMarketResponse
    {
        public string? ConcessionName { get; set; } = string.Empty;

        public int ItemId { get; set; }
        public string? ItemName { get; set; } = string.Empty;
        public decimal ItemPrice { get; set; }
        public int CategoryId { get; set; }
    }



    /*  public class GetSavedItemsRequest
      {
          public int ConcessionId { get; set; }
          public int UserId { get; set; }
      }

      public class UserSavedItemsResponse
      {
          public string ConcessionName { get; set; } = string.Empty;
          public List<SavedItemDto> Items { get; set; } = new();
      }*/

    public class SavedItemDto
    {
        public int ItemId { get; set; }
        public int CategoryId { get; set; }
        public string? ItemName { get; set; }
        public decimal? ItemPrice { get; set; }
    }

    public class CreateOrderRequest
    {
        public int ConcessionId { get; set; }

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public int ItemId { get; set; }
        public string? ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal ItemPrice { get; set; }
    }

    public class ActiveOrderResponse
    {
        public string? ConcessionName { get; set; } = string.Empty;

        public int ItemId { get; set; }

        public string? ItemName { get; set; } = string.Empty;

        public decimal ItemPrice { get; set; }

        public int Quantity { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime OrderDate { get; set; }
    }

    public class DeleteOrderRequest
    {
        public int ConcessionId { get; set; }

        public int OrderNo { get; set; }

        public int ItemId { get; set; }

        public int customerId { get; set; }
    }

    public class ItemCategoryResponse
    {
        public int CategoryId { get; set; }

        public string? CategoryName { get; set; } = string.Empty;
    }


    public class ConcessionByItemResponse
    {
        public string? ConcessionName { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        public string? Email { get; set; } = string.Empty;

        public string? FcmToken { get; set; } = string.Empty;

        public string? uuid { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string? Name { get; set; } = string.Empty;
        public string? Email { get; set; } = string.Empty;
        public string? Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? FcmToken { get; set; } = string.Empty;

        public string? uuid { get; set; } = string.Empty;
    }

    public class ConcessionByCategoryResponse
    {
        public int ConcessionId { get; set; }

        public string ConcessionName { get; set; } = string.Empty;
    }

    public class UnsaveItemRequest
    {
        public int ConcessionId { get; set; }
        public int ItemId { get; set; }
        public int CustomerId { get; set; }
    }

    public class PaymentSettingsResponse
    {
        public string? TerminalId { get; set; }

        public string? XWebId { get; set; }

        public string? AuthId { get; set; }

        public string? PaymentUrl { get; set; }

        public string? QueryPaymentUrl { get; set; }
    }

    //public class ConfirmPaymentRequest
    //{
    //    public int ConcessionId { get; set; }
    //    public int OrderNumber { get; set; }
    //    public int CustomerId { get; set; }

    //    public int PaymentMode { get; set; }

    //    public string ResponseCode { get; set; } = string.Empty;

    //    public string? ResponseDescription { get; set; }

    //    public int OrderId { get; set; }
    //}

    public class ConfirmPaymentRequest
    {
        public int ConcessionId { get; set; }

        public int OrderNumber { get; set; }

        public int CustomerId { get; set; }

        public int PaymentMode { get; set; }

        public string ResponseCode { get; set; } = string.Empty;

        public string? ResponseDescription { get; set; }

        public string OrderId { get; set; } = string.Empty;
    }

    public class AddFoodModifierRequest
    {
        public int ConcessionId { get; set; }
        public int OrderNo { get; set; }
        public int CustomerId { get; set; }
        public int ItemId { get; set; }

        public int FoodModifierId { get; set; }
        public string? FoodModifierName { get; set; }
    }

    public class CustomerOrderResponse
    {
        public string ConcessionName { get; set; } = string.Empty;

        public long OrderNo { get; set; }

        public DateTime? OrderDate { get; set; }

        public decimal NetOrderAmount { get; set; }

        public string OrderStatus { get; set; } = string.Empty;
    }

    public class OrderItemModel
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public string ItemDescription { get; set; } = string.Empty;
    }

    public class OrderDetailsGroupedResponse
    {
        public long OrderNo { get; set; }

        public DateTime? OrderDate { get; set; }

        public decimal NetOrderAmount { get; set; }

        public decimal TotalItemAmount { get; set; }

        public List<OrderItemModel> Items { get; set; } = new();
    }

    public class PromotionResponse
    {
        public int ConcessionId { get; set; }
        public string ConcessionName { get; set; }

        public string PromotionId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string CategoryName { get; set; }

        public decimal ActualPrice { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal DiscountPrice { get; set; }
    }

    public class ComboResponse
    {
        public string ComboName { get; set; } = string.Empty;

        // 🔥 FIX: BIGINT → long
        public long ComboId { get; set; }

        public decimal Price { get; set; }
    }


    public class ComboDetailsResponse
    {
        public long ComboId { get; set; }
        public int StallId { get; set; }
        public string ComboName { get; set; } = string.Empty;
        public decimal Price { get; set; }

        public List<ComboItemModel> Items { get; set; } = new();
    }
   

    public class ComboItemModel
    {
        public string Category { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public long ItemId { get; set; }
        public int Priority { get; set; }
    }




}