using System;
using System.Text;

namespace MyApp.Repositories
{
    
    public class EncryptionService
    {

        public string Encrypt(string plainText)
        {
            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));

            var bytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(bytes);
        }

        
        public string Decrypt(string base64Text)
        {
            if (base64Text == null)
                throw new ArgumentNullException(nameof(base64Text));

            var bytes = Convert.FromBase64String(base64Text);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
