namespace GmrLib.Models
{
    public class PackagedUser
    {
        #region Properties

        public long UserId { get; set; }

        public int TurnOrder { get; set; }

        #endregion

        #region Constructor

        public PackagedUser()
        {
            UserId = -1;
            TurnOrder = -1;
        }

        public PackagedUser(GamePlayer gp)
        {
            UserId = gp.UserID;
            TurnOrder = gp.TurnOrder;
        }

        #endregion
    }
}