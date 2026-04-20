
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConcessionTrackerAPI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace ConcessionTrackerAPI.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<UserRepository>? _logger;
        private readonly AppDbContext _db;

        public UserRepository(IConfiguration configuration, AppDbContext db, ILogger<UserRepository>? logger = null)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new ArgumentNullException("DefaultConnection");
            _logger = logger;

            _db = db;
        }


        //public async Task<bool> EmailExistsAsync(string email)
        //{
        //    const string sql = "SELECT COUNT(1) FROM Users WHERE usr_vch_emailid = @Email";
        //    await using var conn = new SqlConnection(_connectionString);
        //    await using var cmd = new SqlCommand(sql, conn);
        //    cmd.Parameters.AddWithValue("@Email", email);
        //    await conn.OpenAsync();
        //    var result = await cmd.ExecuteScalarAsync();
        //    return Convert.ToInt32(result) > 0;
        //}

        //public async Task<int> CreateUserAsync(CTUser user)
        //{
        //    const string sql = @"
        //        INSERT INTO Users (usr_vch_name, usr_vch_emailid, usr_vch_pswd)
        //        VALUES (@Name, @Email, @Password);
        //        SELECT SCOPE_IDENTITY();
        //    ";

        //    await using var conn = new SqlConnection(_connectionString);
        //    await using var cmd = new SqlCommand(sql, conn);
        //    cmd.Parameters.AddWithValue("@Name", user.usr_vch_name);
        //    cmd.Parameters.AddWithValue("@Email", user.usr_vch_emailid);
        //    cmd.Parameters.AddWithValue("@Password", user.usr_vch_pswd);

        //    await conn.OpenAsync();
        //    var scalar = await cmd.ExecuteScalarAsync();
        //    var id = Convert.ToInt32(Math.Round(Convert.ToDecimal(scalar)));
        //    return id;
        //}

        //public async Task<CTUser?> GetUserByEmailAsync(string email)
        //{
        //    const string sql = @"
        //        SELECT usr_int_id, usr_vch_name, usr_vch_emailid, usr_vch_pswd
        //        FROM Users
        //        WHERE usr_vch_emailid = @Email
        //    ";

        //    await using var conn = new SqlConnection(_connectionString);
        //    await using var cmd = new SqlCommand(sql, conn);
        //    cmd.Parameters.AddWithValue("@Email", email);

        //    await conn.OpenAsync();
        //    await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);

        //    if (!await reader.ReadAsync())
        //        return null;

        //    var user = new CTUser
        //    {
        //        usr_int_id = reader.GetInt32(reader.GetOrdinal("usr_int_id")),
        //        usr_vch_name = reader.GetString(reader.GetOrdinal("usr_vch_name")),
        //        usr_vch_emailid = reader.GetString(reader.GetOrdinal("usr_vch_emailid")),
        //        usr_vch_pswd = reader.GetString(reader.GetOrdinal("usr_vch_pswd"))
        //    };

        //    return user;
        //}

        public async Task<bool> EmailExistsAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            return await _db.Users.AnyAsync(u => u.usr_vch_emailid == email.Trim());
        }

        public async Task<object?> CreateUserAsync(CTUser user, string fcmToken, string uuid)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            // 🔹 CHECK DUPLICATE EMAIL
            var existingUser = await _db.Users
                .FirstOrDefaultAsync(x => x.usr_vch_emailid == user.usr_vch_emailid);

            if (existingUser != null)
            {
                return new
                {
                    message = "Email already registered"
                };
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var encryptedPassword = DecryptXOR(user.usr_vch_pswd ?? string.Empty);

                var newUser = new CTUser
                {
                    usr_vch_name = user.usr_vch_name,
                    usr_vch_emailid = user.usr_vch_emailid,
                    usr_vch_pswd = encryptedPassword,
                    usr_vch_phoneno = user.usr_vch_phoneno
                };

                _db.Users.Add(newUser);
                await _db.SaveChangesAsync();

                var loginDetail = new AppLoginDetail
                {
                    apl_int_usrid = newUser.usr_int_usrid,
                    apl_vch_emailid = user.usr_vch_emailid,
                    apl_vch_password = encryptedPassword,
                    apl_vch_fcmtoken = fcmToken,
                    apl_bit_loginstatus = true,
                    apl_dt_logintime = DateTime.Now,
                    apl_vch_phoneno = user.usr_vch_phoneno,
                    apl_vch_uuid = uuid
                };

                _db.AppLoginDetail.Add(loginDetail);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                return new
                {
                    userId = newUser.usr_int_usrid,
                    userName = newUser.usr_vch_name,
                    userPhoneNumber = newUser.usr_vch_phoneno,
                    userEmailId = newUser.usr_vch_emailid,
                    loginStatus = true
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CTUser?> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.usr_vch_emailid == email.Trim());
        }
        public async Task<(CTUser? user, string message)> ValidateUserCredentialsAsync(
            string email,
            string plainPassword,
            string fcmToken, string uuid)
        {
            if (string.IsNullOrWhiteSpace(email) || plainPassword == null)
                return (null, "Invalid input");

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.usr_vch_emailid == email.Trim());

            if (user == null)
                return (null, "Invalid credentials");

            var decryptedStored = DecryptXOR(user.usr_vch_pswd ?? string.Empty);

            if (decryptedStored != plainPassword)
                return (null, "Invalid credentials");

            var alreadyLoggedIn = await _db.AppLoginDetail
                .FirstOrDefaultAsync(a =>
                    a.apl_vch_emailid == email &&
                    a.apl_int_usrid == user.usr_int_usrid &&
                    a.apl_vch_fcmtoken == fcmToken &&
                    a.apl_bit_loginstatus == true);

            if (alreadyLoggedIn != null)
                return (null, "User already logged in on this device");

            var loginDetail = new AppLoginDetail
            {
                apl_vch_emailid = email,
                apl_vch_password = EncryptXOR(plainPassword),
                apl_vch_fcmtoken = fcmToken,
                apl_bit_loginstatus = true,
                apl_int_usrid = user.usr_int_usrid,
                apl_dt_logintime = DateTime.Now,
                apl_vch_uuid = uuid
            };

            _db.AppLoginDetail.Add(loginDetail);
            await _db.SaveChangesAsync();

            return (user, "success");
        }


        public string DecryptXOR(string input)
        {
            if (input == null) return "";
            return new string(input.Select(c => (char)(c ^ 30)).ToArray());
        }


        public async Task<List<MarketInfo>> GetMarketsByCityAsync(string city)
        {
            var result = new List<MarketInfo>();

            const string sql = @"
                SELECT marinfo_vch_marketname, marinfo_vch_city
                FROM MarketInfo
                WHERE marinfo_vch_city = @City
                ORDER BY marinfo_vch_marketname
            ";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@City", city);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

            while (await reader.ReadAsync())
            {
                var market = new MarketInfo
                {
                    marinfo_vch_marketname = reader.IsDBNull(reader.GetOrdinal("marinfo_vch_marketname"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("marinfo_vch_marketname")),
                    marinfo_vch_city = reader.IsDBNull(reader.GetOrdinal("marinfo_vch_city"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("marinfo_vch_city"))
                };
                result.Add(market);
            }

            return result;
        }

        public async Task<MarketConcessionResponse?> GetConcessionsByMarketAsync(string marketName)
        {
            var response = new MarketConcessionResponse();

            const string sql = @"
        SELECT 
            C.coninfo_vch_conname,
            M.marinfo_int_marketid
        FROM ConcessionInfo C
        INNER JOIN MarketInfo M 
            ON C.coninfo_int_marketid = M.marinfo_int_marketid
        WHERE M.marinfo_vch_marketname = @MarketName
        ORDER BY C.coninfo_vch_conname
    ";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@MarketName", SqlDbType.VarChar).Value = marketName ?? string.Empty;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

            bool marketIdSet = false;

            while (await reader.ReadAsync())
            {
                if (!marketIdSet)
                {
                    response.MarketId = reader.GetInt32(reader.GetOrdinal("marinfo_int_marketid"));
                    marketIdSet = true;
                }

                if (!reader.IsDBNull(reader.GetOrdinal("coninfo_vch_conname")))
                {
                    response.Concessions.Add(
                        reader.GetString(reader.GetOrdinal("coninfo_vch_conname"))
                    );
                }
            }

            if (!marketIdSet)
                return null;

            return response;
        }

        //public async Task<List<ItemResponse>?> GetItemsByConcessionAsync(string concessionName)
        //{
        //    if (string.IsNullOrWhiteSpace(concessionName))
        //        throw new ArgumentException("Concession name is required.", nameof(concessionName));


        //    const string connSql = @"
        //        SELECT coninfo_vch_dbconnectionstring 
        //        FROM ConcessionInfo 
        //        WHERE coninfo_var_conname = @ConName
        //    ";

        //    string? encryptedConnString;
        //    await using (var centralConn = new SqlConnection(_connectionString))
        //    await using (var connCmd = new SqlCommand(connSql, centralConn))
        //    {
        //        connCmd.Parameters.AddWithValue("@ConName", concessionName);
        //        await centralConn.OpenAsync();
        //        var scalar = await connCmd.ExecuteScalarAsync();
        //        encryptedConnString = scalar as string;
        //    }

        //    if (string.IsNullOrWhiteSpace(encryptedConnString))
        //        return null;

        //    string validatedConn;
        //    try
        //    {
        //        validatedConn = DecryptConnectionString(encryptedConnString);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new InvalidOperationException("Failed to obtain a valid connection string for the concession.", ex);
        //    }

        //    const string itemSql = @"
        //        SELECT itm_mny_ItemPrice, itm_vch_ItemDescription
        //        FROM items
        //    ";

        //    var items = new List<ItemResponse>();

        //    try
        //    {
        //        await using (var targetConn = new SqlConnection(validatedConn))
        //        await using (var itemCmd = new SqlCommand(itemSql, targetConn))
        //        {
        //            await targetConn.OpenAsync();
        //            await using var reader = await itemCmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        //            while (await reader.ReadAsync())
        //            {
        //                var priceOrdinal = reader.GetOrdinal("itm_mny_ItemPrice");
        //                var descOrdinal = reader.GetOrdinal("itm_vch_ItemDescription");

        //                var item = new ItemResponse
        //                {
        //                    ItemPrice = reader.IsDBNull(priceOrdinal) ? 0m : reader.GetDecimal(priceOrdinal),
        //                    ItemName = reader.IsDBNull(descOrdinal) ? string.Empty : reader.GetString(descOrdinal)
        //                };

        //                items.Add(item);
        //            }
        //        }
        //    }
        //    catch (SqlException sEx)
        //    {
        //        throw new InvalidOperationException("Failed to connect/query the concession database. See inner exception for details.", sEx);
        //    }

        //    return items;
        //}

        public async Task<GetItemsResponse?> GetItemsByConcessionAsync(GetItemsRequest request)
        {
            var concessionName = request.ConcessionName.Trim();

            const string connSql = @"
        SELECT coninfo_int_conid,
               coninfo_vch_dbconnectionstring 
        FROM ConcessionInfo 
        WHERE coninfo_vch_conname = @ConName
    ";

            string? encryptedConnString = null;
            int concessionId = 0;

            // 🔹 STEP 1 — Get Connection String
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConName", concessionName);

                await centralConn.OpenAsync();

                await using var reader = await connCmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    concessionId = reader.GetInt32(0);
                    encryptedConnString = reader.GetString(1);
                }
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return null;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            var items = new List<ItemResponse>();

            await using (var targetConn = new SqlConnection(validatedConn))
            {
                await targetConn.OpenAsync();

                // 🔹 STEP 2 — Ensure Customer Exists
                const string customerInsertSql = @"
            IF NOT EXISTS (
                SELECT 1 FROM Customer WHERE csmr_int_csid = @UserId
            )
            BEGIN
                INSERT INTO Customer
                (csmr_int_csid, csmr_vch_csname, csmr_vch_csemail)
                VALUES
                (@UserId, @UserName, @UserEmail)
            END
        ";

                await using (var custCmd = new SqlCommand(customerInsertSql, targetConn))
                {
                    custCmd.Parameters.AddWithValue("@UserId", request.UserId);
                    custCmd.Parameters.AddWithValue("@UserName", request.UserName ?? string.Empty);
                    custCmd.Parameters.AddWithValue("@UserEmail", request.UserEmail ?? string.Empty);

                    await custCmd.ExecuteNonQueryAsync();
                }

                // 🔹 STEP 3 — Fetch Items (UPDATED)
                const string itemSql = @"
            SELECT 
                itm_int_ItemID,
                itm_int_CategoryID,
                itm_mny_ItemPrice,
                itm_vch_ItemDescription,
                itm_int_ItemImageSize,
                itm_var_ItemImage
            FROM Items
            WHERE ISNULL(itm_bln_ListOnMobileApp, 0) = 1
        ";

                await using (var itemCmd = new SqlCommand(itemSql, targetConn))
                await using (var reader = await itemCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        byte[]? imageBytes = null;

                        if (!reader.IsDBNull(5))
                            imageBytes = (byte[])reader[5];

                        items.Add(new ItemResponse
                        {
                            ItemId = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]),
                            CategoryId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]),
                            ItemPrice = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader[2]),
                            ItemName = reader.IsDBNull(3) ? string.Empty : reader[3].ToString()!,

                            ImageSize = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]),

                            // 🔥 Convert to Base64
                            ImageBase64 = imageBytes != null
                                ? Convert.ToBase64String(imageBytes)
                                : string.Empty
                        });
                    }
                }
            }

            return new GetItemsResponse
            {
                ConcessionId = concessionId,
                Items = items
            };
        }


        public async Task<List<CategoryResponse>?> GetCategoriesByConcessionAsync(string concessionName)
        {
            if (string.IsNullOrWhiteSpace(concessionName))
                throw new ArgumentException("Concession name is required.", nameof(concessionName));

            const string connSql = @"
                SELECT coninfo_vch_dbconnectionstring 
                FROM ConcessionInfo 
                WHERE coninfo_vch_conname = @ConName
            ";

            string? encryptedConnString;
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConName", concessionName);
                await centralConn.OpenAsync();
                var scalar = await connCmd.ExecuteScalarAsync();
                encryptedConnString = scalar as string;
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return null;

            string validatedConn;
            try
            {
                validatedConn = DecryptConnectionString(encryptedConnString);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to obtain a valid connection string for the concession.", ex);
            }

            const string catSql = @"
                SELECT cat_vch_CategoryName, cat_int_CategoryID
                FROM ItemCategory
            ";

            var categories = new List<CategoryResponse>();

            try
            {
                await using (var targetConn = new SqlConnection(validatedConn))
                await using (var catCmd = new SqlCommand(catSql, targetConn))
                {
                    await targetConn.OpenAsync();
                    await using var reader = await catCmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

                    while (await reader.ReadAsync())
                    {
                        var nameOrdinal = reader.GetOrdinal("cat_vch_CategoryName");
                        var idOrdinal = reader.GetOrdinal("cat_int_CategoryID");

                        var cat = new CategoryResponse
                        {
                            CategoryId = reader.IsDBNull(idOrdinal) ? 0 : reader.GetInt32(idOrdinal),
                            CategoryName = reader.IsDBNull(nameOrdinal) ? string.Empty : reader.GetString(nameOrdinal)
                        };

                        categories.Add(cat);
                    }
                }
            }
            catch (SqlException sEx)
            {
                throw new InvalidOperationException("Failed to connect/query the concession database for categories. See inner exception for details.", sEx);
            }

            return categories;
        }

        private string DecryptConnectionString(string encrypted)
        {
            if (string.IsNullOrWhiteSpace(encrypted))
                throw new ArgumentException("Encrypted connection string is empty.");


            static string Clean(string s)
            {
                if (s == null) return string.Empty;
                var t = s.Trim();
                if ((t.StartsWith('"') && t.EndsWith('"')) || (t.StartsWith('\'') && t.EndsWith('\'')))
                    t = t.Substring(1, t.Length - 2);
                t = t.Replace("\0", string.Empty);
                t = t.Replace(@"\\", @"\");
                return t.Trim();
            }

            var attempts = new List<string>();


            try
            {
                var base64Ascii = DecryptBase64(encrypted);
                attempts.Add(Clean(DecryptXOR(base64Ascii)));
                attempts.Add(Clean(base64Ascii));
            }
            catch { /* ignore base64 failures here */ }


            try
            {
                var base64Utf8 = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encrypted));
                attempts.Add(Clean(DecryptXOR(base64Utf8)));
                attempts.Add(Clean(base64Utf8));
            }
            catch { }


            try
            {
                attempts.Add(Clean(DecryptXOR(encrypted)));
            }
            catch { }


            attempts.Add(Clean(encrypted));


            var candidates = attempts
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();


            var validCandidates = new List<string>();
            foreach (var candidate in candidates)
            {
                try
                {
                    var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(candidate);

                    if (!string.IsNullOrWhiteSpace(builder.DataSource) || !string.IsNullOrWhiteSpace(builder.InitialCatalog))
                        validCandidates.Add(candidate);
                }
                catch
                {

                }
            }

            if (validCandidates.Count == 0)
            {
                var preview = candidates
                    .Select(c => (c.Length <= 200 ? c : c.Substring(0, 200) + "..."))
                    .Take(5);
                var message = $"Decryption produced no valid connection string variants. Tried {candidates.Count} candidates. Examples: {string.Join(" | ", preview)}";
                _logger?.LogError(message);
                throw new InvalidOperationException(message);
            }

            var chosen = validCandidates.First();


            _logger?.LogInformation("Decrypted concession connection string (redacted): {Conn}", RedactConnectionString(chosen));

            return chosen;
        }

        public string DecryptBase64(string encryptedString)
        {
            return System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(encryptedString));
        }

        private static string RedactConnectionString(string conn)
        {
            if (string.IsNullOrWhiteSpace(conn)) return string.Empty;
            try
            {
                var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(conn);
                if (builder.ContainsKey("Password")) builder.Password = "*****";
                if (builder.ContainsKey("User ID")) builder.UserID = "*****";
                return builder.ToString();
            }
            catch
            {
                var lowered = conn.ToLowerInvariant();
                var idx = lowered.IndexOf("password=", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var cut = conn.Substring(0, idx);
                    return cut + "password=*****";
                }
                return conn.Length > 200 ? conn.Substring(0, 200) + "..." : conn;
            }
        }

        //public async Task<List<ConcessionSearchResult>> SearchConcessionsByItemKeywordAsync(string keyword)
        //{
        //    if (string.IsNullOrWhiteSpace(keyword))
        //        throw new ArgumentException("Keyword is required.", nameof(keyword));

        //    var results = new List<ConcessionSearchResult>();


        //    const string centralSql = @"
        //        SELECT coninfo_var_conname, coninfo_vch_dbconnectionstring
        //        FROM ConcessionInfo
        //    ";

        //    var concessions = new List<(string Name, string EncConn)>();

        //    await using (var centralConn = new SqlConnection(_connectionString))
        //    await using (var cmd = new SqlCommand(centralSql, centralConn))
        //    {
        //        await centralConn.OpenAsync();
        //        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        //        while (await reader.ReadAsync())
        //        {
        //            var nameOrdinal = reader.GetOrdinal("coninfo_var_conname");
        //            var connOrdinal = reader.GetOrdinal("coninfo_vch_dbconnectionstring");

        //            if (reader.IsDBNull(nameOrdinal) || reader.IsDBNull(connOrdinal))
        //                continue;

        //            var name = reader.GetString(nameOrdinal);
        //            var enc = reader.GetString(connOrdinal);

        //            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(enc))
        //            {
        //                concessions.Add((name, enc));
        //            }
        //        }
        //    }

        //    if (concessions.Count == 0)
        //        return results;


        //    var likePattern = $"%{keyword.Trim()}%";


        //    foreach (var concession in concessions)
        //    {
        //        string decryptedConn;
        //        try
        //        {
        //            decryptedConn = DecryptConnectionString(concession.EncConn);
        //        }
        //        catch (Exception ex)
        //        {

        //            _logger?.LogWarning(ex,
        //                "Skipping concession {Concession} due to connection string decryption/validation error.",
        //                concession.Name);
        //            continue;
        //        }

        //        try
        //        {
        //            await using var targetConn = new SqlConnection(decryptedConn);
        //            await using var searchCmd = new SqlCommand(@"
        //                SELECT TOP 1 itm_vch_ItemName, itm_vch_ItemDescription
        //                FROM Items
        //                WHERE itm_vch_ItemName LIKE @Keyword
        //            ", targetConn);

        //            searchCmd.Parameters.AddWithValue("@Keyword", likePattern);

        //            await targetConn.OpenAsync();
        //            await using var reader = await searchCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);


        //            if (await reader.ReadAsync())
        //            {
        //                results.Add(new ConcessionSearchResult
        //                {
        //                    ConcessionName = concession.Name
        //                });
        //            }
        //        }
        //        catch (SqlException ex)
        //        {

        //            _logger?.LogWarning(ex,
        //                "Skipping concession {Concession} due to SQL error while searching items.",
        //                concession.Name);
        //            continue;
        //        }
        //    }

        //    return results;
        //}



        public async Task<List<string>> SearchConcessionsByItemKeywordAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                throw new ArgumentException("Keyword is required.", nameof(keyword));

            var concessions = await _db.ConcessionInfo
                .Select(c => new
                {
                    Name = c.coninfo_vch_conname,
                    EncConn = c.coninfo_vch_dbconnectionstring
                })
                .ToListAsync();

            var result = new List<string>();
            var pattern = "%" + keyword + "%";

            foreach (var c in concessions)
            {
                if (string.IsNullOrWhiteSpace(c.Name) ||
                    string.IsNullOrWhiteSpace(c.EncConn))
                {
                    continue;
                }

                string validatedConn;
                try
                {
                    validatedConn = DecryptConnectionString(c.EncConn);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex,
                        "Skipping concession {Concession} due to decryption error.",
                        c.Name);
                    continue;
                }

                const string sql = @"
                                            SELECT TOP 1 itm_vch_ItemName, itm_vch_ItemDescription
                                            FROM Items
                                            WHERE itm_vch_ItemName LIKE @Pattern
                     ";

                try
                {
                    await using var conn = new SqlConnection(validatedConn);
                    await using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Pattern", pattern);

                    await conn.OpenAsync();
                    await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);

                    if (await reader.ReadAsync())
                    {
                        result.Add(c.Name!);
                    }
                }
                catch (SqlException ex)
                {
                    _logger?.LogError(ex,
                        "Error querying Items table for concession {Concession}",
                        c.Name);
                }
            }

            return result
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<CTUser?> ValidateUserLoginAsync(string email, string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(plainPassword))
                return null;

            var normalizedEmail = email.Trim();

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.usr_vch_emailid == normalizedEmail);

            if (user == null)
                return null;


            var storedDecrypted = DecryptXOR(user.usr_vch_pswd ?? string.Empty);

            if (string.Equals(storedDecrypted, plainPassword, StringComparison.Ordinal))
            {
                return user;
            }

            return null;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {

            if (userId <= 0) return false;
            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
                return false;

            var user = await _db.Users.FirstOrDefaultAsync(u => u.usr_int_usrid == userId);

            if (user == null) return false;

            var storedDecrypted = DecryptXOR(user.usr_vch_pswd ?? string.Empty);

            if (!string.Equals(storedDecrypted, oldPassword, StringComparison.Ordinal))
            {
                return false;
            }

            user.usr_vch_pswd = EncryptXOR(newPassword);

            try
            {
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                _logger?.LogError(dbEx, "DB update error while changing password for user {UserId}", userId);
                throw;
            }
        }

        public string EncryptXOR(string input)
        {
            if (input == null) return string.Empty;
            return new string(input.Select(c => (char)(c ^ 30)).ToArray());
        }

        public async Task LogAppLoginAsync(
            int userId,
            string email,
            string password,
            string fcmToken)
        {
            var login = new AppLoginDetail
            {
                apl_int_usrid = userId,
                apl_vch_emailid = email,
                apl_vch_password = password,
                apl_vch_fcmtoken = string.IsNullOrWhiteSpace(fcmToken) ? null : fcmToken,
                apl_bit_loginstatus = true,
                apl_dt_logintime = DateTime.Now,
                apl_dt_logouttime = null
            };

            _db.AppLoginDetail.Add(login);
            await _db.SaveChangesAsync();
        }


        public async Task<bool> LogoutUserAsync(string email, string fcmToken, string uuid)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fcmToken))
                return false;

            // 🔹 Find active login for THIS email + THIS FCM token
            var login = await _db.AppLoginDetail
                .FirstOrDefaultAsync(x =>
                    x.apl_vch_emailid == email &&
                    x.apl_vch_fcmtoken == fcmToken &&
                    x.apl_vch_uuid == uuid &&
                    x.apl_bit_loginstatus == true);

            if (login == null)
                return false;

            // 🔹 Update logout details
            login.apl_dt_logouttime = DateTime.Now;
            login.apl_bit_loginstatus = false;

            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<object> SocialLoginAsync(SocialLoginRequest request)
        {
            // 1️⃣ Find user by email (email is unique)
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.usr_vch_emailid == request.Email);

            if (user == null)
            {
                user = new CTUser
                {
                    usr_vch_emailid = request.Email,
                    usr_vch_name = request.Name,
                    usr_vch_provider = request.Provider,
                    usr_vch_photo_url = request.PhotoUrl
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            // 2️⃣ Check AppLoginDetail table
            var existingLogin = await _db.AppLoginDetail
                .Where(x =>
                    x.apl_int_usrid == user.usr_int_usrid &&
                    x.apl_vch_fcmtoken == request.FcmToken)
                .OrderByDescending(x => x.apl_dt_logintime)
                .FirstOrDefaultAsync();

            // 🚫 BLOCK LOGIN
            if (existingLogin != null && existingLogin.apl_bit_loginstatus == true)
            {
                return new
                {
                    message = "A login for this device already exists."
                };
            }

            // 3️⃣ Allow login
            var newLogin = new AppLoginDetail
            {
                apl_int_usrid = user.usr_int_usrid,
                apl_vch_emailid = request.Email,
                apl_vch_fcmtoken = request.FcmToken,
                apl_vch_providertoken = request.ProviderToken,
                apl_bit_loginstatus = true,
                apl_dt_logintime = DateTime.Now,
                apl_dt_logouttime = null,
                apl_vch_uuid = request.uuid
            };

            _db.AppLoginDetail.Add(newLogin);
            await _db.SaveChangesAsync();

            return new
            {
                user_id = user.usr_int_usrid,
                name = user.usr_vch_name,
                emailid = user.usr_vch_emailid,
                login_status = 1,
                message = "Login successful."
            };
        }

        public async Task<List<ItemResponse>?> GetItemsByCategoryAsync(GetItemsByCategoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ConcessionName))
                throw new ArgumentException("Concession name is required.");

            // 🔹 STEP 1 — Get encrypted connection string from CTUsers DB
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring 
        FROM ConcessionInfo 
        WHERE coninfo_vch_conname = @ConName
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConName", request.ConcessionName.Trim());

                await centralConn.OpenAsync();
                encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return null;

            // 🔹 STEP 2 — Decrypt connection string
            string validatedConn;
            try
            {
                validatedConn = DecryptConnectionString(encryptedConnString);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decrypt connection string.", ex);
            }

            // 🔹 STEP 3 — Query Items table in ConcessionTracker DB
            const string itemSql = @"
            SELECT 
                itm_int_ItemID,
                itm_int_CategoryID,
                itm_vch_ItemName,
                itm_mny_ItemPrice
            FROM Items
            WHERE itm_int_CategoryID = @CategoryId
        ";

            var items = new List<ItemResponse>();

            try
            {
                await using (var targetConn = new SqlConnection(validatedConn))
                await using (var itemCmd = new SqlCommand(itemSql, targetConn))
                {
                    itemCmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);

                    await targetConn.OpenAsync();

                    await using var reader = await itemCmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

                    while (await reader.ReadAsync())
                    {
                        items.Add(new ItemResponse
                        {
                            ItemId = reader["itm_int_ItemID"] != DBNull.Value
                                ? Convert.ToInt32(reader["itm_int_ItemID"])
                                : 0,

                            CategoryId = reader["itm_int_CategoryID"] != DBNull.Value
                                ? Convert.ToInt32(reader["itm_int_CategoryID"])
                                : 0,

                            ItemName = reader["itm_vch_ItemName"] != DBNull.Value
                                ? reader["itm_vch_ItemName"].ToString()!
                                : string.Empty,

                            ItemPrice = reader["itm_mny_ItemPrice"] != DBNull.Value
                                ? Convert.ToDecimal(reader["itm_mny_ItemPrice"])
                                : 0
                        });
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to query Items table from concession database.", ex);
            }

            return items;
        }


        public async Task<List<FoodModifierResponse>?> GetFoodModifiersAsync(int concessionId)
        {
            if (concessionId <= 0)
                throw new ArgumentException("Invalid concession ID.");

            // 🔹 STEP 1 — Get encrypted connection string using coninfo_int_conid
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring 
        FROM ConcessionInfo 
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", concessionId);

                await centralConn.OpenAsync();
                encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return null;

            // 🔹 STEP 2 — Decrypt connection string
            string validatedConn;

            try
            {
                validatedConn = DecryptConnectionString(encryptedConnString);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decrypt connection string.", ex);
            }

            // 🔹 STEP 3 — Fetch Food Modifiers from target DB
            const string foodSql = @"
        SELECT fmd_seq_FoodModifierID, fmd_vch_FoodModifier
        FROM FoodModifiers
    ";

            var modifiers = new List<FoodModifierResponse>();

            try
            {
                await using (var targetConn = new SqlConnection(validatedConn))
                await using (var cmd = new SqlCommand(foodSql, targetConn))
                {
                    await targetConn.OpenAsync();

                    await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

                    while (await reader.ReadAsync())
                    {
                        modifiers.Add(new FoodModifierResponse
                        {
                            FoodModifierId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            FoodModifierName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                        });
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to query FoodModifiers table.", ex);
            }

            return modifiers;
        }

        public async Task<bool> SaveItemAsync(SaveItemRequest request)
        {
            if (request == null || request.ConcessionId <= 0)
                return false;

            // 🔹 STEP 1 — Get encrypted connection string
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", request.ConcessionId);

                await centralConn.OpenAsync();
                encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return false;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            await using var targetConn = new SqlConnection(validatedConn);
            await targetConn.OpenAsync();

            await using var transaction = await targetConn.BeginTransactionAsync();

            try
            {
                // 🔹 STEP 2 — Check if item already exists for THIS user
                const string checkSql = @"
            SELECT COUNT(1)
            FROM SavedItems
            WHERE svitm_int_ItemId = @ItemId
            AND svitm_int_csid = @CustomerId
        ";

                await using var checkCmd = new SqlCommand(checkSql, targetConn, (SqlTransaction)transaction);

                checkCmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                checkCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (count > 0)
                {
                    await transaction.RollbackAsync();
                    return false; // Already saved for this user
                }

                // 🔹 STEP 3 — Insert into SavedItems
                const string insertSql = @"
            INSERT INTO SavedItems
            (
                svitm_int_ItemId,
                svitm_int_csid,
                svitm_int_CategoryId,
                svitm_vch_ItemName,
                svitm_mny_ItemPrice
            )
            VALUES
            (
                @ItemId,
                @CustomerId,
                @CategoryId,
                @ItemName,
                @ItemPrice
            )
        ";

                await using var insertCmd = new SqlCommand(insertSql, targetConn, (SqlTransaction)transaction);

                insertCmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                insertCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                insertCmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
                insertCmd.Parameters.AddWithValue("@ItemName", request.ItemName ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@ItemPrice", request.ItemPrice ?? (object)DBNull.Value);

                await insertCmd.ExecuteNonQueryAsync();

                // 🔹 STEP 4 — Update Items table
                const string updateItemSql = @"
            UPDATE Items
            SET itm_bit_isSavedItem = 1
            WHERE itm_int_ItemID = @ItemId
        ";

                await using var updateCmd = new SqlCommand(updateItemSql, targetConn, (SqlTransaction)transaction);

                updateCmd.Parameters.AddWithValue("@ItemId", request.ItemId);

                await updateCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<SavedItemMarketResponse>> GetSavedItemsByMarketAsync(int marketId, int userId)
        {
            var finalResult = new List<SavedItemMarketResponse>();

            // 🔹 STEP 1 — Fetch concessions from CTUsers DB
            const string concessionSql = @"
        SELECT coninfo_vch_conname, coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_marketid = @MarketId
    ";

            var concessions = new List<(string Name, string ConnStr)>();

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(concessionSql, centralConn))
            {
                cmd.Parameters.AddWithValue("@MarketId", marketId);
                await centralConn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                    {
                        concessions.Add((
                            reader.GetString(0),
                            reader.GetString(1)
                        ));
                    }
                }
            }

            if (concessions.Count == 0)
                return finalResult;

            // 🔹 STEP 2 — Loop each concession
            foreach (var concession in concessions)
            {
                try
                {
                    string validatedConn = DecryptConnectionString(concession.ConnStr);

                    await using var targetConn = new SqlConnection(validatedConn);
                    await targetConn.OpenAsync();

                    // 🔹 STEP 3 — Check if customer exists
                    const string customerCheckSql = @"
                SELECT COUNT(1)
                FROM Customer
                WHERE csmr_int_csid = @UserId
            ";

                    await using var checkCmd = new SqlCommand(customerCheckSql, targetConn);
                    checkCmd.Parameters.AddWithValue("@UserId", userId);

                    var exists = (int)await checkCmd.ExecuteScalarAsync();

                    if (exists == 0)
                        continue; // Skip this DB

                    // 🔹 STEP 4 — Fetch saved items for that user
                    const string savedItemsSql = @"
                SELECT 
                    S.svitm_int_ItemId,
                    S.svitm_vch_ItemName,
                    S.svitm_mny_ItemPrice,
                    S.svitm_int_CategoryId
                FROM SavedItems S
                INNER JOIN Customer C
                    ON S.svitm_int_csid = C.csmr_int_csid
                WHERE C.csmr_int_csid = @UserId
            ";

                    await using var savedCmd = new SqlCommand(savedItemsSql, targetConn);
                    savedCmd.Parameters.AddWithValue("@UserId", userId);

                    await using var reader = await savedCmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        finalResult.Add(new SavedItemMarketResponse
                        {
                            ConcessionName = concession.Name,
                            ItemId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            ItemName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            ItemPrice = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                            CategoryId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                        });
                    }
                }
                catch
                {
                    // 🔥 DO NOT crash entire API if one concession fails
                    continue;
                }
            }

            return finalResult;
        }

        public async Task<int?> CreateOrderAsync(CreateOrderRequest request)
        {
            if (request == null || request.ConcessionId <= 0 || request.Items == null || request.Items.Count == 0)
                return null;

            // 🔹 STEP 1 — Get encrypted concession connection string
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", request.ConcessionId);
                await centralConn.OpenAsync();
                encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return null;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            await using var targetConn = new SqlConnection(validatedConn);
            await targetConn.OpenAsync();
            await using var transaction = await targetConn.BeginTransactionAsync();

            try
            {
                int orderNo;

                // 🔹 STEP 2 — Check for active order for this customer
                const string activeOrderSql = @"
            SELECT TOP 1 cros_seq_OrderNo
            FROM CustomerOrderSummary
            WHERE cros_bit_OrderStatus = 0
            AND cros_int_csid = @CustomerId
            ORDER BY cros_seq_OrderNo DESC
        ";

                await using (var activeCmd = new SqlCommand(activeOrderSql, targetConn, (SqlTransaction)transaction))
                {
                    activeCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                    var activeResult = await activeCmd.ExecuteScalarAsync();

                    if (activeResult != null && activeResult != DBNull.Value)
                    {
                        orderNo = Convert.ToInt32(activeResult);
                    }
                    else
                    {
                        // 🔹 Create new order summary WITH CustomerId
                        const string insertSummarySql = @"
                    INSERT INTO CustomerOrderSummary
                    (
                        cros_int_csid,
                        cros_mny_TotalItemAmount,
                        cros_mny_NetOrderAmount,
                        cros_bit_OrderStatus
                    )
                    VALUES
                    (
                        @CustomerId,
                        0,
                        0,
                        0
                    );

                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                ";

                        await using var summaryCmd =
                            new SqlCommand(insertSummarySql, targetConn, (SqlTransaction)transaction);

                        summaryCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                        orderNo = Convert.ToInt32(await summaryCmd.ExecuteScalarAsync());
                    }
                }

                // 🔹 STEP 3 — Insert or Update Items
                foreach (var item in request.Items)
                {
                    const string checkItemSql = @"
                SELECT croi_int_ItemQuantity
                FROM CustomerOrderItem
                WHERE croi_int_OrderNo = @OrderNo
                AND croi_int_ItemId = @ItemId
                AND croi_int_csid = @CustomerId
            ";

                    await using var checkCmd =
                        new SqlCommand(checkItemSql, targetConn, (SqlTransaction)transaction);

                    checkCmd.Parameters.AddWithValue("@OrderNo", orderNo);
                    checkCmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                    checkCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                    var existingQtyObj = await checkCmd.ExecuteScalarAsync();

                    if (existingQtyObj != null && existingQtyObj != DBNull.Value)
                    {
                        const string updateItemSql = @"
                    UPDATE CustomerOrderItem
                    SET croi_int_ItemQuantity = croi_int_ItemQuantity + @Quantity
                    WHERE croi_int_OrderNo = @OrderNo
                    AND croi_int_ItemId = @ItemId
                    AND croi_int_csid = @CustomerId
                ";

                        await using var updateCmd =
                            new SqlCommand(updateItemSql, targetConn, (SqlTransaction)transaction);

                        updateCmd.Parameters.AddWithValue("@OrderNo", orderNo);
                        updateCmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                        updateCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                        updateCmd.Parameters.AddWithValue("@Quantity", item.Quantity);

                        await updateCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        const string insertItemSql = @"
                    INSERT INTO CustomerOrderItem
                    (
                        croi_int_OrderNo,
                        croi_date_OrderDate,
                        croi_int_csid,
                        croi_vch_csname,
                        croi_int_ItemId,
                        croi_vch_ItemName,
                        croi_int_ItemQuantity,
                        croi_mny_ItemPrice,
                        croi_bit_OrderStatus,
                        croi_bit_paymentstatus
                    )
                    VALUES
                    (
                        @OrderNo,
                        @OrderDate,
                        @CustomerId,
                        @CustomerName,
                        @ItemId,
                        @ItemName,
                        @Quantity,
                        @ItemPrice,
                        0,
                        0
                    )
                ";

                        await using var insertCmd =
                            new SqlCommand(insertItemSql, targetConn, (SqlTransaction)transaction);

                        insertCmd.Parameters.AddWithValue("@OrderNo", orderNo);
                        insertCmd.Parameters.AddWithValue("@OrderDate", DateTime.Now);
                        insertCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                        insertCmd.Parameters.AddWithValue("@CustomerName", request.CustomerName ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                        insertCmd.Parameters.AddWithValue("@ItemName", item.ItemName ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                        insertCmd.Parameters.AddWithValue("@ItemPrice", item.ItemPrice);

                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }

                // 🔹 STEP 4 — Recalculate totals
                const string updateTotalSql = @"
            UPDATE CustomerOrderSummary
            SET 
                cros_mny_TotalItemAmount =
                    ISNULL(
                        (SELECT SUM(croi_mny_ItemPrice * croi_int_ItemQuantity)
                         FROM CustomerOrderItem
                         WHERE croi_int_OrderNo = @OrderNo),
                        0
                    ),
                cros_mny_NetOrderAmount =
                    ISNULL(
                        (SELECT SUM(croi_mny_ItemPrice * croi_int_ItemQuantity)
                         FROM CustomerOrderItem
                         WHERE croi_int_OrderNo = @OrderNo),
                        0
                    )
            WHERE cros_seq_OrderNo = @OrderNo
        ";

                await using var totalCmd =
                    new SqlCommand(updateTotalSql, targetConn, (SqlTransaction)transaction);

                totalCmd.Parameters.AddWithValue("@OrderNo", orderNo);

                await totalCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                return orderNo;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<ActiveOrderResponse>> GetActiveOrdersAsync(int marketId, int userId)
        {
            var finalResult = new List<ActiveOrderResponse>();

            const string concessionSql = @"
        SELECT coninfo_vch_conname, coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_marketid = @MarketId
    ";

            var concessions = new List<(string Name, string ConnStr)>();

            // 🔹 STEP 1 — Fetch concessions
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(concessionSql, centralConn))
            {
                cmd.Parameters.AddWithValue("@MarketId", marketId);
                await centralConn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                    {
                        concessions.Add((
                            reader.GetString(0),
                            reader.GetString(1)
                        ));
                    }
                }
            }

            // 🔹 STEP 2 — Loop each concession DB
            foreach (var concession in concessions)
            {
                try
                {
                    string validatedConn = DecryptConnectionString(concession.ConnStr);

                    await using var targetConn = new SqlConnection(validatedConn);
                    await targetConn.OpenAsync();

                    const string orderSql = @"
                SELECT 
                    O.croi_int_ItemId,
                    O.croi_vch_ItemName,
                    O.croi_mny_ItemPrice,
                    O.croi_int_ItemQuantity,
                    S.cros_mny_TotalItemAmount,
                    S.cros_dt_OrderDate
                FROM CustomerOrderItem O
                INNER JOIN Customer C 
                    ON O.croi_int_csid = C.csmr_int_csid
                INNER JOIN CustomerOrderSummary S 
                    ON S.cros_seq_OrderNo = O.croi_int_OrderNo
                WHERE 
                    S.cros_bit_OrderStatus = 0
                    AND O.croi_bit_OrderStatus = 0
                    AND O.croi_bit_paymentstatus = 0
                    AND O.croi_int_csid = @UserId
            ";

                    await using var orderCmd = new SqlCommand(orderSql, targetConn);
                    orderCmd.Parameters.AddWithValue("@UserId", userId);

                    await using var reader = await orderCmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

                    while (await reader.ReadAsync())
                    {
                        finalResult.Add(new ActiveOrderResponse
                        {
                            ConcessionName = concession.Name,

                            ItemId = reader["croi_int_ItemId"] != DBNull.Value
                                ? Convert.ToInt32(reader["croi_int_ItemId"])
                                : 0,

                            ItemName = reader["croi_vch_ItemName"] != DBNull.Value
                                ? reader["croi_vch_ItemName"].ToString()!
                                : string.Empty,

                            ItemPrice = reader["croi_mny_ItemPrice"] != DBNull.Value
                                ? Convert.ToDecimal(reader["croi_mny_ItemPrice"])
                                : 0,

                            Quantity = reader["croi_int_ItemQuantity"] != DBNull.Value
                                ? Convert.ToInt32(reader["croi_int_ItemQuantity"])
                                : 0,

                            TotalAmount = reader["cros_mny_TotalItemAmount"] != DBNull.Value
                                ? Convert.ToDecimal(reader["cros_mny_TotalItemAmount"])
                                : 0,

                            OrderDate = reader["cros_dt_OrderDate"] != DBNull.Value
                                ? Convert.ToDateTime(reader["cros_dt_OrderDate"])
                                : DateTime.MinValue
                        });
                    }
                }
                catch
                {
                    // Skip failing concession DB
                    continue;
                }
            }

            return finalResult;
        }



        public async Task<bool> DeleteOrderItemAsync(int concessionId, int orderNo, int itemId, int customerId)
        {
            if (concessionId <= 0 || orderNo <= 0 || itemId <= 0 || customerId <= 0)
                return false;

            // 🔹 STEP 1 — Get concession DB connection string
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(connSql, centralConn))
            {
                cmd.Parameters.AddWithValue("@ConId", concessionId);

                await centralConn.OpenAsync();
                encryptedConnString = (string?)await cmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return false;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            await using var targetConn = new SqlConnection(validatedConn);
            await targetConn.OpenAsync();

            await using var transaction = await targetConn.BeginTransactionAsync();

            try
            {
                // 🔹 STEP 2 — Delete item for specific customer
                const string deleteItemSql = @"
            DELETE FROM CustomerOrderItem
            WHERE croi_int_csid = @CustomerId
            AND croi_int_OrderNo = @OrderNo
            AND croi_int_ItemId = @ItemId
        ";

                await using var deleteItemCmd =
                    new SqlCommand(deleteItemSql, targetConn, (SqlTransaction)transaction);

                deleteItemCmd.Parameters.AddWithValue("@CustomerId", customerId);
                deleteItemCmd.Parameters.AddWithValue("@OrderNo", orderNo);
                deleteItemCmd.Parameters.AddWithValue("@ItemId", itemId);

                int deletedRows = await deleteItemCmd.ExecuteNonQueryAsync();

                if (deletedRows == 0)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // 🔹 STEP 3 — Delete all food modifiers for that item
                const string deleteModifierSql = @"
            DELETE FROM MobileFoodModifier
            WHERE mfm_int_OrderNo = @OrderNo
            AND mfm_int_CustomerId = @CustomerId
            AND mfm_int_ItemId = @ItemId
        ";

                await using var deleteModifierCmd =
                    new SqlCommand(deleteModifierSql, targetConn, (SqlTransaction)transaction);

                deleteModifierCmd.Parameters.AddWithValue("@OrderNo", orderNo);
                deleteModifierCmd.Parameters.AddWithValue("@CustomerId", customerId);
                deleteModifierCmd.Parameters.AddWithValue("@ItemId", itemId);

                await deleteModifierCmd.ExecuteNonQueryAsync();

                // 🔹 STEP 4 — Check remaining items
                const string checkItemsSql = @"
            SELECT COUNT(*)
            FROM CustomerOrderItem
            WHERE croi_int_csid = @CustomerId
            AND croi_int_OrderNo = @OrderNo
        ";

                await using var checkCmd =
                    new SqlCommand(checkItemsSql, targetConn, (SqlTransaction)transaction);

                checkCmd.Parameters.AddWithValue("@CustomerId", customerId);
                checkCmd.Parameters.AddWithValue("@OrderNo", orderNo);

                int remainingItems = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (remainingItems == 0)
                {
                    // 🔥 No items left → delete summary
                    const string deleteSummarySql = @"
                DELETE FROM CustomerOrderSummary
                WHERE cros_seq_OrderNo = @OrderNo
            ";

                    await using var deleteSummaryCmd =
                        new SqlCommand(deleteSummarySql, targetConn, (SqlTransaction)transaction);

                    deleteSummaryCmd.Parameters.AddWithValue("@OrderNo", orderNo);

                    await deleteSummaryCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // 🔹 STEP 5 — Recalculate totals
                    const string updateTotalSql = @"
                UPDATE CustomerOrderSummary
                SET 
                    cros_mny_TotalItemAmount =
                        ISNULL(
                            (SELECT SUM(croi_mny_ItemPrice * croi_int_ItemQuantity)
                             FROM CustomerOrderItem
                             WHERE croi_int_OrderNo = @OrderNo),
                            0
                        ),
                    cros_mny_NetOrderAmount =
                        ISNULL(
                            (SELECT SUM(croi_mny_ItemPrice * croi_int_ItemQuantity)
                             FROM CustomerOrderItem
                             WHERE croi_int_OrderNo = @OrderNo),
                            0
                        )
                WHERE cros_seq_OrderNo = @OrderNo
            ";

                    await using var totalCmd =
                        new SqlCommand(updateTotalSql, targetConn, (SqlTransaction)transaction);

                    totalCmd.Parameters.AddWithValue("@OrderNo", orderNo);

                    await totalCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<ItemCategoryResponse>> GetItemCategoriesByMarketAsync(string marketName)
        {
            var finalCategories = new List<ItemCategoryResponse>();

            if (string.IsNullOrWhiteSpace(marketName))
                return finalCategories;

            // 🔥 STEP 1 — Fetch ONLY concessions for this market
            const string centralSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_marketid IN
        (
            SELECT marinfo_int_marketid
            FROM MarketInfo
            WHERE marinfo_vch_marketname = @MarketName
        )
    ";

            var connectionStrings = new List<string>();

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(centralSql, centralConn))
            {
                cmd.Parameters.AddWithValue("@MarketName", marketName.Trim());

                await centralConn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        connectionStrings.Add(reader.GetString(0));
                }
            }

            // 🔥 If no concessions found → return empty
            if (connectionStrings.Count == 0)
                return finalCategories;

            // 🔥 STEP 2 — Loop EACH concession DB
            foreach (var encryptedConn in connectionStrings)
            {
                try
                {
                    string validatedConn = DecryptConnectionString(encryptedConn);

                    await using var targetConn = new SqlConnection(validatedConn);
                    await targetConn.OpenAsync();

                    const string categorySql = @"
                SELECT cat_int_CategoryID, cat_vch_CategoryName
                FROM ItemCategory
            ";

                    await using var categoryCmd = new SqlCommand(categorySql, targetConn);
                    await using var reader = await categoryCmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int categoryId = reader["cat_int_CategoryID"] != DBNull.Value
                            ? Convert.ToInt32(reader["cat_int_CategoryID"])
                            : 0;

                        string categoryName = reader["cat_vch_CategoryName"] != DBNull.Value
                            ? reader["cat_vch_CategoryName"].ToString()!
                            : string.Empty;

                        if (!string.IsNullOrWhiteSpace(categoryName))
                        {
                            finalCategories.Add(new ItemCategoryResponse
                            {
                                CategoryId = categoryId,
                                CategoryName = categoryName.Trim()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    // IMPORTANT: log error instead of silent fail
                    Console.WriteLine($"Error connecting to concession DB: {ex.Message}");
                    continue;
                }
            }

            // 🔥 STEP 3 — Remove duplicates (by CategoryName)
            var distinctCategories = finalCategories
                .GroupBy(x => x.CategoryName.ToLower())
                .Select(g => g.First())
                .ToList();

            return distinctCategories;
        }

        public async Task<List<ConcessionByItemResponse>> GetConcessionsByItemIdAsync(int itemId)
        {
            var result = new List<ConcessionByItemResponse>();

            if (itemId <= 0)
                return result;

            const string centralSql = @"
        SELECT 
            coninfo_vch_conname,
            coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
    ";

            var concessions = new List<(string Name, string ConnStr)>();

            // 🔹 STEP 1 — Fetch all concessions
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(centralSql, centralConn))
            {
                await centralConn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                    {
                        concessions.Add((
                            reader.GetString(0),
                            reader.GetString(1)
                        ));
                    }
                }
            }

            // 🔹 STEP 2 — Loop each concession DB
            foreach (var concession in concessions)
            {
                try
                {
                    string validatedConn = DecryptConnectionString(concession.ConnStr);

                    await using var targetConn = new SqlConnection(validatedConn);
                    await targetConn.OpenAsync();

                    const string itemCheckSql = @"
                SELECT COUNT(1)
                FROM Items
                WHERE itm_int_ItemId = @ItemId
            ";

                    await using var itemCmd = new SqlCommand(itemCheckSql, targetConn);
                    itemCmd.Parameters.AddWithValue("@ItemId", itemId);

                    int count = Convert.ToInt32(await itemCmd.ExecuteScalarAsync());

                    if (count > 0)
                    {
                        result.Add(new ConcessionByItemResponse
                        {
                            ConcessionName = concession.Name
                        });
                    }
                }
                catch
                {
                    // Skip failing DB
                    continue;
                }
            }

            return result;
        }

        public async Task<List<ConcessionByCategoryResponse>>
    GetConcessionsByMarketAndCategoryAsync(int marketId, int categoryId)
        {
            var result = new List<ConcessionByCategoryResponse>();

            if (marketId <= 0 || categoryId <= 0)
                return result;

            const string centralSql = @"
        SELECT 
            c.coninfo_vch_dbconnectionstring,
            c.coninfo_vch_conname,
            c.coninfo_int_conid
        FROM ConcessionInfo c
        WHERE 
            c.coninfo_int_marketid = @MarketId
            AND c.coninfo_vch_dbconnectionstring IS NOT NULL
    ";

            var concessions = new List<(string ConnStr, string Name, int Id)>();

            // 🔹 STEP 1 — Fetch concessions by market
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(centralSql, centralConn))
            {
                cmd.Parameters.AddWithValue("@MarketId", marketId);

                await centralConn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        concessions.Add((
                            reader["coninfo_vch_dbconnectionstring"].ToString()!,
                            reader["coninfo_vch_conname"].ToString()!,
                            Convert.ToInt32(reader["coninfo_int_conid"])
                        ));
                    }
                }
            }

            // 🔹 STEP 2 — Check category inside each concession DB
            foreach (var concession in concessions)
            {
                try
                {
                    string validatedConn = DecryptConnectionString(concession.ConnStr);

                    await using var targetConn = new SqlConnection(validatedConn);
                    await targetConn.OpenAsync();

                    const string checkSql = @"
                SELECT COUNT(1)
                FROM ItemCategory
                WHERE cat_int_CategoryID = @CategoryId
            ";

                    await using var checkCmd = new SqlCommand(checkSql, targetConn);
                    checkCmd.Parameters.AddWithValue("@CategoryId", categoryId);

                    int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (count > 0)
                    {
                        result.Add(new ConcessionByCategoryResponse
                        {
                            ConcessionId = concession.Id,
                            ConcessionName = concession.Name
                        });
                    }
                }
                catch
                {
                    // Skip failing DB safely
                    continue;
                }
            }

            return result;
        }

        public async Task<bool> UnsaveItemAsync(UnsaveItemRequest request)
        {
            if (request == null || request.ConcessionId <= 0 || request.ItemId <= 0)
                return false;

            // 🔹 STEP 1 — Fetch encrypted connection string safely
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString = null;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", request.ConcessionId);

                await centralConn.OpenAsync();

                var scalar = await connCmd.ExecuteScalarAsync();

                // 🔥 SAFE NULL CHECK
                if (scalar == null || scalar == DBNull.Value)
                    return false;

                encryptedConnString = scalar.ToString();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return false;

            string validatedConn;

            try
            {
                validatedConn = DecryptConnectionString(encryptedConnString);
            }
            catch
            {
                return false;
            }

            await using var targetConn = new SqlConnection(validatedConn);
            await targetConn.OpenAsync();

            await using var transaction = await targetConn.BeginTransactionAsync();

            try
            {
                // 🔹 STEP 2 — Delete from SavedItems for this user only
                const string deleteSql = @"
            DELETE FROM SavedItems
            WHERE svitm_int_ItemId = @ItemId
            AND svitm_int_csid = @CustomerId
        ";

                await using var deleteCmd =
                    new SqlCommand(deleteSql, targetConn, (SqlTransaction)transaction);

                deleteCmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                deleteCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                int deletedRows = await deleteCmd.ExecuteNonQueryAsync();

                if (deletedRows == 0)
                {
                    await transaction.RollbackAsync();
                    return false; // Nothing deleted
                }

                // 🔹 STEP 3 — Check if any other users still have this item saved
                const string checkSql = @"
            SELECT COUNT(1)
            FROM SavedItems
            WHERE svitm_int_ItemId = @ItemId
        ";

                await using var checkCmd =
                    new SqlCommand(checkSql, targetConn, (SqlTransaction)transaction);

                checkCmd.Parameters.AddWithValue("@ItemId", request.ItemId);

                int remainingCount =
                    Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                // 🔹 STEP 4 — Only update Items table if no one else saved it
                if (remainingCount == 0)
                {
                    const string updateSql = @"
                UPDATE Items
                SET itm_bit_isSavedItem = 0
                WHERE itm_int_ItemID = @ItemId
            ";

                    await using var updateCmd =
                        new SqlCommand(updateSql, targetConn, (SqlTransaction)transaction);

                    updateCmd.Parameters.AddWithValue("@ItemId", request.ItemId);

                    await updateCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<bool> LogoutAsync(int userId, string fcmToken)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(fcmToken))
                return false;

            var loginRecord = await _db.AppLoginDetail
                .FirstOrDefaultAsync(a =>
                    a.apl_int_usrid == userId &&
                    a.apl_vch_fcmtoken == fcmToken &&
                    a.apl_bit_loginstatus == true);

            if (loginRecord == null)
                return false;

            loginRecord.apl_bit_loginstatus = false;
            loginRecord.apl_dt_logouttime = DateTime.Now;

            _db.AppLoginDetail.Update(loginRecord);
            await _db.SaveChangesAsync();

            return true;
        }


        public async Task<PaymentSettingsResponse?> GetPaymentSettingsAsync(int concessionId)
        {
            if (concessionId <= 0)
                return null;

            // STEP 1 — Fetch encrypted connection string
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString = null;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", concessionId);

                await centralConn.OpenAsync();

                var scalar = await connCmd.ExecuteScalarAsync();

                if (scalar == null || scalar == DBNull.Value)
                    return null;

                encryptedConnString = scalar.ToString();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return null;

            // STEP 2 — Decrypt connection string
            string validatedConn;

            try
            {
                validatedConn = DecryptConnectionString(encryptedConnString);
            }
            catch
            {
                return null;
            }

            // STEP 3 — Query RestaurantSettings
            const string settingsSql = @"
        SELECT 
            set_vch_ExgeExpressTerminalID,
            set_vch_ExgeExpressXWebId,
            set_vch_ExgeExpressAuthID,
            set_vch_ExgeExpressPaymentURL,
            set_vch_ExgeExpressQueryPaymentURL
        FROM RestaurantSettings
    ";

            await using var targetConn = new SqlConnection(validatedConn);
            await using var cmd = new SqlCommand(settingsSql, targetConn);

            await targetConn.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new PaymentSettingsResponse
            {
                TerminalId = reader["set_vch_ExgeExpressTerminalID"] == DBNull.Value
                    ? null
                    : reader["set_vch_ExgeExpressTerminalID"].ToString(),

                XWebId = reader["set_vch_ExgeExpressXWebId"] == DBNull.Value
                    ? null
                    : reader["set_vch_ExgeExpressXWebId"].ToString(),

                AuthId = reader["set_vch_ExgeExpressAuthID"] == DBNull.Value
                    ? null
                    : reader["set_vch_ExgeExpressAuthID"].ToString(),

                PaymentUrl = reader["set_vch_ExgeExpressPaymentURL"] == DBNull.Value
                    ? null
                    : reader["set_vch_ExgeExpressPaymentURL"].ToString(),

                QueryPaymentUrl = reader["set_vch_ExgeExpressQueryPaymentURL"] == DBNull.Value
                    ? null
                    : reader["set_vch_ExgeExpressQueryPaymentURL"].ToString()
            };
        }

        public async Task<bool> ConfirmPaymentAsync(ConfirmPaymentRequest request)
        {
            if (request == null || request.ConcessionId <= 0 || request.OrderNumber <= 0 || request.CustomerId <= 0)
                return false;

            const string connSql = @"SELECT coninfo_vch_dbconnectionstring
                             FROM ConcessionInfo
                             WHERE coninfo_int_conid = @ConId";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", request.ConcessionId);
                await centralConn.OpenAsync();
                encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return false;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            await using var targetConn = new SqlConnection(validatedConn);
            await targetConn.OpenAsync();

            await using var transaction = await targetConn.BeginTransactionAsync();

            try
            {
                //----------------------------------
                // Get Stall + Terminal
                //----------------------------------
                const string stallSql = @"SELECT TOP 1 set_int_StallID,set_vch_ExgeExpressTerminalID 
                                  FROM RestaurantSettings";

                int stallId = 0;
                string terminalId = "";

                await using (var stallCmd = new SqlCommand(stallSql, targetConn, (SqlTransaction)transaction))
                await using (var reader = await stallCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        stallId = reader.GetInt32(0);
                        terminalId = reader.GetString(1);
                    }
                }

                //----------------------------------
                // Get Net Amount
                //----------------------------------
                const string totalSql = @"
        SELECT cros_mny_TotalItemAmount,cros_mny_NetOrderAmount
        FROM CustomerOrderSummary
        WHERE cros_seq_OrderNo=@OrderNo
        AND cros_int_csid=@CustomerId";

                decimal totalAmount = 0;
                decimal netAmount = 0;

                await using (var totalCmd = new SqlCommand(totalSql, targetConn, (SqlTransaction)transaction))
                {
                    totalCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
                    totalCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                    await using var reader = await totalCmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        totalAmount = reader.GetDecimal(0);
                        netAmount = reader.GetDecimal(1);
                    }
                }

                bool isFreeOrder = netAmount == 0;

                //----------------------------------
                // VerifoneUnsaved ONLY if amount >0
                //----------------------------------
                if (!isFreeOrder)
                {
                    const string verifoneSql = @"
            INSERT INTO VerifoneUnsaved
            (
                vfs_dtm_PayDate,
                vfs_num_AccountReceipts,
                vfs_txt_Card,
                vfs_cur_Amount,
                vfs_txt_ProcessStatus,
                vfs_txt_TerminalID,
                vfs_txt_OrderID,
                vfs_txt_TransType,
                vfs_txt_OpenEdgeTranseType,
                vfs_dtm_TransTime,
                vfs_txt_EntryMethod,
                vfs_txt_ApprovalCode
            )
            VALUES
            (
                GETDATE(),
                @OrderNo,
                'Credit Card',
                @Amount,
                'Completed',
                @TerminalId,
                @RequestOrderId,
                'Payment',
                'Purchase',
                GETDATE(),
                'CNP',
                @ApprovalCode
            )";

                    await using var verifoneCmd = new SqlCommand(verifoneSql, targetConn, (SqlTransaction)transaction);

                    verifoneCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
                    verifoneCmd.Parameters.AddWithValue("@Amount", netAmount);
                    verifoneCmd.Parameters.AddWithValue("@TerminalId", terminalId);
                    verifoneCmd.Parameters.AddWithValue("@RequestOrderId", request.OrderId);
                    verifoneCmd.Parameters.AddWithValue("@ApprovalCode", request.ResponseCode);

                    await verifoneCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Update CustomerOrderSummary
                //----------------------------------
                const string updateSummarySql = @"
        UPDATE CustomerOrderSummary
        SET 
            cros_int_PaymentMode=@PaymentMode,
            cros_vch_CCResCode=@ResponseCode,
            cros_vch_PayResDesc=@ResponseDescription,
            cros_int_CCOrderId=@OrderId,
            cros_bit_PaymentStatus=1,
            cros_bit_OrderStatus=1
        WHERE cros_seq_OrderNo=@OrderNo
        AND cros_int_csid=@CustomerId";

                await using (var summaryCmd = new SqlCommand(updateSummarySql, targetConn, (SqlTransaction)transaction))
                {
                    summaryCmd.Parameters.AddWithValue("@PaymentMode", request.PaymentMode);

                    if (isFreeOrder)
                    {
                        summaryCmd.Parameters.AddWithValue("@ResponseCode", DBNull.Value);
                        summaryCmd.Parameters.AddWithValue("@ResponseDescription", DBNull.Value);
                        summaryCmd.Parameters.AddWithValue("@OrderId", DBNull.Value);
                    }
                    else
                    {
                        summaryCmd.Parameters.AddWithValue("@ResponseCode", request.ResponseCode);
                        summaryCmd.Parameters.AddWithValue("@ResponseDescription", request.ResponseDescription ?? (object)DBNull.Value);
                        summaryCmd.Parameters.AddWithValue("@OrderId", request.OrderId);
                    }

                    summaryCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
                    summaryCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                    await summaryCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Insert OrderSummary
                //----------------------------------
                const string orderSummarySql = @"
        INSERT INTO OrderSummary
        (
            ors_int_StallID,
            ors_dtm_OrderDate,
            ors_vch_Customer,
            ors_bit_Void,
            ors_mny_TotalItemAmount,
            ors_mny_TotalTax,
            ors_mny_NetOrderAmount,
            ors_vch_UserID,
            ors_vch_Terminal,
            ors_bit_IsMobile,
            ors_int_MobileCustomerId,
            ors_int_OrderStatus
        )
        VALUES
        (
            @StallId,
            GETDATE(),
            '',
            0,
            @TotalAmount,
            0,
            @NetAmount,
            'MobileOrder',
            'MobileOrder',
            1,
            @CustomerId,
            1
        );

        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newOrderNo;

                await using (var orderCmd = new SqlCommand(orderSummarySql, targetConn, (SqlTransaction)transaction))
                {
                    orderCmd.Parameters.AddWithValue("@StallId", stallId);
                    orderCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                    orderCmd.Parameters.AddWithValue("@NetAmount", netAmount);
                    orderCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                    newOrderNo = Convert.ToInt32(await orderCmd.ExecuteScalarAsync());
                }

                //----------------------------------
                // Insert OrderItems
                //----------------------------------
                const string insertItemsSql = @"
        INSERT INTO OrderItems
        (
            ori_int_OrderNo,
            ori_int_StallID,
            ori_int_ItemID,
            ori_mny_ItemPrice,
            ori_mny_DiscountedPrice,
            ori_int_Quantity,
            ori_mny_FirstTax,
            ori_mny_SecTax,
            ori_mny_OtherTax,
            ori_mny_NetPrice,
            ori_bln_ReturnItem,
            ori_int_ItemSerialNo
        )
        SELECT
            @NewOrderNo,
            @StallId,
            croi_int_ItemId,
            croi_mny_ItemPrice,
            croi_mny_ItemPrice,
            croi_int_ItemQuantity,
            0,0,0,
            croi_mny_ItemPrice * croi_int_ItemQuantity,
            0,
            ROW_NUMBER() OVER (ORDER BY croi_int_ItemId)
        FROM CustomerOrderItem
        WHERE croi_int_OrderNo=@OldOrderNo
        AND croi_int_csid=@CustomerId";

                await using (var itemsCmd = new SqlCommand(insertItemsSql, targetConn, (SqlTransaction)transaction))
                {
                    itemsCmd.Parameters.AddWithValue("@NewOrderNo", newOrderNo);
                    itemsCmd.Parameters.AddWithValue("@OldOrderNo", request.OrderNumber);
                    itemsCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                    itemsCmd.Parameters.AddWithValue("@StallId", stallId);

                    await itemsCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Insert Food Modifiers
                //----------------------------------
                const string modifierSql = @"
        INSERT INTO OrderFoodModifiers
        (
            ofm_int_StallID,
            ofm_int_OrderNo,
            ofm_int_ItemSerialNo,
            ofm_int_FoodModifierID,
            ofm_vch_FoodModifierName,
            ofm_bit_IsMobileOrder
        )
        SELECT
            @StallId,
            @NewOrderNo,
            oi.ori_int_ItemSerialNo,
            mfm.mfm_int_FoodModifierId,
            mfm.mfm_vch_FoodModifierName,
            1
        FROM MobileFoodModifier mfm
        INNER JOIN OrderItems oi
            ON oi.ori_int_ItemID = mfm.mfm_int_ItemId
            AND oi.ori_int_OrderNo = @NewOrderNo";

                await using (var modifierCmd = new SqlCommand(modifierSql, targetConn, (SqlTransaction)transaction))
                {
                    modifierCmd.Parameters.AddWithValue("@StallId", stallId);
                    modifierCmd.Parameters.AddWithValue("@NewOrderNo", newOrderNo);

                    await modifierCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Payments only if amount >0
                //----------------------------------
                if (!isFreeOrder)
                {
                    const string paymentSql = @"
            INSERT INTO Payments
            (
                pay_int_OrderNo,
                pay_int_StallID,
                pay_dtm_Date,
                pay_mny_OrderAmount,
                pay_mny_PaidAmount,
                pay_int_PaymentModeId
            )
            VALUES
            (
                @OrderNo,
                @StallId,
                GETDATE(),
                @OrderAmount,
                @PaidAmount,
                3
            )";

                    await using var paymentCmd = new SqlCommand(paymentSql, targetConn, (SqlTransaction)transaction);

                    paymentCmd.Parameters.AddWithValue("@OrderNo", newOrderNo);
                    paymentCmd.Parameters.AddWithValue("@StallId", stallId);
                    paymentCmd.Parameters.AddWithValue("@OrderAmount", netAmount);
                    paymentCmd.Parameters.AddWithValue("@PaidAmount", netAmount);

                    await paymentCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Delete cart
                //----------------------------------
                const string deleteCartSql = @"DELETE FROM CustomerOrderItem WHERE croi_int_csid=@CustomerId";

                await using (var deleteCmd = new SqlCommand(deleteCartSql, targetConn, (SqlTransaction)transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                    await deleteCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> ConfirmFreeOrderAsync(ConfirmPaymentRequest request)
        {
            if (request == null || request.ConcessionId <= 0 || request.OrderNumber <= 0 || request.CustomerId <= 0)
                return false;

            const string connSql = @"SELECT coninfo_vch_dbconnectionstring
                             FROM ConcessionInfo
                             WHERE coninfo_int_conid=@ConId";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", request.ConcessionId);
                await centralConn.OpenAsync();
                encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return false;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            await using var targetConn = new SqlConnection(validatedConn);
            await targetConn.OpenAsync();

            await using var transaction = await targetConn.BeginTransactionAsync();

            try
            {
                //----------------------------------
                // Update CustomerOrderSummary
                //----------------------------------
                const string updateSummarySql = @"
        UPDATE CustomerOrderSummary
        SET 
            cros_int_PaymentMode = 0,
            cros_vch_CCResCode = NULL,
            cros_vch_PayResDesc = NULL,
            cros_int_CCOrderId = NULL,
            cros_bit_PaymentStatus = 1,
            cros_bit_OrderStatus = 1
        WHERE cros_seq_OrderNo = @OrderNo
        AND cros_int_csid = @CustomerId";

                await using (var summaryCmd = new SqlCommand(updateSummarySql, targetConn, (SqlTransaction)transaction))
                {
                    summaryCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
                    summaryCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                    await summaryCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Update CustomerOrderItem
                //----------------------------------
                const string updateItemsSql = @"
        UPDATE CustomerOrderItem
        SET croi_bit_paymentstatus = 1
        WHERE croi_int_OrderNo=@OrderNo
        AND croi_int_csid=@CustomerId";

                await using (var itemCmd = new SqlCommand(updateItemsSql, targetConn, (SqlTransaction)transaction))
                {
                    itemCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
                    itemCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                    await itemCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Fetch StallID
                //----------------------------------
                const string stallSql = @"SELECT TOP 1 set_int_StallID FROM RestaurantSettings";

                int stallId = 0;

                await using (var stallCmd = new SqlCommand(stallSql, targetConn, (SqlTransaction)transaction))
                {
                    var result = await stallCmd.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                        throw new Exception("StallID not found in RestaurantSettings");

                    stallId = Convert.ToInt32(result);
                }

                //----------------------------------
                // Fetch Order Totals
                //----------------------------------
                const string totalSql = @"
        SELECT cros_mny_TotalItemAmount,cros_mny_NetOrderAmount
        FROM CustomerOrderSummary
        WHERE cros_seq_OrderNo=@OrderNo
        AND cros_int_csid=@CustomerId";

                decimal totalAmount = 0;
                decimal netAmount = 0;

                await using (var totalCmd = new SqlCommand(totalSql, targetConn, (SqlTransaction)transaction))
                {
                    totalCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
                    totalCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                    await using var reader = await totalCmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        totalAmount = reader.GetDecimal(0);
                        netAmount = reader.GetDecimal(1);
                    }
                }

                //----------------------------------
                // Insert OrderSummary
                //----------------------------------
                const string orderSummarySql = @"
        INSERT INTO OrderSummary
        (
            ors_int_StallID,
            ors_dtm_OrderDate,
            ors_vch_Customer,
            ors_bit_Void,
            ors_mny_TotalItemAmount,
            ors_mny_TotalTax,
            ors_mny_NetOrderAmount,
            ors_vch_UserID,
            ors_vch_Terminal,
            ors_bit_IsMobile,
            ors_int_MobileCustomerId,
            ors_int_OrderStatus
        )
        VALUES
        (
            @StallId,
            GETDATE(),
            '',
            0,
            @TotalAmount,
            0,
            @NetAmount,
            'MobileOrder',
            'MobileOrder',
            1,
            @CustomerId,
            1
        );

        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newOrderNo;

                await using (var orderCmd = new SqlCommand(orderSummarySql, targetConn, (SqlTransaction)transaction))
                {
                    orderCmd.Parameters.AddWithValue("@StallId", stallId);
                    orderCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                    orderCmd.Parameters.AddWithValue("@NetAmount", netAmount);
                    orderCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

                    newOrderNo = Convert.ToInt32(await orderCmd.ExecuteScalarAsync());
                }

                //----------------------------------
                // Insert OrderItems
                //----------------------------------
                const string insertItemsSql = @"
        INSERT INTO OrderItems
        (
            ori_int_OrderNo,
            ori_int_StallID,
            ori_int_ItemID,
            ori_mny_ItemPrice,
            ori_mny_DiscountedPrice,
            ori_int_Quantity,
            ori_mny_FirstTax,
            ori_mny_SecTax,
            ori_mny_OtherTax,
            ori_mny_NetPrice,
            ori_bln_ReturnItem,
            ori_int_ItemSerialNo
        )
        SELECT
            @NewOrderNo,
            @StallId,
            croi_int_ItemId,
            croi_mny_ItemPrice,
            croi_mny_ItemPrice,
            croi_int_ItemQuantity,
            0,0,0,
            croi_mny_ItemPrice * croi_int_ItemQuantity,
            0,
            ROW_NUMBER() OVER (ORDER BY croi_int_ItemId)
        FROM CustomerOrderItem
        WHERE croi_int_OrderNo=@OldOrderNo
        AND croi_int_csid=@CustomerId";

                await using (var orderItemsCmd = new SqlCommand(insertItemsSql, targetConn, (SqlTransaction)transaction))
                {
                    orderItemsCmd.Parameters.AddWithValue("@NewOrderNo", newOrderNo);
                    orderItemsCmd.Parameters.AddWithValue("@OldOrderNo", request.OrderNumber);
                    orderItemsCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                    orderItemsCmd.Parameters.AddWithValue("@StallId", stallId);

                    await orderItemsCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Transfer Food Modifiers
                //----------------------------------
                const string modifierSql = @"
        INSERT INTO OrderFoodModifiers
        (
            ofm_int_StallID,
            ofm_int_OrderNo,
            ofm_int_ItemSerialNo,
            ofm_int_FoodModifierID,
            ofm_vch_FoodModifierName,
            ofm_bit_IsMobileOrder
        )
        SELECT
            @StallId,
            @NewOrderNo,
            oi.ori_int_ItemSerialNo,
            mfm.mfm_int_FoodModifierId,
            mfm.mfm_vch_FoodModifierName,
            1
        FROM MobileFoodModifier mfm
        INNER JOIN OrderItems oi
        ON oi.ori_int_ItemID = mfm.mfm_int_ItemId
        AND oi.ori_int_OrderNo = @NewOrderNo";

                await using (var modifierCmd = new SqlCommand(modifierSql, targetConn, (SqlTransaction)transaction))
                {
                    modifierCmd.Parameters.AddWithValue("@StallId", stallId);
                    modifierCmd.Parameters.AddWithValue("@NewOrderNo", newOrderNo);
                    await modifierCmd.ExecuteNonQueryAsync();
                }

                //----------------------------------
                // Delete Cart
                //----------------------------------
                const string deleteCartSql = @"DELETE FROM CustomerOrderItem WHERE croi_int_csid=@CustomerId";

                await using (var deleteCmd = new SqlCommand(deleteCartSql, targetConn, (SqlTransaction)transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                    await deleteCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("ConfirmFreeOrderAsync failed: " + ex.Message);
            }
        }

        //    public async Task<bool> ConfirmPaymentAsync(ConfirmPaymentRequest request)
        //    {
        //        if (request == null ||
        //            request.ConcessionId <= 0 ||
        //            request.OrderNumber <= 0 ||
        //            request.CustomerId <= 0)
        //            return false;

        //        // STEP 1 — Get encrypted connection string
        //        const string connSql = @"
        //    SELECT coninfo_vch_dbconnectionstring
        //    FROM ConcessionInfo
        //    WHERE coninfo_int_conid = @ConId
        //";

        //        string? encryptedConnString;

        //        await using (var centralConn = new SqlConnection(_connectionString))
        //        await using (var connCmd = new SqlCommand(connSql, centralConn))
        //        {
        //            connCmd.Parameters.AddWithValue("@ConId", request.ConcessionId);
        //            await centralConn.OpenAsync();
        //            encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
        //        }

        //        if (string.IsNullOrWhiteSpace(encryptedConnString))
        //            return false;

        //        string validatedConn = DecryptConnectionString(encryptedConnString);

        //        await using var targetConn = new SqlConnection(validatedConn);
        //        await targetConn.OpenAsync();

        //        await using var transaction = await targetConn.BeginTransactionAsync();

        //        try
        //        {
        //            // STEP 2 — Update CustomerOrderSummary
        //            const string updatePaymentSql = @"
        //    UPDATE CustomerOrderSummary
        //    SET 
        //        cros_int_PaymentMode = @PaymentMode,
        //        cros_vch_CCResCode = @ResponseCode,
        //        cros_vch_PayResDesc = @ResponseDescription,
        //        cros_int_CCOrderId = @OrderId,
        //        cros_bit_PaymentStatus = 1,
        //        cros_bit_OrderStatus = 1
        //    WHERE 
        //        cros_seq_OrderNo = @OrderNo
        //        AND cros_int_csid = @CustomerId
        //    ";

        //            await using var paymentCmd =
        //                new SqlCommand(updatePaymentSql, targetConn, (SqlTransaction)transaction);

        //            paymentCmd.Parameters.AddWithValue("@PaymentMode", request.PaymentMode);
        //            paymentCmd.Parameters.Add("@ResponseCode", SqlDbType.VarChar).Value = request.ResponseCode;
        //            paymentCmd.Parameters.AddWithValue("@ResponseDescription", request.ResponseDescription ?? (object)DBNull.Value);
        //            paymentCmd.Parameters.AddWithValue("@OrderId", request.OrderId);
        //            paymentCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
        //            paymentCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

        //            int rows = await paymentCmd.ExecuteNonQueryAsync();

        //            if (rows == 0)
        //            {
        //                await transaction.RollbackAsync();
        //                return false;
        //            }

        //            // STEP 3 — Update CustomerOrderItem payment status
        //            const string updateItemsSql = @"
        //    UPDATE CustomerOrderItem
        //    SET croi_bit_paymentstatus = 1
        //    WHERE croi_int_OrderNo = @OrderNo
        //    AND croi_int_csid = @CustomerId
        //    ";

        //            await using var itemsCmd =
        //                new SqlCommand(updateItemsSql, targetConn, (SqlTransaction)transaction);

        //            itemsCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
        //            itemsCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

        //            await itemsCmd.ExecuteNonQueryAsync();

        //            // STEP 4 — Get StallID
        //            const string stallSql = @"SELECT TOP 1 set_int_StallID FROM RestaurantSettings";

        //            int stallId = 0;

        //            await using (var stallCmd = new SqlCommand(stallSql, targetConn, (SqlTransaction)transaction))
        //            {
        //                var result = await stallCmd.ExecuteScalarAsync();
        //                stallId = result != null ? Convert.ToInt32(result) : 0;
        //            }

        //            // STEP 5 — Get totals
        //            const string totalsSql = @"
        //    SELECT 
        //        cros_mny_TotalItemAmount,
        //        cros_mny_NetOrderAmount
        //    FROM CustomerOrderSummary
        //    WHERE cros_seq_OrderNo = @OrderNo
        //    AND cros_int_csid = @CustomerId
        //    ";

        //            decimal totalAmount = 0;
        //            decimal netAmount = 0;

        //            await using (var totalsCmd = new SqlCommand(totalsSql, targetConn, (SqlTransaction)transaction))
        //            {
        //                totalsCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
        //                totalsCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

        //                await using var reader = await totalsCmd.ExecuteReaderAsync();

        //                if (await reader.ReadAsync())
        //                {
        //                    totalAmount = reader.GetDecimal(0);
        //                    netAmount = reader.GetDecimal(1);
        //                }
        //            }

        //            // STEP 6 — Insert OrderSummary
        //            const string insertOrderSummarySql = @"
        //    INSERT INTO OrderSummary
        //    (
        //        ors_int_StallID,
        //        ors_dtm_OrderDate,
        //        ors_vch_Customer,
        //        ors_bit_Void,
        //        ors_mny_TotalItemAmount,
        //        ors_mny_TotalTax,
        //        ors_mny_NetOrderAmount,
        //        ors_vch_UserID,
        //        ors_vch_Terminal,
        //        ors_txt_Zipcode,
        //        ors_bit_IsMobile,
        //        ors_int_MobileCustomerId,
        //        ors_int_OrderStatus
        //    )
        //    VALUES
        //    (
        //        @StallId,
        //        GETDATE(),
        //        '',
        //        0,
        //        @TotalAmount,
        //        0,
        //        @NetAmount,
        //        'MobileOrder',
        //        'MobileOrder',
        //        NULL,
        //        1,
        //        @CustomerId,
        //        1
        //    );

        //    SELECT CAST(SCOPE_IDENTITY() AS INT);
        //    ";

        //            int newOrderNo;

        //            await using (var insertCmd =
        //                new SqlCommand(insertOrderSummarySql, targetConn, (SqlTransaction)transaction))
        //            {
        //                insertCmd.Parameters.AddWithValue("@StallId", stallId);
        //                insertCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
        //                insertCmd.Parameters.AddWithValue("@NetAmount", netAmount);
        //                insertCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

        //                newOrderNo = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
        //            }

        //            // STEP 7 — Fetch items from CustomerOrderItem
        //            const string fetchItemsSql = @"
        //    SELECT 
        //        croi_int_ItemId,
        //        croi_mny_ItemPrice,
        //        croi_int_ItemQuantity
        //    FROM CustomerOrderItem
        //    WHERE croi_int_OrderNo = @OrderNo
        //    AND croi_int_csid = @CustomerId
        //    ";

        //            var items = new List<(int itemId, decimal price, int qty)>();

        //            await using (var fetchCmd =
        //                new SqlCommand(fetchItemsSql, targetConn, (SqlTransaction)transaction))
        //            {
        //                fetchCmd.Parameters.AddWithValue("@OrderNo", request.OrderNumber);
        //                fetchCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);

        //                await using var reader = await fetchCmd.ExecuteReaderAsync();

        //                while (await reader.ReadAsync())
        //                {
        //                    items.Add((
        //                        reader.GetInt32(0),
        //                        reader.GetDecimal(1),
        //                        reader.GetInt32(2)
        //                    ));
        //                }
        //            }

        //            // STEP 8 — Insert OrderItems
        //            const string insertOrderItemsSql = @"
        //    INSERT INTO OrderItems
        //    (
        //        ori_int_OrderNo,
        //        ori_int_StallID,
        //        ori_int_ItemID,
        //        ori_mny_ItemPrice,
        //        ori_mny_DiscountedPrice,
        //        ori_int_Quantity,
        //        ori_mny_FirstTax,
        //        ori_mny_SecTax,
        //        ori_mny_OtherTax,
        //        ori_mny_NetPrice,
        //        ori_bln_ReturnItem
        //    )
        //    VALUES
        //    (
        //        @OrderNo,
        //        @StallId,
        //        @ItemId,
        //        @ItemPrice,
        //        @ItemPrice,
        //        @Quantity,
        //        0.00,
        //        0.00,
        //        0.00,
        //        @NetPrice,
        //        0
        //    )
        //    ";

        //            foreach (var item in items)
        //            {
        //                decimal netPrice = item.price * item.qty;

        //                await using var itemCmd =
        //                    new SqlCommand(insertOrderItemsSql, targetConn, (SqlTransaction)transaction);

        //                itemCmd.Parameters.AddWithValue("@OrderNo", newOrderNo);
        //                itemCmd.Parameters.AddWithValue("@StallId", stallId);
        //                itemCmd.Parameters.AddWithValue("@ItemId", item.itemId);
        //                itemCmd.Parameters.AddWithValue("@ItemPrice", item.price);
        //                itemCmd.Parameters.AddWithValue("@Quantity", item.qty);
        //                itemCmd.Parameters.AddWithValue("@NetPrice", netPrice);

        //                await itemCmd.ExecuteNonQueryAsync();
        //            }

        //            // STEP 9 — Insert Payment
        //            decimal paidAmount = 0;

        //            if (request.ResponseCode == "000")
        //                paidAmount = netAmount;

        //            const string insertPaymentSql = @"
        //    INSERT INTO Payments
        //    (
        //        pay_int_OrderNo,
        //        pay_int_StallID,
        //        pay_dtm_Date,
        //        pay_mny_OrderAmount,
        //        pay_mny_PaidAmount,
        //        pay_int_PaymentModeId
        //    )
        //    VALUES
        //    (
        //        @OrderNo,
        //        @StallId,
        //        GETDATE(),
        //        @OrderAmount,
        //        @PaidAmount,
        //        3
        //    )
        //    ";

        //            await using var paymentInsertCmd =
        //                new SqlCommand(insertPaymentSql, targetConn, (SqlTransaction)transaction);

        //            paymentInsertCmd.Parameters.AddWithValue("@OrderNo", newOrderNo);
        //            paymentInsertCmd.Parameters.AddWithValue("@StallId", stallId);
        //            paymentInsertCmd.Parameters.AddWithValue("@OrderAmount", netAmount);
        //            paymentInsertCmd.Parameters.AddWithValue("@PaidAmount", paidAmount);

        //            await paymentInsertCmd.ExecuteNonQueryAsync();

        //            await transaction.CommitAsync();
        //            return true;
        //        }
        //        catch
        //        {
        //            await transaction.RollbackAsync();
        //            throw;
        //        }
        //    }


        public async Task<bool> AddFoodModifierAsync(AddFoodModifierRequest request)
        {
            if (request == null ||
                request.ConcessionId <= 0 ||
                request.OrderNo <= 0 ||
                request.CustomerId <= 0 ||
                request.ItemId <= 0 ||
                request.FoodModifierId <= 0)
                return false;

            // STEP 1 — Get encrypted connection string
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var connCmd = new SqlCommand(connSql, centralConn))
            {
                connCmd.Parameters.AddWithValue("@ConId", request.ConcessionId);
                await centralConn.OpenAsync();
                encryptedConnString = (string?)await connCmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return false;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            await using var targetConn = new SqlConnection(validatedConn);
            await targetConn.OpenAsync();
            await using var transaction = await targetConn.BeginTransactionAsync();

            try
            {
                // STEP 2 — Validate item exists
                const string checkItemSql = @"
            SELECT COUNT(*)
            FROM CustomerOrderItem
            WHERE croi_int_OrderNo = @OrderNo
            AND croi_int_csid = @CustomerId
            AND croi_int_ItemId = @ItemId
        ";

                await using var checkCmd =
                    new SqlCommand(checkItemSql, targetConn, (SqlTransaction)transaction);

                checkCmd.Parameters.AddWithValue("@OrderNo", request.OrderNo);
                checkCmd.Parameters.AddWithValue("@CustomerId", request.CustomerId);
                checkCmd.Parameters.AddWithValue("@ItemId", request.ItemId);

                int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (exists == 0)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // STEP 3 — Insert modifier
                const string insertSql = @"
            INSERT INTO MobileFoodModifier
            (
                mfm_int_OrderNo,
                mfm_int_CustomerId,
                mfm_int_ItemId,
                mfm_int_FoodModifierId,
                mfm_vch_FoodModifierName,
                mfm_bit_IsFoodModifier
            )
            VALUES
            (
                @OrderNo,
                @CustomerId,
                @ItemId,
                @FoodModifierId,
                @FoodModifierName,
                1
            )
        ";

                await using var insertCmd =
                    new SqlCommand(insertSql, targetConn, (SqlTransaction)transaction);

                insertCmd.Parameters.Add("@OrderNo", SqlDbType.Int).Value = request.OrderNo;
                insertCmd.Parameters.Add("@CustomerId", SqlDbType.Int).Value = request.CustomerId;
                insertCmd.Parameters.Add("@ItemId", SqlDbType.Int).Value = request.ItemId;
                insertCmd.Parameters.Add("@FoodModifierId", SqlDbType.Int).Value = request.FoodModifierId;
                insertCmd.Parameters.Add("@FoodModifierName", SqlDbType.NVarChar).Value =
                    request.FoodModifierName ?? (object)DBNull.Value;

                await insertCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool?> GetFoodModifierStatusAsync(
    int concessionId,
    int orderNo,
    int customerId,
    int itemId,
    int foodModifierId)
        {
            if (concessionId <= 0 || orderNo <= 0 || customerId <= 0 || itemId <= 0 || foodModifierId <= 0)
                return null;

            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(connSql, centralConn))
            {
                cmd.Parameters.AddWithValue("@ConId", concessionId);

                await centralConn.OpenAsync();
                encryptedConnString = (string?)await cmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return null;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            const string statusSql = @"
        SELECT mfm_bit_IsFoodModifier
        FROM MobileFoodModifier
        WHERE mfm_int_OrderNo = @OrderNo
        AND mfm_int_CustomerId = @CustomerId
        AND mfm_int_ItemId = @ItemId
        AND mfm_int_FoodModifierId = @ModifierId
    ";

            await using var targetConn = new SqlConnection(validatedConn);
            await using var cmdStatus = new SqlCommand(statusSql, targetConn);

            cmdStatus.Parameters.Add("@OrderNo", SqlDbType.Int).Value = orderNo;
            cmdStatus.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;
            cmdStatus.Parameters.Add("@ItemId", SqlDbType.Int).Value = itemId;
            cmdStatus.Parameters.Add("@ModifierId", SqlDbType.Int).Value = foodModifierId;

            await targetConn.OpenAsync();

            var result = await cmdStatus.ExecuteScalarAsync();

            if (result == null)
                return false;

            return Convert.ToBoolean(result);
        }

        public async Task<bool> RemoveFoodModifierAsync(
       int concessionId,
       int orderNo,
       int customerId,
       int itemId,
       int foodModifierId)
        {
            if (concessionId <= 0 || orderNo <= 0 || customerId <= 0 || itemId <= 0 || foodModifierId <= 0)
                return false;

            // 🔹 STEP 1 — Get encrypted connection string
            const string connSql = @"
        SELECT coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_conid = @ConId
    ";

            string? encryptedConnString;

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(connSql, centralConn))
            {
                cmd.Parameters.AddWithValue("@ConId", concessionId);

                await centralConn.OpenAsync();
                encryptedConnString = (string?)await cmd.ExecuteScalarAsync();
            }

            if (string.IsNullOrWhiteSpace(encryptedConnString))
                return false;

            string validatedConn = DecryptConnectionString(encryptedConnString);

            // 🔹 STEP 2 — Delete modifier
            const string deleteSql = @"
        DELETE FROM MobileFoodModifier
        WHERE mfm_int_OrderNo = @OrderNo
        AND mfm_int_CustomerId = @CustomerId
        AND mfm_int_ItemId = @ItemId
        AND mfm_int_FoodModifierId = @ModifierId
    ";

            await using var targetConn = new SqlConnection(validatedConn);
            await using var deleteCmd = new SqlCommand(deleteSql, targetConn);

            deleteCmd.Parameters.Add("@OrderNo", SqlDbType.Int).Value = orderNo;
            deleteCmd.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;
            deleteCmd.Parameters.Add("@ItemId", SqlDbType.Int).Value = itemId;
            deleteCmd.Parameters.Add("@ModifierId", SqlDbType.Int).Value = foodModifierId;

            await targetConn.OpenAsync();

            int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

            return rowsAffected > 0;
        }


        public async Task<List<CustomerOrderResponse>> GetCustomerOrdersAsync(int marketId, int customerId)
        {
            var finalResult = new List<CustomerOrderResponse>();

            const string concessionSql = @"
    SELECT 
        coninfo_vch_conname,
        coninfo_vch_dbconnectionstring
    FROM ConcessionInfo
    WHERE coninfo_int_marketid = @MarketId
    AND coninfo_vch_dbconnectionstring IS NOT NULL
    AND LTRIM(RTRIM(coninfo_vch_dbconnectionstring)) <> ''
    ";

            var concessions = new List<(string Name, string ConnStr)>();

            // 🔹 STEP 1 — Fetch concessions
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(concessionSql, centralConn))
            {
                cmd.Parameters.Add("@MarketId", SqlDbType.Int).Value = marketId;

                await centralConn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(1))
                    {
                        concessions.Add((
                            reader.IsDBNull(0) ? "" : reader.GetString(0),
                            reader.GetString(1)
                        ));
                    }
                }
            }

            if (concessions.Count == 0)
            {
                _logger?.LogWarning("No concessions found for marketId {MarketId}", marketId);
                return finalResult;
            }

            // 🔹 STEP 2 — Loop through concessions
            foreach (var concession in concessions)
            {
                try
                {
                    // ✅ Correct decryption (Base64 handled inside your method)
                    var decryptedConn = DecryptConnectionString(concession.ConnStr);

                    if (string.IsNullOrWhiteSpace(decryptedConn))
                    {
                        _logger?.LogWarning("Empty decrypted connection string for {Concession}", concession.Name);
                        continue;
                    }

                    await using var targetConn = new SqlConnection(decryptedConn);
                    await targetConn.OpenAsync();

                    // 🔹 DEBUG (optional but useful)
                    var dbNameCmd = new SqlCommand("SELECT DB_NAME()", targetConn);
                    var dbName = await dbNameCmd.ExecuteScalarAsync();
                    _logger?.LogInformation("Connected to DB: {DB}", dbName);

                    // 🔹 STEP 3 — Fetch orders
                    const string orderSql = @"
            SELECT 
                O.ors_seq_OrderNo,
                O.ors_dtm_OrderDate,
                O.ors_mny_NetOrderAmount,
                T.orst_vch_OrderStatusType
            FROM OrderSummary O
            INNER JOIN OrderStatusType T
                ON O.ors_int_OrderStatus = T.orst_int_OrderStatusId
            WHERE O.ors_int_MobileCustomerId = @CustomerId
            ";

                    await using var orderCmd = new SqlCommand(orderSql, targetConn);
                    orderCmd.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;

                    await using var reader = await orderCmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        finalResult.Add(new CustomerOrderResponse
                        {
                            ConcessionName = concession.Name,
                            OrderNo = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader[0]),
                            OrderDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                            NetOrderAmount = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                            OrderStatus = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error fetching orders from concession {Concession}", concession.Name);
                    continue; // do not break entire API
                }
            }

            return finalResult;
        }

        public async Task<OrderDetailsGroupedResponse?> GetOrderDetailsAsync(int concessionId, long orderNumber)
        {
            OrderDetailsGroupedResponse? order = null;

            const string concessionSql = @"
    SELECT coninfo_vch_dbconnectionstring
    FROM ConcessionInfo
    WHERE coninfo_int_conid = @ConcessionId
    ";

            string encryptedConn = "";

            // 🔹 STEP 1 — Get connection string
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(concessionSql, centralConn))
            {
                cmd.Parameters.Add("@ConcessionId", SqlDbType.Int).Value = concessionId;

                await centralConn.OpenAsync();

                var obj = await cmd.ExecuteScalarAsync();
                if (obj != null)
                    encryptedConn = obj.ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(encryptedConn))
                return null;

            try
            {
                var decryptedConn = DecryptConnectionString(encryptedConn);

                await using var targetConn = new SqlConnection(decryptedConn);
                await targetConn.OpenAsync();

                const string orderSql = @"
        SELECT 
            c.ors_seq_OrderNo,
            c.ors_dtm_OrderDate,
            c.ors_mny_NetOrderAmount,
            c.ors_mny_TotalItemAmount,
            o.ori_int_ItemID,
            o.ori_int_Quantity,
            i.itm_vch_ItemDescription
        FROM OrderSummary c
        LEFT JOIN OrderItems o
            ON c.ors_seq_OrderNo = o.ori_int_OrderNo
        LEFT JOIN Items i
            ON o.ori_int_ItemID = i.itm_int_ItemID
        WHERE c.ors_seq_OrderNo = @OrderNo
        ";

                await using var sqlCmd = new SqlCommand(orderSql, targetConn);
                sqlCmd.Parameters.Add("@OrderNo", SqlDbType.BigInt).Value = orderNumber;

                await using var reader = await sqlCmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    // 🔹 Create order once
                    if (order == null)
                    {
                        order = new OrderDetailsGroupedResponse
                        {
                            OrderNo = reader.GetInt64(0),
                            OrderDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                            NetOrderAmount = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                            TotalItemAmount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                            Items = new List<OrderItemModel>()
                        };
                    }

                    // 🔹 Only add item if exists
                    if (!reader.IsDBNull(4))
                    {
                        order.Items.Add(new OrderItemModel
                        {
                            ItemId = Convert.ToInt32(reader[4]),
                            Quantity = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                            ItemDescription = reader.IsDBNull(6) ? "" : reader.GetString(6)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching grouped order details");
            }

            return order;
        }

        public async Task<List<PromotionResponse>> GetPromotionsByMarketAsync(int marketId)
        {
            var finalResult = new List<PromotionResponse>();

            //-----------------------------------------
            // STEP 1 — Fetch concessions
            //-----------------------------------------
            const string concessionSql = @"
        SELECT 
            coninfo_int_conid,
            coninfo_vch_conname,
            coninfo_vch_dbconnectionstring
        FROM ConcessionInfo
        WHERE coninfo_int_marketid = @MarketId
        AND coninfo_vch_dbconnectionstring IS NOT NULL
        AND LTRIM(RTRIM(coninfo_vch_dbconnectionstring)) <> ''
    ";

            var concessions = new List<(int Id, string Name, string ConnStr)>();

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(concessionSql, centralConn))
            {
                cmd.Parameters.Add("@MarketId", SqlDbType.Int).Value = marketId;

                await centralConn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    concessions.Add((
                        reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        reader.IsDBNull(1) ? "" : reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2)
                    ));
                }
            }

            if (concessions.Count == 0)
                return finalResult;


            foreach (var concession in concessions)
            {
                try
                {

                    var decryptedConn = DecryptConnectionString(concession.ConnStr);

                    if (string.IsNullOrWhiteSpace(decryptedConn))
                        continue;

                    await using var targetConn = new SqlConnection(decryptedConn);
                    await targetConn.OpenAsync();

                    var dbCmd = new SqlCommand("SELECT DB_NAME()", targetConn);
                    var dbName = await dbCmd.ExecuteScalarAsync();
                    Console.WriteLine($"Connected DB: {dbName}");
                    const string promoSql = @"
                SELECT 
                    pri_vch_PromotionID,
                    pri_int_ItemId,
                    pri_vch_ItemName,
                    pri_vch_ItemCategeroy,
                    pri_cur_ActualPrice,
                    pri_vch_DiscountType,
                    pri_int_DiscountValue,
                    pri_cur_DiscountPrice
                FROM PromotionItems
            ";

                    await using var promoCmd = new SqlCommand(promoSql, targetConn);
                    await using var reader = await promoCmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        finalResult.Add(new PromotionResponse
                        {
                            ConcessionId = concession.Id,
                            ConcessionName = concession.Name,

                            PromotionId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            ItemId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            ItemName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            CategoryName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            ActualPrice = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                            DiscountType = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            DiscountValue = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                            DiscountPrice = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7)
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in concession {concession.Name}: {ex.Message}");
                    continue; // do not break loop
                }
            }

            return finalResult;
        }

        public async Task<List<ComboResponse>> GetCombosAsync(int concessionId)
        {
            var result = new List<ComboResponse>();

            const string concessionSql = @"
    SELECT coninfo_vch_dbconnectionstring
    FROM ConcessionInfo
    WHERE coninfo_int_conid = @ConcessionId
    AND coninfo_vch_dbconnectionstring IS NOT NULL
    AND LTRIM(RTRIM(coninfo_vch_dbconnectionstring)) <> ''
    ";

            string encryptedConn = "";

            // 🔹 STEP 1 — Get connection string from central DB
            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(concessionSql, centralConn))
            {
                cmd.Parameters.Add("@ConcessionId", SqlDbType.Int).Value = concessionId;

                await centralConn.OpenAsync();

                var obj = await cmd.ExecuteScalarAsync();

                if (obj != null)
                    encryptedConn = obj.ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(encryptedConn))
            {
                _logger?.LogWarning("No connection string found for ConcessionId {ConcessionId}", concessionId);
                return result;
            }

            try
            {
                // 🔹 STEP 2 — Decrypt connection string
                var decryptedConn = DecryptConnectionString(encryptedConn);

                await using var targetConn = new SqlConnection(decryptedConn);
                await targetConn.OpenAsync();

                // 🔹 DEBUG (optional but useful)
                var dbName = await new SqlCommand("SELECT DB_NAME()", targetConn).ExecuteScalarAsync();
                _logger?.LogInformation("Connected DB: {DB}", dbName);

                // 🔹 STEP 3 — Fetch combos
                const string comboSql = @"
        SELECT 
            cmb_vch_ComboName,
            cmb_int_ComboID,
            cmb_cur_Price
        FROM Combo
        WHERE ISNULL(cmb_bit_Available, 0) = 1
        ";

                await using var comboCmd = new SqlCommand(comboSql, targetConn);

                await using var reader = await comboCmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new ComboResponse
                    {
                        ComboName = reader.IsDBNull(0) ? "" : reader.GetString(0),

                        // 🔥 FIX: BIGINT safe conversion
                        ComboId = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader[1]),

                        Price = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching combos for ConcessionId {ConcessionId}", concessionId);
            }

            return result;
        }


        public async Task<ComboDetailsResponse?> GetComboDetailsAsync(int concessionId, long comboId)
        {
            ComboDetailsResponse? combo = null;

            const string concessionSql = @"
    SELECT coninfo_vch_dbconnectionstring
    FROM ConcessionInfo
    WHERE coninfo_int_conid = @ConcessionId
    AND coninfo_vch_dbconnectionstring IS NOT NULL
    AND LTRIM(RTRIM(coninfo_vch_dbconnectionstring)) <> ''
    ";

            string encryptedConn = "";

            await using (var centralConn = new SqlConnection(_connectionString))
            await using (var cmd = new SqlCommand(concessionSql, centralConn))
            {
                cmd.Parameters.Add("@ConcessionId", SqlDbType.Int).Value = concessionId;

                await centralConn.OpenAsync();

                var obj = await cmd.ExecuteScalarAsync();
                if (obj != null)
                    encryptedConn = obj.ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(encryptedConn))
                return null;

            try
            {
                var decryptedConn = DecryptConnectionString(encryptedConn);

                await using var targetConn = new SqlConnection(decryptedConn);
                await targetConn.OpenAsync();

                const string comboSql = @"
        SELECT 
            C.cmb_int_ComboID,
            C.cmb_int_StallID,
            C.cmb_vch_ComboName,
            C.cmb_cur_Price,
            I.cmb_vch_ItemCategory,
            I.cmb_vch_ItemName,
            I.cmb_int_ItemId,
            I.cmb_int_Priority
        FROM Combo C
        INNER JOIN ComboItems I
            ON C.cmb_int_ComboID = I.cmb_int_ComboID
        WHERE C.cmb_int_ComboID = @ComboId
        AND C.cmb_bit_Available = 1
        ORDER BY I.cmb_int_Priority
        ";

                await using var sqlCmd = new SqlCommand(comboSql, targetConn);
                sqlCmd.Parameters.Add("@ComboId", SqlDbType.BigInt).Value = comboId;

                await using var reader = await sqlCmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (combo == null)
                    {
                        combo = new ComboDetailsResponse
                        {
                            ComboId = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader[0]),
                            StallId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            ComboName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Price = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
                        };
                    }

                    combo.Items.Add(new ComboItemModel
                    {
                        Category = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        ItemName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        ItemId = reader.IsDBNull(6) ? 0 : Convert.ToInt64(reader[6]),
                        Priority = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching combo details");
            }

            return combo;
        }

        public async Task<UserResponse?> GetUserByIdAsync(int userId)
        {
            UserResponse? user = null;

            const string query = @"
    SELECT 
        usr_int_usrid,
        usr_vch_name,
        usr_vch_emailid,
        usr_vch_provider,
        usr_vch_photo_url,
        usr_vch_phoneno
    FROM Users
    WHERE usr_int_usrid = @UserId
    ";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await using var cmd = new SqlCommand(query, conn);

                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                await conn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    user = new UserResponse
                    {
                        UserId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Provider = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PhotoUrl = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        PhoneNo = reader.IsDBNull(5) ? "" : reader.GetString(5)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching user {UserId}", userId);
            }

            return user;
        }

        public async Task<bool> UploadUserImageAsync(int userId, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return false;

                // 🔒 Size limit (2MB)
                if (file.Length > 2 * 1024 * 1024)
                    throw new Exception("File must be less than 2MB");

                // 🔒 File type validation
                var ext = Path.GetExtension(file.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png" };

                if (!allowed.Contains(ext))
                    throw new Exception("Invalid file type");

                // 📦 Convert to byte[]
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var imageBytes = ms.ToArray();

                const string query = @"
        UPDATE Users
        SET usr_img_photo = @Image
        WHERE usr_int_usrid = @UserId
        ";

                await using var conn = new SqlConnection(_connectionString);
                await using var cmd = new SqlCommand(query, conn);

                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                cmd.Parameters.Add("@Image", SqlDbType.VarBinary).Value = imageBytes;

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error uploading image");
                return false;
            }
        }

        public async Task<UserImageResponse?> GetUserImageAsync(int userId)
        {
            try
            {
                const string query = @"
        SELECT usr_img_photo, usr_vch_photo_url, usr_vch_provider
        FROM Users
        WHERE usr_int_usrid = @UserId
        ";

                await using var conn = new SqlConnection(_connectionString);
                await using var cmd = new SqlCommand(query, conn);

                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                await conn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                string provider = reader.IsDBNull(2) ? "" : reader.GetString(2).ToLower();

                // 🔥 CASE 1 → GOOGLE / FACEBOOK
                if (provider == "google" || provider == "facebook")
                {
                    if (!reader.IsDBNull(1))
                    {
                        var url = reader.GetString(1);

                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            return new UserImageResponse
                            {
                                ImageUrl = url
                            };
                        }
                    }

                    return null;
                }

                // 🔥 CASE 2 → NORMAL USERS (BLOB)
                if (!reader.IsDBNull(0))
                {
                    byte[] imageBytes = (byte[])reader[0];

                    string contentType = "image/jpeg";

                    if (imageBytes.Length > 4)
                    {
                        if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50)
                            contentType = "image/png";
                        else if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                            contentType = "image/jpeg";
                    }

                    return new UserImageResponse
                    {
                        ImageBytes = imageBytes,
                        ContentType = contentType
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching image");
                return null;
            }
        }

        public async Task<bool> UpdatePhoneNumberAsync(int userId, string phoneNumber)
        {
            try
            {
                const string query = @"
        UPDATE Users
        SET usr_vch_phoneno = @Phone
        WHERE usr_int_usrid = @UserId
        ";

                await using var conn = new SqlConnection(_connectionString);
                await using var cmd = new SqlCommand(query, conn);

                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                cmd.Parameters.Add("@Phone", SqlDbType.NVarChar).Value = phoneNumber;

                await conn.OpenAsync();
                var rows = await cmd.ExecuteNonQueryAsync();

                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating phone number");
                return false;
            }
        }


    }

}
