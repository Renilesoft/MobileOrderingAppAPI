using Microsoft.AspNetCore.Mvc;
using ConcessionTrackerAPI.Models;
using ConcessionTrackerAPI.Repositories;
using System.Data.SqlClient;
using System.Text;

namespace ConcessionTrackerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        //private readonly IUserRepository _repo;
        //private readonly ILogger<UsersController> _logger;

        //public UsersController(IUserRepository repo, ILogger<UsersController> logger)
        //{
        //    _repo = repo;
        //    _logger = logger;
        //}

        //[HttpPost]
        //public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        //{
        //    if (request == null ||
        //        string.IsNullOrWhiteSpace(request.Name) ||
        //        string.IsNullOrWhiteSpace(request.Email) ||
        //        string.IsNullOrWhiteSpace(request.Password))
        //    {
        //        return BadRequest(new { message = "Name, Email and Password are required." });
        //    }

        //    var email = request.Email.Trim();

        //    try
        //    {

        //        if (await _repo.EmailExistsAsync(email))
        //        {
        //            return Conflict(new { message = "Email already exists." });
        //        }

        //        var passwordBytes = Encoding.UTF8.GetBytes(request.Password);
        //        var base64Password = Convert.ToBase64String(passwordBytes);

        //        var user = new CTUser
        //        {
        //            usr_vch_name = request.Name.Trim(),
        //            usr_vch_emailid = email,
        //            usr_vch_pswd = base64Password
        //        };

        //        var newId = await _repo.CreateUserAsync(user);

        //        if (newId > 0)
        //        {
        //            return Ok(new
        //            {
        //                message = "success",
        //                usr_vch_name = user.usr_vch_name,
        //                usr_vch_emailid = user.usr_vch_emailid
        //            });
        //        }
        //        else
        //        {
        //            return StatusCode(500, new { message = "Failed to insert data." });
        //        }
        //    }
        //    catch (SqlException sqlEx)
        //    {
        //        _logger.LogError(sqlEx, "SQL error while creating user.");
        //        return Conflict(new { message = "Email already exists." });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "General error while creating user.");
        //        return StatusCode(500, new { message = "Internal server error" });
        //    }
        //}

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginRequest request)
        //{
        //    if (request == null ||
        //        string.IsNullOrWhiteSpace(request.Email) ||
        //        string.IsNullOrWhiteSpace(request.Password))
        //    {
        //        return BadRequest(new { message = "Email and Password are required." });
        //    }

        //    var email = request.Email.Trim();

        //    try
        //    {
        //        var user = await _repo.GetUserByEmailAsync(email);
        //        if (user == null)
        //        {
        //            return Ok(new { message = "invalid" });
        //        }

        //        var incomingBytes = Encoding.UTF8.GetBytes(request.Password);
        //        var incomingBase64 = Convert.ToBase64String(incomingBytes);

        //        if (!string.Equals(incomingBase64, user.usr_vch_pswd, StringComparison.Ordinal))
        //        {
        //            return Ok(new { message = "invalid" });
        //        }

        //        return Ok(new
        //        {
        //            message = "success",
        //            usr_int_id = user.usr_int_id,
        //            usr_vch_name = user.usr_vch_name
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error during login for email {Email}", email);
        //        return StatusCode(500, new { message = "Internal server error" });
        //    }
        //}

        private readonly IUserRepository _repo;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserRepository repo, ILogger<UsersController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.FcmToken) ||
                string.IsNullOrWhiteSpace(request.uuid))
            {
                return BadRequest(new { message = "Invalid input." });
            }

            var user = new CTUser
            {
                usr_vch_name = request.Name,
                usr_vch_emailid = request.Email,
                usr_vch_pswd = request.Password,
                usr_vch_phoneno = request.PhoneNumber
            };

            var result = await _repo.CreateUserAsync(user, request.FcmToken, request.uuid);

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email and Password are required." });
            }

            var email = request.Email.Trim();

            try
            {
                var (user, message) = await _repo.ValidateUserCredentialsAsync(
                                                                        request.Email,
                                                                        request.Password,
                                                                        request.FcmToken,
                                                                        request.uuid);

                if (user == null)
                    return Ok(new { message });

                return Ok(new
                {
                    message = "success",
                    usr_int_id = user.usr_int_usrid,
                    usr_vch_name = user.usr_vch_name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for {Email}", email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }





        [HttpGet("markets")]
        public async Task<IActionResult> GetMarkets([FromQuery] string? city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest(new { message = "City is required as query parameter 'city'." });

            try
            {
                var markets = await _repo.GetMarketsByCityAsync(city.Trim());
                return Ok(markets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching markets for city {City}", city);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("concessions")]
        public async Task<IActionResult> GetConcessionsByMarket([FromQuery] string? marketName)
        {
            if (string.IsNullOrWhiteSpace(marketName))
                return BadRequest(new { message = "marketName query parameter is required." });

            var name = marketName.Trim();

            try
            {
                var result = await _repo.GetConcessionsByMarketAsync(name);

                if (result == null)
                    return NotFound(new { message = "No concessions found for this market." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching concessions for market {Market}", name);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


        //[HttpPost("items")]
        //public async Task<IActionResult> GetItemsByConcession([FromBody] GetItemsRequest request)
        //{
        //    if (request == null || string.IsNullOrWhiteSpace(request.ConcessionName))
        //        return BadRequest(new { message = "ConcessionName is required." });

        //    var concessionName = request.ConcessionName.Trim();

        //    try
        //    {
        //        var items = await _repo.GetItemsByConcessionAsync(concessionName);

        //        if (items == null)
        //            return NotFound(new { message = $"No concession found with name '{concessionName}'." });

        //        return Ok(items);
        //    }
        //    catch (InvalidOperationException ex) when (ex.Message.Contains("base64", StringComparison.OrdinalIgnoreCase) ||
        //                                                ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase))
        //    {
        //        _logger.LogWarning(ex, "Decryption error for concession {Concession}", concessionName);
        //        return BadRequest(new { message = "Unable to decrypt connection string; invalid format." });
        //    }
        //    catch (InvalidOperationException ex) when (ex.Message.Contains("query target concession", StringComparison.OrdinalIgnoreCase) ||
        //                                                ex.Message.Contains("concession database", StringComparison.OrdinalIgnoreCase))
        //    {
        //        _logger.LogError(ex, "Target DB query failed for concession {Concession}", concessionName);
        //        return StatusCode(500, new { message = "Failed to query concession database." });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Unexpected error while getting items for concession {Concession}", concessionName);
        //        return StatusCode(500, new { message = "Internal server error" });
        //    }
        //}


        [HttpPost("items")]
        public async Task<IActionResult> GetItemsByConcession([FromBody] GetItemsRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.ConcessionName) ||
                request.UserId <= 0 ||
                string.IsNullOrWhiteSpace(request.UserEmail))
            {
                return BadRequest(new { message = "Invalid request parameters." });
            }

            try
            {
                var response = await _repo.GetItemsByConcessionAsync(request);

                if (response == null)
                    return NotFound(new { message = "No concession found." });

                return Ok(new
                {
                    message = "Items fetched successfully.",
                    concessionId = response.ConcessionId,
                    items = response.Items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing concession request.");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }



        [HttpPost("categories")]
        public async Task<IActionResult> GetCategoriesByConcession([FromBody] GetCategoriesRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ConcessionName))
            {
                return BadRequest(new { message = "ConcessionName is required." });
            }

            var concessionName = request.ConcessionName.Trim();

            try
            {
                var categories = await _repo.GetCategoriesByConcessionAsync(concessionName);

                if (categories == null) // no connection string found
                    return NotFound(new { message = $"No concession found with name '{concessionName}'." });

                return Ok(categories);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase) ||
                                                        ex.Message.Contains("connection string", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "Decryption/validation error for concession {Concession}", concessionName);
                return BadRequest(new { message = "Unable to decrypt/validate connection string; invalid format." });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to connect/query", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(ex, "Target DB query failed for concession {Concession}", concessionName);
                return StatusCode(500, new { message = "Failed to query concession database." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories for concession {Concession}", concessionName);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        //[HttpPost("search-concessions-by-item")]
        //public async Task<IActionResult> SearchConcessionsByItem([FromBody] SearchByItemRequest request)
        //{
        //    if (request == null || string.IsNullOrWhiteSpace(request.Keyword))
        //    {
        //        return BadRequest(new { message = "Keyword is required." });
        //    }

        //    var keyword = request.Keyword.Trim();

        //    try
        //    {
        //        var results = await _repo.SearchConcessionsByItemKeywordAsync(keyword);

        //        // Return empty list if nothing matches; caller can handle "no results" case
        //        return Ok(results);
        //    }
        //    catch (ArgumentException ex)
        //    {
        //        _logger.LogWarning(ex, "Invalid keyword supplied.");
        //        return BadRequest(new { message = ex.Message });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error while searching concessions by item keyword {Keyword}", keyword);
        //        return StatusCode(500, new { message = "Internal server error" });
        //    }
        //}

        [HttpPost("search-concessions")]
        public async Task<IActionResult> SearchConcessionsByItemKeyword([FromBody] SearchConcessionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Keyword))
            {
                return BadRequest(new { message = "Keyword is required." });
            }

            var keyword = request.Keyword.Trim();

            try
            {
                var concessions = await _repo.SearchConcessionsByItemKeywordAsync(keyword);

                // You can choose: if none found, return empty list or 404.
                // Here we return empty list with 200 OK.
                return Ok(concessions);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Bad keyword for search.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while searching concessions by keyword {Keyword}", keyword);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "UserId, OldPassword and NewPassword are required." });

            if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "UserId , Old Password and New Password are required." });

            }

            try
            {
                var success = await _repo.ChangePasswordAsync(request.UserId, request.OldPassword, request.NewPassword);

                if (!success)
                {
                    return BadRequest(new { message = "Password change failed. Either user not found or old password is incorrect." });
                }

                return Ok(new { message = "success" });
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", request.UserId);
                return StatusCode(500, new { message = "Internal server error" });
            }

        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.FcmToken))
            {
                return BadRequest(new { message = "Email and FCM token are required." });
            }

            var result = await _repo.LogoutUserAsync(request.Email, request.FcmToken, request.uuid);

            if (!result)
                return Ok(new { message = "Invalid session or already logged out." });

            return Ok(new { message = "Logout successful." });
        }

        [HttpPost("social-login")]
        public async Task<IActionResult> SocialLogin([FromBody] SocialLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Provider) ||
                string.IsNullOrWhiteSpace(request.ProviderToken))
            {
                return BadRequest(new { message = "Invalid request" });
            }

            try
            {
                var response = await _repo.SocialLoginAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Social login failed");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("items-by-category")]
        public async Task<IActionResult> GetItemsByCategory([FromBody] GetItemsByCategoryRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.ConcessionName) ||
                request.CategoryId <= 0)
            {
                return BadRequest(new { message = "Invalid request parameters." });
            }

            try
            {
                var items = await _repo.GetItemsByCategoryAsync(request);

                if (items == null || items.Count == 0)
                    return NotFound(new { message = "No items found for this category." });

                return Ok(items);
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid concession connection string." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Target DB query failed for concession {Concession}", request.ConcessionName);
                return StatusCode(500, new { message = "Failed to query concession database." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching items.");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


        [HttpGet("food-modifiers/{concessionId}")]
        public async Task<IActionResult> GetFoodModifiers(int concessionId)
        {
            if (concessionId <= 0)
                return BadRequest(new { message = "Invalid concession ID." });

            try
            {
                var modifiers = await _repo.GetFoodModifiersAsync(concessionId);

                if (modifiers == null || modifiers.Count == 0)
                    return NotFound(new { message = "No food modifiers found." });

                return Ok(modifiers);
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid concession connection string." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Target DB query failed for concession ID {ConId}", concessionId);
                return StatusCode(500, new { message = "Failed to query concession database." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching food modifiers.");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("save-item")]
        public async Task<IActionResult> SaveItem([FromBody] SaveItemRequest request)
        {
            if (request == null || request.ConcessionId <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                var inserted = await _repo.SaveItemAsync(request);

                if (!inserted)
                    return Conflict(new { message = "Item already exists." });

                return Ok(new { message = "Item saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving item.");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("saved-items/{marketId}/{userId}")]
        public async Task<IActionResult> GetSavedItems(int marketId, int userId)
        {
            if (marketId <= 0 || userId <= 0)
                return BadRequest(new { message = "Invalid parameters." });

            try
            {
                var result = await _repo.GetSavedItemsByMarketAsync(marketId, userId);

                if (result.Count == 0)
                    return Ok(new { message = "No saved items found.", data = result });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching saved items.");
                return StatusCode(500, new { message = "Internal server error." });
            }
        }


        [HttpPost("create-order")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var orderNo = await _repo.CreateOrderAsync(request);

                if (orderNo == null)
                    return BadRequest(new { message = "Order creation failed." });

                return Ok(new
                {
                    message = "Order created successfully.",
                    orderNo = orderNo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order creation failed.");
                return StatusCode(500, new { message = "Internal server error." });
            }
        }

        [HttpGet("active-orders/{marketId}/{userId}")]
        public async Task<IActionResult> GetActiveOrders(int marketId, int userId)
        {
            if (marketId <= 0 || userId <= 0)
                return BadRequest(new { message = "Invalid parameters." });

            try
            {
                var result = await _repo.GetActiveOrdersAsync(marketId, userId);

                if (result.Count == 0)
                    return Ok(new { message = "No active orders found.", data = result });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active orders.");
                return StatusCode(500, new { message = "Internal server error." });
            }
        }


        [HttpDelete("delete-order")]
        public async Task<IActionResult> DeleteOrder([FromBody] DeleteOrderRequest request)
        {
            if (request == null || request.ConcessionId <= 0 || request.OrderNo <= 0)
                return BadRequest(new { message = "Invalid request." });

            try
            {
                var result = await _repo.DeleteOrderItemAsync(request.ConcessionId, request.OrderNo, request.ItemId, request.customerId);

                if (!result)
                    return NotFound(new { message = "Order not found." });

                return Ok(new { message = "Order deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order.");
                return StatusCode(500, new { message = "Internal server error." });
            }
        }

        [HttpGet("item-categories/{marketName}")]
        public async Task<IActionResult> GetItemCategories(string marketName)
        {
            if (string.IsNullOrWhiteSpace(marketName))
                return BadRequest(new { message = "Market name is required." });

            try
            {
                var result = await _repo.GetItemCategoriesByMarketAsync(marketName);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching item categories.");
                return StatusCode(500, new { message = "Internal server error." });
            }
        }


        [HttpGet("concessions-by-item/{itemId}")]
        public async Task<IActionResult> GetConcessionsByItem(int itemId)
        {
            if (itemId <= 0)
                return BadRequest(new { message = "Invalid ItemId." });

            try
            {
                var result = await _repo.GetConcessionsByItemIdAsync(itemId);

                if (result.Count == 0)
                    return Ok(new { message = "No concessions found for this item.", data = result });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching concessions by item.");
                return StatusCode(500, new { message = "Internal server error." });
            }
        }

        [HttpGet("concessions-by-category/{marketId}/{categoryId}")]
        public async Task<IActionResult> GetConcessionsByCategory(int marketId, int categoryId)
        {
            if (marketId <= 0 || categoryId <= 0)
                return BadRequest(new { message = "Invalid parameters." });

            var result = await _repo
                .GetConcessionsByMarketAndCategoryAsync(marketId, categoryId);

            if (result.Count == 0)
                return Ok(new { message = "No concessions found.", data = result });

            return Ok(result);
        }

        [HttpPost("unsave-item")]
        public async Task<IActionResult> UnsaveItem([FromBody] UnsaveItemRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Invalid request." });

            var result = await _repo.UnsaveItemAsync(request);

            if (!result)
                return Ok(new { message = "Item not found or already unsaved." });

            return Ok(new { message = "Item unsaved successfully." });
        }

        [HttpGet("edgexpress-details")]
        public async Task<IActionResult> GetPaymentSettings(int concessionId)
        {
            if (concessionId <= 0)
                return BadRequest(new { message = "Invalid concession id." });

            var result = await _repo.GetPaymentSettingsAsync(concessionId);

            if (result == null)
                return NotFound(new { message = "Payment settings not found." });

            return Ok(result);
        }

        //[HttpPost("confirm-payment")]
        //public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    var result = await _repo.ConfirmPaymentAsync(request);

        //    if (!result)
        //        return BadRequest("Payment confirmation failed");

        //    return Ok("Payment confirmed successfully");
        //}

        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            if (request == null)
                return BadRequest("Invalid request");

            try
            {
                var result = await _repo.ConfirmPaymentAsync(request);

                if (!result)
                    return BadRequest("Payment confirmation failed");

                return Ok(new
                {
                    message = "Payment confirmed successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("add-food-modifier")]
        public async Task<IActionResult> AddFoodModifier([FromBody] AddFoodModifierRequest request)
        {
            if (request == null)
                return BadRequest("Invalid request");

            var result = await _repo.AddFoodModifierAsync(request);

            if (!result)
                return BadRequest("Failed to add food modifier");

            return Ok(new
            {
                message = "Food modifier added successfully",
                orderNo = request.OrderNo,
                itemId = request.ItemId,
                modifierId = request.FoodModifierId
            });
        }

        [HttpGet("get-food-modifier-status")]
        public async Task<IActionResult> GetFoodModifierStatus(
    int concessionId,
    int orderNo,
    int customerId,
    int itemId,
    int foodModifierId)
        {
            var status = await _repo.GetFoodModifierStatusAsync(
                concessionId,
                orderNo,
                customerId,
                itemId,
                foodModifierId);

            if (status == null)
                return NotFound("Modifier not found.");

            return Ok(new
            {
                isModifierSelected = status
            });
        }

        [HttpDelete("remove-food-modifier")]
        public async Task<IActionResult> RemoveFoodModifier(int concessionId, int orderNo, int customerId, int itemId, int foodModifierId)
        {
            var result = await _repo.RemoveFoodModifierAsync(
                concessionId,
                orderNo,
                customerId,
                itemId,
                foodModifierId);

            if (!result)
                return BadRequest("Food modifier record not found.");

            return Ok(new
            {
                message = "Food modifier deleted successfully",
                orderNo,
                itemId,
                foodModifierId
            });
        }

        [HttpPost("confirm-free-order")]
        public async Task<IActionResult> ConfirmFreeOrder([FromBody] ConfirmPaymentRequest request)
        {
            if (request == null)
                return BadRequest("Invalid request");

            var result = await _repo.ConfirmFreeOrderAsync(request);

            if (!result)
                return BadRequest("Free order confirmation failed");

            return Ok(new
            {
                message = "Free order confirmed successfully"
            });
        }

        [HttpGet("getCustomerOrders")]
        public async Task<IActionResult> GetCustomerOrders(int marketId, int customerId)
        {
            try
            {
                var result = await _repo.GetCustomerOrdersAsync(marketId, customerId);

                if (result == null || result.Count == 0)
                    return Ok("No Orders Found");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer orders");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("getOrderDetails")]
        public async Task<IActionResult> GetOrderDetails(int concessionId, long orderNumber)
        {
            try
            {
                var result = await _repo.GetOrderDetailsAsync(concessionId, orderNumber);

                if (result == null)
                    return Ok("No Order Found");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching order details");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("promotions")]
        public async Task<IActionResult> GetPromotions(int marketId)
        {
            try
            {

                if (marketId <= 0)
                    return BadRequest(new { message = "Invalid marketId" });

                var result = await _repo.GetPromotionsByMarketAsync(marketId);


                if (result == null || result.Count == 0)
                {
                    return Ok(new
                    {
                        message = "No promotions found",
                        data = new List<object>()
                    });
                }


                return Ok(new
                {
                    message = "Promotions fetched successfully",
                    count = result.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {

                return StatusCode(500, new
                {
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }
        [HttpGet("getCombos")]
        public async Task<IActionResult> GetCombos(int concessionId)
        {
            try
            {
                var result = await _repo.GetCombosAsync(concessionId);

                if (result == null || result.Count == 0)
                    return Ok("No Combos Found");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching combos");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("getComboDetails")]
        public async Task<IActionResult> GetComboDetails(int concessionId, long comboId)
        {
            var result = await _repo.GetComboDetailsAsync(concessionId, comboId);

            if (result == null)
                return Ok("No Combo Found");

            return Ok(result);
        }

        [HttpGet("getUser")]
        public async Task<IActionResult> GetUser(int userId)
        {
            try
            {
                var result = await _repo.GetUserByIdAsync(userId);

                if (result == null)
                    return Ok("User Not Found");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPost("uploadUserImage")]
        public async Task<IActionResult> UploadUserImage(int userId, IFormFile file)
        {
            try
            {
                var success = await _repo.UploadUserImageAsync(userId, file);

                if (!success)
                    return BadRequest("Upload failed");

                return Ok("Image uploaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload error");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("getUserImage")]
        public async Task<IActionResult> GetUserImage(int userId)
        {
            try
            {
                var result = await _repo.GetUserImageAsync(userId);

                if (result == null)
                    return NotFound("No image found");

                // 🔹 GOOGLE / FACEBOOK IMAGE (URL)
                if (!string.IsNullOrWhiteSpace(result.ImageUrl))
                {
                    using var httpClient = new HttpClient();

                    var imageBytes = await httpClient.GetByteArrayAsync(result.ImageUrl);

                    return File(imageBytes, "image/jpeg");
                }

                // 🔹 BLOB IMAGE
                if (result.ImageBytes != null)
                {
                    return File(result.ImageBytes, result.ContentType);
                }

                return NotFound("No image found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fetch error");
                return StatusCode(500, "Internal Server Error");
            }
        }

        private bool IsValidUSPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            var digits = new string(phone.Where(char.IsDigit).ToArray());

            // Valid lengths: 10 or 11 (with country code)
            return digits.Length == 10 || (digits.Length == 11 && digits.StartsWith("1"));
        }

        private string NormalizePhone(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());

            if (digits.Length == 10)
                return "+1" + digits;

            if (digits.Length == 11 && digits.StartsWith("1"))
                return "+" + digits;

            return phone; // fallback
        }

        [HttpPost("updatePhone")]
        public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneRequest request)
        {
            try
            {
                if (request == null || request.UserId <= 0)
                    return BadRequest("Invalid request");

                // 🔒 Validate USA phone format
                if (!IsValidUSPhone(request.PhoneNumber))
                    return BadRequest("Invalid US phone number format");

                // 👉 Normalize phone (store clean)
                var normalizedPhone = NormalizePhone(request.PhoneNumber);

                var success = await _repo.UpdatePhoneNumberAsync(request.UserId, normalizedPhone);

                if (!success)
                    return NotFound("User not found");

                return Ok(new
                {
                    message = "Phone number updated successfully",
                    phone = normalizedPhone
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating phone");
                return StatusCode(500, "Internal Server Error");
            }
        }

    }

}
