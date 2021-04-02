namespace GmrLib.Models
{
    public partial class User
    {
        public UserAccountType AccountType
        {
            get { return (UserAccountType)AccountTypeInt; }
            set { AccountTypeInt = (int)value; }
        }

        public string NamePlusHost(User hostingUser)
        {
            return string.Format("{0}{1}",
                UserName,
                (hostingUser == this) ? " (Host)" : string.Empty);
        }
    }

    public enum UserAccountType
    {
        Free = 0,
        Tier1 = 1,
        Tier2 = 2,
        Unlimited = 3
    }
}