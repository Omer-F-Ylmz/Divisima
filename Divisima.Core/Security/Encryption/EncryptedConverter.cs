using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Divisima.Core.Security.Encryption
{
    // Açıklayıcı yorum: EF value converter - property DB'ye yazılırken şifreler, okunurken çözer.
    // Kullanım: DbContext'te .HasConversion(new EncryptedConverter(provider)).
    public class EncryptedConverter : ValueConverter<string, string>
    {
        public EncryptedConverter(IEncryptionProvider provider)
            : base(v => provider.Encrypt(v), v => provider.Decrypt(v))
        {
        }
    }
}
