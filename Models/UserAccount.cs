using System.Collections.Generic;
using System.Linq;
using System.Security;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Security;
using Newtonsoft.Json;

namespace ICFaucet.Models
{
    public class UserAccount
    {
        public UserAccount()
        {

        }

        public UserAccount(long id, string mnemonic)
        {
            this.id = id;
            this.secret = mnemonic.ToSecureString();
        }

        public long id { get; set; }
        public string mnemonic { get; set; } = null;

        [JsonIgnore]
        private SecureString secret { get; set; }


        public string GetSecret() => this.secret.Release();
        public void SetSecret(string s) => this.secret = s.ToSecureString();

        public string JsonSerialize()
        {
            return new UserAccount()
            { 
                id = this.id,
                mnemonic = secret.Release()
            
            }.JsonSerialize(Newtonsoft.Json.Formatting.None);
        }

        public UserAccount SecureCopy()
            => new UserAccount(this.id, this.mnemonic ?? this.secret.Release());
    }
}
